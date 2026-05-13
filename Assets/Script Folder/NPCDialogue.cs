using UnityEngine;
using System.Collections.Generic;

public class NPCDialogue : MonoBehaviour
{
    private TextToSpeech tts;
    private List<string> lines = new List<string>();
    private int currentLine = 0;

    void Start()
    {
        // 找到同一個物件上的 TextToSpeech 組件
        tts = GetComponent<TextToSpeech>();
        if (tts == null)
        {
            Debug.LogError("找不到 TextToSpeech 組件！");
            return;
        }

        // 訂閱 TTS 完成事件，播完一句再播下一句
        tts.OnSpeechCompleted += SpeakNextLine;

        // 讀取 Resources/Dialogues/stories 底下所有文字檔
        TextAsset[] files = Resources.LoadAll<TextAsset>("Dialogues/stories");
        if (files.Length == 0)
        {
            Debug.LogError("找不到任何故事檔案！");
            return;
        }

        foreach (var file in files)
        {
            string[] fileLines = file.text.Split('\n');
            foreach (var line in fileLines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    lines.Add(line.Trim());
            }
        }

        // 從第一句開始播放
        SpeakNextLine();
    }

    void SpeakNextLine()
    {
        if (lines != null && currentLine < lines.Count)
        {
            string sentence = lines[currentLine];
            tts.ConvertTextToSpeech(sentence);
            currentLine++;
        }
        else
        {
            Debug.Log("所有故事已播放完畢！");
        }
    }
}
