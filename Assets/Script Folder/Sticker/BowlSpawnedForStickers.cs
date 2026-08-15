using UnityEngine;

// 掛在會被 PhotonRoomWordPuzzle.CreateInteractableObjects() 用 PhotonNetwork.InstantiateRoomObject
// 動態生成的碗 prefab 上（例如 青瓷蓮花式溫碗）。Start() 在每個 client 自己那份 instance 生成完之後
// 都會各自執行一次（跟同專案 TableAnchorAsParent.cs 用 Start() 做「生成後接掛」的做法一致），
// 所以不用擔心「其他 client 還沒生成」的時機問題——把場景裡所有貼紙的 bowlTarget 指過來，
// StickerDraggable 才知道要往哪個碗貼。
//
// 貼紙面板（Chapter 4 Panel / Sticker_Panel）在劇情走到貼貼紙環節前是關閉的，而碗通常在更早
// 的章節就已經生成——這代表底下這次 FindObjectsByType 掃描當下，貼紙很可能還是 inactive，
// 就算改成 Include inactive 掃到了，等面板真正打開時 GameObject 也早就是同一批，指派不會過期，
// 但保險起見還是把碗的 Transform 存成 static，讓 StickerDraggable 自己在被啟用（面板打開）時，
// 不管碗是先生成還是後生成，都能主動補上 bowlTarget，兩邊時序不管誰先誰後都能兜起來。
public class BowlSpawnedForStickers : MonoBehaviour
{
    public static Transform CurrentBowl { get; private set; }

    private void Start()
    {
        CurrentBowl = transform;

        foreach (var sticker in FindObjectsByType<StickerDraggable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            sticker.SetBowlTarget(transform);
        }
    }

    private void OnDestroy()
    {
        if (CurrentBowl == transform) CurrentBowl = null;
    }
}
