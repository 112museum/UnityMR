using System;
using MixedReality.Toolkit.SpatialManipulation;
using Photon.Pun;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// 掛在玩家自己那盤（Tray）的貼紙上，該物件需要有 ObjectManipulator（MRTK3）讓玩家能捏住拖曳，
// 以及一個非 Trigger 的 Collider 供抓取判定用。
// 放開時如果離目標（碗）夠近，就用 PhotonNetwork.Instantiate 在該處生成一顆「已貼上」的網路貼紙
// （見 StickerPlacedNetworked.cs），讓房間裡的另一位玩家立刻看到；本體貼紙則彈回原本在盤子上
// 的位置，可以無限次重複拿取貼上——貼貼紙是自由裝飾，不是有次數限制的任務，方便反覆測試。
[RequireComponent(typeof(ObjectManipulator))]
public class StickerDraggable : MonoBehaviour
{
    // 給面板的裁切設定用（例如 StickerPanelClippingSetup）：貼紙離開面板變成實際尺寸的瞬間、
    // 跟放開後彈回面板的瞬間各觸發一次，讓外部決定「這時候要不要把 Renderer 從 ClippingBox 移除/加回」。
    // StickerDraggable 本身不認識 ClippingBox（那是面板專屬的視覺效果，一般 Tray 貼紙用不到）。
    public event Action<StickerDraggable> LiftedOffPanel;
    public event Action<StickerDraggable> ReturnedToPanel;

    [SerializeField] private Color stickerColor = Color.red; // 沒指定 stickerMaterial 時的備用做法：直接染色，不帶圖案
    [SerializeField] private Material stickerMaterial; // 貼紙圖案本身的材質；有設定的話貼上碗後會直接套用同一份材質，保留圖案（材質要放在 Resources 底下，見 placedStickerMaterialsResourcesPath）
    [SerializeField] private string placedStickerMaterialsResourcesPath = "Sticker/Materials/"; // stickerMaterial 要能在接收端用 Resources.Load 找到，得放在 Assets/Resources/ 這個路徑底下
    [SerializeField] private Transform bowlTarget;
    [SerializeField] private float placementRange = 0.15f; // 離碗的實際表面多近才算「貼上去」
    [SerializeField] private string placedStickerPrefabName = "Sticker/StickerPlaced";
    [SerializeField] private float surfaceOffset = 0.002f; // 貼紙沿法線微幅推出表面，避免跟碗的曲面 z-fighting
    [SerializeField] private float stackOffsetStep = 0.0005f; // 每多一顆已貼的貼紙，再多推出一點，避免兩顆貼紙重疊處彼此 z-fighting

    [Header("面板預覽（選填）")]
    // 面板上顯示的大小就是這個物件本身的 Transform Scale，所見即所得，不用另外設定；
    // 這裡只要填「拿起來瞬間要變成的實際尺寸」。留 (0,0,0) 就跟原本 Tray 的用法一樣，完全不做縮放。
    [SerializeField] private Vector3 actualLocalScale = Vector3.zero;

    private ObjectManipulator _manipulator;
    private Vector3 _trayLocalPosition;
    private Quaternion _trayLocalRotation;
    private Vector3 _panelLocalScale;
    private bool _hasPreviewScale;

    private void Awake()
    {
        _manipulator = GetComponent<ObjectManipulator>();
        _trayLocalPosition = transform.localPosition;
        _trayLocalRotation = transform.localRotation;
        _panelLocalScale = transform.localScale; // Editor 上原本設定的大小，就是面板要顯示的大小

        _hasPreviewScale = actualLocalScale != Vector3.zero;
    }

    private void OnEnable()
    {
        _manipulator.selectEntered.AddListener(HandleGrabbed);
        _manipulator.selectExited.AddListener(HandleReleased);
    }

    private void OnDisable()
    {
        _manipulator.selectEntered.RemoveListener(HandleGrabbed);
        _manipulator.selectExited.RemoveListener(HandleReleased);
    }

    // Slot 跟 Content（滑動背板）的 Collider 已經分開註冊，抓到這個物件就一定是「要拿走」，
    // 不會是在滑面板瀏覽圖樣，所以一抓起來就直接瞬間變成實際尺寸，不用等拉開一段距離才判斷。
    private void HandleGrabbed(SelectEnterEventArgs args)
    {
        if (_hasPreviewScale)
        {
            transform.localScale = actualLocalScale;
            LiftedOffPanel?.Invoke(this);
        }
    }

    // 給劇情中才用 PhotonNetwork.Instantiate 動態生成的碗使用：那種碗在存場景檔當下還不存在，
    // 沒辦法在 Inspector 裡先把 bowlTarget 拖進去，只能等碗生成完之後由呼叫端（例如碗身上
    // 實作 IPunInstantiateMagicCallback 的腳本，在 OnPhotonInstantiate 裡）呼叫這個方法補上。
    public void SetBowlTarget(Transform bowl) => bowlTarget = bowl;

    private void HandleReleased(SelectExitEventArgs args)
    {
        TryPlaceOnBowl();
        ReturnToTray();
    }

    private void TryPlaceOnBowl()
    {
        if (bowlTarget == null) return;

        Collider bowlCollider = bowlTarget.GetComponentInChildren<Collider>();
        if (bowlCollider == null) return;

        // 用 Collider 實際表面做 Raycast（而不是 Renderer.bounds 的 AABB），
        // 對圓弧形的碗/罐才能算出真正貼在表面上的點跟法線；就算是非 convex 的
        // MeshCollider，Raycast 一樣能打到正確表面（不像 ClosestPoint 只支援 convex）。
        Vector3 toBowlCenter = bowlCollider.bounds.center - transform.position;
        float maxDistance = toBowlCenter.magnitude + bowlCollider.bounds.extents.magnitude;
        if (maxDistance < 0.0001f) return;

        if (!bowlCollider.Raycast(new Ray(transform.position, toBowlCenter), out RaycastHit hit, maxDistance))
        {
            return; // 沒打中碗的表面，距離太遠或角度不對
        }

        float distance = Vector3.Distance(transform.position, hit.point);
        if (distance > placementRange) return;

        Quaternion rotation = Quaternion.LookRotation(-hit.normal, Vector3.up);

        // 每顆貼紙沿法線推出的距離都跟「這顆碗上已經貼了幾顆」有關，就算兩顆貼紙
        // 貼在同一個位置重疊，深度也一定不一樣，不會剛好卡在同一個平面上互相 z-fighting。
        int existingStickerCount = bowlCollider.transform.childCount;
        float totalOffset = surfaceOffset + existingStickerCount * stackOffsetStep;
        Vector3 placementPosition = hit.point + hit.normal * totalOffset;

        // 有指定 stickerMaterial 就把它在 Resources 底下的路徑帶過去，讓接收端用 Resources.Load
        // 找到同一份材質、連圖案一起套用；沒指定的話（例如舊的示範貼紙）就退回原本只傳顏色染色的做法。
        string materialResourcePath = stickerMaterial != null
            ? placedStickerMaterialsResourcesPath + stickerMaterial.name
            : string.Empty;

        PhotonNetwork.Instantiate(
            placedStickerPrefabName,
            placementPosition,
            rotation,
            data: new object[]
            {
                stickerColor.r, stickerColor.g, stickerColor.b, bowlTarget.name, materialResourcePath,
                // 傳世界尺寸（lossyScale）而不是 localScale：接收端的 parent（碗）如果自己也有
                // 縮放，兩個 localScale 會疊乘在一起讓貼紙變得更小，用世界尺寸換算才不會多乘一次。
                transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z,
            });
    }

    private void ReturnToTray()
    {
        transform.localPosition = _trayLocalPosition;
        transform.localRotation = _trayLocalRotation;

        // 不管這次有沒有真的拉出去過，放開後一律重置回面板顯示尺寸，格子才能無限次再拖出同樣圖樣。
        if (_hasPreviewScale)
        {
            transform.localScale = _panelLocalScale;
            ReturnedToPanel?.Invoke(this);
        }
    }
}
