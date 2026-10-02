using UnityEngine;

// 把「疊在真實溫碗上的 3D 校色模型」接進現有的色弱濾鏡系統。用法：拿現成的
// 青瓷蓮花式溫碗.prefab 生一份複製，拖到掛了 BowlAnchorAligner 的 "BowlAnchor"
// 物件底下當子物件（確認真碗跟虛擬展桌不是同一張桌子後，改用獨立的 QR +
// BowlAnchorAligner 定位，不再是 TableAnchorAsParent/TableAnchor），再掛這支
// 腳本，並手動調整這個物件的 local position/rotation/scale，讓疊放的模型跟
// 真碗的實際位置、大小對齊（現場試調）。
//
// 這支腳本本身做兩件事：
// 1. Start() 時把自己的 Renderer 加進 ColorBlindFilterToggle.targetRenderers，讓濾鏡開啟
//    時這個疊放模型也套用一樣的紅/綠/藍倍率（跟其他虛擬物件同一套邏輯，見
//    ColorBlindFilterToggle.GetMultipliers()）。
// 2. 訂閱 ColorBlindFilterToggle.FilterStateChanged，濾鏡沒開的時候把 Renderer 關掉——
//    沒有色弱、沒掃過測驗 QR 的玩家看到的應該只有真碗本身，不該多一個疊在上面的分身。
//
// 不直接關掉整個 GameObject（用 SetActive），是因為這個物件自己的 Start() 要能正常執行
// 才有機會完成第 1 點的註冊；如果一開始就 inactive，Start() 根本不會被呼叫。
public class PhysicalBowlFilterOverlay : MonoBehaviour
{
    [Tooltip("疊放模型上的 Renderer，留空的話會自動抓這個物件（含子物件）底下所有 Renderer。")]
    [SerializeField] private Renderer[] overlayRenderers;

    private void Start()
    {
        if (overlayRenderers == null || overlayRenderers.Length == 0)
        {
            overlayRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        }

        if (ColorBlindFilterToggle.Instance == null)
        {
            Debug.LogError("[PhysicalBowlFilterOverlay] 場景裡找不到 ColorBlindFilterToggle，無法把疊放模型接進濾鏡系統。");
            return;
        }

        ColorBlindFilterToggle.Instance.RegisterTargetRenderers(overlayRenderers);
        ColorBlindFilterToggle.Instance.FilterStateChanged += SetVisible;

        // 剛註冊時，濾鏡目前的開關狀態可能早於這支腳本執行完成（例如碗的定位 QR 比色覺
        // 測驗 QR 晚掃到），所以要用現在的狀態校正一次可見度，不能假設一律從關閉開始。
        SetVisible(ColorBlindFilterToggle.Instance.IsFilterOn);
    }

    private void OnDestroy()
    {
        if (ColorBlindFilterToggle.Instance != null)
        {
            ColorBlindFilterToggle.Instance.FilterStateChanged -= SetVisible;
        }
    }

    private void SetVisible(bool on)
    {
        foreach (var rend in overlayRenderers)
        {
            if (rend != null) rend.enabled = on;
        }
    }
}
