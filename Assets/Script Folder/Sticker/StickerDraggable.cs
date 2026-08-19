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
    [SerializeField] private Color stickerColor = Color.red;
    [SerializeField] private Transform bowlTarget;
    [SerializeField] private float placementRange = 0.15f; // 離碗的實際表面多近才算「貼上去」
    [SerializeField] private string placedStickerPrefabName = "Sticker/StickerPlaced";
    [SerializeField] private float surfaceOffset = 0.002f; // 貼紙沿法線微幅推出表面，避免跟碗的曲面 z-fighting
    [SerializeField] private float stackOffsetStep = 0.0005f; // 每多一顆已貼的貼紙，再多推出一點，避免兩顆貼紙重疊處彼此 z-fighting

    private ObjectManipulator _manipulator;
    private Vector3 _trayLocalPosition;
    private Quaternion _trayLocalRotation;

    private void Awake()
    {
        _manipulator = GetComponent<ObjectManipulator>();
        _trayLocalPosition = transform.localPosition;
        _trayLocalRotation = transform.localRotation;
    }

    private void OnEnable()
    {
        _manipulator.selectExited.AddListener(HandleReleased);

        // 貼紙面板打開的時機（劇情觸發）跟碗生成的時機（更早的章節）沒有保證的先後順序，
        // 所以除了 BowlSpawnedForStickers.Start() 那次主動指派之外，貼紙自己被啟用時也補問一次
        // 「現在有沒有已經生成的碗」——不管碗是先生成還是後生成，兩邊有一個成功指派就夠了。
        if (bowlTarget == null && BowlSpawnedForStickers.CurrentBowl != null)
        {
            bowlTarget = BowlSpawnedForStickers.CurrentBowl;
        }
    }

    // 給 BowlSpawnedForStickers.Start() 呼叫，讓碗生成後能主動指派給場景裡每一顆貼紙。
    public void SetBowlTarget(Transform target)
    {
        bowlTarget = target;
    }

    private void OnDisable()
    {
        _manipulator.selectExited.RemoveListener(HandleReleased);
    }

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

        PhotonNetwork.Instantiate(
            placedStickerPrefabName,
            placementPosition,
            rotation,
            data: new object[] { stickerColor.r, stickerColor.g, stickerColor.b, bowlTarget.name });
    }

    private void ReturnToTray()
    {
        transform.localPosition = _trayLocalPosition;
        transform.localRotation = _trayLocalRotation;
    }
}
