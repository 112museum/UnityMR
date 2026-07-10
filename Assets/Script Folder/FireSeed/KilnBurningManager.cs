using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 掛在窯爐物件上（需要 NetworkObject，由 Host/Server 端 Spawn）。
// 兩位玩家同時按住各自的火種，燒製進度才會增加；有人放開就暫停或衰退。
// 進度到 1 觸發完成事件（燒製特效、開放貼貼紙等）。
public class KilnBurningManager : NetworkBehaviour
{
    [Header("兩位玩家的火種")]
    [SerializeField] private NetworkedFireSeed player1FireSeed;
    [SerializeField] private NetworkedFireSeed player2FireSeed;

    [Header("燒製設定")]
    [SerializeField] private float burnDurationSeconds = 15f; // 兩人同時按滿幾秒算完成
    [SerializeField] private bool decayWhenReleased = true;
    [SerializeField] private float decayRate = 0.3f;          // 放開時，每秒下降的比例(0~1)

    [Header("UI (world-space canvas 上的進度條)")]
    [SerializeField] private Slider progressSlider; // 或改用 Image + fillAmount，效果一樣

    [Header("完成事件")]
    public UnityEvent onBurnComplete;

    // Server 為權威來源，所有 client 只讀
    public NetworkVariable<float> BurnProgress = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private bool _completeFired;
    private bool _player1Assigned;
    private bool _player2Assigned;

    public override void OnNetworkSpawn()
    {
        BurnProgress.OnValueChanged += HandleProgressChanged;
        HandleProgressChanged(0f, BurnProgress.Value); // 初始化 UI

        if (IsServer)
        {
            AssignFireSeedOwnership();
        }
    }

    public override void OnNetworkDespawn()
    {
        BurnProgress.OnValueChanged -= HandleProgressChanged;

        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnClientConnectedCallback -= TryAssignFireSeedTo;
        }
    }

    // 兩顆火種是場景預先擺放的 NetworkObject，Spawn 當下 owner 預設是 Server。
    // 這裡依照玩家連線順序，把第一個連上的 client 指派給 player1、第二個指派給 player2。
    private void AssignFireSeedOwnership()
    {
        // 這個 Manager Spawn 時可能已經有 client 連上了(例如 Host 自己)，先處理一輪
        foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
        {
            TryAssignFireSeedTo(clientId);
        }

        NetworkManager.OnClientConnectedCallback += TryAssignFireSeedTo;
    }

    private void TryAssignFireSeedTo(ulong clientId)
    {
        if (!IsServer) return;

        if (!_player1Assigned && player1FireSeed != null)
        {
            player1FireSeed.NetworkObject.ChangeOwnership(clientId);
            _player1Assigned = true;
            Debug.Log($"[KilnBurningManager] Player1 火種 ownership 指派給 client {clientId}");
            return;
        }

        if (!_player2Assigned && player2FireSeed != null)
        {
            player2FireSeed.NetworkObject.ChangeOwnership(clientId);
            _player2Assigned = true;
            Debug.Log($"[KilnBurningManager] Player2 火種 ownership 指派給 client {clientId}");
        }
    }

    private void Update()
    {
        // 只有 Server(Host)真正計算進度，Client 只顯示同步結果
        if (!IsServer) return;
        if (_completeFired) return;

        bool bothHeld = player1FireSeed != null && player2FireSeed != null
                         && player1FireSeed.IsHeldNetworked.Value
                         && player2FireSeed.IsHeldNetworked.Value;

        float progress = BurnProgress.Value;

        if (bothHeld)
        {
            progress += Time.deltaTime / burnDurationSeconds;
        }
        else if (decayWhenReleased)
        {
            progress -= Time.deltaTime * decayRate;
        }

        progress = Mathf.Clamp01(progress);
        BurnProgress.Value = progress;

        if (progress >= 1f)
        {
            _completeFired = true;
            Debug.Log("[KilnBurningManager] Burn complete.");
            CompleteBurnClientRpc();
        }
    }

    private void HandleProgressChanged(float oldValue, float newValue)
    {
        if (progressSlider != null)
        {
            progressSlider.value = newValue;
        }
    }

    [ClientRpc]
    private void CompleteBurnClientRpc()
    {
        // 兩台 HoloLens 都會收到，各自播放特效、開放後續貼紙客製化流程
        onBurnComplete?.Invoke();
    }
}
