using System;
using System.IO;
using SocketIOClient;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 接續 GroupPhotoCapture 做完的 UC006 步驟 6-7：拍完合照後跳出信箱輸入面板，
// 玩家填了才把照片寄出；不填（或按跳過）就直接把剛拍好的照片檔案刪掉，不留檔。
//
// 寄信這件事沒辦法在 HoloLens/Unity 端直接做，所以沿用 StoryPromptManager/
// GroupChatManager 已經在用的同一台 Socket.IO 後端 (backendUrl)，新增一個
// "send_group_photo" 事件，把信箱 + 照片(base64) 丟給後端，由後端在伺服器端寄信。
// 這支腳本能動，還需要後端那邊補上 send_group_photo 的處理邏輯（收到後寄信，
// 完成/失敗時回丟 "photo_email_sent" / "photo_email_failed"）。
public class GroupPhotoEmailPrompt : MonoBehaviour
{
    [Header("依賴")]
    public GroupPhotoCapture photoCapture;

    [Header("Backend Config（沿用 story/chat 用的同一台後端）")]
    public string backendUrl = "http://192.168.0.76:5050";

    [Header("UI")]
    public GameObject emailPanel;
    public TMP_InputField emailInputField;
    public Button sendButton;
    public Button skipButton;
    public TMP_Text statusText;

    private SocketIOUnity socket;
    private string pendingPhotoPath;

    private void Awake()
    {
        var uri = new Uri(backendUrl);
        socket = new SocketIOUnity(uri, new SocketIOOptions
        {
            Transport = SocketIOClient.Transport.TransportProtocol.WebSocket
        });

        socket.On("error", response => Debug.LogError($"[GroupPhotoEmailPrompt] Server error: {response}"));
        socket.OnUnityThread("photo_email_sent", _ => ShowStatus("已寄出，請至信箱查收！"));
        socket.OnUnityThread("photo_email_failed", response => ShowStatus("寄送失敗，請確認網路後再試一次"));
        socket.ConnectAsync();
    }

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

        ShowStatus("寄送中...");
        socket.EmitAsync("send_group_photo", new
        {
            email,
            photo_base64 = photoBase64,
            file_name = Path.GetFileName(pendingPhotoPath)
        });

        // 照片內容已經整包送進 EmitAsync 了，本機這份留著也沒用，馬上清掉；
        // 面板先收起來避免玩家在等後端回覆的空檔重複按送出。
        if (emailPanel != null) emailPanel.SetActive(false);
        CleanupLocalFile();
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

    private void OnDestroy()
    {
        socket?.DisconnectAsync();
        socket?.Dispose();
    }
}
