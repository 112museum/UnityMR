using System;
using System.Collections;
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
        // apiKey = EnvLoader.Get("AZURE_SPEECH_KEY");
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
