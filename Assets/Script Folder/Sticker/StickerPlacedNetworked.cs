using Photon.Pun;
using UnityEngine;

// 掛在 Assets/Resources/Sticker/StickerPlaced.prefab 上（該 prefab 需要有 PhotonView 元件）。
// 由 StickerDraggable 呼叫 PhotonNetwork.Instantiate 動態生成，生成當下透過
// PhotonView.InstantiationData 帶入顏色跟碗的物件名稱，讓房間裡所有玩家都看到同一顆貼紙、
// 同一個顏色、貼在碗的同一個位置；接到碗底下之後，碗被移動/旋轉貼紙才會跟著走，不會留在原地。
public class StickerPlacedNetworked : MonoBehaviourPun, IPunInstantiateMagicCallback
{
    [SerializeField] private Renderer stickerRenderer;

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        object[] data = photonView.InstantiationData;
        if (data == null || data.Length < 4 || stickerRenderer == null) return;

        var color = new Color((float)data[0], (float)data[1], (float)data[2]);
        stickerRenderer.material.color = color;

        // 碗是每個玩家本地端都有、名稱相同的場景物件（不是網路生成物），
        // 用名稱在本機場景裡找到對應的碗。傳過來的名字是最外層的錨點物件，
        // 實際會被抓取移動的是它底下真正有 Collider 的那個子物件（模型本體），
        // 掛在錨點上的話錨點本身不會動，貼紙等於沒跟著碗走，所以要往下找到
        // 真正會動的那個 Transform 再掛上去。
        var bowlName = (string)data[3];
        GameObject bowlAnchor = GameObject.Find(bowlName);
        if (bowlAnchor != null)
        {
            Collider bowlCollider = bowlAnchor.GetComponentInChildren<Collider>();
            Transform followTarget = bowlCollider != null ? bowlCollider.transform : bowlAnchor.transform;
            transform.SetParent(followTarget, worldPositionStays: true);
        }
    }
}
