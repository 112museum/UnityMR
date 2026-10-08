using UnityEngine;

// 色弱濾鏡的「整畫面調色」那一半：原本（HeadLockedColorFilm）是跟著頭走、蓋滿整個視野的
// 2D 薄膜，現在改成釘在場景裡一個固定座標的 3D 半透明光暈，mesh 直接沿用碗的形狀
// （Assets/Chinese Exhibits/635.obj 的 mmGroup0），而不是整個畫面都加一層色。掛在場景裡
// 想讓光暈出現的那個位置的 GameObject 上即可，光暈會以這個物件的原點為中心生成，
// 不會再去抓 Camera.main 的位置。
//
// 顏色跟濃淡沿用色覺測驗 QR 的同一個代碼（例如 "B2"），跟虛擬物件的濾鏡一致：
//   字母 → 顏色：取現有濾鏡（ColorBlindFilterToggle.GetMultipliers）加強的那兩個通道——
//     B 紅色弱 加強紅+藍 = 洋紅、C 綠色弱 加強綠+藍 = 青、D 藍色弱 加強紅+綠 = 黃。
//   數字 → 濃淡：跟著 intensity（1 重度 1.5 / 2 中度 1.3 / 3 輕度 1.15）走，重度最濃。
//
// 用法：掛在場景裡「光暈該出現的那個固定位置」的 GameObject 上（不需要是 Camera 或任何
// 一直存在的物件，只要位置是你要的就好）。光暈跟著 ColorBlindFilterToggle 的濾鏡一起開關；
// 玩家沒辦法跟它互動（沒有 Collider、放在 Ignore Raycast 圖層，MRTK 的手部射線會直接穿過去）。
public class SceneColorHalo : MonoBehaviour
{
    [Header("各色弱類型的光暈顏色（A 透明度不用調，由下面的 Max Alpha 和程度決定）")]
    [SerializeField] private Color protanColor = new Color(1f, 0.55f, 0.85f);   // B 紅色弱：柔和洋紅
    [SerializeField] private Color deuteranColor = new Color(0.55f, 0.9f, 1f);  // C 綠色弱：柔和青
    [SerializeField] private Color tritanColor = new Color(1f, 0.9f, 0.55f);    // D 藍色弱：柔和黃

    [Tooltip("重度（代碼數字 1）時的透明度；中度約 6 成、輕度約 3 成。越小越淡。")]
    [Range(0f, 1f)]
    [SerializeField] private float maxAlpha = 0.4f;

    [Tooltip("光暈邊緣的柔化範圍（0~1）。數值愈大，正對鏡頭的核心愈小、愈像一顆柔邊發光的碗；" +
             "愈接近 0，整個碗的輪廓邊界愈死硬。Mesh 表面每一點到物體中心的距離不是固定值，" +
             "但碗這種凹凸起伏的形狀用「離中心多遠」淡出會很不均勻，所以這裡改用視線跟表面" +
             "法線的夾角（Fresnel）來決定淡出程度，不管 mesh 形狀長怎樣效果都一致。")]
    [Range(0.01f, 1f)]
    [SerializeField] private float edgeSoftness = 0.4f;

    [Tooltip("光暈用的 Mesh，從 Assets/Chinese Exhibits/635.obj 底下的 mmGroup0 子物件拖進來" +
             "（Project 視窗展開 635.obj 即可看到）。")]
    [SerializeField] private Mesh bowlMesh;

    [Tooltip("套在 bowlMesh 原始大小上的縮放倍率（直接對應生成物件的 localScale），" +
             "(1,1,1) 代表維持 mesh 原本的大小。")]
    [SerializeField] private Vector3 scale = Vector3.one;

    [Tooltip("Always Visible 測試時、還沒掃測驗 QR 的情況下用哪一種類型的顏色。")]
    [SerializeField] private ColorBlindFilterToggle.ColorBlindType previewType = ColorBlindFilterToggle.ColorBlindType.Protanomalous;

    [Tooltip("勾起來的話不管濾鏡有沒有開都顯示（測試用）。正式流程要取消勾選。")]
    [SerializeField] private bool alwaysVisible = false;

    private Renderer haloRenderer;
    private Material haloMaterial;
    private bool isNormalColor;

    private void Start()
    {
        var shader = Shader.Find("Custom/ColorHalo");
        if (shader == null)
        {
            Debug.LogError("[SceneColorHalo] 找不到 Custom/ColorHalo shader。");
            return;
        }

        if (bowlMesh == null)
        {
            Debug.LogError("[SceneColorHalo] 沒有指定 bowlMesh，光暈不會顯示任何東西。");
            return;
        }

        var halo = new GameObject("ColorHalo");
        halo.layer = 2; // Ignore Raycast
        halo.transform.SetParent(transform, false);
        halo.transform.localPosition = Vector3.zero;
        halo.transform.localRotation = Quaternion.identity;
        halo.transform.localScale = scale;
        halo.AddComponent<MeshFilter>().sharedMesh = bowlMesh;

        haloMaterial = new Material(shader);
        haloRenderer = halo.AddComponent<MeshRenderer>();
        haloRenderer.sharedMaterial = haloMaterial;
        haloRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        haloRenderer.receiveShadows = false;

        var toggle = ColorBlindFilterToggle.Instance;
        if (toggle != null)
        {
            toggle.FilterStateChanged += SetVisible;
            SetVisible(toggle.IsFilterOn);
        }
        else
        {
            Debug.LogWarning("[SceneColorHalo] 場景裡找不到 ColorBlindFilterToggle，光暈只會依 alwaysVisible 顯示。");
            SetVisible(false);
        }
    }

    private ColorBlindFilterToggle.ColorBlindType lastType;
    private float lastIntensity = -1f;

    private void Update()
    {
        // 掃測驗 QR 只會把結果存起來，濾鏡要等第二幕開場才打開——但光暈顯示著的時候（例如
        // Always Visible 測試）測驗結果一變就要馬上換色，不能只靠 FilterStateChanged。
        var toggle = ColorBlindFilterToggle.Instance;
        if (haloMaterial == null || toggle == null) return;
        if (toggle.DetectedType == lastType && Mathf.Approximately(toggle.intensity, lastIntensity)) return;

        lastType = toggle.DetectedType;
        lastIntensity = toggle.intensity;
        UpdateColor();
        Debug.Log($"[SceneColorHalo] 測驗結果變成 {lastType}（intensity {lastIntensity}），光暈顏色 {haloMaterial.color}");
    }

    private void OnValidate()
    {
        // 在 Play 模式裡調 Inspector 的顏色可以馬上看到效果。
        if (haloMaterial != null) UpdateColor();
        if (haloRenderer != null) haloRenderer.transform.localScale = scale;
    }

    private void OnDestroy()
    {
        if (ColorBlindFilterToggle.Instance != null)
        {
            ColorBlindFilterToggle.Instance.FilterStateChanged -= SetVisible;
        }
        if (haloMaterial != null) Destroy(haloMaterial);
    }

    private void SetVisible(bool on)
    {
        if (haloRenderer == null) return;
        UpdateColor(); // 濾鏡打開的當下測驗結果已經確定，這時候換成對應的顏色
        // 色覺正常不需要套用任何濾鏡，不管 on/alwaysVisible 怎麼說都強制隱形。
        haloRenderer.enabled = !isNormalColor && (on || alwaysVisible);
    }

    private void UpdateColor()
    {
        var toggle = ColorBlindFilterToggle.Instance;
        var type = toggle != null && toggle.DetectedType != ColorBlindFilterToggle.ColorBlindType.Normal
            ? toggle.DetectedType
            : previewType;

        isNormalColor = type == ColorBlindFilterToggle.ColorBlindType.Normal;
        if (isNormalColor) return; // 不用算顏色/透明度，SetVisible 會把 renderer 關掉

        float intensity = toggle != null ? toggle.intensity : 1.5f;

        var color = type switch
        {
            ColorBlindFilterToggle.ColorBlindType.Protanomalous => protanColor,
            ColorBlindFilterToggle.ColorBlindType.Deuteranomalous => deuteranColor,
            ColorBlindFilterToggle.ColorBlindType.Tritanomalous => tritanColor,
            _ => Color.clear,
        };
        // intensity 1.15 / 1.3 / 1.5 → 濃淡 0.3 / 0.6 / 1.0 倍的 maxAlpha
        color.a = maxAlpha * Mathf.Clamp01((intensity - 1f) / 0.5f);
        haloMaterial.color = color;
        haloMaterial.SetFloat("_EdgeSoftness", edgeSoftness);
    }

    // 在 Editor 按 Play 後，對 Inspector 上這個元件按右鍵（或右上角 ⋮）就能模擬掃到測驗 QR，
    // 走的是跟真的掃 QR 一樣的 ColorBlindFilterToggle.ApplyCode。
    [ContextMenu("模擬掃到 B2（紅色弱 中度）")] private void SimulateB2() => SimulateScan("B2");
    [ContextMenu("模擬掃到 C2（綠色弱 中度）")] private void SimulateC2() => SimulateScan("C2");
    [ContextMenu("模擬掃到 D2（藍色弱 中度）")] private void SimulateD2() => SimulateScan("D2");
    [ContextMenu("模擬掃到 C1（綠色弱 重度）")] private void SimulateC1() => SimulateScan("C1");
    [ContextMenu("模擬掃到 C3（綠色弱 輕度）")] private void SimulateC3() => SimulateScan("C3");

    private void SimulateScan(string code)
    {
        if (!Application.isPlaying || ColorBlindFilterToggle.Instance == null)
        {
            Debug.LogWarning("[SceneColorHalo] 要先按 Play 才能模擬掃 QR。");
            return;
        }
        ColorBlindFilterToggle.Instance.ApplyCode(code);
    }
}
