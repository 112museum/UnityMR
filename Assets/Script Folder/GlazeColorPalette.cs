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

    [Header("面板預覽模型（3D，選色時即時換色，尚未確認）")]
    public Renderer[] previewRenderers;

    [Header("面板預覽圖示（2D UI Image，選色時即時換色，尚未確認）")]
    public Image[] previewImages;

    [Header("真正的碗本體（按下確認後才套色；若碗是場景中動態生成的，改用 RegisterActualBowl() 註冊，這裡留空即可）")]
    public Renderer[] actualRenderers;

    [Header("確認選色後觸發（推進劇情用，這時碗本體還沒變色）")]
    public UnityEvent onColorConfirmed;

    [Header("顏色真正套到碗本體後觸發")]
    public UnityEvent onColorAppliedToBowl;

    public int SelectedIndex { get; private set; } = -1;
    public bool IsConfirmed { get; private set; }
    public bool IsAppliedToBowl { get; private set; }

    private Material[][] _previewMaterialInstances;
    private Material[][] _actualMaterialInstances;

    private void Start()
    {
        _previewMaterialInstances = CacheMaterials(previewRenderers);
        _actualMaterialInstances = CacheMaterials(actualRenderers);
    }

    // Renderer.materials 存取時會自動幫每個 Renderer 建立獨立的材質實例，
    // 改色才不會牽動到其他共用同一份 sharedMaterial 的物件
    private static Material[][] CacheMaterials(Renderer[] renderers)
    {
        int count = renderers != null ? renderers.Length : 0;
        var result = new Material[count][];

        for (int i = 0; i < count; i++)
        {
            var rend = renderers[i];
            if (rend == null) continue;

            result[i] = rend.materials;
        }

        return result;
    }

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
        _actualMaterialInstances = CacheMaterials(actualRenderers);

        if (IsAppliedToBowl && SelectedIndex >= 0)
        {
            ApplyColorToTargets(_actualMaterialInstances, paletteColors[SelectedIndex]);
        }
    }

    // 把這個方法掛到「確認」按鈕的 On Click()：只鎖定顏色、存起來，不會馬上套到真正的碗上。
    public void ConfirmSelection()
    {
        if (IsConfirmed) return;
        if (SelectedIndex < 0) return; // 還沒選色不能確認

        photonView.RPC(nameof(RpcConfirm), RpcTarget.All);
    }

    // 真正把已鎖定的顏色套到碗本體，在你需要的時機（例如碗被放進窯爐、或劇情推進到某個
    // 特定步驟）從別的地方呼叫這個。一定要先 ConfirmSelection() 過才有效。
    public void ApplyColorToBowl()
    {
        if (!IsConfirmed) return; // 還沒確認顏色，不能套用
        if (IsAppliedToBowl) return; // 已經套過了
        if (SelectedIndex < 0) return;

        photonView.RPC(nameof(RpcApplyToBowl), RpcTarget.All);
    }

    [PunRPC]
    private void RpcApplyColor(int colorIndex)
    {
        SelectedIndex = colorIndex;
        Color color = paletteColors[colorIndex];
        ApplyColorToTargets(_previewMaterialInstances, color);
        ApplyColorToImages(color);
    }

    [PunRPC]
    private void RpcConfirm()
    {
        IsConfirmed = true;
        onColorConfirmed?.Invoke();
    }

    [PunRPC]
    private void RpcApplyToBowl()
    {
        IsAppliedToBowl = true;
        ApplyColorToTargets(_actualMaterialInstances, paletteColors[SelectedIndex]);
        onColorAppliedToBowl?.Invoke();
    }

    private static void ApplyColorToTargets(Material[][] materialInstances, Color color)
    {
        if (materialInstances == null) return;

        for (int i = 0; i < materialInstances.Length; i++)
        {
            if (materialInstances[i] == null) continue;

            foreach (Material mat in materialInstances[i])
            {
                if (mat.HasProperty("_Color"))
                {
                    mat.SetColor("_Color", color);
                }
                else if (mat.HasProperty("_BaseColor"))
                {
                    mat.SetColor("_BaseColor", color);
                }
            }
        }
    }

    private void ApplyColorToImages(Color color)
    {
        if (previewImages == null) return;

        foreach (Image img in previewImages)
        {
            if (img == null) continue;
            img.color = color;
        }
    }
}
