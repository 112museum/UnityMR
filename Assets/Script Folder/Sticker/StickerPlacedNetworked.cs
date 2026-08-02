using Photon.Pun;
using UnityEngine;

// 掛在 Assets/Resources/Sticker/StickerPlaced.prefab 上（該 prefab 需要有 PhotonView 元件）。
// 由 StickerDraggable 呼叫 PhotonNetwork.Instantiate 動態生成，生成當下透過
// PhotonView.InstantiationData 帶入顏色（或材質路徑）跟碗的物件名稱，讓房間裡所有玩家都看到
// 同一顆貼紙、同一個圖案、貼在碗的同一個位置；接到碗底下之後，碗被移動/旋轉貼紙才會跟著走，
// 不會留在原地。
public class StickerPlacedNetworked : MonoBehaviourPun, IPunInstantiateMagicCallback
{
    [SerializeField] private Renderer stickerRenderer;

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        object[] data = photonView.InstantiationData;
        if (data == null || data.Length < 8 || stickerRenderer == null) return;

        // 有帶材質路徑（貼紙本身有指定圖案）就整份材質換掉，保留圖案；
        // 沒有的話（舊的示範貼紙，只有顏色）退回原本的染色做法。
        var materialResourcePath = (string)data[4];
        if (!string.IsNullOrEmpty(materialResourcePath))
        {
            Material material = Resources.Load<Material>(materialResourcePath);
            if (material != null)
            {
                stickerRenderer.material = material;
            }
            else
            {
                // 常見原因：材質沒有放在 Resources 資料夾底下，或路徑/檔名對不起來。
                Debug.LogWarning($"[StickerPlacedNetworked] Resources.Load 找不到材質：{materialResourcePath}，貼紙會維持 prefab 原本的預設材質。");
            }
        }
        else
        {
            var color = new Color((float)data[0], (float)data[1], (float)data[2]);
            stickerRenderer.material.color = color;
        }

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

        // 尺寸要在 SetParent 之後才設定：SetParent(worldPositionStays: true) 會為了維持世界大小
        // 不變而自動重算 localScale，在它之前設定的話會被蓋掉。
        // data[5..7] 是玩家手上那顆貼紙當時的「世界尺寸」（lossyScale），不能直接塞進 localScale——
        // 這顆貼紙現在的 parent 是碗，如果碗自己的 Scale 不是 (1,1,1)，直接塞會變成兩個 scale
        // 疊乘在一起、比預期更小。要除掉 parent 目前的世界縮放，換算成正確的 localScale，
        // 這樣不管碗本身有沒有被縮放過，貼上去的世界尺寸都會跟玩家拿在手上時一致。
        var desiredWorldScale = new Vector3((float)data[5], (float)data[6], (float)data[7]);
        Vector3 parentLossyScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(
            desiredWorldScale.x / parentLossyScale.x,
            desiredWorldScale.y / parentLossyScale.y,
            desiredWorldScale.z / parentLossyScale.z);
    }
}
