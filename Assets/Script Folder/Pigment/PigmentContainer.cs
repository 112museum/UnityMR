using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

// 掛在調釉容器上（玩家要把顏料丟進去的那個容器，不是碗），該物件需要有 PhotonView 元件
// （場景內固定擺放即可，不需要 PhotonNetwork.Instantiate）。容器內部要放一個 IsTrigger 的
// Collider（可以是本體或子物件，只要子物件的 Collider 事件會傳上來即可）當作偵測範圍；
// 丟進來的顏料物件需要掛 PigmentItem 並填好 pigmentId，另外要有 Rigidbody 觸發事件才會發生
// （放在顏料物件或容器任一邊都可以，Unity 規則是至少一邊要有 Rigidbody）。
//
// 前一步驟玩家會在 GlazeColorPalette 的調色面板從 8 種色票選一個顏色，而這 8 種顏色各自是由
// 顏料排列組成（例如兩種顏料各丟1個，或同一種顏料丟2個）。所以這裡「需要放什麼顏料」不是
// 寫死的，而是依照 colorRecipes 裡對應 GlazeColorPalette.SelectedIndex 那筆配方，在玩家確認
// 選色的當下（GlazeColorPalette.onColorConfirmed）動態算出來。
//
// 做法比照 GlazeColorPalette / StickerStepConfirm：偵測到顏料丟進來後不直接在本機改資料，
// 而是透過 RPC 廣播給房間所有人，兩人才會同時看到面板數字變化、以及全部放完後的變色與
// 面板隱藏——只在本機處理的話，另一位玩家畫面不會更新，兩人會卡在不同進度。
public class PigmentContainer : MonoBehaviourPun
{
    [System.Serializable]
    public class PigmentAmount
    {
        [Tooltip("要跟丟進來的 PigmentItem.pigmentId 完全一致")]
        public string pigmentId;

        [Tooltip("這個顏色需要這種顏料幾個（同一種顏料出現兩次的顏色，count 就填2）")]
        public int count = 1;
    }

    [System.Serializable]
    public class ColorPigmentRecipe
    {
        [Tooltip("對應 GlazeColorPalette.paletteColors / SelectedIndex 的色票索引（從0開始）")]
        public int colorIndex;

        [Tooltip("這個顏色由哪些顏料排列組成")]
        public List<PigmentAmount> pigments = new List<PigmentAmount>();
    }

    [System.Serializable]
    public class PigmentCatalogEntry
    {
        public string pigmentId;

        [Tooltip("面板上顯示的名稱，例如「紅色」")]
        public string displayName;
    }

    private class PigmentRequirement
    {
        public string pigmentId;
        public string displayName;
        public int remainingCount;
    }

    [Header("已選色的調色盤（8 色），也是完成後套色的來源")]
    [SerializeField] private GlazeColorPalette glazeColorPalette;

    [Header("8 種色票各自需要的顏料配方，colorIndex 要對應 glazeColorPalette 的色票索引")]
    [SerializeField] private List<ColorPigmentRecipe> colorRecipes = new List<ColorPigmentRecipe>();

    [Header("顏料代號 -> 面板顯示名稱 對照表")]
    [SerializeField] private List<PigmentCatalogEntry> pigmentCatalog = new List<PigmentCatalogEntry>();

    [Header("調色面板")]
    [SerializeField] private GameObject SwitchColorCanvas;

    [Header("面板上顯示需求的文字（Canva2_addPigment 底下的 Text (TMP)）")]
    [SerializeField] private TMP_Text requirementText;

    [Header("文字最前面固定的提示字，後面接每種顏料還缺幾個")]
    [SerializeField] private string panelPrefix = "需要顏料：";

    [Header("放完顏料後要變色的目標物件（不是碗本體）")]
    [SerializeField] private Renderer[] targetRenderers;

    [Header("放完所有顏料後觸發（推進劇情用，掛 StoryModeManager.OnPlayerInteractSuccess）")]
    public UnityEvent onAllPigmentsAdded;

    // 這次選色算出來的需求清單，玩家確認選色時（見 SetupRequirements）才會重新產生。
    private readonly List<PigmentRequirement> requirements = new List<PigmentRequirement>();

    // 記錄「哪些顏料丟進來過」的完整清單，不管是不是容器當下還缺的種類都會記到，
    // 供之後如果要檢查玩家操作紀錄、除錯用。
    private readonly List<string> collectedPigmentIds = new List<string>();

    private bool _isComplete;

    private void Start()
    {
        // 保底用：萬一 GlazeColorPalette.onColorConfirmed 沒接到這裡（例如忘記在 Inspector 連事件），
        // 場景一開始如果選色已經確認過，還是能算出正確的需求清單，不會整個容器沒有需求可以放。
        SetupRequirements();
    }

    // 把這個方法掛到 GlazeColorPalette 的 onColorConfirmed（Inspector 連事件），
    // 玩家一確認選色，就依照 SelectedIndex 對應的配方重新計算這裡要放的顏料與數量。
    public void SetupRequirements()
    {
        requirements.Clear();

        if (glazeColorPalette == null || !glazeColorPalette.IsConfirmed) return;

        ColorPigmentRecipe recipe = colorRecipes.Find(r => r.colorIndex == glazeColorPalette.SelectedIndex);
        if (recipe == null)
        {
            Debug.LogWarning($"[PigmentContainer] 找不到色票索引 {glazeColorPalette.SelectedIndex} 對應的顏料配方");
            RefreshRequirementText();
            return;
        }

        foreach (PigmentAmount amount in recipe.pigments)
        {
            requirements.Add(new PigmentRequirement
            {
                pigmentId = amount.pigmentId,
                displayName = GetDisplayName(amount.pigmentId),
                remainingCount = amount.count
            });
        }

        RefreshRequirementText();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isComplete) return;

        PigmentItem pigment = other.GetComponentInParent<PigmentItem>();
        if (pigment == null || string.IsNullOrEmpty(pigment.pigmentId)) return;

        photonView.RPC(nameof(RpcAddPigment), RpcTarget.All, pigment.pigmentId);
    }

    [PunRPC]
    private void RpcAddPigment(string pigmentId)
    {
        if (_isComplete) return;

        collectedPigmentIds.Add(pigmentId); // 記錄什麼顏料進去了

        PigmentRequirement requirement = requirements.Find(r => r.pigmentId == pigmentId);
        if (requirement != null && requirement.remainingCount > 0)
        {
            requirement.remainingCount--; // 面板要求的顏料數字扣1
        }

        RefreshRequirementText();

        if (AllRequirementsMet())
        {
            CompleteContainer();
        }
    }

    private bool AllRequirementsMet()
    {
        if (requirements.Count == 0) return false; // 還沒算出需求清單時不能算完成

        foreach (PigmentRequirement requirement in requirements)
        {
            if (requirement.remainingCount > 0) return false;
        }
        return true;
    }

    private void CompleteContainer()
    {
        _isComplete = true;

        if (SwitchColorCanvas != null) SwitchColorCanvas.SetActive(false); // 面板隱藏

        ApplyColorToTargetRenderers(); // 在目標物件自己原本的材質上改色（不是套碗專用材質）

        onAllPigmentsAdded?.Invoke(); // 轉到下一幕（掛 StoryModeManager.OnPlayerInteractSuccess）
    }

    // 保留 targetRenderers 原本的材質/貼圖/shader，只把顏色改成玩家已確認的那個顏色
    // （跟碗同一份 Color 值，但不套用碗專用的 glazeGrayscaleMaterial）。
    private void ApplyColorToTargetRenderers()
    {
        if (glazeColorPalette == null || targetRenderers == null) return;

        Color? color = glazeColorPalette.ConfirmedColor;
        if (color == null) return;

        foreach (Renderer rend in targetRenderers)
        {
            if (rend == null) continue;

            Material mat = rend.material; // 第一次存取時 Unity 會自動生成這顆 renderer 專屬的材質實例
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color.Value);
            else if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color.Value);
        }
    }

    private string GetDisplayName(string pigmentId)
    {
        PigmentCatalogEntry entry = pigmentCatalog.Find(e => e.pigmentId == pigmentId);
        return entry != null ? entry.displayName : pigmentId;
    }

    // 把目前每種顏料還缺幾個組成面板文字，重新整理一次 TMP 顯示內容
    private void RefreshRequirementText()
    {
        if (requirementText == null) return;

        string text = panelPrefix;
        foreach (PigmentRequirement requirement in requirements)
        {
            text += $"\n{requirement.displayName} x{Mathf.Max(requirement.remainingCount, 0)}";
        }
        requirementText.text = text;
    }
}
