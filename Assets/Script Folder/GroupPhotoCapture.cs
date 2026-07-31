using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
#if WINDOWS_UWP

#endif

// 第四幕結尾「大合照」（需求書 UC006 步驟1-5，不含信箱寄送——那段要等團隊決定要不要做）。
// HoloLens 是光學透視，看得到的「玩家本人」是真實世界、不是全息物件，所以不能只用一般
// 螢幕截圖(ScreenCapture.CaptureScreenshot)，那只會拍到全息物件、拍不到人。這裡改用
// HoloLens 的 Locatable Camera（PhotoCapture API），它會把「真實世界相機畫面」跟「全息物件
// （客製化的碗、NPC 乾隆）」合成在同一張照片裡，才會是劇本寫的「跟NPC、碗一起拍大合照」。
// 兩位玩家各自的 HoloLens 都掛這支腳本，各自拍下自己視角看到的合照即可，不需要 Photon 同步。
// 只在真正的 UWP/HoloLens 平台上才會執行拍照；Editor 或其他平台呼叫 TakeGroupPhoto() 會直接
// 略過並記錄 log，方便沒有 HoloLens 環境時測試劇情流程不會卡住或編譯失敗。
public class GroupPhotoCapture : MonoBehaviour
{
    [Serializable] public class PhotoSavedEvent : UnityEvent<string> { }
    [Serializable] public class PhotoFailedEvent : UnityEvent<string> { }

    [Header("拍照開始時觸發（可接淡出提示、倒數音效）")]
    public UnityEvent onPhotoStarting;

    [Header("拍照成功時觸發，帶入存檔完整路徑（Editor/非UWP平台下路徑為空字串）")]
    public PhotoSavedEvent onPhotoSaved;

    [Header("拍照失敗時觸發，帶入錯誤訊息")]
    public PhotoFailedEvent onPhotoFailed;

    private bool _isCapturing;

#if WINDOWS_UWP
    private UnityEngine.Windows.WebCam.PhotoCapture _photoCapture;
#endif

    // 把這個方法掛到「拍照」按鈕的 On Click()，或在貼紙裝飾流程結束後直接呼叫
    public void TakeGroupPhoto()
    {
        if (_isCapturing) return;

#if WINDOWS_UWP
        _isCapturing = true;
        onPhotoStarting?.Invoke();
        UnityEngine.Windows.WebCam.PhotoCapture.CreateAsync(showHolograms: true, OnPhotoCaptureCreated);
#else
        Debug.Log("[GroupPhotoCapture] 目前平台不是 UWP/HoloLens，略過實際拍照（Editor 測試用）。");
        onPhotoStarting?.Invoke();
        onPhotoSaved?.Invoke(string.Empty);
#endif
    }

#if WINDOWS_UWP
    private void OnPhotoCaptureCreated(UnityEngine.Windows.WebCam.PhotoCapture captureObject)
    {
        _photoCapture = captureObject;

        Resolution cameraResolution = UnityEngine.Windows.WebCam.PhotoCapture.SupportedResolutions
            .OrderByDescending(res => res.width * res.height)
            .First();

        var cameraParameters = new UnityEngine.Windows.WebCam.CameraParameters
        {
            hologramOpacity = 1.0f,
            cameraResolutionWidth = cameraResolution.width,
            cameraResolutionHeight = cameraResolution.height,
            pixelFormat = UnityEngine.Windows.WebCam.CapturePixelFormat.BGRA32
        };

        _photoCapture.StartPhotoModeAsync(cameraParameters, OnPhotoModeStarted);
    }

    private void OnPhotoModeStarted(UnityEngine.Windows.WebCam.PhotoCapture.PhotoCaptureResult result)
    {
        if (!result.success)
        {
            FailAndCleanup($"StartPhotoModeAsync 失敗 (hResult={result.hResult})");
            return;
        }

        string fileName = $"重塑天青_大合照_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
        string filePath = Path.Combine(Application.persistentDataPath, fileName);

        _photoCapture.TakePhotoAsync(filePath, UnityEngine.Windows.WebCam.PhotoCaptureFileOutputFormat.JPG,
            (photoResult) => OnPhotoSaved(photoResult, filePath));
    }

    private void OnPhotoSaved(UnityEngine.Windows.WebCam.PhotoCapture.PhotoCaptureResult result, string filePath)
    {
        if (!result.success)
        {
            FailAndCleanup($"TakePhotoAsync 失敗 (hResult={result.hResult})");
            return;
        }

        Debug.Log($"[GroupPhotoCapture] 合照已存檔：{filePath}");
        _photoCapture.StopPhotoModeAsync((stopResult) => OnPhotoModeStopped(filePath));
    }

    private void OnPhotoModeStopped(string filePath)
    {
        _photoCapture.Dispose();
        _photoCapture = null;
        _isCapturing = false;
        onPhotoSaved?.Invoke(filePath);
    }

    private void FailAndCleanup(string message)
    {
        Debug.LogError($"[GroupPhotoCapture] {message}");
        _isCapturing = false;
        onPhotoFailed?.Invoke(message);

        if (_photoCapture != null)
        {
            _photoCapture.Dispose();
            _photoCapture = null;
        }
    }
#endif
}
