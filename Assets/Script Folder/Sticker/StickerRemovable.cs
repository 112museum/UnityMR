using MixedReality.Toolkit.SpatialManipulation;
using Photon.Pun;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// 掛在 Assets/Resources/Sticker/StickerPlaced.prefab 上，讓已經貼在碗上的貼紙可以被抓起來調整位置，
// 或拔掉。放開時判斷的不是「離原本貼著的那個點多遠」，而是「離碗表面多遠」——貼紙的 parent 本來就是
// 碗身上有 Collider 的那個子物件（見 StickerPlacedNetworked），所以放開時直接對這個 Collider 重新
// 做一次 Raycast（跟 StickerDraggable.TryPlaceOnBowl 判斷貼不貼得上去用的是同一招）：
//   - Raycast 打得到碗表面、而且夠近 → 代表玩家只是要調整位置，直接重新貼到最近的表面上。
//   - 打不到、或離表面太遠 → 代表玩家是真的要把它從碗上拔掉，才觸發移除。
// 這樣不管在碗的哪個位置放開都能持續調整，不會因為離「當初貼上去那個點」有點距離就被誤判成要拔掉，
// 也不會因為在碗表面上滑一下就彈回原本那個死點。
// 移除是透過 RPC 請 MasterClient 執行 PhotonNetwork.Destroy——MasterClient 對場上任何網路物件都有
// 刪除權限，不受 ownership 限制，這樣不管是誰貼的貼紙，房間裡任何一個玩家都能把它拔掉。
[RequireComponent(typeof(ObjectManipulator))]
public class StickerRemovable : MonoBehaviourPun
{
    [SerializeField] private float removeDistance = 0.05f; // 離碗表面多遠才算「要拔掉」而不是「調整位置」
    [SerializeField] private float surfaceOffset = 0.002f; // 貼紙沿法線微幅推出表面，避免跟碗的曲面 z-fighting

    private ObjectManipulator _manipulator;

    private void Awake()
    {
        _manipulator = GetComponent<ObjectManipulator>();
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
        Collider bowlCollider = transform.parent != null ? transform.parent.GetComponent<Collider>() : null;

        if (bowlCollider == null || !TryRepositionOnSurface(bowlCollider))
        {
            photonView.RPC(nameof(RequestDestroy), RpcTarget.MasterClient);
        }
    }

    // 跟 StickerDraggable.TryPlaceOnBowl 同樣用 Collider 實際表面做 Raycast（而不是 Renderer.bounds
    // 的 AABB），對圓弧形的碗才能算出真正貼在表面上的點跟法線。成功貼回表面就回傳 true。
    private bool TryRepositionOnSurface(Collider bowlCollider)
    {
        Vector3 toBowlCenter = bowlCollider.bounds.center - transform.position;
        float maxDistance = toBowlCenter.magnitude + bowlCollider.bounds.extents.magnitude;
        if (maxDistance < 0.0001f) return false;

        if (!bowlCollider.Raycast(new Ray(transform.position, toBowlCenter), out RaycastHit hit, maxDistance))
        {
            return false; // 沒打中碗的表面，距離太遠或角度不對
        }

        if (Vector3.Distance(transform.position, hit.point) > removeDistance) return false;

        transform.position = hit.point + hit.normal * surfaceOffset;
        transform.rotation = Quaternion.LookRotation(-hit.normal, Vector3.up);
        return true;
    }

    [PunRPC]
    private void RequestDestroy()
    {
        // 只有 MasterClient 會執行到這裡；MasterClient 可以刪除場上任何網路物件，
        // 不管原本是哪個玩家貼上去的都能拔掉。
        PhotonNetwork.Destroy(gameObject);
    }
}
