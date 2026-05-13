using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using Microsoft.CognitiveServices.Speech;
using TMPro;

public class APIRequestController : MonoBehaviour
{
    public LunarcomController lunarcomController;
    // public Text responseText;
    public Text responseText;
    public string language = "en_US";
    private string apiUrl = "http://192.168.1.103:5050/api/generate";

    // Azure Speech Service settings
    public string subscriptionKey = "YourAzureSubscriptionKey";
    public string region = "YourServiceRegion";
    public string voiceName = "en-GB-RyanNeural"; // Default voice name

    public UserInteractionRecorder interactionRecorder; // Reference to UserInteractionRecorder

    void Start()
    {
        if (lunarcomController != null)
        {
            // string query = lunarcomController.toAPIText;
            // string query = "What is the capital of France?";
            // StartCoroutine(SendRequestToAPI(query));
        }
        else
        {
            Debug.LogError("LunarcomController is not assigned.");
        }

        if (interactionRecorder == null)
        {
            Debug.LogError("UserInteractionRecorder is not assigned.");
        }
    }

    public IEnumerator SendRequestToAPI(string query)
    {
        UpdateVoiceByLanguage(language);
        string apiLangCode = language.Replace("-", "_");
        var json = new JObject
        {
            { "query", query },
            { "lang", apiLangCode },
        };

        string jsonData = json.ToString();
        byte[] body = Encoding.UTF8.GetBytes(jsonData);

        UnityWebRequest request = new UnityWebRequest(apiUrl, "POST");
        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        // Set timeout to 30 seconds
        request.timeout = 30;

        
        

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("Response: " + request.downloadHandler.text);
            Debug.Log("Sending JSON: " + jsonData);
            ProcessResponse(request.downloadHandler.text);
            // Record the interaction
            if (interactionRecorder != null)
            {
                interactionRecorder.RecordInteraction(query);
            }
        }
        else
        {
            Debug.LogError("Error: " + request.error);
            Debug.LogError("Sending JSON: " + jsonData);
            Debug.LogError("HTTP Response Code: " + request.responseCode);
            Debug.LogError("URL: " + apiUrl);
            Debug.LogError("Response: " + request.downloadHandler.text);
            ProcessResponse("This is the alternative response. Server internal error.");
        }
    }
    
    async void ProcessResponse(string rawJson)
    {
        Debug.Log("Processed Response RAW: " + rawJson);

        JObject json = JObject.Parse(rawJson);

        string npcResponse = json["response"]?.ToString();

        if (string.IsNullOrEmpty(npcResponse))
        {
            Debug.LogError("NPC response is empty!");
            return;
        }

        responseText.text = npcResponse;
        await ConvertTextToSpeech(npcResponse);
    }

    private async System.Threading.Tasks.Task ConvertTextToSpeech(string text)
    {
        var config = SpeechConfig.FromSubscription(subscriptionKey, region);
        config.SpeechSynthesisVoiceName = voiceName; // Set the desired voice

        using (var synthesizer = new SpeechSynthesizer(config))
        {
            var result = await synthesizer.SpeakTextAsync(text);

            if (result.Reason == ResultReason.SynthesizingAudioCompleted)
            {
                Debug.Log("Speech synthesis succeeded.");
            }
            else if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                Debug.LogError($"CANCELED: Reason={cancellation.Reason}");
                Debug.LogError($"CANCELED: ErrorDetails={cancellation.ErrorDetails}");
            }
        }
    }

    private void UpdateVoiceByLanguage(string lang)
    {
        switch (lang)
        {
            case "en-US":
            case "en_US":
                voiceName = "en-US-AndrewMultilingualNeural";
                break;

            case "de-DE":
            case "de_DE":
                voiceName = "de-DE-KatjaNeural";
                break;

            case "zh-TW":
            case "zh_TW":
                voiceName = "zh-TW-HsiaoChenNeural";
                break;

            case "ja-JP":
            case "ja_JP":
                voiceName = "ja-JP-NanamiNeural";
                break;

            default:
                Debug.LogWarning($"No voice mapping for language {lang}, using default.");
                voiceName = "en-GB-RyanNeural";
                break;
        }

        Debug.Log($"[TTS] Language={lang}, Voice={voiceName}");
    }

}