using UnityEngine;
using TMPro;

// 掛在任意 GameObject 上皆可（不再需要 Camera 元件——不是全螢幕後製，是改指定物件的材質）。
// 只會改變 targetRenderers 裡指定的物件（例如展品、調色盤）的材質，不影響畫面其他部分，
// 因為 HoloLens 是光學透視、看得到真實世界，全螢幕後製也碰不到真實物件，不如直接限定範圍。
//
// 類型不再讓玩家自己選——大部分色弱者其實不知道自己是哪個亞型。改成跟著學姊的流程走：
// 玩家事先在外部評估網站做色覺測驗（Cambridge Color Vision Test 架構），測驗結果連同 QR code
// 由 ColorVisionQRScanner 掃描解碼後呼叫 SetDetectedType() 存起來；接著在第二幕開場時由
// ColorBlindChapterTrigger（訂閱 StoryModeManager.OnShowObjectTag）呼叫 ActivateFromChapter2()
// 才真正套用濾鏡，並持續到體驗結束，除非玩家自己用 ManualToggle() 關掉。
public class ColorBlindFilterToggle : MonoBehaviour
{
    public enum ColorBlindType
    {
        Normal,          // 對應 QR 代碼 A，色覺正常，不需要矯正
        Protanomalous,   // 紅色弱，對應 QR 代碼 B
        Deuteranomalous, // 綠色弱，對應 QR 代碼 C
        Tritanomalous    // 藍色弱，對應 QR 代碼 D
    }

    public static ColorBlindFilterToggle Instance { get; private set; }

    [Header("要套用色弱濾鏡的物件（例如展品、調色盤）")]
    public Renderer[] targetRenderers;

    [Header("套用濾鏡用的 Shader（留空會自動用 Custom/NewSurfaceShader）")]
    public Shader colorBlindShader;

    [Range(1f, 2f)]
    public float intensity = 1.3f; // 對應學姊原本 factor: 1.15 輕度 / 1.3 中度 / 1.5 重度

    [Header("按鈕文字（可選，用來顯示目前開/關狀態）")]
    public TMP_Text buttonLabel;

    private bool isFilterOn = false;
    private bool hasDetectedType = false; // 玩家掃過 QR、確實拿到測驗結果了嗎
    private ColorBlindType detectedType = ColorBlindType.Normal;
    private Material[][] _originalMaterials;
    private Material[][] _tintedMaterials;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (colorBlindShader == null)
        {
            colorBlindShader = Shader.Find("Custom/NewSurfaceShader");
        }

        UpdateButtonLabel();
    }

    // 掛給 ColorVisionQRScanner：QR 解碼出玩家的色覺類型與程度後呼叫這個存起來。
    // 只是記錄，不會馬上套濾鏡——濾鏡要等第二幕開場（ActivateFromChapter2）才開。
    // severity 對應學姊原本 QRScanner 解出來的 level 字串："severe"/"moderate"/"mild"。
    public void SetDetectedType(ColorBlindType type, string severity)
    {
        detectedType = type;
        hasDetectedType = true;

        intensity = severity switch
        {
            "severe" => 1.5f,
            "moderate" => 1.3f,
            "mild" => 1.15f,
            _ => intensity,
        };

        Debug.Log($"[ColorBlindFilterToggle] 測驗結果：{detectedType} / {severity}（尚未套用，等第二幕開場）");
    }

    // Button.OnClick() 的下拉選單只列得出回傳 void 的方法，ApplyCode 為了讓呼叫端能判斷
    // 代碼格式對不對而回傳 bool，所以按鈕綁不到——這個 void 包裝專門給 On Click() 用，
    // 字串參數填 "B2"／"C1"／"D3" 之類的代碼即可模擬掃描結果，不用真的印一張 QR 出來測。
    public void ApplyCodeFromButton(string code) => ApplyCode(code);

    // 跟 ColorVisionQRScanner.TryApplyCode 共用同一份「兩碼格式」解析邏輯，讓真的掃 QR
    // 跟手動測試走的是同一段程式碼。回傳 false 代表代碼格式不對，沒有套用；程式呼叫
    // （例如 ColorVisionQRScanner）可以用這個回傳值判斷，UI 按鈕請改綁 ApplyCodeFromButton。
    public bool ApplyCode(string code)
    {
        if (string.IsNullOrEmpty(code)) return false;

        ColorBlindType? type = code[0] switch
        {
            'A' => ColorBlindType.Normal,
            'B' => ColorBlindType.Protanomalous,
            'C' => ColorBlindType.Deuteranomalous,
            'D' => ColorBlindType.Tritanomalous,
            _ => null,
        };

        if (type == null) return false;

        string severity = type == ColorBlindType.Normal
            ? "normal"
            : code.Length > 1 ? code[1] switch
            {
                '1' => "severe",
                '2' => "moderate",
                '3' => "mild",
                _ => "",
            } : "";

        SetDetectedType(type.Value, severity);
        return true;
    }

    // 掛給 ColorBlindChapterTrigger：第二幕開場的 tag 觸發時呼叫。
    // 玩家測出來是 Normal（QR 代碼 A）或根本沒掃過 QR 的話，不套濾鏡。
    public void ActivateFromChapter2()
    {
        if (isFilterOn)
        {
            Debug.Log("[ColorBlindFilterToggle] ActivateFromChapter2() 被呼叫，但濾鏡已經是開啟狀態，略過。");
            return;
        }

        if (!hasDetectedType || detectedType == ColorBlindType.Normal)
        {
            Debug.Log($"[ColorBlindFilterToggle] ActivateFromChapter2() 被呼叫，但沒有套用濾鏡（hasDetectedType={hasDetectedType}, detectedType={detectedType}）。");
            return;
        }

        CacheMaterials();
        ApplyMultipliers();
        ApplyToTargets(true);
        isFilterOn = true;
        UpdateButtonLabel();
        Debug.Log($"[ColorBlindFilterToggle] 濾鏡已套用：type={detectedType}, intensity={intensity}, targets={targetRenderers?.Length ?? 0}");
    }

    // 舊名稱別名——Assets/Scenes/張/Rose Seman.unity 裡已經有顆按鈕的 On Click() 綁的是
    // ToggleFilter()，保留這個名字避免那顆按鈕失效。新的地方請直接綁 ManualToggle()。
    public void ToggleFilter() => ManualToggle();

    // 掛給玩家手動開關（例如場景裡的一顆按鈕）的 On Click()。
    // 關閉時直接關掉；重新打開則沿用同一組測驗結果（不會再跳自選面板）。
    public void ManualToggle()
    {
        if (isFilterOn)
        {
            isFilterOn = false;
            ApplyToTargets(false);
            UpdateButtonLabel();
            return;
        }

        if (!hasDetectedType || detectedType == ColorBlindType.Normal)
        {
            Debug.LogWarning("[ColorBlindFilterToggle] 還沒有測驗結果可套用，先完成 QR 掃描。");
            return;
        }

        CacheMaterials();
        ApplyMultipliers();
        ApplyToTargets(true);
        isFilterOn = true;
        UpdateButtonLabel();
    }

    // 幫每個目標物件各自準備一份「濾鏡材質」，保留該物件「目前」的貼圖/顏色(_MainTex)，
    // 只是額外疊加 RGB 倍率，開關時直接整組換掉該 Renderer 的 sharedMaterials。
    // 特意不在 Start() 就烤好快取，而是每次「即將開啟濾鏡」前才重新抓一次目前的
    // sharedMaterials——這樣玩家在濾鏡開啟之前調色盤重新上的色，濾鏡才會跟著吃到，
    // 而不是永遠套用 Start() 當下、最原始的那個顏色。
    private void CacheMaterials()
    {
        int count = targetRenderers != null ? targetRenderers.Length : 0;
        _originalMaterials = new Material[count][];
        _tintedMaterials = new Material[count][];

        for (int i = 0; i < count; i++)
        {
            var rend = targetRenderers[i];
            if (rend == null) continue;

            Material[] originals = rend.sharedMaterials;
            _originalMaterials[i] = originals;

            Material[] tinted = new Material[originals.Length];
            for (int m = 0; m < originals.Length; m++)
            {
                // HoloLens 用 Single Pass Instanced 立體渲染，shader 沒開 instancing 的話畫面在裝置上不會正確顯示
                Material tintedMat = new Material(colorBlindShader) { enableInstancing = true };
                if (originals[m] != null)
                {
                    // 貼圖有就直接複製；沒有貼圖(例如測試用的純色色塊 Cube)就維持 Shader
                    // 預設的白色貼圖。真正的顏色一律交給下面的 _Color 去乘——不管物件是
                    // 用貼圖上色還是只在 material 上調了 _Color(Built-in Standard) /
                    // _BaseColor(URP Lit)，濾鏡材質都能吃到「目前」實際顯示的顏色，
                    // 而不是永遠只認貼圖、把手動調的顏色忽略掉。
                    if (originals[m].mainTexture != null)
                    {
                        tintedMat.SetTexture("_MainTex", originals[m].mainTexture);
                    }

                    Color tint = Color.white;
                    if (originals[m].HasProperty("_Color"))
                    {
                        tint = originals[m].GetColor("_Color");
                    }
                    else if (originals[m].HasProperty("_BaseColor"))
                    {
                        tint = originals[m].GetColor("_BaseColor");
                    }
                    tintedMat.SetColor("_Color", tint);
                }
                tinted[m] = tintedMat;
            }
            _tintedMaterials[i] = tinted;
        }
    }

    private void ApplyMultipliers()
    {
        float redMultiplier = 1f;
        float greenMultiplier = 1f;
        float blueMultiplier = 1f;

        switch (detectedType)
        {
            case ColorBlindType.Protanomalous:
                redMultiplier = intensity;
                blueMultiplier = intensity;
                break;
            case ColorBlindType.Deuteranomalous:
                greenMultiplier = intensity;
                blueMultiplier = intensity;
                break;
            case ColorBlindType.Tritanomalous:
                redMultiplier = intensity;
                greenMultiplier = intensity;
                break;
        }

        for (int i = 0; i < _tintedMaterials.Length; i++)
        {
            if (_tintedMaterials[i] == null) continue;

            foreach (Material mat in _tintedMaterials[i])
            {
                mat.SetFloat("_RedMultiplier", redMultiplier);
                mat.SetFloat("_GreenMultiplier", greenMultiplier);
                mat.SetFloat("_BlueMultiplier", blueMultiplier);

                // 學姊的 Shader 用 texcoord(0~1) * 640 / 480 跟 _Rect 比對決定要不要套用增強，
                // 設成 (0,0,640,480) 等同於涵蓋整個 0~1 UV 範圍，也就是整個物件表面都套用。
                // 原本 shader 設計是兩個「各自獨立的物件」bounding box（來自舊版 YOLO 偵測，通常
                // 不重疊），frag() 對 Rect1、Rect2 各自用 if（不是 else if）判斷、各自疊乘一次。
                // 這裡如果把 _Rect2 也設成跟 _Rect1 一樣的全範圍，等於每個像素兩個 if 都會成立，
                // RGB 倍率會被乘兩次（等同 intensity 平方），顏色會比 Inspector 上設的 intensity
                // 強烈很多，嚴重程度的三個級距也會被這個平方關係打亂。_Rect2 留一個「不可能成立」
                // 的退化 rect（min==max，嚴格不等式恆假）等於停用第二個判斷，只留 _Rect1 這一次套用。
                mat.SetVector("_Rect1", new Vector4(0, 0, 640, 480));
                mat.SetVector("_Rect2", new Vector4(0, 0, 0, 0));
            }
        }
    }

    private void ApplyToTargets(bool on)
    {
        if (targetRenderers == null) return;

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer rend = targetRenderers[i];
            if (rend == null) continue;

            rend.sharedMaterials = on ? _tintedMaterials[i] : _originalMaterials[i];
        }
    }

    private void UpdateButtonLabel()
    {
        if (buttonLabel != null)
        {
            buttonLabel.text = isFilterOn ? "關閉濾鏡" : "開啟濾鏡";
        }
    }
}
