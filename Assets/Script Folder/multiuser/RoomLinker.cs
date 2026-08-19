// RoomLinker.cs — Joins a single fixed PUN room for all players, on demand
using System;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class RoomLinker : MonoBehaviourPunCallbacks
{
    public GroupChatManager chatManager;
    public StoryModeManager storyManager;
    public string fixedRoomName = "MRMuseum";
    public int playersNeededToStartStory = 2;

    // Room custom property key. Only the master client sets it; Photon syncs the value to
    // every other client via OnRoomPropertiesUpdate, so the whole group agrees on one id
    // for both the story rooms and the free-chat rooms (see StoryModeManager, GroupChatManager).
    private const string VisitSessionPropKey = "visitSessionId";

    private bool storyStarted = false;

    // Called by a UI button. If not yet connected to Photon, this kicks off the
    // connect and OnConnectedToMaster() joins the room once that completes.
    public void JoinRoom()
    {
        if (PhotonNetwork.InRoom) return;

        // Client is already fully connected to the master server and ready for matchmaking
        // (as opposed to just PhotonNetwork.IsConnected, which is also true mid-handshake,
        // e.g. ConnectingToMasterServer/Authenticating — calling JoinOrCreateRoom then fails
        // with "not ready for operations").
        if (PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterServer)
        {
            JoinOrCreateFixedRoom();
            return;
        }

        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
        }
        // else: already connecting/authenticating — OnConnectedToMaster() below will fire once ready.
    }

    public override void OnConnectedToMaster() => JoinOrCreateFixedRoom();

    private void JoinOrCreateFixedRoom()
    {
        // CleanupCacheOnLeave = false so a player's PhotonNetwork.Instantiate()'d objects
        // (e.g. the interactable exhibits) survive after that player leaves the room.
        PhotonNetwork.JoinOrCreateRoom(fixedRoomName, new RoomOptions { MaxPlayers = 20, CleanupCacheOnLeave = false }, TypedLobby.Default);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"Joined PUN room: {PhotonNetwork.CurrentRoom.Name}");
        chatManager?.OnPhotonRoomJoined();
        UpdatePlayerCount();
    }

    public override void OnLeftRoom()
    {
        chatManager?.OnPhotonRoomLeft();
        chatManager?.EndChat();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer) => UpdatePlayerCount();

    public override void OnPlayerLeftRoom(Player otherPlayer) => UpdatePlayerCount();

    // Shows the current headcount to the players and, once enough have joined, mints a
    // shared session id for this visit (previously the story start was gated behind manual
    // role selection).
    private void UpdatePlayerCount()
    {
        int count = PhotonNetwork.CurrentRoom.PlayerCount;
        SubtitleDisplayManager.Instance?.DisplayHintText($"房間人數：{count}/{playersNeededToStartStory}");

        if (!storyStarted && count >= playersNeededToStartStory)
        {
            storyStarted = true;
            SubtitleDisplayManager.Instance?.HideHint();

            // Only the master client mints the id; everyone (including the master, via its
            // own OnRoomPropertiesUpdate callback) picks it up from the synced room property
            // instead of generating their own — otherwise each client would get a different
            // id and players would end up in different free-chat rooms.
            if (PhotonNetwork.IsMasterClient)
            {
                string sessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { VisitSessionPropKey, sessionId } });
            }
        }
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (!propertiesThatChanged.ContainsKey(VisitSessionPropKey)) return;

        string sessionId = (string)propertiesThatChanged[VisitSessionPropKey];
        chatManager?.SetVisitSessionId(sessionId);
        storyManager?.StartStoryMode(sessionId);
    }
}
