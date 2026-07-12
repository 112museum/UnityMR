using UnityEngine;
using Photon.Pun;
using System.Linq;

public class StoryModeManager : MonoBehaviour
{
    public enum StoryState { Selection, Start, Chapter1, Chapter2, Chapter3, Ending }

    // 網路同步變數 (以預想的同步邏輯為例)
    public StoryState currentStatus = StoryState.Selection;
    public int currentLineIndex = 0;

    public StoryChapterData[] allChapters; // 放你剛剛建立的 5 個章節資料

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

    // Free chat currently in progress (null when not in free-chat mode).
    private NPCChat activeFreeChatNpc;

    // Fixed marker the backend is instructed to say once the player's answer
    // satisfies the line's freeChatSuccessKeyword (see rag/llama_index.py).
    private const string FreeChatSuccessMarker = "答對了！";

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

        StoryPromptManager.Instance.RequestLine(currentStatus.ToString(), currentLineIndex, line.speakerName, line.dialogueText, playthroughId);
    }

    private void HandleDialogueResponse(string speaker, string generatedText)
    {
        Debug.Log($"[StoryModeManager] OnStoryLine received: speaker='{speaker}' (pending='{pendingSpeaker}'), lineIndex={currentLineIndex} (pending={pendingLineIndex})");
        if (speaker != pendingSpeaker || currentLineIndex != pendingLineIndex) return;

        StoryChapterData currentChapter = allChapters[(int)currentStatus - 1];
        lineBeingSpoken = currentChapter.dialogueLines[currentLineIndex];

        if (textManager != null) textManager.UpdateText(generatedText);
        if (talker != null) talker.Speak(generatedText);

        Debug.Log($"[StoryModeManager] {speaker}: {generatedText}");
    }

    // Fires once the scripted line's TTS actually finishes speaking (not when the
    // text/response first arrives), so free chat opens at the right moment.
    private void HandleLineSpeechCompleted()
    {
        if (lineBeingSpoken == null) return;

        DialogueLine line = lineBeingSpoken.Value;
        lineBeingSpoken = null;

        if (line.startsFreeChat) StartFreeChat(line);
    }

    private void StartFreeChat(DialogueLine line)
    {
        NPCChat npc = FindObjectsOfType<NPCChat>().FirstOrDefault(n => n.npcRole == line.speakerName);
        if (npc == null)
        {
            Debug.LogError($"[StoryModeManager] startsFreeChat set but no NPCChat found for speaker '{line.speakerName}'");
            return;
        }

        activeFreeChatNpc = npc;

        if (GroupChatManager.Instance != null)
            GroupChatManager.Instance.OnNPCResponse += HandleFreeChatNpcResponse;

        npc.StartChat(line.freeChatSuccessKeyword);
    }

    // The NPC (backend-side) judges whether the player's answer satisfies
    // freeChatSuccessKeyword and says FreeChatSuccessMarker when it does — far more
    // reliable than pattern-matching the player's raw, freely-worded message.
    private void HandleFreeChatNpcResponse(string npcRole, string response)
    {
        if (activeFreeChatNpc == null || npcRole != activeFreeChatNpc.npcRole) return;
        if (!response.Contains(FreeChatSuccessMarker)) return;

        EndFreeChat();
    }

    private void EndFreeChat()
    {
        activeFreeChatNpc?.EndChat();
        StopFreeChatListening();

        currentLineIndex++;
        PlayCurrentLine();
    }

    private void StopFreeChatListening()
    {
        if (GroupChatManager.Instance != null)
            GroupChatManager.Instance.OnNPCResponse -= HandleFreeChatNpcResponse;

        activeFreeChatNpc = null;
    }

    public void OnPlayerInteractSuccess()
    {
        // 當觸發了特定互動（例如上一題提到的：A 摸了某物件）
        // 呼叫此函式推進劇情
        currentLineIndex++;
        PlayCurrentLine();
    }

    private void NextChapter()
    {
        int nextNum = (int)currentStatus + 1;
        if (nextNum <= 4) EnterChapter(nextNum);
        else currentStatus = StoryState.Ending;
    }
}