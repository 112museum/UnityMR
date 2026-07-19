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
    [SerializeField] private float placementRange = 0.15f; // 離碗的 Renderer bounds 多近才算「貼上去」
    [SerializeField] private string placedStickerPrefabName = "Sticker/StickerPlaced";

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

        Renderer bowlRenderer = bowlTarget.GetComponentInChildren<Renderer>();
        if (bowlRenderer == null) return;

        Vector3 closestPoint = bowlRenderer.bounds.ClosestPoint(transform.position);
        float distance = Vector3.Distance(transform.position, closestPoint);
        if (distance > placementRange) return;

        // 用「碗中心 -> 最近表面點」的方向近似表面法線，讓貼紙面朝外，
        // 對圓弧形的碗/罐來說是合理的簡化(不用真的採樣 mesh 法線)。
        Vector3 outwardNormal = (closestPoint - bowlRenderer.bounds.center).normalized;
        if (outwardNormal == Vector3.zero) outwardNormal = Vector3.up;
        Quaternion rotation = Quaternion.LookRotation(-outwardNormal, Vector3.up);

        PhotonNetwork.Instantiate(
            placedStickerPrefabName,
            closestPoint,
            rotation,
            data: new object[] { stickerColor.r, stickerColor.g, stickerColor.b });
    }

    private void ReturnToTray()
    {
        transform.localPosition = _trayLocalPosition;
        transform.localRotation = _trayLocalRotation;
    }
}
