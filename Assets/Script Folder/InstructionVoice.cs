using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Microsoft.CognitiveServices.Speech;
using System.Threading.Tasks;

public class InstructionVoice : MonoBehaviour
{
    public string stringToConvert;
    public string subscriptionKey = "4968672a35e040c182e965c879351d64";
    public string region = "eastasia";
    public string voiceName = "en-US-AndrewMultilingualNeural"; // Default voice name

    void Start()
    {
        // Example usage
        ConvertTextToSpeech(stringToConvert);
    }

    public async void ConvertTextToSpeech(string text)
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
}
