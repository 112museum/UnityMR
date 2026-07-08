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
        if (GroupChatManager.Instance == null)
        {
            Debug.LogError("[NPCChat] GroupChatManager not found in scene");
            return;
        }
        GroupChatManager.Instance.StartChat(npcRole, personality, isRag, textManager, ttsManager);
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
