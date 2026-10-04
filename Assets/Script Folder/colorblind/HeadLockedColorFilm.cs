using UnityEngine;

// 在玩家眼前蓋一層跟著頭走的半透明色彩薄膜，取代「疊在真碗上的虛擬碗」（碗的對位不準也不穩定）。
// HoloLens 是透明鏡片、只能加光，所以這層薄膜是「在整個視野上加一層淡淡的顏色」，沒辦法像真的
// 有色眼鏡那樣擋掉某種顏色的光——顏色和透明度都在 Inspector 調，太濃畫面會整個泛色。
//
// 顏色跟濃淡沿用色覺測驗 QR 的同一個代碼（例如 "B2"），跟虛擬物件的濾鏡一致：
//   字母 → 顏色：取現有濾鏡（ColorBlindFilterToggle.GetMultipliers）加強的那兩個通道——
//     B 紅色弱 加強紅+藍 = 洋紅、C 綠色弱 加強綠+藍 = 青、D 藍色弱 加強紅+綠 = 黃。
//   數字 → 濃淡：跟著 intensity（1 重度 1.5 / 2 中度 1.3 / 3 輕度 1.15）走，重度最濃。
//
// 用法：掛在場景裡任何一直存在的物件上（例如有 ColorBlindFilterToggle 的那個物件）。
// 薄膜跟著 ColorBlindFilterToggle 的濾鏡一起開關；玩家沒辦法跟它互動（沒有 Collider、
// 放在 Ignore Raycast 圖層，MRTK 的手部射線會直接穿過去）。
public class HeadLockedColorFilm : MonoBehaviour
{
    [Header("各色弱類型的薄膜顏色（A 透明度不用調，由下面的 Max Alpha 和程度決定）")]
    [SerializeField] private Color protanColor = new Color(1f, 0.55f, 0.85f);   // B 紅色弱：柔和洋紅
    [SerializeField] private Color deuteranColor = new Color(0.55f, 0.9f, 1f);  // C 綠色弱：柔和青
    [SerializeField] private Color tritanColor = new Color(1f, 0.9f, 0.55f);    // D 藍色弱：柔和黃

    [Tooltip("重度（代碼數字 1）時的透明度；中度約 6 成、輕度約 3 成。越小越淡。")]
    [Range(0f, 1f)]
    [SerializeField] private float maxAlpha = 0.4f;

    [Tooltip("視野最外圈多寬的範圍往外變淡（0 = 不淡、邊緣很明顯；0.1 = 最外面一成）。中間整片濃淡一致。")]
    [Range(0f, 0.5f)]
    [SerializeField] private float edgeFade = 0.1f;

    [Tooltip("Always Visible 測試時、還沒掃測驗 QR 的情況下用哪一種類型的顏色。")]
    [SerializeField] private ColorBlindFilterToggle.ColorBlindType previewType = ColorBlindFilterToggle.ColorBlindType.Protanomalous;

    [Tooltip("薄膜離眼睛多遠（公尺）。shader 一律畫在最上層，所以不管虛擬物件多近都會被染到色。" +
             "HoloLens 2 的對焦距離約 2 公尺，放太近（例如 0.5）眼睛會一直想對焦，戴久了不舒服。")]
    [SerializeField] private float distance = 2f;

    [Tooltip("勾起來的話不管濾鏡有沒有開都顯示（測試用）。正式流程要取消勾選。")]
    [SerializeField] private bool alwaysVisible = false;

    private Renderer filmRenderer;
    private Material filmMaterial;

    private void Start()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[HeadLockedColorFilm] 找不到 Main Camera，無法建立色彩薄膜。");
            return;
        }

        var shader = Shader.Find("Custom/ColorFilm");
        if (shader == null)
        {
            Debug.LogError("[HeadLockedColorFilm] 找不到 Custom/ColorFilm shader。");
            return;
        }

        var film = GameObject.CreatePrimitive(PrimitiveType.Quad);
        film.name = "ColorFilm";
        Destroy(film.GetComponent<Collider>());
        film.layer = 2; // Ignore Raycast
        film.transform.SetParent(cam.transform, false);
        film.transform.localPosition = new Vector3(0f, 0f, distance);
        film.transform.localRotation = Quaternion.identity;
        // 蓋滿整個視野還多留一些（HoloLens 2 視角約 43°×29°）。
        film.transform.localScale = new Vector3(distance * 3f, distance * 3f, 1f);

        filmMaterial = new Material(shader);
        filmRenderer = film.GetComponent<Renderer>();
        filmRenderer.sharedMaterial = filmMaterial;
        filmRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        filmRenderer.receiveShadows = false;

        var toggle = ColorBlindFilterToggle.Instance;
        if (toggle != null)
        {
            toggle.FilterStateChanged += SetVisible;
            SetVisible(toggle.IsFilterOn);
        }
        else
        {
            Debug.LogWarning("[HeadLockedColorFilm] 場景裡找不到 ColorBlindFilterToggle，薄膜只會依 alwaysVisible 顯示。");
            SetVisible(false);
        }
    }

    private ColorBlindFilterToggle.ColorBlindType lastType;
    private float lastIntensity = -1f;

    private void Update()
    {
        // 掃測驗 QR 只會把結果存起來，濾鏡要等第二幕開場才打開——但薄膜顯示著的時候（例如
        // Always Visible 測試）測驗結果一變就要馬上換色，不能只靠 FilterStateChanged。
        var toggle = ColorBlindFilterToggle.Instance;
        if (filmMaterial == null || toggle == null) return;
        if (toggle.DetectedType == lastType && Mathf.Approximately(toggle.intensity, lastIntensity)) return;

        lastType = toggle.DetectedType;
        lastIntensity = toggle.intensity;
        UpdateColor();
        Debug.Log($"[HeadLockedColorFilm] 測驗結果變成 {lastType}（intensity {lastIntensity}），薄膜顏色 {filmMaterial.color}");
    }

    private void OnValidate()
    {
        // 在 Play 模式裡調 Inspector 的顏色可以馬上看到效果。
        if (filmMaterial != null) UpdateColor();
    }

    private void OnDestroy()
    {
        if (ColorBlindFilterToggle.Instance != null)
        {
            ColorBlindFilterToggle.Instance.FilterStateChanged -= SetVisible;
        }
        if (filmMaterial != null) Destroy(filmMaterial);
    }

    private void SetVisible(bool on)
    {
        if (filmRenderer == null) return;
        UpdateColor(); // 濾鏡打開的當下測驗結果已經確定，這時候換成對應的顏色
        filmRenderer.enabled = on || alwaysVisible;
    }

    private void UpdateColor()
    {
        var toggle = ColorBlindFilterToggle.Instance;
        var type = toggle != null && toggle.DetectedType != ColorBlindFilterToggle.ColorBlindType.Normal
            ? toggle.DetectedType
            : previewType;
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
        filmMaterial.color = color;
        filmMaterial.SetFloat("_EdgeFade", edgeFade);
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
            Debug.LogWarning("[HeadLockedColorFilm] 要先按 Play 才能模擬掃 QR。");
            return;
        }
        ColorBlindFilterToggle.Instance.ApplyCode(code);
    }
}
