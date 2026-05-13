using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class ExhibitInteraction : MonoBehaviour
{
    [Header("API Configuration")]
    public string apiUrl = "http://140.119.19.195:3000/interaction/exhibit/add";

    [Header("Interaction Data")]
    public string content;
    public string visitorID;
    public string exhibitID;

    // Method to send the interaction data to the API
    public void SendInteraction()
    {
        StartCoroutine(PostInteraction());
    }

    IEnumerator PostInteraction()
    {
        // Create the data to be sent in the POST request
        InteractionData interactionData = new InteractionData
        {
            content = content,
            visitorID = visitorID,
            exhibitID = exhibitID
        };

        string jsonData = JsonUtility.ToJson(interactionData);

        // Create a UnityWebRequest to send the POST request
        UnityWebRequest request = new UnityWebRequest(apiUrl, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("accept", "application/json");

        // Send the request and wait for a response
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError("Error: " + request.error);
        }
        else
        {
            Debug.Log("Response: " + request.downloadHandler.text);
        }
    }

    [System.Serializable]
    private class InteractionData
    {
        public string content;
        public string visitorID;
        public string exhibitID;
    }
}
