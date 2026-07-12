// StoryPromptManager.cs — Sends the story director's authored dialogueText (as a prompt)
// to the backend LLM and syncs the generated line to every player. Deliberately separate
// from GroupChatManager: that one is the player-initiated NPC chat (room "museum-{npcRole}",
// event "send_chat_message"/"npc_response"), which needs its own conversation context. This
// manager owns the story-mode room "story-{playthroughId}-{chapterId}-{npcRole}" and events
// "story_prompt"/"story_response" instead, so a scripted narrative line and a live player Q&A
// with the same NPC don't share/pollute each other's backend context. playthroughId is fresh
// per playthrough (see StoryModeManager.StartStoryMode) so a new group of players never
// inherits an earlier group's conversation history.
using System;
using System.Text.Json.Serialization;
using UnityEngine;
using SocketIOClient;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

public class StoryPromptManager : MonoBehaviour, IOnEventCallback
{
    public static StoryPromptManager Instance { get; private set; }

    [Header("Backend Config")]
    public string backendUrl = "http://192.168.0.76:5050";

    private SocketIOUnity socket;
    private bool isConnected = false;

    // Only the client that actually called RequestLine relays the backend's reply to the
    // other player (mirrors GroupChatManager's NPC_RESPONSE_EVENT pattern), so both players
    // see the same generated line instead of two independently-generated ones.
    private bool isLastSender = false;

    private const byte STORY_RESPONSE_EVENT = 44;

    public event Action<string, string> OnStoryLine; // (npcRole, generatedText)

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable() => PhotonNetwork.AddCallbackTarget(this);
    private void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);

    private void Start()
    {
        var uri = new Uri(backendUrl);
        socket = new SocketIOUnity(uri, new SocketIOOptions
        {
            Transport = SocketIOClient.Transport.TransportProtocol.WebSocket
        });

        socket.OnConnected += (sender, args) =>
        {
            isConnected = true;
            Debug.Log("[StoryPromptManager] Connected to backend");
        };

        socket.On("error", (response) =>
        {
            Debug.LogError($"[StoryPromptManager] Server error: {response}");
        });

        socket.OnUnityThread("story_response", (response) =>
        {
            var data = response.GetValue<StoryResponseData>();
            Debug.Log($"[StoryPromptManager] story_response received: npc={data.NpcRole}, isLastSender={isLastSender}, inRoom={PhotonNetwork.InRoom}");
            RaiseOrApply(data.NpcRole, data.Response);
        });

        socket.ConnectAsync();
    }

    public void RequestLine(string chapterId, int lineIndex, string npcRole, string prompt, string playthroughId)
    {
        if (!isConnected)
        {
            Debug.LogWarning("[StoryPromptManager] Not connected to backend yet; dropping request.");
            return;
        }

        isLastSender = true;
        // playthroughId makes the room unique per playthrough — "story-{chapterId}-{npcRole}"
        // alone is a fixed name nobody ever "leaves", so without it a new group of players
        // would inherit whatever conversation history the previous group left behind.
        string roomId = $"story-{playthroughId}-{chapterId}-{npcRole}";
        Debug.Log($"[StoryPromptManager] Sending story_prompt: room={roomId}, npc={npcRole}, prompt={prompt}");
        socket.EmitAsync("story_prompt", new
        {
            room_id = roomId,
            npc_role = npcRole,
            chapter_id = chapterId,
            line_index = lineIndex,
            prompt
        });
    }

    private void RaiseOrApply(string npcRole, string responseText)
    {
        if (PhotonNetwork.InRoom)
        {
            if (isLastSender)
            {
                isLastSender = false;
                Debug.Log($"[StoryPromptManager] Relaying via PhotonNetwork.RaiseEvent: npc={npcRole}");
                object[] content = { npcRole, responseText };
                PhotonNetwork.RaiseEvent(STORY_RESPONSE_EVENT, content,
                    new RaiseEventOptions { Receivers = ReceiverGroup.All },
                    SendOptions.SendReliable);
            }
            else
            {
                Debug.Log($"[StoryPromptManager] In room but not last sender — waiting for OnEvent relay from the requester.");
            }
            return;
        }

        Debug.Log($"[StoryPromptManager] Not in a Photon room — invoking OnStoryLine directly.");
        OnStoryLine?.Invoke(npcRole, responseText);
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != STORY_RESPONSE_EVENT) return;

        object[] data = (object[])photonEvent.CustomData;
        Debug.Log($"[StoryPromptManager] OnEvent received relayed story line: npc={data[0]}");
        OnStoryLine?.Invoke((string)data[0], (string)data[1]);
    }

    private void OnDestroy()
    {
        socket?.DisconnectAsync();
        socket?.Dispose();
    }

    private void OnApplicationQuit()
    {
        socket?.DisconnectAsync();
        socket?.Dispose();
    }

    private class StoryResponseData
    {
        [JsonPropertyName("npc_role")] public string NpcRole { get; set; }
        [JsonPropertyName("response")] public string Response { get; set; }
    }
}
