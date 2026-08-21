using System;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using System.Linq;

public class StoryModeManager : MonoBehaviour
{
    public enum StoryState { Selection, Start, Chapter1, Chapter2, Chapter3, Chapter4, Ending }

    // So StoryObjectVisibility (and anything else reacting to story progression) can find
    // this without a scene-wide search. Set in Awake() — see the Start()/Instance ordering
    // note below for why listeners must subscribe in their own Start(), not OnEnable().
    public static StoryModeManager Instance { get; private set; }

    // Fired once per tag in a line's objectsToShow/objectsToHide when that line finishes
    // speaking (see HandleLineSpeechCompleted). StoryObjectVisibility listens for these.
    public event Action<string> OnShowObjectTag;
    public event Action<string> OnHideObjectTag;

    // 網路同步變數 (以預想的同步邏輯為例)
    public StoryState currentStatus = StoryState.Selection;
    public int currentLineIndex = 0;

    public StoryChapterData[] allChapters; // 依序放 Start, Chapter1~4, Ending，共 6 個章節資料

    [Header("LLM Dialogue Display")]
    [Tooltip("DialogueLine.dialogueText is only sent to the backend as a prompt — this is what actually shows/speaks the LLM's generated line to the player.")]
    public TextManager textManager;
    // Speaks each generated line through Talker.Speak(string) so scripted lines share
    // Talker's isGlobalSpeaking lock / 429 rate-limit buffer with everything else that
    // talks. talker.ttsManager must be the same TextToSpeech instance Talker itself uses.
    public Talker talker;
    public Animator animator;

    // Fresh per playthrough (minted in StartStoryMode), so the backend room_id for this
    // group's lines never collides with an earlier group's — otherwise the story-mode
    // room ("story-{chapterId}-{npcRole}") is a fixed name nobody ever "leaves", so a new
    // group would inherit the previous group's conversation history from the backend.
    private string playthroughId = "";

    // Guards against applying a backend response that arrives after the player has already
    // moved past the line it was requested for.
    private int pendingLineIndex = -1;
    private string pendingSpeaker = "";

    // Set right before a line's TTS starts, so we know which line just finished
    // speaking when TextToSpeech.OnSpeechCompleted fires.
    private DialogueLine? lineBeingSpoken;

    // The LLM-generated text for lineBeingSpoken (not the same as its dialogueText, which
    // is only the director-side prompt) — carried into StartFreeChat as the free-chat room's
    // opening_line, so the NPC still knows what it just asked even though that room is a
    // fresh, isolated backend session (see StoryPromptManager's room-isolation comment).
    private string lineBeingSpokenText;

    // Free chat currently in progress (null when not in free-chat mode).
    private NPCChat activeFreeChatNpc;

    // The active line's own freeChatSuccessKeyword — the backend is instructed to reply
    // with this exact phrase once the player's answer satisfies it (see rag/llama_index.py
    // _format_success_condition), so we watch for this same value, not a fixed marker.
    private string activeFreeChatSuccessKeyword;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        if (talker != null && talker.ttsManager != null)
        {
            talker.ttsManager.OnSpeechCompleted += HandleLineSpeechCompleted;
        }
    }

    // StoryPromptManager.Instance is set in ITS OWN Awake(), and Unity doesn't guarantee
    // this object's OnEnable() runs after that — so subscribing here instead of OnEnable()
    // is required: Unity always finishes every object's Awake() before any object's Start().
    private void Start()
    {
        if (StoryPromptManager.Instance != null)
        {
            StoryPromptManager.Instance.OnStoryLine += HandleDialogueResponse;
        }
        else
        {
            Debug.LogError("[StoryModeManager] StoryPromptManager.Instance is still null in Start() — is StoryPromptManager in the scene?");
        }
    }

    private void OnDisable()
    {
        if (StoryPromptManager.Instance != null)
        {
            StoryPromptManager.Instance.OnStoryLine -= HandleDialogueResponse;
        }
        if (talker != null && talker.ttsManager != null)
        {
            talker.ttsManager.OnSpeechCompleted -= HandleLineSpeechCompleted;
        }
        StopFreeChatListening();
    }

    // Called by RoomLinker once the room has enough players — replaces the old
    // manual two-player role-selection flow. visitSessionId comes from RoomLinker
    // (synced via Photon room properties so every player agrees on the same value)
    // and is also used to scope this visit's free-chat rooms — see GroupChatManager.
    public void StartStoryMode(string visitSessionId)
    {
        playthroughId = visitSessionId;
        EnterChapter(1);
    }

    public void EnterChapter(int chapterNum)
    {
        currentStatus = (StoryState)chapterNum;
        currentLineIndex = 0;
        PlayCurrentLine();
    }

    public void PlayCurrentLine()
    {
        StoryChapterData currentChapter = allChapters[(int)currentStatus - 1];

        if (currentLineIndex < currentChapter.dialogueLines.Count)
        {
            DialogueLine line = currentChapter.dialogueLines[currentLineIndex];

            if (line.beats == null || line.beats.Count == 0)
            {
                // 沒有台詞可講：跳過後端 LLM/TTS，直接套用這句的效果（顯示/隱藏物件等）
                // 並往下推進，讓場景可以在 AI 開口前先讓特定物件出現/消失。
                ApplyLineEffectsAndAdvance(line, string.Empty);
                return;
            }

            RequestDialogueLine(line);
            // animator.SetTrigger(line.npcAnimationTrigger);
        }
        else
        {
            // 這一幕的對話播完了，檢查是否有互動任務，或者直接進下一幕
            NextChapter();
        }
    }

    private void RequestDialogueLine(DialogueLine line)
    {
        pendingLineIndex = currentLineIndex;
        pendingSpeaker = line.speakerName;

        // Only one client actually asks the backend; every client (including this one)
        // receives the synced reply through StoryPromptManager.OnStoryLine.
        if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
        {
            Debug.Log($"[StoryModeManager] Not master client (inRoom={PhotonNetwork.InRoom}) — waiting for the master client's request to come back via Photon.");
            return;
        }

        Debug.Log($"[StoryModeManager] Requesting line for speaker '{line.speakerName}', lineIndex={currentLineIndex}, inRoom={PhotonNetwork.InRoom}, isMaster={PhotonNetwork.IsMasterClient}");

        if (StoryPromptManager.Instance == null)
        {
            Debug.LogError("[StoryModeManager] StoryPromptManager not found in scene");
            return;
        }

        List<string> prompts = line.beats.Select(b => b.dialogueText).ToList();
        StoryPromptManager.Instance.RequestLine(currentStatus.ToString(), currentLineIndex, line.speakerName, prompts, playthroughId);
    }

    private void HandleDialogueResponse(string speaker, string generatedText, List<string> segments)
    {
        Debug.Log($"[StoryModeManager] OnStoryLine received: speaker='{speaker}' (pending='{pendingSpeaker}'), lineIndex={currentLineIndex} (pending={pendingLineIndex})");
        if (speaker != pendingSpeaker || currentLineIndex != pendingLineIndex) return;

        StoryChapterData currentChapter = allChapters[(int)currentStatus - 1];
        lineBeingSpoken = currentChapter.dialogueLines[currentLineIndex];
        lineBeingSpokenText = generatedText;

        if (textManager != null) textManager.UpdateText(generatedText);
        List<string> triggers = lineBeingSpoken.Value.beats.Select(b => b.npcAnimationTrigger).ToList();

        if (talker != null)
        {
            // 後端針對每個 beat.dialogueText 各自生成了一段回覆（segments[i] 對應 beats[i]），
            // 但那一段本身可能是好幾句連在一起的話；這裡再依標點把每個 beat 拆成更細的句子，
            // 讓字幕逐句更新、TTS 逐句播放（跟原本單一台詞的節奏一致），動畫 trigger 則仍然
            // 只在每個 beat 的第一句開始播放時觸發一次，不會因為拆更細就重複觸發。
            List<string> segmentsToSpeak = (segments != null && segments.Count > 0)
                ? segments
                : new List<string> { generatedText };

            List<string> subSentences = new List<string>();
            List<int> beatIndexOfSubSentence = new List<int>();
            for (int beatIndex = 0; beatIndex < segmentsToSpeak.Count; beatIndex++)
            {
                foreach (string sentence in Talker.SplitIntoSentences(segmentsToSpeak[beatIndex]))
                {
                    subSentences.Add(sentence);
                    beatIndexOfSubSentence.Add(beatIndex);
                }
            }

            if (subSentences.Count > 0)
            {
                int lastFiredBeat = -1;
                int subIndex = 0;
                talker.Speak(subSentences, sentence =>
                {
                    int beatIndex = beatIndexOfSubSentence[subIndex];
                    if (beatIndex != lastFiredBeat)
                    {
                        lastFiredBeat = beatIndex;
                        if (animator != null && beatIndex < triggers.Count && !string.IsNullOrEmpty(triggers[beatIndex]))
                            animator.SetTrigger(triggers[beatIndex]);
                    }
                    subIndex++;
                });
            }
        }

        Debug.Log($"[StoryModeManager] {speaker}: {generatedText}");
    }

    // Fires once the scripted line's TTS actually finishes speaking (not when the
    // text/response first arrives), so free chat opens at the right moment.
    private void HandleLineSpeechCompleted()
    {
        if (lineBeingSpoken == null) return;

        DialogueLine line = lineBeingSpoken.Value;
        string generatedText = lineBeingSpokenText;
        lineBeingSpoken = null;
        lineBeingSpokenText = null;

        ApplyLineEffectsAndAdvance(line, generatedText);
    }

    // Shared by both the normal "AI finished speaking this line" path
    // (HandleLineSpeechCompleted) and the "dialogueText is empty, nothing to speak"
    // path (PlayCurrentLine) — same show/hide/hint/advance behavior either way.
    private void ApplyLineEffectsAndAdvance(DialogueLine line, string generatedText)
    {
        if (line.objectsToShow != null)
            foreach (string tag in line.objectsToShow) OnShowObjectTag?.Invoke(tag);
        if (line.objectsToHide != null)
            foreach (string tag in line.objectsToHide) OnHideObjectTag?.Invoke(tag);

        // Independent of startsFreeChat/requiresInteraction below — shows whichever of
        // taskTextPlayer1/2 matches this client's join order, or hides the panel if this
        // line didn't set one (so a previous line's hint doesn't linger).
        ShowHintForLocalPlayer(line);

        if (line.startsFreeChat)
        {
            StartFreeChat(line, generatedText);
        }
        else if (line.requiresInteraction)
        {
            // Wait for the outside world to call OnPlayerInteractSuccess() before advancing.
        }
        else
        {
            currentLineIndex++;
            PlayCurrentLine();
        }
    }

    // ActorNumber is assigned by Photon in join order — the first player to join the room
    // is always 1, so this needs no separate role-assignment scheme (see RoomLinker).
    private void ShowHintForLocalPlayer(DialogueLine line)
    {
        if (SubtitleDisplayManager.Instance == null) return;

        bool isPlayerOne = PhotonNetwork.LocalPlayer.ActorNumber == 1;
        string content = isPlayerOne ? line.taskTextPlayer1 : line.taskTextPlayer2;

        if (string.IsNullOrEmpty(content))
            SubtitleDisplayManager.Instance.HideHint();
        else
            SubtitleDisplayManager.Instance.DisplayHintText(content);
    }

    private void StartFreeChat(DialogueLine line, string openingLine)
    {
        NPCChat npc = FindObjectsOfType<NPCChat>().FirstOrDefault(n => n.npcRole == line.speakerName);
        if (npc == null)
        {
            Debug.LogError($"[StoryModeManager] startsFreeChat set but no NPCChat found for speaker '{line.speakerName}'");
            return;
        }

        activeFreeChatNpc = npc;
        activeFreeChatSuccessKeyword = line.freeChatSuccessKeyword;

        if (GroupChatManager.Instance != null)
            GroupChatManager.Instance.OnNPCResponse += HandleFreeChatNpcResponse;

        npc.StartChat(line.freeChatSuccessKeyword, line.freeChatAnswerKey, openingLine, line.freeChatHint);
    }

    // The NPC (backend-side) judges whether the player's answer satisfies
    // freeChatSuccessKeyword and, if so, replies with that exact same phrase — far more
    // reliable than pattern-matching the player's raw, freely-worded message.
    private void HandleFreeChatNpcResponse(string npcRole, string response)
    {
        if (activeFreeChatNpc == null || npcRole != activeFreeChatNpc.npcRole) return;

        SubtitleDisplayManager.Instance?.DisplaySubtitleText(response);

        if (string.IsNullOrEmpty(activeFreeChatSuccessKeyword)) return;
        if (!response.Contains(activeFreeChatSuccessKeyword)) return;

        EndFreeChat();
    }

    private void EndFreeChat()
    {
        activeFreeChatNpc?.EndChat();
        StopFreeChatListening();

        SubtitleDisplayManager.Instance?.HideSubtitle();
        SubtitleDisplayManager.Instance?.HideTask();

        currentLineIndex++;
        PlayCurrentLine();
    }

    private void StopFreeChatListening()
    {
        if (GroupChatManager.Instance != null)
            GroupChatManager.Instance.OnNPCResponse -= HandleFreeChatNpcResponse;

        activeFreeChatNpc = null;
        activeFreeChatSuccessKeyword = null;
    }

    public void OnPlayerInteractSuccess()
    {
        // 當觸發了特定互動（例如上一題提到的：A 摸了某物件）
        // 呼叫此函式推進劇情
        Debug.Log($"[StoryModeManager] OnPlayerInteractSuccess called, currentLineIndex was {currentLineIndex}");
        currentLineIndex++;
        PlayCurrentLine();
    }

    private void NextChapter()
    {
        int nextNum = (int)currentStatus + 1;
        if (nextNum <= 5) EnterChapter(nextNum);
        else currentStatus = StoryState.Ending;
    }
}