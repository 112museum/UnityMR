using UnityEngine;
using SocketIOClient;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public class NPCRequestManager : MonoBehaviour
{
    [Header("Backend Config")]
    public string backendUrl = "http://192.168.0.76:5050";

    [Header("NPC Config")]
    public string npc_role = "白起";
    public string personality = "introvert";
    public bool is_rag = true;
    public TextToSpeech ttsManager;
    public TextManager textManager;

    private SocketIOUnity socket;
    private string roomId;
    private bool isInRoom = false;
    private readonly Queue<string> pendingMessages = new Queue<string>();

    void Start()
    {
        // Unique per instance so two kiosks talking to the same npc_role at
        // the same time don't end up sharing one conversation history.
        roomId = $"solo-{npc_role}-{Guid.NewGuid():N}";
        InitializeSocket();
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
            Debug.Log("[NPCRequestManager] Connected to AI Backend");
            JoinRoom();
        };

        socket.On("error", (response) =>
        {
            Debug.LogError($"[NPCRequestManager] Server error: {response}");
        });

        socket.On("room_joined", (response) =>
        {
            isInRoom = true;
            Debug.Log($"[NPCRequestManager] Joined room: {roomId}");
            FlushPendingMessages();
        });

        socket.OnUnityThread("npc_response", (response) =>
        {
            var data = response.GetValue<NpcResponseData>();
            Debug.Log("Response: " + data.Response);

            if (textManager != null)
                textManager.UpdateText(data.Response);

            if (ttsManager != null)
                ttsManager.ConvertTextToSpeech(data.Response);
        });

        socket.OnUnityThread("npc_typing", (response) =>
        {
            if (textManager != null)
                textManager.UpdateText("NPC typing...");
        });

        socket.ConnectAsync();
    }

    void JoinRoom()
    {
        socket.EmitAsync("join", new
        {
            room_id = roomId,
            user_name = $"SoloUser_{roomId}",
            npc_role,
            lang = LanguageState.ApiLang,
            personality,
            is_rag
        });
    }

    public void SendNPCRequest(string query)
    {
        if (!isInRoom)
        {
            Debug.Log("[NPCRequestManager] Not in a room yet; queuing message until joined");
            pendingMessages.Enqueue(query);
            return;
        }

        try
        {
            Debug.Log("Sending message: " + query);
            Debug.Log($"[NPCRequestManager] Using lang = {LanguageState.ApiLang}");
            socket.EmitAsync("send_chat_message", new { message = query });
        }
        catch (Exception ex)
        {
            Debug.LogError("Error: " + ex.Message);
            ttsManager?.ConvertTextToSpeech("Server Error");
        }
    }

    private void FlushPendingMessages()
    {
        while (pendingMessages.Count > 0)
        {
            var msg = pendingMessages.Dequeue();
            SendNPCRequest(msg);
        }
    }

    void OnDestroy()
    {
        socket?.DisconnectAsync();
        socket?.Dispose();
    }

    private class NpcResponseData
    {
        [JsonPropertyName("npc_role")] public string NpcRole { get; set; }
        [JsonPropertyName("response")] public string Response { get; set; }
    }
}
