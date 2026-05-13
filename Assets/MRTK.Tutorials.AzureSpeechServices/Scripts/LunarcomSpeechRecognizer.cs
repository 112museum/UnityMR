using UnityEngine;
using Microsoft.CognitiveServices.Speech;
using System.Collections;
using System.Threading.Tasks;

public class LunarcomSpeechRecognizer : MonoBehaviour
{
    private string recognizedString = "Select a mode to begin.";
    private object threadLocker = new object();

    private SpeechRecognizer recognizer;

    private bool micPermissionGranted = false;

    public string fromLanguage = "en_US";

    private LunarcomController lunarcomController;

    void Start()
    {
        lunarcomController = LunarcomController.lunarcomController;

        if (lunarcomController.outputText == null)
        {
            Debug.LogError("outputText property is null! Assign a UI Text element to it.");
        }
        else
        {
            micPermissionGranted = true;
        }

        lunarcomController.onSelectRecognitionMode += HandleOnSelectRecognitionMode;
    }

    public void HandleOnSelectRecognitionMode(RecognitionMode recognitionMode)
    {
        if (recognitionMode == RecognitionMode.Speech_Recognizer)
        {
            BeginRecognizing();
        }
        else
        {
            if (recognizer != null)
            {
                Task.Run(() => recognizer.StopContinuousRecognitionAsync());
            } 
            recognizer = null;
            recognizedString = string.Empty;
        }
    }

    public void BeginRecognizing()
    {
        if (micPermissionGranted)
        {
            StartCoroutine(StartRecognitionCoroutine());
        }
        else
        {
            recognizedString = "This app cannot function without access to the microphone.";
        }
    }

    private IEnumerator StartRecognitionCoroutine()
    {
        yield return StartCoroutine(CreateSpeechRecognizerCoroutine());

        if (recognizer != null)
        {
            var startRecognitionTask = recognizer.StartContinuousRecognitionAsync();
            yield return new WaitUntil(() => startRecognitionTask.IsCompleted);
            recognizedString = "Say something...";
        }
    }

    private IEnumerator CreateSpeechRecognizerCoroutine()
    {
        if (recognizer == null)
        {
            SpeechConfig config = SpeechConfig.FromSubscription(lunarcomController.SpeechServiceAPIKey, lunarcomController.SpeechServiceRegion);
            config.SpeechRecognitionLanguage = fromLanguage;
            recognizer = new SpeechRecognizer(config);
            if (recognizer != null)
            {
                recognizer.Recognizing += RecognizingHandler;
                recognizer.Recognized += RecognizedHandler;
                recognizer.SpeechStartDetected += SpeechStartDetected;
                recognizer.SpeechEndDetected += SpeechEndDetectedHandler;
                recognizer.Canceled += CancelHandler;
                recognizer.SessionStarted += SessionStartedHandler;
                recognizer.SessionStopped += SessionStoppedHandler;
            }
        }
        yield return null;
    }

    #region Speech Recognition Event Handlers
    private void SessionStartedHandler(object sender, SessionEventArgs e)
    {
    }

    private void SessionStoppedHandler(object sender, SessionEventArgs e)
    {
        recognizer = null;
    }

    private void RecognizingHandler(object sender, SpeechRecognitionEventArgs e)
    {
        if (e.Result.Reason == ResultReason.RecognizingSpeech)
        {
            lock (threadLocker)
            {
                recognizedString = $"{e.Result.Text}";
            }
        }
    }

    private void RecognizedHandler(object sender, SpeechRecognitionEventArgs e)
    {
        if (e.Result.Reason == ResultReason.RecognizedSpeech)
        {
            lock (threadLocker)
            {
                recognizedString = $"{e.Result.Text}";
            }
        }
        else if (e.Result.Reason == ResultReason.NoMatch)
        {
        }
    }

    private void SpeechStartDetected(object sender, RecognitionEventArgs e)
    {
    }

    private void SpeechEndDetectedHandler(object sender, RecognitionEventArgs e)
    {
    }

    private void CancelHandler(object sender, RecognitionEventArgs e)
    {
    }
    #endregion

    private void Update()
    {
        if (lunarcomController.CurrentRecognitionMode() == RecognitionMode.Speech_Recognizer)
        {
            if (recognizedString != string.Empty)
            {
                lunarcomController.UpdateLunarcomText(recognizedString);
            }
        }
    }

    void OnDestroy()
    {
        if (recognizer != null)
        {
            recognizer.Dispose();
        }
    }
}
