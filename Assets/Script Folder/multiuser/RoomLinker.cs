// RoomLinker.cs — Auto-joins a single fixed PUN room for all players
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class RoomLinker : MonoBehaviourPunCallbacks
{
    public GroupChatManager chatManager;
    public string fixedRoomName = "MRMuseum";

    void Start()
    {
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
        }
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
    }

    public override void OnLeftRoom()
    {
        chatManager?.OnPhotonRoomLeft();
        chatManager.EndChat();
    }
}
