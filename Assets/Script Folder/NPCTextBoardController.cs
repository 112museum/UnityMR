using System;
using System.Collections;
using UnityEngine;
using Microsoft.CognitiveServices.Speech;
using System.Threading.Tasks;

public class NPCTextBoardController : MonoBehaviour
{
    public GameObject panel; // 字幕 Panel
    public TextToSpeech tts; // 你的 TTS 腳本

    void Start()
    {
        panel.SetActive(false);

        // 監聽語音結束事件
        tts.OnSpeechCompleted += HidePanel;
    }

    // 這個方法掛在按鈕上
    public void OnAButtonPressed()
    {
        panel.SetActive(true); // 按鈕被按下 → 顯示 Panel
    }

    void HidePanel()
    {
        panel.SetActive(false); // 語音結束 → 隱藏 Panel
    }
}