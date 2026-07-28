using MixedReality.Toolkit.SpatialManipulation;
using Photon.Pun;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// 掛在 Assets/Resources/Sticker/StickerPlaced.prefab 上，讓已經貼在碗上的貼紙可以被抓起來拔掉。
// 跟 StickerDraggable 的手感相反：抓起來、拉開一段距離（相對於貼著的那個點，會隨碗一起移動）
// 再放開才真的移除；只是碰一下、沒拉多遠就放開的話，貼紙會彈回原本貼著的位置/角度，避免手滑誤刪。
// 移除是透過 RPC 請 MasterClient 執行 PhotonNetwork.Destroy——MasterClient 對場上任何網路物件都有
// 刪除權限，不受 ownership 限制，這樣不管是誰貼的貼紙，房間裡任何一個玩家都能把它拔掉。
[RequireComponent(typeof(ObjectManipulator))]
public class StickerRemovable : MonoBehaviourPun
{
    [SerializeField] private float removeDistance = 0.05f; // 從原本貼著的位置拉開多遠才算「要拔掉」

    private ObjectManipulator _manipulator;
    private Transform _parentAtPlacement;
    private Vector3 _placedLocalPosition;
    private Quaternion _placedLocalRotation;

    private void Awake()
    {
        _manipulator = GetComponent<ObjectManipulator>();
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

    private void HandleGrabbed(SelectEnterEventArgs args)
    {
        // 記錄「被抓起來當下」貼著的狀態，放開時用來判斷有沒有被拉開，或是要彈回原位
        _parentAtPlacement = transform.parent;
        _placedLocalPosition = transform.localPosition;
        _placedLocalRotation = transform.localRotation;
    }

    private void HandleReleased(SelectExitEventArgs args)
    {
        Vector3 placedWorldPosition = _parentAtPlacement != null
            ? _parentAtPlacement.TransformPoint(_placedLocalPosition)
            : _placedLocalPosition;

        float distance = Vector3.Distance(transform.position, placedWorldPosition);

        if (distance >= removeDistance)
        {
            photonView.RPC(nameof(RequestDestroy), RpcTarget.MasterClient);
        }
        else
        {
            SnapBackToPlacement();
        }
    }

    private void SnapBackToPlacement()
    {
        if (_parentAtPlacement != null)
        {
            transform.SetParent(_parentAtPlacement, false);
        }
        transform.localPosition = _placedLocalPosition;
        transform.localRotation = _placedLocalRotation;
    }

    [PunRPC]
    private void RequestDestroy()
    {
        // 只有 MasterClient 會執行到這裡；MasterClient 可以刪除場上任何網路物件，
        // 不管原本是哪個玩家貼上去的都能拔掉。
        PhotonNetwork.Destroy(gameObject);
    }
}
