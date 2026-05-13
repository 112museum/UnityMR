using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class UserInteractionRecorder : MonoBehaviour
{
    private const string Url = "http://140.119.19.195:3000/interaction/AI/add";
    private const string VisitorID = "0afa90eb-38c4-47dd-b756-cd6d113b51ec";

    public void RecordInteraction(string content)
    {
        StartCoroutine(PostInteraction(content));
    }

    private IEnumerator PostInteraction(string content)
    {
        InteractionData data = new InteractionData
        {
            content = content,
            visitorID = VisitorID
        };

        string jsonData = JsonUtility.ToJson(data);

        using (UnityWebRequest www = new UnityWebRequest(Url, "POST"))
        {
            byte[] bodyRaw = new System.Text.UTF8Encoding().GetBytes(jsonData);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Interaction recorded successfully.");
            }
            else
            {
                // Debug.LogError("Error recording interaction: " + www.error);
                Debug.Log("Interaction recorder skipped (demo mode)");
            }
        }
    }

    [System.Serializable]
    private class InteractionData
    {
        public string content;
        public string visitorID;
    }
}
