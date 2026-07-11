using UnityEngine;
using SocketIOClient;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using System.IO;

public class GroupChatManager : MonoBehaviour, IOnEventCallback
{
    public static GroupChatManager Instance { get; private set; }

    [Header("Backend Config")]
    public string backendUrl = "http://192.168.0.76:5050";

    [Header("Player Config")]
    private string userName;
    public string UserName => userName;

    private SocketIOUnity socket;
    private bool isInRoom = false;
    private bool isPhotonRoomReady = false;
    private string currentNpcRole = "";
    private bool isLastSender = false;
    private readonly Queue<string> pendingUserMessages = new Queue<string>();

    private const byte NPC_RESPONSE_EVENT = 42;
    private const byte NPC_TYPING_EVENT = 43;

    private readonly Queue<object[]> pendingNpcResponseEvents = new Queue<object[]>();
    private readonly Queue<object[]> pendingNpcTypingEvents = new Queue<object[]>();

    // Chat history storage
    private List<ChatMessage> chatHistory = new List<ChatMessage>();
    private string chatLogPath;

    public event Action<string, string> OnUserMessage;
    public event Action<string, string> OnNPCResponse;
    public event Action<string> OnSystemMessage;
    public event Action<bool> OnNPCTyping;

    void Awake()
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

        userName = "User_" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
    }

    void OnEnable() => PhotonNetwork.AddCallbackTarget(this);
    void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);

    void Start()
    {
        InitializeSocket();
        // Ensure logging has a session id at app/startup so gaze events are recorded
        try
        {
            if (LoggingManager.Instance != null)
            {
                string sessionId = $"user_{userName}_{DateTime.Now:yyyyMMdd_HHmmss}";
                LoggingManager.Instance.SetSessionId(sessionId);
                chatLogPath = Path.Combine(Application.persistentDataPath, $"chat_history_{sessionId}.json");
                Debug.Log($"[GroupChatManager] Logging session set: {sessionId}");
            }
            else
            {
                Debug.LogWarning("[GroupChatManager] LoggingManager.Instance is null; session ID not set.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GroupChatManager] Failed to set logging session id: {ex.Message}");
        }
    }

    void InitializeSocket()
    {
        var uri = new Uri(backendUrl);
        socket = new SocketIOUnity(uri, new SocketIOOptions
        {
            Transport = SocketIOClient.Transport.TransportProtocol.WebSocket
        });

        socket.OnConnected += (sender, args) =>
        {
            Debug.Log("[GroupChatManager] Connected to AI Backend");
        };

        socket.On("error", (response) =>
        {
            Debug.LogError($"[Socket] Server error: {response}");
        });

        socket.On("room_joined", (response) =>
        {
            var data = response.GetValue<RoomJoinedData>();
            isInRoom = true;
            Debug.Log($"[GroupChatManager] Joined room: {data.RoomId}");
            FlushPendingUserMessages();
        });

        socket.On("room_left", (response) =>
        {
            isInRoom = false;
            Debug.Log("[GroupChatManager] Left room");
            pendingUserMessages.Clear();
        });

        socket.OnUnityThread("user_message", (response) =>
        {
            var data = response.GetValue<UserMessageData>();
            chatHistory.Add(new ChatMessage { Timestamp = DateTime.Now, Type = "user", Sender = data.UserName, Message = data.Message });
            OnUserMessage?.Invoke(data.UserName, data.Message);
        });

        socket.OnUnityThread("npc_response", (response) =>
        {
            var data = response.GetValue<NpcResponseData>();
            chatHistory.Add(new ChatMessage { Timestamp = DateTime.Now, Type = "npc", Sender = data.NpcRole, Message = data.Response });
            SaveChatHistory();
            RaiseOrApplyNPCResponse(data.NpcRole, data.Response);
        });

        socket.OnUnityThread("npc_typing", (response) =>
        {
            RaiseOrApplyNPCTyping(currentNpcRole, true);
        });

        socket.OnUnityThread("system_message", (response) =>
        {
            var data = response.GetValue<SystemMessageData>();
            chatHistory.Add(new ChatMessage { Timestamp = DateTime.Now, Type = "system", Sender = "System", Message = data.Message });
            OnSystemMessage?.Invoke(data.Message);
        });

        socket.ConnectAsync();
    }

    public void OnPhotonRoomJoined()
    {
        isPhotonRoomReady = true;
        FlushPendingPhotonEvents();
    }

    public void OnPhotonRoomLeft()
    {
        isPhotonRoomReady = false;
        pendingNpcResponseEvents.Clear();
        pendingNpcTypingEvents.Clear();
    }

    private void FlushPendingPhotonEvents()
    {
        if (!isLastSender) return;

        while (pendingNpcResponseEvents.Count > 0)
        {
            var content = pendingNpcResponseEvents.Dequeue();
            PhotonNetwork.RaiseEvent(NPC_RESPONSE_EVENT, content,
                new RaiseEventOptions { Receivers = ReceiverGroup.Others },
                SendOptions.SendReliable);
        }

        while (pendingNpcTypingEvents.Count > 0)
        {
            var content = pendingNpcTypingEvents.Dequeue();
            PhotonNetwork.RaiseEvent(NPC_TYPING_EVENT, content,
                new RaiseEventOptions { Receivers = ReceiverGroup.Others },
                SendOptions.SendReliable);
        }
    }

    private void FlushPendingUserMessages()
    {
        while (pendingUserMessages.Count > 0)
        {
            var msg = pendingUserMessages.Dequeue();
            try
            {
                socket.EmitAsync("send_chat_message", new { message = msg });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GroupChatManager] Failed to flush queued message: {ex.Message}");
            }
        }
    }

    private void RaiseOrApplyNPCResponse(string npcRole, string responseText)
    {
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom && isPhotonRoomReady)
        {
            if (isLastSender)
            {
                isLastSender = false;
                object[] content = new object[] { npcRole, responseText };
                PhotonNetwork.RaiseEvent(NPC_RESPONSE_EVENT, content,
                    new RaiseEventOptions { Receivers = ReceiverGroup.All },
                    SendOptions.SendReliable);
            }
            return;
        }

        object[] queuedContent = new object[] { npcRole, responseText };
        pendingNpcResponseEvents.Enqueue(queuedContent);
        ApplyNPCResponse(npcRole, responseText);
    }

    private void RaiseOrApplyNPCTyping(string npcRole, bool isTyping)
    {
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom && isPhotonRoomReady)
        {
            if (isLastSender)
            {
                object[] content = new object[] { npcRole, isTyping };
                PhotonNetwork.RaiseEvent(NPC_TYPING_EVENT, content,
                    new RaiseEventOptions { Receivers = ReceiverGroup.All },
                    SendOptions.SendReliable);
            }
            return;
        }

        object[] queuedContent = new object[] { npcRole, isTyping };
        pendingNpcTypingEvents.Enqueue(queuedContent);
        ApplyNPCTyping(npcRole, isTyping);
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code == NPC_RESPONSE_EVENT)
        {
            object[] data = (object[])photonEvent.CustomData;
            ApplyNPCResponse((string)data[0], (string)data[1]);
        }
        else if (photonEvent.Code == NPC_TYPING_EVENT)
        {
            object[] data = (object[])photonEvent.CustomData;
            ApplyNPCTyping((string)data[0], (bool)data[1]);
        }
    }

    private void ApplyNPCResponse(string npcRole, string responseText)
    {
        OnNPCTyping?.Invoke(false);
        OnNPCResponse?.Invoke(npcRole, responseText);

        foreach (var npc in FindObjectsOfType<NPCChat>())
        {
            if (npc.npcRole == npcRole)
            {
                if (npc.textManager != null) npc.textManager.UpdateText(responseText);
                if (npc.ttsManager != null) npc.ttsManager.ConvertTextToSpeech(responseText);
                break;
            }
        }
    }

    private void ApplyNPCTyping(string npcRole, bool isTyping)
    {
        OnNPCTyping?.Invoke(isTyping);

        if (!isTyping) return;

        foreach (var npc in FindObjectsOfType<NPCChat>())
        {
            if (npc.npcRole == npcRole)
            {
                if (npc.textManager != null) npc.textManager.UpdateText("NPC typing...");
                break;
            }
        }
    }

    public void StartChat(string npcRole, string personality = "", bool isRag = true, TextManager textManager = null, TextToSpeech ttsManager = null, string successKeyword = null)
    {
        if (isInRoom && currentNpcRole == npcRole)
        {
            Debug.Log($"[GroupChatManager] Already in room for {npcRole}");
            return;
        }

        if (isInRoom)
        {
            socket.EmitAsync("leave");
            isInRoom = false;
        }

        currentNpcRole = npcRole;
        string resolvedUserName = userName;

        socket.EmitAsync("join", new
        {
            room_id = $"museum-{npcRole}",
            user_name = resolvedUserName,
            npc_role = npcRole,
            lang = "zh-TW",
            personality,
            is_rag = isRag,
            success_keyword = successKeyword
        });

        Debug.Log($"[GroupChatManager] Starting chat with {npcRole}, socket connected: {socket.Connected}");
    }

    public void EndChat()
    {
        if (isInRoom)
        {
            socket.EmitAsync("leave");
            isInRoom = false;
        }
    }

    public void SendChatMessage(string message)
    {
        if (!isInRoom)
        {
            Debug.LogWarning("[GroupChatManager] Not in a room; queuing message until joined");
            pendingUserMessages.Enqueue(message);
            return;
        }

        try
        {
            isLastSender = true;
            socket.EmitAsync("send_chat_message", new { message });
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GroupChatManager] Error sending message: {ex.Message}");
        }
    }

    void OnDestroy()
    {
        socket?.DisconnectAsync();
        socket?.Dispose();
    }

    private void SaveChatHistory()
    {
        if (chatHistory.Count == 0 || chatLogPath == null) return;

        var options = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        string json = System.Text.Json.JsonSerializer.Serialize(chatHistory, options);
        File.WriteAllText(chatLogPath, json);
        Debug.Log($"[GroupChatManager] Chat history saved to: {chatLogPath}");
    }

    void OnApplicationQuit()
    {
        SaveChatHistory();
        socket?.DisconnectAsync();
        socket?.Dispose();
    }

    private class RoomJoinedData { [JsonPropertyName("room_id")] public string RoomId { get; set; } }
    private class UserMessageData { [JsonPropertyName("user_name")] public string UserName { get; set; } [JsonPropertyName("message")] public string Message { get; set; } }
    private class NpcResponseData { [JsonPropertyName("npc_role")] public string NpcRole { get; set; } [JsonPropertyName("response")] public string Response { get; set; } }
    private class SystemMessageData { [JsonPropertyName("message")] public string Message { get; set; } }

    private class ChatMessage
    {
        public DateTime Timestamp { get; set; }
        public string Type { get; set; } // "user", "npc", "system"
        public string Sender { get; set; }
        public string Message { get; set; }
    }
}
