using Photon.Pun;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// 掛在跟 XRSocketInteractor 同一個 GameObject 上（例如碗的吸附 zone）。
// Socket 是每個 client 各自一份的本地元件：碗吸進 zone 時，兩台裝置的 socket 都會在本機 select 碗，
// 之後一個人把碗拿出來，另一台的 socket 還抓著碗——GenericNetSync 看到 interactable.isSelected
// 就以為「本機有人拿著」而不套用網路同步回來的位置，碗就卡在那台的 zone 裡。
// 所以只讓碗目前的 Photon owner 那台的 socket 能抓碗；其他 client 不抓，單純跟著同步位置走，
// 吸附結果一樣會透過 owner 的 GenericNetSync 傳過去。
[RequireComponent(typeof(XRSocketInteractor))]
public class OwnerOnlySocket : MonoBehaviour, IXRSelectFilter
{
    private XRSocketInteractor _socket;

    public bool canProcess => isActiveAndEnabled;

    private void Awake()
    {
        _socket = GetComponent<XRSocketInteractor>();
    }

    private void OnEnable()
    {
        _socket.selectFilters.Add(this);
    }

    private void OnDisable()
    {
        _socket.selectFilters.Remove(this);
    }

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        return IsMine(interactable);
    }

    private void Update()
    {
        // 另一個玩家把碗從 zone 拿走時，ownership 會轉到對方（OwnershipHandler 在 grab 時 RequestOwnership），
        // 這台原本吸住碗的 socket 必須主動放手，不然本機的碗會一直被吸在 attach point 上，跟同步位置打架。
        if (!_socket.hasSelection) return;

        var held = _socket.firstInteractableSelected;
        if (!IsMine(held))
        {
            _socket.interactionManager.SelectExit(_socket, held);
        }
    }

    private static bool IsMine(IXRSelectInteractable interactable)
    {
        var view = interactable.transform.GetComponentInParent<PhotonView>();
        // 沒有 PhotonView 的物件不是網路物件，照原本 socket 行為處理
        return view == null || view.IsMine;
    }
}
