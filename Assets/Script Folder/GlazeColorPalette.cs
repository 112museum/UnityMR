using Photon.Pun;
using UnityEngine;
using UnityEngine.Events;

// 第二幕「瑪瑙入釉」調色盤：掛在調色盤 UI 的固定場景物件上，該物件需要有 PhotonView 元件
// （場景內固定擺放即可，不需要 PhotonNetwork.Instantiate）。
// 色票按鈕的 On Click() 綁定 SelectColor(index)：任一位玩家點選色票，都會透過 RPC 廣播給雙方，
// 讓兩人看到同一個正在變化的碗（所見即所得）；targetRenderers 同時放「碗本體」與「面板預覽模型」，
// 兩者就會一起換色。「確認」按鈕綁定 ConfirmSelection()，鎖定顏色並觸發 onColorConfirmed
// （接後續轉場，例如推進 StoryModeManager 到第三幕）。
public class GlazeColorPalette : MonoBehaviourPun
{
    [Header("調色盤色票（依序對應面板上的色票按鈕，SelectColor 用索引指定）")]
    public Color[] paletteColors;

    [Header("要套色的碗（素坯本體＋面板預覽模型都放進來）")]
    public Renderer[] targetRenderers;

    [Header("確認選色後觸發（推進劇情用）")]
    public UnityEvent onColorConfirmed;

    public int SelectedIndex { get; private set; } = -1;
    public bool IsConfirmed { get; private set; }

    private Material[][] _materialInstances;

    private void Start()
    {
        CacheMaterials();
    }

    // Renderer.materials 存取時會自動幫每個 Renderer 建立獨立的材質實例，
    // 改色才不會牽動到其他共用同一份 sharedMaterial 的物件
    private void CacheMaterials()
    {
        int count = targetRenderers != null ? targetRenderers.Length : 0;
        _materialInstances = new Material[count][];

        for (int i = 0; i < count; i++)
        {
            var rend = targetRenderers[i];
            if (rend == null) continue;

            _materialInstances[i] = rend.materials;
        }
    }

    // 把這個方法掛到每個色票按鈕的 On Click()，colorIndex 對應 paletteColors 的索引
    public void SelectColor(int colorIndex)
    {
        if (IsConfirmed) return; // 已確認就不能再改
        if (paletteColors == null || colorIndex < 0 || colorIndex >= paletteColors.Length) return;

        photonView.RPC(nameof(RpcApplyColor), RpcTarget.All, colorIndex);
    }

    // 把這個方法掛到「確認」按鈕的 On Click()
    public void ConfirmSelection()
    {
        if (IsConfirmed) return;
        if (SelectedIndex < 0) return; // 還沒選色不能確認

        photonView.RPC(nameof(RpcConfirm), RpcTarget.All);
    }

    [PunRPC]
    private void RpcApplyColor(int colorIndex)
    {
        SelectedIndex = colorIndex;
        ApplyColorToTargets(paletteColors[colorIndex]);
    }

    [PunRPC]
    private void RpcConfirm()
    {
        IsConfirmed = true;
        onColorConfirmed?.Invoke();
    }

    private void ApplyColorToTargets(Color color)
    {
        if (_materialInstances == null) return;

        for (int i = 0; i < _materialInstances.Length; i++)
        {
            if (_materialInstances[i] == null) continue;

            foreach (Material mat in _materialInstances[i])
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
}
