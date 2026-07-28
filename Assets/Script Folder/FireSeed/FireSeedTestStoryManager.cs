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

    private bool _completeShown;
    private bool _wasBurning;

    private void Start()
    {
        // RoomLinker 現在不會自己在 Start() 連線了(要靠 UI 按鈕呼叫 JoinRoom())，
        // 這個測試場景進場就直接幫忙連，不用另外放一顆「連線」按鈕。
        roomLinker?.JoinRoom();
        ShowIdleHint();
    }

    private void Update()
    {
        if (_completeShown || kilnManager == null) return;

        bool isBurning = kilnManager.BurnProgress > 0f && kilnManager.BurnProgress < 1f;
        if (isBurning != _wasBurning)
        {
            _wasBurning = isBurning;
            if (isBurning) ShowBurningHint();
            else if (kilnManager.BurnProgress <= 0f) ShowIdleHint();
        }

        if (kilnManager.BurnProgress >= 1f)
        {
            _completeShown = true;
            ShowCompleteHint();
        }
    }

    private void ShowIdleHint()
    {
        SetText("P1、P2 請同時按住兩側的按鈕，開始燒製");
    }

    private void ShowBurningHint()
    {
        SetText("燒製中...請兩人都不要放開");
    }

    private void ShowCompleteHint()
    {
        SetText("燒製完成！");
    }

    private void SetText(string message)
    {
        if (hintText != null) hintText.text = message;
    }
}
