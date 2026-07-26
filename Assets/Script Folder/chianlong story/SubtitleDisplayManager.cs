using UnityEngine;
using TMPro;
using System.Collections;

public class SubtitleDisplayManager : MonoBehaviour
{
    public static SubtitleDisplayManager Instance;

    [Header("UI 引用")]
    public TextMeshProUGUI subtitleText;
    public TextMeshProUGUI hintText;
    public TextMeshProUGUI taskText;

    [Header("面板引用")]
    public GameObject subtitlePanel;
    public GameObject hintPanel;
    public GameObject taskPanel;

    private Coroutine subtitleCoroutine;
    private Coroutine hintCoroutine;
    private string _targetText = "";
    private bool _shouldUpdateUI = false;
    void Awake() { Instance = this; }
    void Start() { HideAll(); }

    void Update()
    {
        // 在 Update 裡更新 UI，這保證是在 Unity 主執行緒執行
        if (_shouldUpdateUI)
        {
            subtitleText.text = _targetText;
            _shouldUpdateUI = false;
        }
    }
    public void HideAll() {
        if (subtitlePanel != null) subtitlePanel.SetActive(false);
        if (hintPanel != null) hintPanel.SetActive(false);
        if (taskPanel != null) taskPanel.SetActive(false);
    }

    // AR組新增功能：借用hintPanel直接印出提示文字
    public void DisplayHintText(string content)
    {
        hintPanel.SetActive(true);
        hintText.text = content;
    }

    // 2. 提示文字
    public void DisplayHint(string fileName)
    {
        hintPanel.SetActive(true);

        string content =
            LoadText("hints", fileName);

        hintText.text = content;
    }
    // 3. 任務文字
    public void DisplayTask(string fileName) {
        taskPanel.SetActive(true);

        string content =
            LoadText("tasks", fileName);

        taskText.text = content;
    }

    // Free chat：AI 回覆整段直接印在 subtitlePanel（跟 StartLiveSubtitle/UpdateLiveSubtitle
    // 的逐字累加不同，這裡是一次性整段設定，見 StoryModeManager.HandleFreeChatNpcResponse）
    public void DisplaySubtitleText(string content)
    {
        subtitlePanel.SetActive(true);
        subtitleText.text = content;
    }

    // Free chat：借用 taskPanel 即時顯示玩家語音辨識結果（見 STTManager）
    public void DisplayPlayerSpeechText(string content)
    {
        taskPanel.SetActive(true);
        taskText.text = content;
    }
    public void HideSubtitle() { if (subtitlePanel != null) subtitlePanel.SetActive(false); }
    public void HideHint() { if (hintPanel != null) hintPanel.SetActive(false); }
    public void HideTask() { if (taskPanel != null) taskPanel.SetActive(false); }

    string LoadText(string folderName, string fileName) {
        string path = "Dialogues/" + folderName + "/" + fileName;
        TextAsset textFile = Resources.Load<TextAsset>(path);
        if (textFile == null) return "File missing: " + path;
        return textFile.text;
    }



    // 新增：供 Azure API 調用的即時更新方法
    // 專供即時語音使用的顯示方法
    public void StartLiveSubtitle()
    {
        _targetText = "";
        subtitleText.text = "";
        subtitlePanel.SetActive(true);
    }

    // 提供給外部（Azure 事件）呼叫
    public void UpdateLiveSubtitle(string word)
    {
        // 直接累加，不要加 " "
        _targetText += word; 
        _shouldUpdateUI = true;
    }

    // 語音結束時呼叫
    public void OnSynthesisFinished()
    {
        StartCoroutine(FadeOutSubtitle(2.0f));
    }

    IEnumerator FadeOutSubtitle(float delay)
    {
        yield return new WaitForSeconds(delay);
        subtitlePanel.SetActive(false);
    }
}