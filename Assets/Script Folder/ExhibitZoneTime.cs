using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class ExhibitZoneTime : MonoBehaviour
{
    public IEnumerator PostDuration(string visitorID, string exhibitID, string duration)
    {
        string url = "http://140.119.19.195:3000/interaction/duration/add";
        string jsonData = $"{{\"content\":\"{duration}\",\"visitorID\":\"{visitorID}\",\"exhibitID\":\"{exhibitID}\"}}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Duration successfully posted to the server.");
            }
            else
            {
                Debug.LogError("Error posting duration to the server: " + request.error);
            }
        }
    }
}
