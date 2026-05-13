using UnityEngine;
using System.Collections;
using System;

public class Talker : MonoBehaviour
{
    public TextToSpeech ttsManager;
    // 在你的 Talker 或 TextToSpeech 腳本中
    // private bool isSpeaking = false;
    private static bool isGlobalSpeaking = false;
    private void Start()
    {
        if (ttsManager != null)
        {
            // 當 Azure 說出一個詞時，直接交給 SubtitleDisplayManager 的 UpdateLiveSubtitle 處理
            ttsManager.OnWordSpoken += SubtitleDisplayManager.Instance.UpdateLiveSubtitle;
        }
    }
    private void OnDestroy()
    {
        if (ttsManager != null && SubtitleDisplayManager.Instance != null)
        {
            ttsManager.OnWordSpoken -= SubtitleDisplayManager.Instance.UpdateLiveSubtitle;
        }
    }
    // 舊方法（維持原本功能）
    public void Speak(string category, string type)
    {
        if (isGlobalSpeaking) 
        {
            Debug.LogWarning("[Talker] 正在說話中，拒絕新的請求：" + type);
            return;
        }
        StartCoroutine(SpeakCoroutine(category, type));
    }

    // 新方法（可以等待語音完成）
    public IEnumerator SpeakCoroutine(string category, string type)
    {
        isGlobalSpeaking = true; // 上鎖

        // 1. 讀取與分割
        string path = string.IsNullOrEmpty(category) ? $"Dialogues/{type}" : $"Dialogues/{category}/{type}";
        TextAsset txt = Resources.Load<TextAsset>(path);
        if (txt == null) { isGlobalSpeaking = false; yield break; }
        
        string[] lines = txt.text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
        {
            // 2. 顯示字幕
            if (SubtitleDisplayManager.Instance.subtitlePanel != null)
                SubtitleDisplayManager.Instance.subtitlePanel.SetActive(true);
            if (SubtitleDisplayManager.Instance.subtitleText != null)
                SubtitleDisplayManager.Instance.subtitleText.text = line.Trim();

            // 3. 這一行的局部鎖
            bool currentLineFinished = false;
            Action onDone = null;
            onDone = () => {
                currentLineFinished = true;
                ttsManager.OnSpeechCompleted -= onDone;
                Debug.Log("[TTS] 行播放完成");
            };
            ttsManager.OnSpeechCompleted += onDone;

            // 4. 發送請求
            ttsManager.ConvertTextToSpeech(line);

            // 5. 強制等待直到 OnSpeechCompleted 被觸發
            // 這是預防 429 的最核心代碼
            while (!currentLineFinished)
            {
                yield return null; 
            }

            // 6. 唸完後強制休息 0.4 秒（給 Azure 關閉 WebSocket 的緩衝時間）
            yield return new WaitForSeconds(0.4f);
        }

        // 全部結束後隱藏面板並解鎖
        SubtitleDisplayManager.Instance.HideSubtitle();
        isGlobalSpeaking = false; 
    }
}