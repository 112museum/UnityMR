// ColorVisionMarkerReader.cs — 用眼鏡內建的 QR 讀碼器讀色覺測驗 QR（A、B1～D3）。
//
// 原本的 ColorVisionQRScanner 用 ZXing 解碼攝影機畫面，對「拍螢幕」「有點斜」的 QR 很不靈敏
// （2026-08 就查過，是 ZXing 本身的問題）；但眼鏡內建的讀碼器（ARMarkerManager，碗的 QR 跟
// 多人桌子的 QRAnchorRig 都在用）一直都讀得到。這支程式不需要自己的 ARMarkerManager：
// 直接聽場景裡現有的每一個 ARMarkerManager，讀到測驗代碼就交給 ColorVisionQRScanner
// （走跟 ZXing 掃到一模一樣的流程），場景裡沒有掃描器的話就直接交給 ColorBlindFilterToggle。
// ZXing 掃描器照樣運作，兩個哪個先讀到都可以。
//
// 用法：掛在場景裡任何一直存在的物件上（例如有 ColorBlindFilterToggle 的那個物件）。
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.MixedReality.OpenXR;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

public class ColorVisionMarkerReader : MonoBehaviour
{
    private static readonly Regex CodePattern = new Regex(@"^(A|[BCD][1-3])$");

    private readonly List<ARMarkerManager> managers = new List<ARMarkerManager>();
    private float sessionStartTime;
    private string lastAppliedCode;

    private void Awake()
    {
        sessionStartTime = Time.realtimeSinceStartup;
    }

    private void Start()
    {
        managers.AddRange(FindObjectsOfType<ARMarkerManager>());
        if (managers.Count == 0)
        {
            Debug.LogWarning("[ColorVisionMarkerReader] 場景裡找不到任何 ARMarkerManager（例如 QRAnchorRig），沒辦法用眼鏡內建讀碼器讀色覺 QR。");
            return;
        }

        foreach (var manager in managers) manager.markersChanged += OnMarkersChanged;
        Debug.Log($"[ColorVisionMarkerReader] 正在聽 {managers.Count} 個 ARMarkerManager。");
    }

    private void OnDestroy()
    {
        foreach (var manager in managers)
        {
            if (manager != null) manager.markersChanged -= OnMarkersChanged;
        }
    }

    private void OnMarkersChanged(ARMarkersChangedEventArgs args)
    {
        foreach (var marker in args.added) TryRead(marker);
        foreach (var marker in args.updated) TryRead(marker);
    }

    private void TryRead(ARMarker marker)
    {
        var code = marker.GetDecodedString()?.Trim();
        if (string.IsNullOrEmpty(code) || !CodePattern.IsMatch(code)) return;

        // 眼鏡會記住之前掃過的 QR，開 app 前就存在的舊紀錄不算（同 BowlAnchorAligner）。
        if (marker.lastSeenTime < sessionStartTime) return;
        if (code == lastAppliedCode) return;
        lastAppliedCode = code;

        var scanner = FindObjectOfType<ColorVisionQRScanner>();
        if (scanner != null)
        {
            scanner.ApplyCodeFromMarker(code);
        }
        else if (ColorBlindFilterToggle.Instance != null)
        {
            Debug.Log($"[ColorVisionMarkerReader] 讀到色覺代碼 \"{code}\"（場景裡沒有 ColorVisionQRScanner，直接交給濾鏡）");
            ColorBlindFilterToggle.Instance.ApplyCode(code);
        }
    }
}
