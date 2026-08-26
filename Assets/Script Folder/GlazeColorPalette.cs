using Photon.Pun;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 第二幕「瑪瑙入釉」調色盤：掛在調色盤 UI 的固定場景物件上，該物件需要有 PhotonView 元件
// （場景內固定擺放即可，不需要 PhotonNetwork.Instantiate）。
// 色票按鈕的 On Click() 綁定 SelectColor(index)：任一位玩家點選色票，都會透過 RPC 廣播給雙方，
// 讓兩人看到同一個正在變化的「面板預覽碗」（所見即所得），但真正的碗本體這時還不會變色，
// 只是玩家在試色。「確認」按鈕綁定 ConfirmSelection()：只鎖定顏色（不能再改選）、存起來，
// 並觸發 onColorConfirmed（接後續轉場，例如推進 StoryModeManager 到第三幕）——這時真正的
// 碗本體還沒變色。真正套色的時機是「別的地方」呼叫 ApplyColorToBowl()（例如碗被放進窯爐、
// 或劇情推進到某個特定步驟時），把已經鎖定的顏色套到 actualRenderers 上。
// 面板預覽碗不需要互動，用平面示意圖即可：把碗的線稿/剪影圖（白色或灰階，才能被染色）
// 做成 UI Image，拖進 previewImages，選色時會直接改 Image.color，不需要 3D 模型。
public class GlazeColorPalette : MonoBehaviourPun
{
    [Header("調色盤色票（依序對應面板上的色票按鈕，SelectColor 用索引指定）")]
    public Color[] paletteColors;

    [Header("面板預覽圖示")]
    public Image[] previewImages;

    [Header("真正的碗本體（按下確認後才套色；若碗是場景中動態生成的，改用 RegisterActualBowl() 註冊，這裡留空即可）")]
    public Renderer[] actualRenderers;

    [Header("套色時換上的黑白釉料材質")]
    public Material glazeGrayscaleMaterial;

    [Header("玩家沒選色的預設材質")]
    public Material defaultMaterial;

    [Header("確認選色後觸發（推進劇情用，這時碗本體還沒變色）")]
    public UnityEvent onColorConfirmed;

    [Header("調色面板")]
    [SerializeField] private GameObject ChangeColorCanvas;

    [Header("顏料指定面板")]
    [SerializeField] private GameObject AddPigmentCanvas;

    [Header("顏色真正套到碗本體後觸發(動畫特效之類的)")]
    public UnityEvent onColorAppliedToBowl;

    public int SelectedIndex { get; private set; } = -1;
    public bool IsConfirmed { get; private set; }
    public bool IsAppliedToBowl { get; private set; }

    // 把這個方法掛到每個色票按鈕的 On Click()，colorIndex 對應 paletteColors 的索引
    public void SelectColor(int colorIndex)
    {
        if (IsConfirmed) return; // 已確認就不能再改
        if (paletteColors == null || colorIndex < 0 || colorIndex >= paletteColors.Length) return;

        photonView.RPC(nameof(RpcApplyColor), RpcTarget.All, colorIndex);
    }

    // 給動態生成的碗 prefab 呼叫（例如掛在該 prefab 上的腳本在 Start() 呼叫），
    // 讓調色盤知道「真正的碗」現在是哪個 instance。如果 ApplyColorToBowl() 早就呼叫過了，
    // 這裡會立刻把已選定的顏色補套上去，不用管碗跟套色動作誰先誰後。
    public void RegisterActualBowl(Renderer[] renderers)
    {
        actualRenderers = renderers;

        if (!IsAppliedToBowl) return;

        if (SelectedIndex >= 0)
        {
            ApplyGlazeToRenderers(actualRenderers, paletteColors[SelectedIndex]);
        }
        else
        {
            ApplyDefaultMaterialToRenderers(actualRenderers);
        }
    }

    // 把這個方法掛到「確認」按鈕的 On Click()：只鎖定顏色、存起來，不會馬上套到真正的碗上。
    public void ConfirmSelection()
    {
        if (IsConfirmed) return;
        if (SelectedIndex < 0) return; // 還沒選色不能確認

        photonView.RPC(nameof(RpcConfirm), RpcTarget.All);
    }

    // 真正把碗本體套上材質，在你需要的時機（例如碗被放進窯爐、或劇情推進到某個特定步驟）
    // 從別的地方呼叫這個。如果玩家已經選色並確認過，套的是上色後的 glazeGrayscaleMaterial；
    // 如果玩家沒選色（沒呼叫過 ConfirmSelection() 或根本沒選），就直接套上 defaultMaterial。
    public void ApplyColorToBowl()
    {
        if (IsAppliedToBowl) return; // 已經套過了

        if (IsConfirmed && SelectedIndex >= 0)
        {
            photonView.RPC(nameof(RpcApplyToBowl), RpcTarget.All);
        }
        else
        {
            photonView.RPC(nameof(RpcApplyDefaultToBowl), RpcTarget.All);
        }
    }

    // 給碗以外的物件讀取「玩家已確認的顏色」用（例如顏料容器裝滿後要在自己原本的材質上
    // 改色，而不是套用碗專用的 glazeGrayscaleMaterial）。沒選色/沒確認時回傳 null，
    // 呼叫端自行決定沒顏色時要怎麼處理。
    public Color? ConfirmedColor => IsConfirmed && SelectedIndex >= 0 ? paletteColors[SelectedIndex] : (Color?)null;

    [PunRPC]
    private void RpcApplyColor(int colorIndex)
    {
        SelectedIndex = colorIndex;
        Color color = paletteColors[colorIndex];
        ApplyColorToImages(color);
    }

    [PunRPC]
    private void RpcConfirm()
    {
        IsConfirmed = true;
        if (ChangeColorCanvas != null) ChangeColorCanvas.SetActive(false);
        if (AddPigmentCanvas != null) AddPigmentCanvas.SetActive(true);
        onColorConfirmed?.Invoke();
    }

    [PunRPC]
    private void RpcApplyToBowl()
    {
        IsAppliedToBowl = true;
        ApplyGlazeToRenderers(actualRenderers, paletteColors[SelectedIndex]);
        onColorAppliedToBowl?.Invoke();
    }

    [PunRPC]
    private void RpcApplyDefaultToBowl()
    {
        IsAppliedToBowl = true;
        ApplyDefaultMaterialToRenderers(actualRenderers);
        onColorAppliedToBowl?.Invoke();
    }

    // 把碗本體的 material 整個換成黑白釉料材質的獨立實例，再上色（而不是像預覽那樣直接改
    // 原本 default material 的 _Color），這樣碗身才會真的套上另一份材質的紋理/質感，不只是變色。
    private void ApplyGlazeToRenderers(Renderer[] renderers, Color color)
    {
        if (renderers == null || glazeGrayscaleMaterial == null) return;

        foreach (Renderer rend in renderers)
        {
            if (rend == null) continue;

            Material instance = new Material(glazeGrayscaleMaterial);
            if (instance.HasProperty("_Color"))
            {
                instance.SetColor("_Color", color);
            }
            else if (instance.HasProperty("_BaseColor"))
            {
                instance.SetColor("_BaseColor", color);
            }

            rend.material = instance;
        }
    }

    // 沒選色時套用的預設材質，不需要上色也不需要各自獨立實例，直接共用同一份 sharedMaterial 即可。
    private void ApplyDefaultMaterialToRenderers(Renderer[] renderers)
    {
        if (renderers == null || defaultMaterial == null) return;

        foreach (Renderer rend in renderers)
        {
            if (rend == null) continue;
            rend.sharedMaterial = defaultMaterial;
        }
    }

    // 不直接寫 img.color，而是透過 ColorBlindFilterToggle 登記「真正選的顏色」——
    // 如果濾鏡當下是開著的，濾鏡會在這個顏色上疊加色弱調整；沒有濾鏡（或場景裡沒放這個
    // 元件）就照原色顯示。這樣選色永遠有優先權，不會被濾鏡自己開關時的舊快取蓋掉。
    private void ApplyColorToImages(Color color)
    {
        if (previewImages == null) return;

        foreach (Image img in previewImages)
        {
            if (img == null) continue;

            if (ColorBlindFilterToggle.Instance != null)
            {
                ColorBlindFilterToggle.Instance.SetTargetImageColor(img, color);
            }
            else
            {
                img.color = color;
            }
        }
    }
}
