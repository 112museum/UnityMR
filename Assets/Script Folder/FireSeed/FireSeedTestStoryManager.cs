using TMPro;
using UnityEngine;

// 掛在這個乾淨測試場景裡的 StoryManager 物件上。
// 只負責一件事：依照 PhotonKilnBurningManager 的燒製進度，切換畫面上的提示文字，
// 讓兩位測試者知道現在該做什麼，不牽涉任何舊的 Talker/TTS/STT 系統
// （這個測試場景本來就沒放那些東西，不需要像 StoryManagerTest.cs 那樣去停用它們）。
public class FireSeedTestStoryManager : MonoBehaviour
{
    [SerializeField] private PhotonKilnBurningManager kilnManager;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private RoomLinker roomLinker;
    [SerializeField] private StoryModeManager storyModeManager;

    [Header("依目前階段對應的提示文字")]
    [SerializeField] private string[] stageBurningHints = new string[]
    {
        "燒製中...請兩人維持在低溫！",
        "燒製中...請兩人維持在高溫！",
    };

    private bool _completeShown;
    private bool _wasBurning;
    private int _lastStageIndex = -1;

    private void Start()
    {
        // 燒製完成的判斷交給 PhotonKilnBurningManager 自己（現在是「連續停在目標區間
        // 內夠久」），這裡只負責監聽結果，不要自己另外土法煉鋼判斷 BurnProgress。
        kilnManager?.onBurnComplete.AddListener(ShowCompleteHint);

        // RoomLinker 現在不會自己在 Start() 連線了(要靠 UI 按鈕呼叫 JoinRoom())，
        // 這個測試場景進場就直接幫忙連，不用另外放一顆「連線」按鈕。
        // roomLinker?.JoinRoom();
        ShowIdleHint();
    }

    private void Update()
    {
        if (_completeShown || kilnManager == null) return;

        bool isBurning = kilnManager.BurnProgress > 0f && kilnManager.BurnProgress < 1f;
        int stageIndex = kilnManager.CurrentStageIndex;

        // 除了「有沒有在燒」的變化之外，還要看階段有沒有換——同一次持續按住的過程中
        // 也可能從第一階段直接推進到第二階段，這時候提示文字也要跟著換。
        bool stageChangedWhileBurning = isBurning && stageIndex != _lastStageIndex;

        if (isBurning != _wasBurning || stageChangedWhileBurning)
        {
            _wasBurning = isBurning;
            _lastStageIndex = stageIndex;
            if (isBurning) ShowBurningHint(stageIndex);
            // else if (kilnManager.BurnProgress <= 0f) ShowIdleHint();
        }
    }

    private void ShowIdleHint()
    {
        SetText("兩位玩家請同時按住兩側的按鈕，開始燒製");
    }

    private void ShowBurningHint(int stageIndex)
    {
        string message = "燒製中...請兩人維持指定溫度！"; // 沒設定對應文字時的保底訊息
        if (stageBurningHints != null && stageIndex >= 0 && stageIndex < stageBurningHints.Length)
        {
            message = stageBurningHints[stageIndex];
        }
        SetText(message);
    }

    private void ShowCompleteHint()
    {
        if (_completeShown) return;
        _completeShown = true;

        SetText("燒製完成！");
        storyModeManager?.OnPlayerInteractSuccess();
    }

    private void SetText(string message)
    {
        if (hintText != null) hintText.text = message;
    }
}
