// RoomLinker.cs — Joins a single fixed PUN room for all players, on demand
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class RoomLinker : MonoBehaviourPunCallbacks
{
    public GroupChatManager chatManager;
    public StoryModeManager storyManager;
    public string fixedRoomName = "MRMuseum";
    public int playersNeededToStartStory = 2;

    private bool storyStarted = false;

    // Called by a UI button. If not yet connected to Photon, this kicks off the
    // connect and OnConnectedToMaster() joins the room once that completes.
    public void JoinRoom()
    {
        if (PhotonNetwork.IsConnected)
        {
            if (!PhotonNetwork.InRoom) OnConnectedToMaster();
            return;
        }

        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
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
        chatManager.EndChat();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer) => UpdatePlayerCount();

    public override void OnPlayerLeftRoom(Player otherPlayer) => UpdatePlayerCount();

    // Shows the current headcount to the players and, once enough have joined,
    // kicks off the scripted story (previously gated behind manual role selection).
    private void UpdatePlayerCount()
    {
        int count = PhotonNetwork.CurrentRoom.PlayerCount;
        SubtitleDisplayManager.Instance?.DisplayHintText($"房間人數：{count}/{playersNeededToStartStory}");

        if (!storyStarted && count >= playersNeededToStartStory)
        {
            storyStarted = true;
            SubtitleDisplayManager.Instance?.HideHint();
            storyManager?.StartStoryMode();
        }
    }
}
