using UnityEngine;

// 掛在會被 PhotonRoomWordPuzzle.CreateInteractableObjects() 用 PhotonNetwork.InstantiateRoomObject
// 動態生成的碗 prefab 上（例如 青瓷蓮花式溫碗）。Start() 在每個 client 自己那份 instance 生成完之後
// 都會各自執行一次（跟同專案 TableAnchorAsParent.cs 用 Start() 做「生成後接掛」的做法一致），
// 所以不用擔心「其他 client 還沒生成」的時機問題——把場景裡所有貼紙的 bowlTarget 指過來，
// StickerDraggable 才知道要往哪個碗貼。
public class BowlSpawnedForStickers : MonoBehaviour
{
    private void Start()
    {
        foreach (var sticker in FindObjectsByType<StickerDraggable>(FindObjectsSortMode.None))
        {
            sticker.SetBowlTarget(transform);
        }
    }
}
