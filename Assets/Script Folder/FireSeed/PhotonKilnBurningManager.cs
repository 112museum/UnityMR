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

    [Header("燒製進度條")]
    [SerializeField] private Slider progressSlider; // 或改用 Image + fillAmount，效果一樣

    [System.Serializable]
    private struct BurnStage
    {
        public float rangeMin;
        public float rangeMax;
        public float requiredTimeInRange; // 連續維持在這個區間內幾秒，這階段才算過關
    }

    [Header("成功條件：依序完成每個階段的維持指定溫度任務")]
    [SerializeField] private BurnStage[] burnStages = new BurnStage[]
    {
        new BurnStage { rangeMin = 0.4f, rangeMax = 0.6f, requiredTimeInRange = 3f },
        new BurnStage { rangeMin = 0.7f, rangeMax = 0.9f, requiredTimeInRange = 3f },
    };

    [Header("目標區間視覺化")]
    // 掛 progressSlider 底下、Background 內的一個 Image RectTransform，
    // 會依目前階段的 rangeMin/rangeMax 自動撐開對應寬度，當作區間色塊。
    [SerializeField] private RectTransform targetRangeIndicator;

    [Header("完成事件")]
    public UnityEvent onBurnComplete;

    public float BurnProgress { get; private set; }

    // 目前正在挑戰第幾階段（0-based）。給 FireSeedTestStoryManager 這類外部腳本讀取，
    // 用來顯示對應階段的提示文字；全部階段通過（>= 陣列長度）才算真正完成。
    public int CurrentStageIndex => _currentStageIndex;
    public int StageCount => burnStages != null ? burnStages.Length : 0;

    private bool _completeFired;
    private int _currentStageIndex; // 目前正在挑戰第幾階段（0-based），全部通過才算真正完成
    private float _timeInRange; // 目前這個階段連續停在區間內的秒數，離開區間就歸零

    private void Start()
    {
        UpdateSlider();
        UpdateTargetRangeIndicator();
    }

    private void Update()
    {
        // 只有 MasterClient 真正計算進度，其餘 client 只顯示 OnPhotonSerializeView 同步回來的結果
        if (!PhotonNetwork.IsMasterClient) return;
        if (_completeFired) return;
        if (burnStages == null || burnStages.Length == 0) return; // Inspector 沒設定階段就先不判定

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

        BurnStage stage = burnStages[_currentStageIndex];
        bool inTargetRange = BurnProgress >= stage.rangeMin && BurnProgress <= stage.rangeMax;
        _timeInRange = inTargetRange ? _timeInRange + Time.deltaTime : 0f;

        if (_timeInRange >= stage.requiredTimeInRange)
        {
            _timeInRange = 0f;
            int nextStageIndex = _currentStageIndex + 1;

            if (nextStageIndex >= burnStages.Length)
            {
                _completeFired = true;
                Debug.Log("[PhotonKilnBurningManager] All stages complete, burn complete.");
                photonView.RPC(nameof(RpcBurnComplete), RpcTarget.All);
            }
            else
            {
                Debug.Log($"[PhotonKilnBurningManager] Stage {_currentStageIndex} complete, advancing to stage {nextStageIndex}.");
                photonView.RPC(nameof(RpcAdvanceStage), RpcTarget.All, nextStageIndex);
            }
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

    // 把區間色塊的水平錨點對齊到「目前階段」rangeMin/rangeMax 在 slider 上的比例位置，
    // 用 InverseLerp 換算是為了保險——就算 progressSlider 的 min/max 之後改成不是 0~1 也不會跑版。
    private void UpdateTargetRangeIndicator()
    {
        if (targetRangeIndicator == null || progressSlider == null) return;
        if (_currentStageIndex >= burnStages.Length) return; // 全部階段都完成了，不用再顯示

        BurnStage stage = burnStages[_currentStageIndex];
        float min01 = Mathf.InverseLerp(progressSlider.minValue, progressSlider.maxValue, stage.rangeMin);
        float max01 = Mathf.InverseLerp(progressSlider.minValue, progressSlider.maxValue, stage.rangeMax);

        targetRangeIndicator.anchorMin = new Vector2(min01, targetRangeIndicator.anchorMin.y);
        targetRangeIndicator.anchorMax = new Vector2(max01, targetRangeIndicator.anchorMax.y);
        targetRangeIndicator.offsetMin = Vector2.zero;
        targetRangeIndicator.offsetMax = Vector2.zero;
    }

    // MasterClient 判定某一階段過關時廣播給房間所有人（含自己），這樣非 MasterClient
    // 的裝置也能知道目前是第幾階段，把區間色塊換到下一階段該顯示的位置。
    [PunRPC]
    private void RpcAdvanceStage(int nextStageIndex)
    {
        _currentStageIndex = nextStageIndex;
        _timeInRange = 0f;
        UpdateTargetRangeIndicator();
    }

    [PunRPC]
    private void RpcBurnComplete()
    {
        // 房間裡兩台 HoloLens 都會收到，各自播放燒製完成特效、開放後續貼紙客製化流程
        onBurnComplete?.Invoke();
    }
}
