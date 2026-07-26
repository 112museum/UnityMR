using UnityEngine;

public class NPCChat : MonoBehaviour
{
    [Header("NPC Config")]
    public string npcRole = "白起";
    public string personality = "introvert";
    public bool isRag = true;

    [Header("Display")]
    public TextManager textManager;
    public TextToSpeech ttsManager;

    // Called by the "Start Chat" button on this NPC
    public void StartChat()
    {
        StartChat(null);
    }

    // Called by StoryModeManager to open free chat with a story-mode success condition:
    // the NPC is told (via the backend prompt) to say successKeyword once the player's
    // answer satisfies answerKey, so the caller can detect success from the NPC's reply.
    // openingLine, if given, is seeded as the room's first NPC turn so the NPC still
    // knows what question it just asked even though this free-chat room is a fresh,
    // isolated backend session (see StoryModeManager/StoryPromptManager room isolation).
    // hint, if given, is never shown to the player directly — the backend prompt only
    // lets the NPC draw on it to nudge a stuck player, on its own judgement of timing.
    public void StartChat(string successKeyword, string answerKey = null, string openingLine = null, string hint = null)
    {
        if (GroupChatManager.Instance == null)
        {
            Debug.LogError("[NPCChat] GroupChatManager not found in scene");
            return;
        }
        GroupChatManager.Instance.StartChat(npcRole, personality, isRag, textManager, ttsManager, successKeyword, answerKey, openingLine, hint);
    }

    // Called by the "End Chat" button
    public void EndChat()
    {
        GroupChatManager.Instance?.EndChat();
    }

    // Called by the send message button, pass the input field text
    public void SendMessage(string message)
    {
        GroupChatManager.Instance?.SendChatMessage(message);
    }
}
