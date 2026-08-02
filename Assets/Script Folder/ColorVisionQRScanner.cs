using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
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

    [Header("即時鏡頭畫面（可選，讓玩家看得到現在對準哪裡，方便瞄準 QR code）")]
    public RawImage cameraPreview;

    [Header("掃到有效 QR 後觸發（例如收起掃描面板、播下一步引導）")]
    public UnityEvent onScanSuccess;

    [Header("除錯用：這台裝置的攝影機畫面是否需要上下翻轉才能正確解碼（不確定的話兩種都試試看）")]
    public bool flipVertically = true;

    private WebCamTexture backCam;
    private bool camAvailable;
    private bool scanCompleted;
    // ZXing.Net's BarcodeReader defaults Options.TryHarder to false — a speed/accuracy
    // tradeoff meant for scanning near-perfectly-aligned codes. A QR held by hand at a
    // slight angle/tilt (as in real use here) routinely fails to decode under that
    // default even though the code itself is perfectly readable; TryHarder fixes it.
    private readonly BarcodeReader barcodeReader = new BarcodeReader
    {
        Options = { TryHarder = true, TryInverted = true },
    };
    private Color32[] _flipBuffer;
    private byte[] _rgbaBuffer;
    private float _nextHeartbeatAt;
    private int _framesTried;

    private void Start()
    {
        // Fixed, modest resolution: faster to decode every frame, and plenty for a small
        // 2-character QR code — no reason to drag a full native-res frame through ZXing.
        backCam = new WebCamTexture(640, 480, 30);
        backCam.Play();
        camAvailable = true;

        if (cameraPreview != null)
        {
            cameraPreview.texture = backCam;
        }

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

        // WebCamTexture.GetPixels32() returns rows bottom-to-top (Unity's usual texture
        // convention), but ZXing expects top-to-bottom like every normal image format.
        // Handed to ZXing as-is, that's a vertical flip — a mirror, not just a rotation —
        // and QR codes don't decode mirrored even though ZXing already tries all 4
        // rotations internally. Flipping the rows back before decoding is the fix.
        Color32[] pixels = backCam.GetPixels32();
        if (flipVertically)
        {
            FlipVertical(pixels, backCam.width, backCam.height);
        }

        _framesTried++;
        if (Time.time >= _nextHeartbeatAt)
        {
            Debug.Log($"[ColorVisionQRScanner] 掃描中…（已嘗試 {_framesTried} 幀，尚未讀到有效 QR）");
            _nextHeartbeatAt = Time.time + 3f;
        }

#if UNITY_EDITOR
        // Debug-only: press D to dump the exact pixels being handed to ZXing this frame,
        // as a PNG next to the project folder. We've now ruled out both flip states and
        // enabled TryHarder with zero decodes across hundreds of frames each — that's the
        // signature of the captured image itself being wrong (garbled/blank/wrong channel
        // order), not an orientation or "hard to read" problem. Looking at the actual bytes
        // ZXing sees settles that directly instead of guessing at another hypothesis.
        if (Input.GetKeyDown(KeyCode.D))
        {
            DumpDebugFrame(pixels, backCam.width, backCam.height);
        }
#endif

        // Confirmed via an independent decoder (pyzbar, fed the exact same Color32 buffer
        // dumped to disk) that the captured frame decodes cleanly with zero rotation/flip
        // needed — the image itself was never the problem, and neither flipVertically state
        // changes the outcome here. That points at zxing.unity.dll's Color32[] overload
        // (Color32LuminanceSource) itself, not our pixels. Converting to a plain RGBA32
        // byte buffer and going through the library's generic byte[]+BitmapFormat decode
        // path instead sidesteps whatever that Color32-specific path is doing wrong.
        _rgbaBuffer ??= new byte[pixels.Length * 4];
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 c = pixels[i];
            int o = i * 4;
            _rgbaBuffer[o] = c.r;
            _rgbaBuffer[o + 1] = c.g;
            _rgbaBuffer[o + 2] = c.b;
            _rgbaBuffer[o + 3] = c.a;
        }

        Result result;
        try
        {
            result = barcodeReader.Decode(_rgbaBuffer, backCam.width, backCam.height, RGBLuminanceSource.BitmapFormat.RGBA32);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ColorVisionQRScanner] 解碼失敗：{ex.Message}");
            return;
        }

        if (result == null || string.IsNullOrEmpty(result.Text)) return;

        Debug.Log($"[ColorVisionQRScanner] 掃到 QR，內容：\"{result.Text}\"");
        TryApplyCode(result.Text);
    }

#if UNITY_EDITOR
    private void DumpDebugFrame(Color32[] pixels, int width, int height)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.SetPixels32(pixels);
        tex.Apply();
        string dir = System.IO.Path.Combine(Application.dataPath, "..", "DebugCapture");
        System.IO.Directory.CreateDirectory(dir);
        string path = System.IO.Path.Combine(dir, $"qr_debug_{System.DateTime.Now:HHmmss}.png");
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Destroy(tex);
        Debug.Log($"[ColorVisionQRScanner] 已存除錯截圖：{path}");
    }
#endif

    private void FlipVertical(Color32[] pixels, int width, int height)
    {
        _flipBuffer ??= new Color32[pixels.Length];
        for (int y = 0; y < height; y++)
        {
            System.Array.Copy(pixels, y * width, _flipBuffer, (height - 1 - y) * width, width);
        }
        System.Array.Copy(_flipBuffer, pixels, pixels.Length);
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
