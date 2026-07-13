using Unity.Netcode;
using UnityEngine;

// [已停用] 專案的雙人共同視角是用 Photon PUN 做的，這支腳本是 Unity Netcode for
// GameObjects (NGO) 版本，場景裡從未真的接上任何 NetworkManager/連線，不要掛到物件上。
// 正式使用請改掛 PhotonFireSeed.cs（邏輯一一對應，只是同步機制換成 Photon）。
// 保留這支腳本只是避免刪掉還沒被採用前的既有程式碼，之後確定不用可以整支移除。
//
// 掛在跟 FireSeedButton 同一個 GameObject 上（該物件需要有 NetworkObject 元件）。
// 把「這顆火種是否被按住」同步給另一台 HoloLens。
// P1、P2 各自的火種物件 Ownership 需設成該玩家自己的 client。
[RequireComponent(typeof(FireSeedButton))]
public class NetworkedFireSeed : NetworkBehaviour
{
    // 任何人都能讀，只有擁有者（該玩家自己的裝置）能寫
    public NetworkVariable<bool> IsHeldNetworked = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private FireSeedButton _button;
    private bool _listenersRegistered;

    private void Awake()
    {
        _button = GetComponent<FireSeedButton>();
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[NetworkedFireSeed] '{gameObject.name}' spawned. IsOwner={IsOwner}, OwnerClientId={OwnerClientId}");

        IsHeldNetworked.OnValueChanged += HandleHeldChanged;

        // 場景預先擺放時，Spawn 當下 owner 預設是 Server，實際玩家是之後才被
        // ChangeOwnership 指派過來的，所以這裡只是處理「一開始就是 owner」的情況
        // （例如 Host 自己那顆），其餘情況交給 OnGainedOwnership()。
        if (IsOwner)
        {
            RegisterListeners();
        }
    }

    public override void OnNetworkDespawn()
    {
        IsHeldNetworked.OnValueChanged -= HandleHeldChanged;
        UnregisterListeners();
    }

    // Server 用 NetworkObject.ChangeOwnership() 把這顆火種指派給對應玩家時觸發
    public override void OnGainedOwnership()
    {
        Debug.Log($"[NetworkedFireSeed] '{gameObject.name}' ownership 轉移給本機 (clientId={OwnerClientId})。");
        RegisterListeners();
    }

    public override void OnLostOwnership()
    {
        Debug.Log($"[NetworkedFireSeed] '{gameObject.name}' 本機失去 ownership。");
        UnregisterListeners();

        // 失去擁有權時如果還處於按住狀態，強制放開，避免發光卡在「按住」的樣子
        if (_button.IsHeld)
        {
            _button.OnPressEnd();
        }
    }

    private void RegisterListeners()
    {
        if (_listenersRegistered) return;
        // 本地端按下/放開時，把結果寫進 NetworkVariable 廣播出去
        _button.onPressStart.AddListener(HandlePressStart);
        _button.onPressEnd.AddListener(HandlePressEnd);
        _listenersRegistered = true;
    }

    private void UnregisterListeners()
    {
        if (!_listenersRegistered) return;
        _button.onPressStart.RemoveListener(HandlePressStart);
        _button.onPressEnd.RemoveListener(HandlePressEnd);
        _listenersRegistered = false;
    }

    private void HandlePressStart() => IsHeldNetworked.Value = true;
    private void HandlePressEnd() => IsHeldNetworked.Value = false;

    private void HandleHeldChanged(bool oldValue, bool newValue)
    {
        Debug.Log($"[NetworkedFireSeed] '{gameObject.name}' IsHeldNetworked {oldValue} -> {newValue} (OwnerClientId={OwnerClientId})");
    }
}
