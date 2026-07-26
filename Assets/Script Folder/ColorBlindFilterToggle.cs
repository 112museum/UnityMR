using UnityEngine;
using TMPro;

// 掛在任意 GameObject 上皆可（不再需要 Camera 元件——不是全螢幕後製，是改指定物件的材質）。
// Button 的 OnClick() 綁定 ToggleFilter()：第一下開啟濾鏡、第二下關閉。
// 只會改變 targetRenderers 裡指定的物件（例如展品、調色盤）的材質，不影響畫面其他部分，
// 因為 HoloLens 是光學透視、看得到真實世界，全螢幕後製也碰不到真實物件，不如直接限定範圍。
public class ColorBlindFilterToggle : MonoBehaviour
{
    public enum ColorBlindType
    {
        Protanomalous,   // 紅色弱
        Deuteranomalous, // 綠色弱
        Tritanomalous    // 藍色弱
    }

    [Header("要套用色弱濾鏡的物件（例如展品、調色盤）")]
    public Renderer[] targetRenderers;

    [Header("套用濾鏡用的 Shader（留空會自動用 Custom/NewSurfaceShader）")]
    public Shader colorBlindShader;

    [Header("色盲類型與強度")]
    public ColorBlindType type = ColorBlindType.Deuteranomalous;
    [Range(1f, 2f)]
    public float intensity = 1.3f; // 對應學姊原本 factor: 1.15 輕度 / 1.3 中度 / 1.5 重度

    [Header("按鈕文字（可選，用來顯示目前開/關狀態）")]
    public TMP_Text buttonLabel;

    [Header("型別選擇面板（按下開啟濾鏡時彈出，讓玩家選色盲類型；面板上三顆按鈕的 OnClick() 各自綁 SelectType(int)，0=Protanomalous/1=Deuteranomalous/2=Tritanomalous，順序需對應上面 enum）")]
    public GameObject typeSelectionPanel;

    private bool isFilterOn = false;
    private Material[][] _originalMaterials;
    private Material[][] _tintedMaterials;

    private void Start()
    {
        if (colorBlindShader == null)
        {
            colorBlindShader = Shader.Find("Custom/NewSurfaceShader");
        }

        CacheMaterials();
        UpdateButtonLabel();

        if (typeSelectionPanel != null)
        {
            typeSelectionPanel.SetActive(false);
        }
    }

    // 幫每個目標物件各自準備一份「濾鏡材質」，保留該物件原本的貼圖(_MainTex)，
    // 只是額外疊加 RGB 倍率，開關時直接整組換掉該 Renderer 的 sharedMaterials。
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
                Material tintedMat = new Material(colorBlindShader);
                if (originals[m] != null)
                {
                    // 有貼圖就直接複製；沒有貼圖(例如物件只設了純色，像測試用的色塊 Cube)
                    // 的話，這顆 Shader 只讀 _MainTex，不會管原本材質是用 _Color(Built-in
                    // Standard) 還是 _BaseColor(URP Lit) 存顏色，所以兩種都試著抓，抓到才能
                    // 把純色烤成一張 1x1 貼圖，濾鏡開啟時才不會變回預設白色
                    Texture mainTex = originals[m].mainTexture;
                    if (mainTex == null)
                    {
                        if (originals[m].HasProperty("_Color"))
                        {
                            mainTex = MakeSolidTexture(originals[m].GetColor("_Color"));
                        }
                        else if (originals[m].HasProperty("_BaseColor"))
                        {
                            mainTex = MakeSolidTexture(originals[m].GetColor("_BaseColor"));
                        }
                    }
                    tintedMat.SetTexture("_MainTex", mainTex);
                }
                tinted[m] = tintedMat;
            }
            _tintedMaterials[i] = tinted;
        }
    }

    private static Texture2D MakeSolidTexture(Color color)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }

    // 把這個方法掛到「開啟/關閉濾鏡」Button 的 On Click()。
    // 關閉時型別已經知道了，直接關掉不用重問；開啟時要先跳出型別選擇面板，
    // 玩家選了之後才真的套用濾鏡（見 SelectType）。
    public void ToggleFilter()
    {
        if (isFilterOn)
        {
            isFilterOn = false;
            ApplyToTargets(false);
            UpdateButtonLabel();
            return;
        }

        if (typeSelectionPanel != null)
        {
            typeSelectionPanel.SetActive(true);
        }
        else
        {
            // 沒接面板就退回舊行為：直接用 Inspector 上設定的 type 開啟，方便單獨測試這顆 script。
            SelectType(type);
        }
    }

    // 掛到型別選擇面板上三顆按鈕的 On Click()，依 enum 順序傳 0/1/2。
    public void SelectType(int typeIndex) => SelectType((ColorBlindType)typeIndex);

    public void SelectType(ColorBlindType selectedType)
    {
        type = selectedType;
        isFilterOn = true;

        ApplyMultipliers();
        ApplyToTargets(true);
        UpdateButtonLabel();

        if (typeSelectionPanel != null)
        {
            typeSelectionPanel.SetActive(false);
        }
    }

    private void ApplyMultipliers()
    {
        float redMultiplier = 1f;
        float greenMultiplier = 1f;
        float blueMultiplier = 1f;

        switch (type)
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
                // 設成 (0,0,640,480) 等同於涵蓋整個 0~1 UV 範圍，也就是整個物件表面都套用
                mat.SetVector("_Rect1", new Vector4(0, 0, 640, 480));
                mat.SetVector("_Rect2", new Vector4(0, 0, 640, 480));
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
