using UnityEngine;
using UnityEngine.Events;
using TMPro;
using ZXing;

// 對應學姊系統文件裡的 User Identification Module (UI)：玩家在體驗開始前，已經在外部評估網站
// 做完 Cambridge Color Vision Test 架構的色覺測驗，網站產生一組 QR code。這支 script 負責用
// HoloLens 內建攝影機掃那組 QR code、解碼出色覺類型與程度，交給 ColorBlindFilterToggle 存起來
// （見 ColorBlindFilterToggle.SetDetectedType）。這裡只做「掃描 + 解碼」，濾鏡什麼時候真的套用
// 是第二幕開場時由 ColorBlindChapterTrigger 決定，不歸這支 script 管。
//
// 只保留 QR 解碼這件事——學姊原本的 QRScanner.cs 裡混了另一條完全不相關的功能（把攝影機畫面
// 用 TCP socket 串流給 PC 端 main.py 跑 YOLOv8 辨識燒杯，對應的是色彩辨識模組 CR，是化學實驗
// 情境用的，跟這裡的色弱濾鏡流程無關），所以沒有整支複製過來。
//
// 【依賴套件】這支 script 需要 ZXing.Net 才能編譯，但目前專案裡（Packages/manifest.json、
// Assets 底下）找不到任何 ZXing 相關檔案——連學姊原本的 QRScanner.cs 應該也編譯不過。
// 需要自己補上 ZXing.Net（例如透過 NuGetForUnity 安裝，或直接把 zxing.unity.dll 放進
// Assets/Plugins），這支 script 才會過編譯。
//
// 【QR 內容格式】沿用學姊原本 QRScanner.cs 的兩碼格式：
//   第 1 碼：A=色覺正常 / B=紅色弱(Protanomalous) / C=綠色弱(Deuteranomalous) / D=藍黃色弱(Tritanomalous)
//   第 2 碼（A 沒有）：1=重度(severe) / 2=中度(moderate) / 3=輕度(mild)
public class ColorVisionQRScanner : MonoBehaviour
{
    [Header("掃描狀態文字（可選）")]
    public TMP_Text statusText;

    [Header("掃到有效 QR 後觸發（例如收起掃描面板、播下一步引導）")]
    public UnityEvent onScanSuccess;

    private WebCamTexture backCam;
    private bool camAvailable;
    private bool scanCompleted;
    private readonly BarcodeReader barcodeReader = new BarcodeReader();

    private void Start()
    {
        backCam = new WebCamTexture();
        backCam.Play();
        camAvailable = true;

        if (statusText != null)
        {
            statusText.text = "請將色覺測驗結果的 QR code 對準鏡頭";
        }
    }

    private void OnDestroy()
    {
        if (backCam != null && backCam.isPlaying)
        {
            backCam.Stop();
        }
    }

    private void Update()
    {
        if (scanCompleted || !camAvailable) return;
        if (!backCam.didUpdateThisFrame) return;

        Result result;
        try
        {
            result = barcodeReader.Decode(backCam.GetPixels32(), backCam.width, backCam.height);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ColorVisionQRScanner] 解碼失敗：{ex.Message}");
            return;
        }

        if (result == null || string.IsNullOrEmpty(result.Text)) return;

        TryApplyCode(result.Text);
    }

    private void TryApplyCode(string code)
    {
        ColorBlindFilterToggle.ColorBlindType? type = code[0] switch
        {
            'A' => ColorBlindFilterToggle.ColorBlindType.Normal,
            'B' => ColorBlindFilterToggle.ColorBlindType.Protanomalous,
            'C' => ColorBlindFilterToggle.ColorBlindType.Deuteranomalous,
            'D' => ColorBlindFilterToggle.ColorBlindType.Tritanomalous,
            _ => null,
        };

        if (type == null)
        {
            Debug.LogWarning($"[ColorVisionQRScanner] 掃到 QR 但內容格式不對：\"{code}\"");
            return;
        }

        string severity = type == ColorBlindFilterToggle.ColorBlindType.Normal
            ? "normal"
            : code.Length > 1 ? code[1] switch
            {
                '1' => "severe",
                '2' => "moderate",
                '3' => "mild",
                _ => "",
            } : "";

        if (ColorBlindFilterToggle.Instance == null)
        {
            Debug.LogError("[ColorVisionQRScanner] 場景裡找不到 ColorBlindFilterToggle，掃描結果無法儲存。");
            return;
        }

        ColorBlindFilterToggle.Instance.SetDetectedType(type.Value, severity);
        scanCompleted = true;

        if (backCam.isPlaying) backCam.Stop();

        if (statusText != null)
        {
            statusText.text = "掃描完成";
        }

        onScanSuccess?.Invoke();
    }
}
