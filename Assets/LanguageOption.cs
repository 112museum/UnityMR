    using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LanguageOption : MonoBehaviour
{
    public LunarcomSpeechRecognizer speechRecognizer;
    public APIRequestController apiRequestController;
    public NPCRequestManager npcRequestManager;
    public SpeechToTextManager speechToTextManager;

    public string Language1 = "en-US";
    public string Language1Alt = "en_US";
    public string Language2 = "zh-TW";
    public string Language2Alt = "zh_TW";
    public string Language3 = "ja-JP";
    public string Language3Alt = "ja_JP";
    public string Language4 = "de-DE";
    public string Language4Alt = "de_DE";
    // Start is called before the first frame update
    void Start()
    {
        if (speechRecognizer == null)
        {
            Debug.LogError("Speech Recognizer reference is not set!");
        }
    }

       private void SetLanguage(string speechLang, string apiLang)
    {
        LanguageState.SpeechLang = speechLang;
        LanguageState.ApiLang = apiLang;

        if (speechRecognizer != null)
        {
            speechRecognizer.fromLanguage = speechLang;
            apiRequestController.language = apiLang;
            speechToTextManager.SetLanguage(speechLang);
        }

        Debug.Log($"[LanguageOption] Language changed to {speechLang}");
    }

    public void English() => SetLanguage(Language1, Language1Alt);
    public void Chinese_TW() => SetLanguage(Language2, Language2Alt);
    public void Japanese() => SetLanguage(Language3, Language3Alt);
    public void German() => SetLanguage(Language4, Language4Alt);
}
