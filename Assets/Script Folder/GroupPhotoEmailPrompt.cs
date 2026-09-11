using System;
using System.Collections;
using System.IO;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// 接續 GroupPhotoCapture 做完的 UC006 步驟 6-7：合照拍完後（面板開啟時機由
// GroupPhotoTurnManager 決定，見該檔案）跳出信箱輸入面板，玩家填了才把照片寄出；
// 不填（或按跳過）就直接把剛拍好的照片檔案刪掉，不留檔。
//
// 兩人輪流拍照後，各自裝置存的是「拍到對方」的那張照片，所以要交換：A 輸入的信箱
// 收的應該是 B 那支裝置拍的（畫面裡是 A）。兩人本來就在同一個 Photon 房間，交換用
// Player Custom Property（groupPhotoEmail / groupPhotoSkipped）直接做，不用動後端。
//
// 寄信這件事沒辦法在 HoloLens/Unity 端直接做，所以送去使用者後端（MRmuseum-backend，
// 跟 UserInteractionRecorder/SubmitScores 等腳本共用同一台伺服器）的 POST /photo/email，
// 由後端在伺服器端寄信。email 邏輯原本掛在 aibackend（story-mode 用的 Socket.IO 後端）
// 那邊，架構上不屬於那裡，已經搬到使用者後端。
public class GroupPhotoEmailPrompt : MonoBehaviourPunCallbacks
{
    private const string EmailPropKey = "groupPhotoEmail";
    private const string SkippedPropKey = "groupPhotoSkipped";

    [Header("依賴")]
    public GroupPhotoCapture photoCapture;

    [Header("UI")]
    public GameObject emailPanel;
    public TMP_InputField emailInputField;
    public Button sendButton;
    public Button skipButton;

    private string pendingPhotoPath;
    private bool waitingForPartnerEmail;

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
        // 這種情況沒有檔案好寄，記錄一下就好，反正 GroupPhotoTurnManager 也不會呼叫 ShowPanel()。
        if (string.IsNullOrEmpty(photoPath))
        {
            Debug.Log("[GroupPhotoEmailPrompt] 沒有實際照片檔案（非 HoloLens 平台），略過信箱面板。");
            return;
        }

        pendingPhotoPath = photoPath;
    }

    // 由 GroupPhotoTurnManager 在雙方都拍完照的時間點呼叫，開啟信箱輸入面板。
    public void ShowPanel()
    {
        if (emailInputField != null) emailInputField.text = "";
        SetInputInteractable(true);
        if (emailPanel != null) emailPanel.SetActive(true);
    }

    private void OnSendClicked()
    {
        string email = emailInputField != null ? emailInputField.text.Trim() : "";

        // 沒填信箱就按送出，視同不願意提供，跟按「跳過」一樣直接丟掉照片。
        if (string.IsNullOrEmpty(email))
        {
            OnSkipClicked();
            return;
        }

        if (string.IsNullOrEmpty(pendingPhotoPath) || !File.Exists(pendingPhotoPath))
        {
            Debug.LogError($"[GroupPhotoEmailPrompt] 找不到要寄送的照片檔案：{pendingPhotoPath}");
            return;
        }

        SetInputInteractable(false);
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { EmailPropKey, email } });

        Player partner = GetPartner();
        string partnerEmail = GetPartnerEmail(partner);
        if (!string.IsNullOrEmpty(partnerEmail))
        {
            SendToEmail(partnerEmail);
            return;
        }

        if (partner != null && partner.CustomProperties.TryGetValue(SkippedPropKey, out var skipped) && (bool)skipped)
        {
            CleanupLocalFile();
            return;
        }

        waitingForPartnerEmail = true;
    }

    private void OnSkipClicked()
    {
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { SkippedPropKey, true } });
        DiscardAndClose();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (!waitingForPartnerEmail) return;
        if (targetPlayer == PhotonNetwork.LocalPlayer) return;

        if (changedProps.TryGetValue(SkippedPropKey, out var skipped) && (bool)skipped)
        {
            waitingForPartnerEmail = false;
            CleanupLocalFile();
            return;
        }

        if (changedProps.TryGetValue(EmailPropKey, out var email))
        {
            waitingForPartnerEmail = false;
            SendToEmail((string)email);
        }
    }

    private Player GetPartner()
    {
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            if (p != PhotonNetwork.LocalPlayer) return p;
        }
        return null;
    }

    private string GetPartnerEmail(Player partner)
    {
        if (partner != null && partner.CustomProperties.TryGetValue(EmailPropKey, out var email))
        {
            return (string)email;
        }
        return null;
    }

    private void SendToEmail(string destinationEmail)
    {
        if (string.IsNullOrEmpty(pendingPhotoPath) || !File.Exists(pendingPhotoPath))
        {
            Debug.LogError($"[GroupPhotoEmailPrompt] 找不到要寄送的照片檔案：{pendingPhotoPath}");
            return;
        }

        byte[] photoBytes = File.ReadAllBytes(pendingPhotoPath);
        string photoBase64 = Convert.ToBase64String(photoBytes);
        string fileName = Path.GetFileName(pendingPhotoPath);

        // 照片內容已經整包讀進記憶體、待會交給 coroutine 送出了，本機這份留著也沒用，
        // 馬上清掉；面板先收起來避免玩家在等後端回覆的空檔重複按送出。
        if (emailPanel != null) emailPanel.SetActive(false);
        StartCoroutine(PostGroupPhoto(destinationEmail, photoBase64, fileName));
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

        using (UnityWebRequest www = UnityWebRequest.PostWwwForm(BackendConfig.PhotoApiUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"[GroupPhotoEmailPrompt] 寄送失敗：{www.error} / {www.downloadHandler.text}");
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

    private void SetInputInteractable(bool interactable)
    {
        if (emailInputField != null) emailInputField.interactable = interactable;
        if (sendButton != null) sendButton.interactable = interactable;
        if (skipButton != null) skipButton.interactable = interactable;
    }

    [System.Serializable]
    private class GroupPhotoEmailRequest
    {
        public string email;
        public string photo_base64;
        public string file_name;
    }
}
