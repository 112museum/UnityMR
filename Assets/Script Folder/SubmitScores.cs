using UnityEngine;
using TMPro; // for TextMeshProUGUI
using UnityEngine.Networking;
using System.Collections;
using MixedReality.Toolkit.UX; // for PressableButton

public class SubmitScores : MonoBehaviour
{
    public MixedReality.Toolkit.UX.Slider interactionSlider;
    public MixedReality.Toolkit.UX.Slider educationSlider;
    public MixedReality.Toolkit.UX.Slider entertainmentSlider;
    public MixedReality.Toolkit.UX.Slider overallSlider;
    public PressableButton submitButton; // Reference to MRTK3 PressableButton
    // public TextMeshProUGUI resultText;
    public GameObject questionnaire; 

    private string apiUrl = "http://140.119.19.195:3000/questionnaire/add";
    private string visitorID = "0afa90eb-38c4-47dd-b756-cd6d113b51ec";

    private void Start()
    {
        // Subscribe to the PressableButton's OnClicked event
        submitButton.OnClicked.AddListener(OnSubmitButtonClick);
    }

    private void OnDestroy()
    {
        // Unsubscribe from the PressableButton's OnClicked event
        submitButton.OnClicked.RemoveListener(OnSubmitButtonClick);
    }

    private void OnSubmitButtonClick()
    {
        int interactionScore = Mathf.RoundToInt(interactionSlider.Value);
        int educationScore = Mathf.RoundToInt(educationSlider.Value);
        int entertainmentScore = Mathf.RoundToInt(entertainmentSlider.Value);
        int overallScore = Mathf.RoundToInt(overallSlider.Value);

        StartCoroutine(SendScores(interactionScore, educationScore, entertainmentScore, overallScore, true, visitorID));
    }

    private IEnumerator SendScores(int interactionScore, int educationScore, int entertainmentScore, int overallScore, bool willVisitAgain, string visitorID)
    {
        // Create the JSON payload
        string jsonData = JsonUtility.ToJson(new QuestionnaireData
        {
            interactionScore = interactionScore,
            educationScore = educationScore,
            entertainmentScore = entertainmentScore,
            overallScore = overallScore,
            willVisitAgain = willVisitAgain,
            visitorID = visitorID
        });

        // Create a UnityWebRequest
        using (UnityWebRequest www = UnityWebRequest.PostWwwForm(apiUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError(www.error);
                // resultText.text = "Error: " + www.error;
            }
            else
            {
                Debug.Log("Form upload complete!");
                // resultText.text = "Success: " + www.downloadHandler.text;
                Debug.Log(www.downloadHandler.text);
                questionnaire.SetActive(false);
            }
        }
    }

    [System.Serializable]
    private class QuestionnaireData
    {
        public int interactionScore;
        public int educationScore;
        public int entertainmentScore;
        public int overallScore;
        public bool willVisitAgain;
        public string visitorID;
    }
}
