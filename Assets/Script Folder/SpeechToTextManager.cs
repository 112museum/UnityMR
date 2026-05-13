using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.CognitiveServices.Speech;

public class SpeechToTextManager : MonoBehaviour
{
    private string subscriptionKey = "4968672a35e040c182e965c879351d64";
    private string region = "eastasia";
    private SpeechRecognizer recognizer;
    private bool isRecognizing = false;
    public NPCRequestManager npcRequestManager; // Reference to the NPCRequestManager

    void Start()
    {
        UnityMainThreadDispatcher.Initialize();
        InitializeRecognizer();
    }

    // 初始化 recognizer
    private void InitializeRecognizer()
    {
        if (recognizer != null)
            recognizer.Dispose();

        var config = SpeechConfig.FromSubscription(subscriptionKey, region);

        // 使用統一語言變數
        string lang = string.IsNullOrEmpty(LanguageState.SpeechLang) ? "en-US" : LanguageState.SpeechLang;
        config.SpeechRecognitionLanguage = lang;

        recognizer = new SpeechRecognizer(config);

        Debug.Log($"[STT] Recognizer initialized with language: {lang}");
    }

    // 語言切換
    public void SetLanguage(string newLanguage)
    {
        LanguageState.SpeechLang = newLanguage;
        InitializeRecognizer(); // 重新初始化 recognizer
        Debug.Log($"[STT] Language switched to: {newLanguage}");
    }

    // 按錄音按鈕才呼叫
    public async void StartRecognition()
    {
        if (recognizer == null)
        {
            Debug.LogError("[STT] Recognizer is null. Did you forget to initialize?");
            return;
        }

        if (isRecognizing)
        {
            Debug.LogWarning("[STT] Recognition already running.");
            return;
        }

        isRecognizing = true;
        Debug.Log("[STT] StartRecognition called");

        try
        {
            var result = await recognizer.RecognizeOnceAsync();

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                if (result.Reason == ResultReason.RecognizedSpeech)
                {
                    Debug.Log($"[STT] Recognized: {result.Text}");
                    npcRequestManager?.SendNPCRequest(result.Text);
                }
                else if (result.Reason == ResultReason.NoMatch)
                {
                    Debug.Log("[STT] No speech could be recognized.");
                }
                else if (result.Reason == ResultReason.Canceled)
                {
                    var cancellation = CancellationDetails.FromResult(result);
                    Debug.LogError($"[STT] CANCELED: Reason={cancellation.Reason}, ErrorDetails={cancellation.ErrorDetails}");
                }
            });
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[STT] Recognition failed: {ex}");
        }
        finally
        {
            isRecognizing = false;
        }
    }

    void OnDestroy()
    {
        if (recognizer != null)
        {
            // 如果正在辨識，先停止 / 等待辨識完成再 Dispose
            if (isRecognizing)
            {
                recognizer.RecognizeOnceAsync().ContinueWith(_ => recognizer.Dispose());
            }
            else
            {
                recognizer.Dispose();
            }
        }
    }
}
