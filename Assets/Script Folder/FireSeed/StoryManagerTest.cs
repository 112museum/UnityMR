using UnityEngine;

// 測試用 Story Manager，掛在 StorySystem 上：
// 1) 停用會報錯的舊對話/語音系統(Talker/TextToSpeech/SpeechToTextManager)，讓這次測試 Console 乾淨。
// 2) 借用既有的 SubtitleDisplayManager.hintPanel，依照火種按住狀態顯示步驟提示文字。
// 之後接正式劇情/雙人網路版本時，把這支腳本移除即可，不會動到 Talker 等原本的腳本。
public class StoryManagerTest : MonoBehaviour
{
    [Header("停用會報錯的舊系統")]
    [SerializeField] private bool disableLegacyStorySystems = true;

    [Header("火種燒製測試")]
    [SerializeField] private FireSeedButton fireSeed;
    [SerializeField] private SoloBurnTestDriver burnDriver;

    private bool _wasHeld;
    private bool _completeShown;

    private void Awake()
    {
        if (disableLegacyStorySystems)
        {
            DisableLegacySystems();
        }
    }

    private void Start()
    {
        if (burnDriver != null)
        {
            burnDriver.onBurnComplete.AddListener(ShowCompleteHint);
        }

        ShowIdleHint();
    }

    private void DisableLegacySystems()
    {
        foreach (Talker talker in FindObjectsOfType<Talker>(true)) talker.enabled = false;
        foreach (TextToSpeech tts in FindObjectsOfType<TextToSpeech>(true)) tts.enabled = false;
        foreach (SpeechToTextManager stt in FindObjectsOfType<SpeechToTextManager>(true)) stt.enabled = false;
        Debug.Log("[StoryManagerTest] 已停用 Talker / TextToSpeech / SpeechToTextManager，避免測試期間噴錯。");
    }

    private void Update()
    {
        if (_completeShown || fireSeed == null) return;

        if (fireSeed.IsHeld != _wasHeld)
        {
            _wasHeld = fireSeed.IsHeld;
            if (_wasHeld) ShowBurningHint();
            else ShowIdleHint();
        }
    }

    private void ShowIdleHint()
    {
        SubtitleDisplayManager.Instance?.DisplayHintText("請按住火種，開始燒製蓮花碗");
    }

    private void ShowBurningHint()
    {
        SubtitleDisplayManager.Instance?.DisplayHintText("燒製中...請按住不要放開");
    }

    private void ShowCompleteHint()
    {
        _completeShown = true;
        SubtitleDisplayManager.Instance?.DisplayHintText("燒製完成！");
    }
}
