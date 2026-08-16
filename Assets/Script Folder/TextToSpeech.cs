using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.CognitiveServices.Speech;
using System.Threading.Tasks;

public class TextToSpeech : MonoBehaviour
{
    private string apiKey = "";
    private string region = "eastasia";
    public string voiceName = "en-GB-RyanNeural";

    private SpeechConfig speechConfig;
    private SpeechSynthesizer synthesizer;
    public event Action OnSpeechCompleted; // 語音完成事件
    public event Action<string> OnWordSpoken; // 新增：當一個單詞（或片段）開始播放時觸發，傳出該單詞
    void Start()
    {
        // Initialize the Speech SDK
        string envApiKey = EnvLoader.Get("AZURE_SPEECH_KEY");
        if (!string.IsNullOrEmpty(envApiKey)) apiKey = envApiKey;
        speechConfig = SpeechConfig.FromSubscription(apiKey, region);
        speechConfig.SpeechSynthesisVoiceName = voiceName;
        synthesizer = new SpeechSynthesizer(speechConfig);

        // --- 重點：訂閱單詞邊界事件 ---
        synthesizer.WordBoundary += (s, e) =>
        {
            // e.Text 是當前正在說的那個單詞
            // 由於這是異步線程，我們需要傳回 Unity 主線程 (如果是簡單顯示可直接 Invoke)
            // 但為了安全，建議在實作中確保 UI 更新在主線程
            OnWordSpoken?.Invoke(e.Text);
        };
        foreach (var device in Microphone.devices)
            Debug.Log("可用麥克風: " + device);
    }

    public void ConvertTextToSpeech(string text)
    {
        Debug.Log($"[TTS 請求] 時間: {Time.time}, 內容: {text}");
        // 如果之前的協程還在跑，先停止它，避免多個協程併發請求
        StopAllCoroutines(); 
        StartCoroutine(SpeakText(text));
    }

    IEnumerator SpeakText(string text)
        {
            // 如果上一個任務還在跑，先強制停止並銷毀
        if (synthesizer != null)
        {
            synthesizer.Dispose();
            synthesizer = null;
        }

        // 重新建立實例，確保這是一個乾淨的 WebSocket 連線
        var config = SpeechConfig.FromSubscription(apiKey, region);
        config.SpeechSynthesisVoiceName = voiceName; // 確保名稱正確
        synthesizer = new SpeechSynthesizer(config);

        var task = Task.Run(async () => await synthesizer.SpeakTextAsync(text));
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.Result.Reason == ResultReason.Canceled)
        {
            var cancellation = SpeechSynthesisCancellationDetails.FromResult(task.Result);
            Debug.LogError($"[TTS 錯誤] {cancellation.ErrorCode} : {cancellation.ErrorDetails}");
        }

        // 不管成功還是失敗，都通知 Talker 這一行結束了
        OnSpeechCompleted?.Invoke();
    }

    // 給需要「連續念很多句、共用同一個連線」的呼叫者用（例如 Talker 把 LLM 長文分句播放）。
    // 跟 ConvertTextToSpeech 不同：中途不會逐句重建 SpeechSynthesizer，只在整個序列開始/
    // 結束各建立/釋放一次連線。onSentenceStart 在每句「開始」念之前呼叫，方便同步更新字幕；
    // OnSpeechCompleted 只在整個序列全部念完後才觸發一次，語意跟單句版一致（讓監聽者知道的是
    // 「這整段話講完了」，不是「這一句講完了」）。
    public IEnumerator SpeakSequenceCoroutine(IEnumerable<string> sentences, Action<string> onSentenceStart = null)
    {
        if (synthesizer != null)
        {
            synthesizer.Dispose();
            synthesizer = null;
        }

        var config = SpeechConfig.FromSubscription(apiKey, region);
        config.SpeechSynthesisVoiceName = voiceName;
        synthesizer = new SpeechSynthesizer(config);

        foreach (string sentence in sentences)
        {
            onSentenceStart?.Invoke(sentence);

            var task = Task.Run(async () => await synthesizer.SpeakTextAsync(sentence));
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.Result.Reason == ResultReason.Canceled)
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(task.Result);
                Debug.LogError($"[TTS 錯誤] {cancellation.ErrorCode} : {cancellation.ErrorDetails}");
            }

            // 保守起見句與句之間還是留一點間隔；共用連線後理論上有機會縮短，
            // 但目前沒有實測數據支持完全拿掉，先維持跟舊版一樣的 0.4 秒。
            yield return new WaitForSeconds(0.4f);
        }

        if (synthesizer != null)
        {
            synthesizer.Dispose();
            synthesizer = null;
        }

        OnSpeechCompleted?.Invoke();
    }

    void OnDestroy()
    {
        // 確保遊戲關閉或腳本銷毀時，徹底釋放 API 連線
        if (synthesizer != null)
        {
            synthesizer.Dispose();
            synthesizer = null;
        }
    }
}
