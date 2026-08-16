using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

// 接續 GroupPhotoCapture 做完的 UC006 步驟 6-7：拍完合照後跳出信箱輸入面板，
// 玩家填了才把照片寄出；不填（或按跳過）就直接把剛拍好的照片檔案刪掉，不留檔。
//
// 寄信這件事沒辦法在 HoloLens/Unity 端直接做，所以送去使用者後端（MRmuseum-backend，
// 跟 UserInteractionRecorder/SubmitScores 等腳本共用同一台伺服器）的 POST /photo/email，
// 由後端在伺服器端寄信。email 邏輯原本掛在 aibackend（story-mode 用的 Socket.IO 後端）
// 那邊，架構上不屬於那裡，已經搬到使用者後端。
public class GroupPhotoEmailPrompt : MonoBehaviour
{
    private const string EmailApiUrl = "http://140.119.19.195:3000/photo/email";

    [Header("依賴")]
    public GroupPhotoCapture photoCapture;

    [Header("UI")]
    public GameObject emailPanel;
    public TMP_InputField emailInputField;
    public Button sendButton;
    public Button skipButton;
    public TMP_Text statusText;

    private string pendingPhotoPath;

    private void Start()
    {
        if (emailPanel != null) emailPanel.SetActive(false);
        if (photoCapture != null) photoCapture.onPhotoSaved.AddListener(HandlePhotoSaved);
        if (sendButton != null) sendButton.onClick.AddListener(OnSendClicked);
        if (skipButton != null) skipButton.onClick.AddListener(OnSkipClicked);
    }

    private void HandlePhotoSaved(string photoPath)
    {
        // Editor/非 UWP 平台下 GroupPhotoCapture 會傳空字串代表「沒有真的拍照」，
        // 這種情況沒有檔案好寄，略過面板，避免測劇情流程時卡在這裡出不去。
        if (string.IsNullOrEmpty(photoPath))
        {
            Debug.Log("[GroupPhotoEmailPrompt] 沒有實際照片檔案（非 HoloLens 平台），略過信箱面板。");
            return;
        }

        pendingPhotoPath = photoPath;
        if (emailInputField != null) emailInputField.text = "";
        if (statusText != null) statusText.text = "";
        if (emailPanel != null) emailPanel.SetActive(true);
    }

    private void OnSendClicked()
    {
        string email = emailInputField != null ? emailInputField.text.Trim() : "";

        // 沒填信箱就按送出，視同不願意提供，跟按「跳過」一樣直接丟掉照片。
        if (string.IsNullOrEmpty(email))
        {
            DiscardAndClose();
            return;
        }

        SendPhoto(email);
    }

    private void OnSkipClicked() => DiscardAndClose();

    private void SendPhoto(string email)
    {
        if (string.IsNullOrEmpty(pendingPhotoPath) || !File.Exists(pendingPhotoPath))
        {
            Debug.LogError($"[GroupPhotoEmailPrompt] 找不到要寄送的照片檔案：{pendingPhotoPath}");
            ShowStatus("找不到照片檔案，寄送失敗");
            return;
        }

        byte[] photoBytes = File.ReadAllBytes(pendingPhotoPath);
        string photoBase64 = Convert.ToBase64String(photoBytes);
        string fileName = Path.GetFileName(pendingPhotoPath);

        ShowStatus("寄送中...");

        // 照片內容已經整包讀進記憶體、待會交給 coroutine 送出了，本機這份留著也沒用，
        // 馬上清掉；面板先收起來避免玩家在等後端回覆的空檔重複按送出。
        if (emailPanel != null) emailPanel.SetActive(false);
        StartCoroutine(PostGroupPhoto(email, photoBase64, fileName));
        CleanupLocalFile();
    }

    private IEnumerator PostGroupPhoto(string email, string photoBase64, string fileName)
    {
        string jsonData = JsonUtility.ToJson(new GroupPhotoEmailRequest
        {
            email = email,
            photo_base64 = photoBase64,
            file_name = fileName
        });

        using (UnityWebRequest www = UnityWebRequest.PostWwwForm(EmailApiUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"[GroupPhotoEmailPrompt] 寄送失敗：{www.error} / {www.downloadHandler.text}");
                ShowStatus("寄送失敗，請確認網路後再試一次");
            }
            else
            {
                ShowStatus("已寄出，請至信箱查收！");
            }
        }
    }

    private void DiscardAndClose()
    {
        CleanupLocalFile();
        if (emailPanel != null) emailPanel.SetActive(false);
    }

    private void CleanupLocalFile()
    {
        if (!string.IsNullOrEmpty(pendingPhotoPath) && File.Exists(pendingPhotoPath))
        {
            try { File.Delete(pendingPhotoPath); }
            catch (Exception ex) { Debug.LogWarning($"[GroupPhotoEmailPrompt] 刪除本機照片失敗：{ex.Message}"); }
        }
        pendingPhotoPath = null;
    }

    private void ShowStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }

    [System.Serializable]
    private class GroupPhotoEmailRequest
    {
        public string email;
        public string photo_base64;
        public string file_name;
    }
}
