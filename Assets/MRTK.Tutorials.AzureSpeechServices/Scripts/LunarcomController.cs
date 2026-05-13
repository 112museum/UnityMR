using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum RecognitionMode { Speech_Recognizer, Intent_Recognizer, Tralation_Recognizer, Disabled, Offline };
public enum SimuilateOfflineMode { Enabled, Disabled };
public enum TranslateToLanguage { Russian, German, Chinese };

public class LunarcomController : MonoBehaviour
{
    public static LunarcomController lunarcomController = null;

    [Header("Speech SDK Credentials")]
    public string SpeechServiceAPIKey = string.Empty;
    public string SpeechServiceRegion = string.Empty;

    [Header("Object References")]
    public GameObject terminal;
    public ConnectionLightController connectionLight;
    public APIRequestController apiRequestController;
    public Text outputText;
    public string toAPIText;
    public List<LunarcomButtonController> buttons;    
    public delegate void OnSelectRecognitionMode(RecognitionMode selectedMode);
    public event OnSelectRecognitionMode onSelectRecognitionMode;

    RecognitionMode speechRecognitionMode = RecognitionMode.Disabled;
    LunarcomButtonController activeButton = null;
    LunarcomButtonController micButtonController;
    LunarcomWakeWordRecognizer lunarcomWakeWordRecognizer = null;
    LunarcomOfflineRecognizer lunarcomOfflineRecognizer = null;

    private bool isSpeaking = false;
    private Coroutine checkSpeakingCoroutine;
    private string lastTextToCheck = "";
    private float lastTextChangeTime;

    private void Awake()
    {
        if (lunarcomController == null)
        {
            lunarcomController = this;
        }
        else if (lunarcomController != this)
        {
            Destroy(gameObject);
        }

        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        GameObject micButton = GameObject.Find("MicButton");

        if (micButton != null)
        {
            micButtonController = micButton.GetComponent<LunarcomButtonController>();

            if (micButtonController == null)
            {
                Debug.LogError("MicButton does not have a LunarcomButtonController component.");
            }
        }
        else
        {
            Debug.LogError("MicButton not found in the scene. Please ensure it is named correctly.");
        }

        if (GetComponent<LunarcomWakeWordRecognizer>())
        {
            lunarcomWakeWordRecognizer = GetComponent<LunarcomWakeWordRecognizer>();
        }
        if (GetComponent<LunarcomOfflineRecognizer>())
        {
            lunarcomOfflineRecognizer = GetComponent<LunarcomOfflineRecognizer>();
            if (lunarcomOfflineRecognizer.simulateOfflineMode == SimuilateOfflineMode.Disabled)
            {
                SetupOnlineMode();
            }
            else
            {
                SetupOfflineMode();
            }
        }
        else
        {
            SetupOnlineMode();
        }

        if (Time.timeScale != 1)
        {
            Debug.LogWarning("Time.timeScale is not 1. This may affect coroutine timing.");
        }
    }

    public bool IsOfflineMode()
    {
        if (lunarcomOfflineRecognizer != null)
        {
            return lunarcomOfflineRecognizer.simulateOfflineMode == SimuilateOfflineMode.Enabled;
        }
        else
        {
            return false;
        }
    }

    private void SetupOnlineMode()
    {
        if (lunarcomWakeWordRecognizer != null)
        {
            if (lunarcomWakeWordRecognizer.WakeWord == string.Empty)
            {
                lunarcomWakeWordRecognizer.WakeWord = "*";
                lunarcomWakeWordRecognizer.DismissWord = "*";
            }

            if (lunarcomWakeWordRecognizer.DismissWord == string.Empty)
            {
                lunarcomWakeWordRecognizer.DismissWord = "*";
            }
        }

        if (GetComponent<LunarcomTranslationRecognizer>())
        {
            ActivateButtonNamed("SatelliteButton");
        }

        if (GetComponent<LunarcomIntentRecognizer>())
        {
            ActivateButtonNamed("RocketButton");
        }

        ShowConnected(true);
    }

    private void SetupOfflineMode()
    {
        if (lunarcomWakeWordRecognizer != null)
        {
            lunarcomWakeWordRecognizer.WakeWord = "*";
            lunarcomWakeWordRecognizer.DismissWord = "*";
        }

        if (GetComponent<LunarcomWakeWordRecognizer>())
        {
            GetComponent<LunarcomWakeWordRecognizer>().enabled = false;
        }
        if (GetComponent<LunarcomSpeechRecognizer>())
        {
            GetComponent<LunarcomSpeechRecognizer>().enabled = false;
        }
        if (GetComponent<LunarcomTranslationRecognizer>())
        {
            GetComponent<LunarcomTranslationRecognizer>().enabled = false;
            ActivateButtonNamed("SatelliteButton", false);
        }
        if (GetComponent<LunarcomIntentRecognizer>())
        {
            GetComponent<LunarcomIntentRecognizer>().enabled = false;
            ActivateButtonNamed("RocketButton", false);
        }

        ShowConnected(false);
    }

    private void ActivateButtonNamed(string name, bool makeActive = true)
    {
        foreach (LunarcomButtonController button in buttons)
        {
            if (button.gameObject.name == name)
            {
                button.gameObject.SetActive(makeActive);
            }
        }
    }

    public RecognitionMode CurrentRecognitionMode()
    {
        return speechRecognitionMode;
    }

    public void SetActiveButton(LunarcomButtonController buttonToSetActive)
    {
        activeButton = buttonToSetActive;
        foreach (LunarcomButtonController button in buttons)
        {
            if (button != activeButton && button.GetIsSelected())
            {
                button.ShowNotSelected();
            }
        }
    }

    public void SelectMode(RecognitionMode speechRecognitionModeToSet)
    {
        speechRecognitionMode = speechRecognitionModeToSet;
        onSelectRecognitionMode(speechRecognitionMode);
        if (speechRecognitionMode == RecognitionMode.Disabled)
        {
            if (outputText.text == "Say something..." || outputText.text == string.Empty)
            {
                outputText.text = "Select a mode to begin.";
            }
        }
    }

    public void ShowConnected(bool showConnected)
    {
        connectionLight.ShowConnected(showConnected);
    }

    public void ShowTerminal()
    {
        terminal.SetActive(true);
    }

    public void HideTerminal()
    {
        if (terminal.activeSelf)
        {
            foreach (LunarcomButtonController button in buttons)
            {
                if (button.GetIsSelected())
                {
                    button.ShowNotSelected();
                }
            }

            outputText.text = "Select a mode to begin.";
            terminal.SetActive(false);
            SelectMode(RecognitionMode.Disabled);
        }
    }

    public void UpdateLunarcomText(string textToUpdate)
    {
        outputText.text = textToUpdate;

        if (textToUpdate == "Say something...")
        {
            toAPIText = "";
        }
        else
        {
            toAPIText = textToUpdate;
        }

        if (textToUpdate != lastTextToCheck)
        {
            lastTextToCheck = textToUpdate;
            lastTextChangeTime = Time.time;

            if (checkSpeakingCoroutine != null)
            {
                StopCoroutine(checkSpeakingCoroutine);
                Debug.Log("Stopped previous CheckIfUserStoppedSpeaking coroutine.");
            }
            checkSpeakingCoroutine = StartCoroutine(CheckIfUserStoppedSpeaking());
            Debug.Log(toAPIText);
            Debug.Log("Started coroutine to check if user stopped speaking.");
        }
    }

    private IEnumerator CheckIfUserStoppedSpeaking()
    {
        Debug.Log("Checking if user stopped speaking...");
        while (true)
        {
            yield return new WaitForSeconds(0.1f); // Check every 0.1 seconds

            if (outputText.text == lastTextToCheck && Time.time - lastTextChangeTime >= 4f)
            {
                isSpeaking = false;
                Debug.Log("User has stopped speaking. isSpeaking = " + isSpeaking);

                if (micButtonController != null)
                {
                    micButtonController.ToggleSelected();
                }
                else
                {
                    Debug.LogError("MicButtonController is not assigned.");
                }

                // Reset the state for next use
                lastTextToCheck = "";
                lastTextChangeTime = 0;

                Debug.Log("Final Text to send to API: " + toAPIText);


                // Send the said text to the API
                apiRequestController.StartCoroutine(apiRequestController.SendRequestToAPI(toAPIText));

                break;
            }
            else if (outputText.text != lastTextToCheck)
            {
                lastTextToCheck = outputText.text;
                lastTextChangeTime = Time.time;
            }
        }
    }
}
