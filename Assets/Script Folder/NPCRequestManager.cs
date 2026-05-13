using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

public class NPCRequestManager : MonoBehaviour
{
    private string apiUrl = "http://192.168.1.103:5050/api/npc/ask";
    public string npc_role = "白起";
    public string personality = "introvert";
    public bool is_rag = true;
    public TextToSpeech ttsManager;
    public TextManager textManager;

    public void SendNPCRequest(string query)
    {
        StartCoroutine(PostRequest(query));
    }

    IEnumerator PostRequest(string query)
    {
        var jsonBody = new NPCRequest
        {
            query = query,
            lang = LanguageState.ApiLang,
            npc_role = npc_role,
            personality = personality,
            is_rag = is_rag
        };

        string jsonData = JsonUtility.ToJson(jsonBody);

        using (UnityWebRequest www = UnityWebRequest.PostWwwForm(apiUrl, "POST"))
        {
            byte[] bodyRaw = new System.Text.UTF8Encoding().GetBytes(jsonData);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("accept", "application/json");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.Log("Error: " + www.error);
                Debug.Log("Sending JSON: " + jsonData);
                Debug.Log($"[NPCRequestManager] Using lang = {LanguageState.ApiLang}");
                ttsManager.ConvertTextToSpeech("Server Error");
            }
            else
            {
                Debug.Log("Sending JSON: " + jsonData);
                Debug.Log($"[NPCRequestManager] Using lang = {LanguageState.ApiLang}");
                Debug.Log("Response: " + www.downloadHandler.text);
                // Send the response text to the TTS manager
                var json = JObject.Parse(www.downloadHandler.text);
                var npcResponse = json["response"]?.ToString();

                if (textManager != null)
                {
                    textManager.UpdateText(npcResponse);
                }

                if (ttsManager != null)
                    ttsManager.ConvertTextToSpeech(npcResponse);
            }
        }
    }

    [System.Serializable]
    public class NPCRequest
    {
        public string query;
        public string lang;
        public string npc_role;
        public string personality;
        public bool is_rag;
    }
}
