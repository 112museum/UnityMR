using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 掛在窯爐物件上，該物件需要有 PhotonView 元件（場景內固定擺放即可，不需要 PhotonNetwork.Instantiate）。
// 玩法：桌上的序列顯示器會一組一組閃出顏色，兩位玩家要記住後照順序按下桌子兩側對應顏色的按鈕。
// 每組可能是「單色」（按一顆）或「雙色」（顯示器上下各一色，左右兩側各一顆，兩人要在 pairPressWindow 秒內都按到）。
// 每完成一組，進度就從「上一關的溫度」往「這一關綠色區間」前進一格，全部完成剛好進入綠色區間＝過關；
// 按錯或雙色沒在時限內湊齊，進度直接跳回上一關的溫度，留在同一關重新閃一次序列再挑戰。
// 判定只由 MasterClient 做（按鈕透過 RPC 把「按了第幾顆」送給 MasterClient），BurnProgress 用
// IPunObservable 廣播；關卡開始/結果用 RPC 通知所有人，讓每台裝置自己播放閃爍、切換按鈕。
public class PhotonKilnBurningManager : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("桌上的顏色按鈕")]
    // 每關只會啟用陣列前 activeButtonCount 個，所以請照「左、右、左、右…」交錯排序：
    // 偶數索引（0,2,4…）視為左側、奇數索引（1,3,5…）視為右側，雙色組會左右各挑一顆。
    // 每顆按鈕的顏色請設成不一樣。
    [SerializeField] private ColorSequenceButton[] buttons;

    [System.Serializable]
    private struct SequenceLevel
    {
        public int activeButtonCount; // 這關桌上啟用幾顆按鈕
        public int sequenceLength;    // 這關有幾組（每組單色或雙色）
        [Range(0f, 1f)]
        public float doubleChance;    // 每組是雙色的機率：0＝全單色、1＝全雙色，介於中間時保證單雙都至少出現一次
        public float rangeMin;        // 這關的綠色成功區間
        public float rangeMax;
    }

    [Header("關卡設定：全部組數完成時，進度會停在該關 rangeMin~rangeMax 的中間")]
    [SerializeField] private SequenceLevel[] levels = new SequenceLevel[]
    {
        new SequenceLevel { activeButtonCount = 4, sequenceLength = 5, doubleChance = 0f,   rangeMin = 0.25f, rangeMax = 0.35f },
        new SequenceLevel { activeButtonCount = 6, sequenceLength = 5, doubleChance = 0.5f, rangeMin = 0.55f, rangeMax = 0.65f },
        new SequenceLevel { activeButtonCount = 8, sequenceLength = 5, doubleChance = 1f,   rangeMin = 0.85f, rangeMax = 0.95f },
    };

    [Header("雙色組：兩顆都要在這個秒數內按到（從第一顆按下開始算）")]
    // 判定以 MasterClient 收到按鈕的時間為準，網路延遲約 0.1~0.2 秒，不要設太短
    [SerializeField] private float pairPressWindow = 1f;

    [Header("顏色序列顯示（上下兩半，各自 Graphic 或 Renderer 擇一指定即可；單色組兩半顯示同一色）")]
    [FormerlySerializedAs("sequenceDisplayGraphic")]
    [SerializeField] private Graphic displayTopGraphic;
    [FormerlySerializedAs("sequenceDisplayRenderer")]
    [SerializeField] private Renderer displayTopRenderer;
    [SerializeField] private Graphic displayBottomGraphic;
    [SerializeField] private Renderer displayBottomRenderer;
    [SerializeField] private Color displayIdleColor = Color.gray; // 沒在閃的時候顯示的顏色
    [SerializeField] private float delayBeforePlayback = 1f;      // 關卡開始後隔多久開始閃
    [SerializeField] private float flashOnSeconds = 0.7f;         // 每組顏色亮多久
    [SerializeField] private float flashGapSeconds = 0.3f;        // 組與組之間暗多久

    [Header("節奏")]
    [SerializeField] private float progressSpeed = 0.3f;        // 進度條每秒最多移動多少（0~1）
    [SerializeField] private float delayAfterLevelClear = 1.5f; // 過關後隔多久開始下一關
    [SerializeField] private float delayAfterWrongPress = 1.5f; // 按錯後隔多久重新閃序列
    [SerializeField] private bool newSequenceOnRetry = false;   // 按錯重來時要不要換一組新順序

    [Header("燒製進度條（劇情把它打開＝遊戲開始）")]
    [SerializeField] private Slider progressSlider; // 或改用 Image + fillAmount，效果一樣

    [Header("目標區間視覺化")]
    // 掛 progressSlider 底下、Background 內的一個 Image RectTransform，
    // 會依目前關卡的 rangeMin/rangeMax 自動撐開對應寬度，當作區間色塊。
    [SerializeField] private RectTransform targetRangeIndicator;

    [Header("完成事件")]
    public UnityEvent onBurnComplete;

    [Header("各關專屬的完成效果，索引要跟 levels 對齊（stageCompleteEvents[0] 對應第 1 關，含最後一關）")]
    [SerializeField] private UnityEvent[] stageCompleteEvents;

    [Header("過程回饋（每台裝置都會觸發，可接音效、提示文字）")]
    public UnityEvent onPlaybackStart;   // 開始閃序列，請玩家記住
    public UnityEvent onInputReady;      // 閃完了，可以開始按
    public UnityEvent onPairHalfPressed; // 雙色組有一人按對了，等另一人按
    public UnityEvent onCorrectPress;    // 完成一整組（單色按到、或雙色兩顆都按到）
    public UnityEvent onWrongPress;      // 按錯，或雙色組沒在時限內湊齊

    public float BurnProgress { get; private set; }

    // 目前正在挑戰第幾關（0-based）。給 FireSeedTestStoryManager 這類外部腳本讀取，
    // 用來顯示對應關卡的提示文字；全部關卡通過才會觸發 onBurnComplete。
    public int CurrentStageIndex => _currentStageIndex;
    public int StageCount => levels != null ? levels.Length : 0;

    // 所有 client 都有（由 RpcStartLevel 同步）
    private bool _started;
    private bool _startRequested; // 這台已經因為進度條打開而送過開始請求，不要每幀重送
    private bool _completeFired;
    private int _currentStageIndex;
    // 這關每一組要按的按鈕索引（對應 buttons 陣列）。_stepB 是 -1 代表單色組。
    // 拆成兩個 int[] 而不是 int[][]，是因為 Photon RPC 不支援鋸齒陣列。
    private int[] _stepA;
    private int[] _stepB;
    private Coroutine _playback;

    // 只有 MasterClient 用得到的判定狀態
    private int _stepIndex;             // 目前在第幾組
    private bool _pressedA;             // 這組的 A / B 是否已經按到
    private bool _pressedB;
    private float _pairFirstPressTime;  // 雙色組第一顆按下的時間，用來判斷有沒有超過 pairPressWindow
    private bool _acceptingInput;       // 閃序列中、判定結果等待中都不收按鈕
    private bool _waitingForLevelClear; // 全部組數都完成了，等進度條跑進綠色區間才算過關
    private float _targetProgress;

    private void Awake()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null) buttons[i].Bind(this, i);
        }
    }

    private void Start()
    {
        // 開始前按鈕全部藏起來，跟進度條一起出現；之後每關由 RpcStartLevel 依 activeButtonCount 逐步加多（4 → 6 → 8）
        ApplyActiveButtons(0);
        SetDisplayColors(displayIdleColor, displayIdleColor);
        UpdateSlider();
        UpdateTargetRangeIndicator();
    }

    private void Update()
    {
        if (!PhotonNetwork.InRoom) return;

        // 劇情把進度條打開時開始遊戲。每台都各自偵測，因為劇情不一定先在 MasterClient 那台打開；
        // 請求一律轉給 MasterClient，它已經開始過就會忽略，所以兩台都送也只會開始一次。
        if (!_started && !_startRequested && progressSlider != null && progressSlider.gameObject.activeInHierarchy)
        {
            _startRequested = true;
            StartGame();
        }

        // 只有 MasterClient 真正計算進度，其餘 client 只顯示 OnPhotonSerializeView 同步回來的結果
        if (!PhotonNetwork.IsMasterClient) return;
        if (_completeFired || !_started) return;

        // 雙色組只按到一顆、另一顆超過時限還沒按到，算失敗
        if (_acceptingInput && _pressedA != _pressedB && Time.time - _pairFirstPressTime > pairPressWindow)
        {
            FailLevel();
        }

        BurnProgress = Mathf.MoveTowards(BurnProgress, _targetProgress, progressSpeed * Time.deltaTime);
        UpdateSlider();

        if (_waitingForLevelClear && Mathf.Approximately(BurnProgress, _targetProgress))
        {
            _waitingForLevelClear = false;
            ClearCurrentLevel();
        }
    }

    // 進度條打開時會自動呼叫；也可以給劇情流程或「開始」按鈕直接呼叫。哪一台呼叫都可以，會轉給 MasterClient 執行。
    public void StartGame()
    {
        photonView.RPC(nameof(RpcRequestStart), RpcTarget.MasterClient);
    }

    // ColorSequenceButton.Press() 呼叫：把「按了第幾顆」送給 MasterClient 判定
    public void SubmitPress(int buttonIndex)
    {
        photonView.RPC(nameof(RpcSubmitPress), RpcTarget.MasterClient, buttonIndex);
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

    // 判定狀態只存在 MasterClient 身上，換人時新的 MasterClient 不知道序列按到哪，
    // 所以直接讓目前這一關從頭重來（重新閃一次序列），不會卡死。
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (!PhotonNetwork.IsMasterClient || !_started || _completeFired) return;
        StopAllCoroutines();
        _playback = null;
        BeginLevel(_currentStageIndex, null, null);
    }

    // ---------------- MasterClient 判定 ----------------

    private void BeginLevel(int levelIndex, int[] stepA, int[] stepB)
    {
        _started = true;
        _stepIndex = 0;
        _pressedA = _pressedB = false;
        _acceptingInput = false;
        _waitingForLevelClear = false;
        BurnProgress = _targetProgress = LevelBaseProgress(levelIndex);
        UpdateSlider();

        if (stepA == null || stepB == null) GenerateSequence(levels[levelIndex], out stepA, out stepB);
        photonView.RPC(nameof(RpcStartLevel), RpcTarget.All, levelIndex, stepA, stepB);
    }

    // 每組隨機決定單色或雙色，再從這關啟用的按鈕裡挑：雙色組左右各一顆，單色組兩側任選一顆。
    // 顏色可以在整串裡重複出現，但相鄰兩組不會用到同一顆按鈕，避免玩家分不清是不是同一組。
    private void GenerateSequence(SequenceLevel level, out int[] stepA, out int[] stepB)
    {
        int count = Mathf.Min(level.activeButtonCount, buttons.Length);
        int length = Mathf.Max(1, level.sequenceLength);

        var isDouble = new bool[length];
        for (int i = 0; i < length; i++) isDouble[i] = Random.value < level.doubleChance;

        // 「有時單有時雙」的關卡，至少要各出現一次，不然隨機可能整關都是同一種
        if (level.doubleChance > 0f && level.doubleChance < 1f && length >= 2)
        {
            if (System.Array.TrueForAll(isDouble, d => !d)) isDouble[Random.Range(0, length)] = true;
            else if (System.Array.TrueForAll(isDouble, d => d)) isDouble[Random.Range(0, length)] = false;
        }

        var left = new List<int>();
        var right = new List<int>();
        for (int i = 0; i < count; i++) (i % 2 == 0 ? left : right).Add(i);

        stepA = new int[length];
        stepB = new int[length];
        int prevA = -1, prevB = -1;

        for (int i = 0; i < length; i++)
        {
            if (isDouble[i] && left.Count > 0 && right.Count > 0)
            {
                int l = PickExcluding(left, prevA, prevB);
                int r = PickExcluding(right, prevA, prevB);
                // 上下兩半哪一半是左側隨機決定，顯示器不透露按鈕在哪一邊
                bool swap = Random.value < 0.5f;
                stepA[i] = swap ? r : l;
                stepB[i] = swap ? l : r;
            }
            else
            {
                var all = new List<int>(left);
                all.AddRange(right);
                stepA[i] = PickExcluding(all, prevA, prevB);
                stepB[i] = -1;
            }
            prevA = stepA[i];
            prevB = stepB[i];
        }
    }

    // 從 pool 隨機挑一顆，避開上一組用過的按鈕；按鈕太少避不開時才允許重複
    private static int PickExcluding(List<int> pool, int excludeA, int excludeB)
    {
        var candidates = pool.FindAll(i => i != excludeA && i != excludeB);
        if (candidates.Count == 0) candidates = pool;
        return candidates[Random.Range(0, candidates.Count)];
    }

    [PunRPC]
    private void RpcRequestStart()
    {
        if (!PhotonNetwork.IsMasterClient || _started) return;
        if (levels == null || levels.Length == 0) return;
        BeginLevel(0, null, null);
    }

    [PunRPC]
    private void RpcSubmitPress(int buttonIndex)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!_acceptingInput || _stepA == null) return;

        int a = _stepA[_stepIndex];
        int b = _stepB[_stepIndex];

        if (buttonIndex == a || buttonIndex == b)
        {
            if (buttonIndex == a) { if (_pressedA) return; _pressedA = true; } // 同一顆重複按，忽略
            else                  { if (_pressedB) return; _pressedB = true; }

            bool stepDone = _pressedA && (b < 0 || _pressedB);
            if (!stepDone)
            {
                // 雙色組的第一顆：開始計時，等另一顆
                _pairFirstPressTime = Time.time;
                photonView.RPC(nameof(RpcPairHalfPressed), RpcTarget.All);
                return;
            }

            _stepIndex++;
            _pressedA = _pressedB = false;
            float t = (float)_stepIndex / _stepA.Length;
            _targetProgress = Mathf.Lerp(LevelBaseProgress(_currentStageIndex), LevelTargetProgress(_currentStageIndex), t);

            if (_stepIndex >= _stepA.Length)
            {
                _acceptingInput = false;
                _waitingForLevelClear = true;
            }
            photonView.RPC(nameof(RpcPressResult), RpcTarget.All, true);
        }
        else
        {
            FailLevel();
        }
    }

    // 按錯或雙色超時：進度直接跳回上一關的溫度（不是慢慢退），留在同一關重來
    private void FailLevel()
    {
        _acceptingInput = false;
        _pressedA = _pressedB = false;
        BurnProgress = _targetProgress = LevelBaseProgress(_currentStageIndex);
        UpdateSlider();
        photonView.RPC(nameof(RpcPressResult), RpcTarget.All, false);
        StartCoroutine(BeginLevelAfterDelay(_currentStageIndex, delayAfterWrongPress,
            newSequenceOnRetry ? null : _stepA, newSequenceOnRetry ? null : _stepB));
    }

    private void ClearCurrentLevel()
    {
        int nextStageIndex = _currentStageIndex + 1;

        if (nextStageIndex >= levels.Length)
        {
            Debug.Log("[PhotonKilnBurningManager] All levels complete, burn complete.");
            photonView.RPC(nameof(RpcBurnComplete), RpcTarget.All);
        }
        else
        {
            Debug.Log($"[PhotonKilnBurningManager] Level {_currentStageIndex} complete, advancing to level {nextStageIndex}.");
            photonView.RPC(nameof(RpcAdvanceStage), RpcTarget.All, nextStageIndex);
            StartCoroutine(BeginLevelAfterDelay(nextStageIndex, delayAfterLevelClear, null, null));
        }
    }

    private IEnumerator BeginLevelAfterDelay(int levelIndex, float delay, int[] stepA, int[] stepB)
    {
        yield return new WaitForSeconds(delay);
        if (!PhotonNetwork.IsMasterClient || _completeFired) yield break;
        BeginLevel(levelIndex, stepA, stepB);
    }

    // 這關全部完成時進度要停的位置：綠色區間正中間
    private float LevelTargetProgress(int levelIndex)
    {
        return (levels[levelIndex].rangeMin + levels[levelIndex].rangeMax) * 0.5f;
    }

    // 這關的起點＝上一關的溫度；第一關從 0 開始
    private float LevelBaseProgress(int levelIndex)
    {
        return levelIndex <= 0 ? 0f : LevelTargetProgress(levelIndex - 1);
    }

    // ---------------- 所有 client ----------------

    [PunRPC]
    private void RpcStartLevel(int levelIndex, int[] stepA, int[] stepB)
    {
        _started = true;
        _currentStageIndex = levelIndex;
        _stepA = stepA;
        _stepB = stepB;
        ApplyActiveButtons(levels[levelIndex].activeButtonCount);
        UpdateTargetRangeIndicator();

        if (_playback != null) StopCoroutine(_playback);
        _playback = StartCoroutine(PlaySequence(stepA, stepB));
    }

    // 每台裝置各自播放閃爍；MasterClient 播完才開始收按鈕，
    // 所以另一台在閃的過程中搶按也會被 MasterClient 忽略。
    private IEnumerator PlaySequence(int[] stepA, int[] stepB)
    {
        SetDisplayColors(displayIdleColor, displayIdleColor);
        onPlaybackStart?.Invoke();
        yield return new WaitForSeconds(delayBeforePlayback);

        for (int i = 0; i < stepA.Length; i++)
        {
            Color top = buttons[stepA[i]].Color;
            Color bottom = stepB[i] >= 0 ? buttons[stepB[i]].Color : top; // 單色組上下同色
            SetDisplayColors(top, bottom);
            yield return new WaitForSeconds(flashOnSeconds);
            SetDisplayColors(displayIdleColor, displayIdleColor);
            yield return new WaitForSeconds(flashGapSeconds);
        }

        _playback = null;
        if (PhotonNetwork.IsMasterClient) _acceptingInput = true;
        onInputReady?.Invoke();
    }

    [PunRPC]
    private void RpcPairHalfPressed()
    {
        onPairHalfPressed?.Invoke();
    }

    [PunRPC]
    private void RpcPressResult(bool correct)
    {
        if (correct) onCorrectPress?.Invoke();
        else onWrongPress?.Invoke();
    }

    // MasterClient 判定某一關過關時廣播給房間所有人（含自己），
    // 把區間色塊換到下一關的位置，同時觸發這一關專屬的完成效果（stageCompleteEvents）。
    [PunRPC]
    private void RpcAdvanceStage(int nextStageIndex)
    {
        int completedStageIndex = nextStageIndex - 1; // 剛過關的是「下一關」的前一個
        _currentStageIndex = nextStageIndex;
        UpdateTargetRangeIndicator();

        InvokeStageCompleteEvents(completedStageIndex);
    }

    [PunRPC]
    private void RpcBurnComplete()
    {
        // 最後一關不會走 RpcAdvanceStage，_currentStageIndex 這時候還停在最後一關的索引，
        // 所以這裡直接拿來當「剛完成的關卡」用。stageCompleteEvents 含最後一關；onBurnComplete 是全部完成的收尾事件。
        // 房間裡兩台裝置都會收到，各自播放對應特效、開放後續貼紙客製化流程。
        _completeFired = true;
        InvokeStageCompleteEvents(_currentStageIndex);
        onBurnComplete?.Invoke();
    }

    private void ApplyActiveButtons(int activeCount)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null) buttons[i].gameObject.SetActive(i < activeCount);
        }
    }

    private void SetDisplayColors(Color top, Color bottom)
    {
        if (displayTopGraphic != null) displayTopGraphic.color = top;
        if (displayTopRenderer != null) displayTopRenderer.material.color = top;
        if (displayBottomGraphic != null) displayBottomGraphic.color = bottom;
        if (displayBottomRenderer != null) displayBottomRenderer.material.color = bottom;
    }

    private void UpdateSlider()
    {
        if (progressSlider != null)
        {
            progressSlider.value = BurnProgress;
        }
    }

    // 把區間色塊的水平錨點對齊到「目前關卡」rangeMin/rangeMax 在 slider 上的比例位置，
    // 用 InverseLerp 換算是為了保險——就算 progressSlider 的 min/max 之後改成不是 0~1 也不會跑版。
    private void UpdateTargetRangeIndicator()
    {
        if (targetRangeIndicator == null || progressSlider == null) return;
        if (levels == null || _currentStageIndex >= levels.Length) return; // 全部關卡都完成了，不用再顯示

        SequenceLevel level = levels[_currentStageIndex];
        float min01 = Mathf.InverseLerp(progressSlider.minValue, progressSlider.maxValue, level.rangeMin);
        float max01 = Mathf.InverseLerp(progressSlider.minValue, progressSlider.maxValue, level.rangeMax);

        targetRangeIndicator.anchorMin = new Vector2(min01, targetRangeIndicator.anchorMin.y);
        targetRangeIndicator.anchorMax = new Vector2(max01, targetRangeIndicator.anchorMax.y);
        targetRangeIndicator.offsetMin = Vector2.zero;
        targetRangeIndicator.offsetMax = Vector2.zero;
    }

    private void InvokeStageCompleteEvents(int completedStageIndex)
    {
        if (stageCompleteEvents != null && completedStageIndex >= 0 && completedStageIndex < stageCompleteEvents.Length)
        {
            stageCompleteEvents[completedStageIndex]?.Invoke();
        }
    }
}
