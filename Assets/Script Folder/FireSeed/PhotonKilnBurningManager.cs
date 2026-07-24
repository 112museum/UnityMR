using Photon.Pun;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 掛在窯爐物件上，該物件需要有 PhotonView 元件（場景內固定擺放即可，不需要 PhotonNetwork.Instantiate）。
// 兩位玩家必須同時按住各自的火種，燒製進度才會增加；有人放開就依 decayRate 衰退。
// 進度只由 MasterClient 計算，透過 IPunObservable 廣播給房間裡所有人；MasterClient 換人時
// Photon 會自動把這顆場景 PhotonView 的控制權轉交給新的 MasterClient，邏輯不需要額外處理。
public class PhotonKilnBurningManager : MonoBehaviourPun, IPunObservable
{
    [Header("兩位玩家的火種")]
    [SerializeField] private PhotonFireSeed player1FireSeed;
    [SerializeField] private PhotonFireSeed player2FireSeed;

    [Header("燒製設定")]
    [SerializeField] private float burnDurationSeconds = 15f; // 兩人同時按滿幾秒算完成
    [SerializeField] private bool decayWhenReleased = true;
    [SerializeField] private float decayRate = 0.3f;          // 放開時，每秒下降的比例(0~1)

    [Header("UI (world-space canvas 上的進度條)")]
    [SerializeField] private Slider progressSlider; // 或改用 Image + fillAmount，效果一樣

    [Header("完成事件")]
    public UnityEvent onBurnComplete;

    public float BurnProgress { get; private set; }

    private bool _completeFired;

    private void Start()
    {
        UpdateSlider();
    }

    private void Update()
    {
        // 只有 MasterClient 真正計算進度，其餘 client 只顯示 OnPhotonSerializeView 同步回來的結果
        if (!PhotonNetwork.IsMasterClient) return;
        if (_completeFired) return;

        bool bothHeld = player1FireSeed != null && player2FireSeed != null
                         && player1FireSeed.IsHeldNetworked
                         && player2FireSeed.IsHeldNetworked;

        float progress = BurnProgress;

        if (bothHeld)
        {
            progress += Time.deltaTime / burnDurationSeconds;
        }
        else if (decayWhenReleased)
        {
            progress -= Time.deltaTime * decayRate;
        }

        BurnProgress = Mathf.Clamp01(progress);
        UpdateSlider();

        if (BurnProgress >= 1f)
        {
            _completeFired = true;
            Debug.Log("[PhotonKilnBurningManager] Burn complete.");
            photonView.RPC(nameof(RpcBurnComplete), RpcTarget.All);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(BurnProgress);
        }
        else
        {
            BurnProgress = (float)stream.ReceiveNext();
            UpdateSlider();
        }
    }

    private void UpdateSlider()
    {
        if (progressSlider != null)
        {
            progressSlider.value = BurnProgress;
        }
    }

    [PunRPC]
    private void RpcBurnComplete()
    {
        // 房間裡兩台 HoloLens 都會收到，各自播放燒製完成特效、開放後續貼紙客製化流程
        onBurnComplete?.Invoke();
    }
}
