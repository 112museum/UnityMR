using System;
using System.Collections.Concurrent;
using Microsoft.CognitiveServices.Speech;
using UnityEngine;

public class STTManager : MonoBehaviour
{
    private string subscriptionKey = "4968672a35e040c182e965c879351d64";
    private string region = "eastasia";
    private SpeechRecognizer recognizer;
    private bool isRecognizing = false;
    public TextManager textManager;

    private readonly ConcurrentQueue<Action> _mainThreadQueue = new();
    private string _accumulatedText = "";

    void Start()
    {
        InitializeRecognizer();
    }

    void Update()
    {
        while (_mainThreadQueue.TryDequeue(out var action))
            action();
    }

    private void InitializeRecognizer()
    {
        if (recognizer != null)
            recognizer.Dispose();

        var config = SpeechConfig.FromSubscription(subscriptionKey, region);
        string lang = string.IsNullOrEmpty(LanguageState.SpeechLang) ? "en-US" : LanguageState.SpeechLang;
        config.SpeechRecognitionLanguage = lang;

        recognizer = new SpeechRecognizer(config);
        Debug.Log($"[STT] Recognizer initialized with language: {lang}");
    }

    public void SetLanguage(string newLanguage)
    {
        LanguageState.SpeechLang = newLanguage;
        InitializeRecognizer();
        Debug.Log($"[STT] Language switched to: {newLanguage}");
    }

    // 按鈕統一呼叫此方法：第一下開始，第二下停止並發送
    public void ToggleRecognition()
    {
        if (isRecognizing) StopAndSend();
        else StartRecognition();
    }

    private async void StartRecognition()
    {
        var npcChat = GetComponent<NPCChat>();
        if (npcChat != null) npcChat.StartChat();

        if (recognizer == null)
        {
            Debug.LogError("[STT] Recognizer is null.");
            return;
        }

        _accumulatedText = "";
        recognizer.Recognizing += OnRecognizing;
        recognizer.Recognized  += OnRecognized;
        recognizer.Canceled    += OnCanceled;

        isRecognizing = true;
        await recognizer.StartContinuousRecognitionAsync();
        Debug.Log("[STT] Continuous recognition started");
    }

    private async void StopAndSend()
    {
        await recognizer.StopContinuousRecognitionAsync();

        recognizer.Recognizing -= OnRecognizing;
        recognizer.Recognized  -= OnRecognized;
        recognizer.Canceled    -= OnCanceled;

        isRecognizing = false;
        Debug.Log("[STT] Continuous recognition stopped");

        string finalText = _accumulatedText.Trim();
        _accumulatedText = "";

        if (string.IsNullOrEmpty(finalText)) return;

        if (textManager != null) textManager.UpdateText(finalText);
        if (GroupChatManager.Instance != null) GroupChatManager.Instance.SendChatMessage(finalText);
        else Debug.LogError("[STT] GroupChatManager instance not found.");
    }

    // 邊說邊觸發，顯示 已確定文字 + 目前 partial
    private void OnRecognizing(object sender, SpeechRecognitionEventArgs e)
    {
        if (e.Result.Reason != ResultReason.RecognizingSpeech) return;
        string display = _accumulatedText + e.Result.Text;
        _mainThreadQueue.Enqueue(() =>
        {
            if (textManager != null) textManager.UpdateText(display);
        });
    }

    // 一段說完，累積進 _accumulatedText，不立即發送
    private void OnRecognized(object sender, SpeechRecognitionEventArgs e)
    {
        if (e.Result.Reason == ResultReason.RecognizedSpeech)
        {
            _accumulatedText += e.Result.Text;
            Debug.Log($"[STT] Segment recognized: {e.Result.Text}");
        }
        else if (e.Result.Reason == ResultReason.NoMatch)
        {
            Debug.Log("[STT] No match.");
        }
    }

    private void OnCanceled(object sender, SpeechRecognitionCanceledEventArgs e)
    {
        Debug.LogError($"[STT] CANCELED: {e.Reason}, {e.ErrorDetails}");
    }

    void OnDestroy()
    {
        if (recognizer != null)
        {
            if (isRecognizing)
                recognizer.StopContinuousRecognitionAsync().Wait();
            recognizer.Dispose();
        }
    }
}
