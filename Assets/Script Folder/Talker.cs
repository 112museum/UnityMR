using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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

    // 給 StoryModeManager 等場景用：播放單一句「當下才拿到的文字」（例如 LLM 即時生成的台詞），
    // 不用先存成 Resources 底下的對話檔。共用同一把 isGlobalSpeaking 鎖和 429 緩衝時間。
    public void Speak(string line)
    {
        if (isGlobalSpeaking)
        {
            Debug.LogWarning("[Talker] 正在說話中，拒絕新的語音請求：" + line);
            return;
        }
        StartCoroutine(SpeakCoroutine(line));
    }

    public IEnumerator SpeakCoroutine(string line)
    {
        isGlobalSpeaking = true; // 上鎖

        // LLM 生成的是一整段文章，字幕一次只能印一句，所以先按句尾標點（。！？!?；;）分句。
        string[] sentences = SplitIntoSentences(line);
        if (sentences.Length == 0) { isGlobalSpeaking = false; yield break; }

        // 整段話共用同一個 SpeechSynthesizer 連線（不用每句都重新握手），
        // OnSpeechCompleted 也只會在全部句子念完後觸發一次。
        yield return ttsManager.SpeakSequenceCoroutine(sentences, sentence =>
        {
            if (SubtitleDisplayManager.Instance.subtitlePanel != null)
                SubtitleDisplayManager.Instance.subtitlePanel.SetActive(true);
            if (SubtitleDisplayManager.Instance.subtitleText != null)
                SubtitleDisplayManager.Instance.subtitleText.text = sentence.Trim();
        });

        // 全部結束後隱藏面板並解鎖
        SubtitleDisplayManager.Instance.HideSubtitle();
        isGlobalSpeaking = false;
    }

    // 給 StoryModeManager 用：句子已經由後端依動作切好（見 DialogueLine.npcAnimationTriggers），
    // 不該再被字幕用的標點正規式重切一次，否則句子邊界會跟動作對不上。
    public void Speak(IReadOnlyList<string> sentences, Action<string> onSentenceStart = null)
    {
        if (isGlobalSpeaking)
        {
            Debug.LogWarning("[Talker] 正在說話中，拒絕新的語音請求（多句版本）");
            return;
        }
        StartCoroutine(SpeakCoroutine(sentences, onSentenceStart));
    }

    public IEnumerator SpeakCoroutine(IReadOnlyList<string> sentences, Action<string> onSentenceStart)
    {
        isGlobalSpeaking = true;

        yield return ttsManager.SpeakSequenceCoroutine(sentences, sentence =>
        {
            if (SubtitleDisplayManager.Instance.subtitlePanel != null)
                SubtitleDisplayManager.Instance.subtitlePanel.SetActive(true);
            if (SubtitleDisplayManager.Instance.subtitleText != null)
                SubtitleDisplayManager.Instance.subtitleText.text = sentence.Trim();

            onSentenceStart?.Invoke(sentence);
        });

        SubtitleDisplayManager.Instance.HideSubtitle();
        isGlobalSpeaking = false;
    }

    private static readonly Regex SentenceRegex = new Regex(@"[^。！？!?；;]+[。！？!?；;]*");

    // public：StoryModeManager 也要用它把每個 beat 的完整回覆再依標點拆成字幕/語音的最小單位
    // （動畫 trigger 仍按 beat 觸發，只有字幕跟語音的顆粒度變細，見 HandleDialogueResponse）。
    public static string[] SplitIntoSentences(string text)
    {
        var sentences = new List<string>();
        foreach (Match m in SentenceRegex.Matches(text ?? ""))
        {
            string s = m.Value.Trim();
            if (!string.IsNullOrEmpty(s)) sentences.Add(s);
        }
        return sentences.ToArray();
    }
}