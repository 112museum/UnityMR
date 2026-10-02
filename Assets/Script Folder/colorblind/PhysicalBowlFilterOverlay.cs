using MixedReality.Toolkit.SpatialManipulation;
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
// 另外兩個方便現場對位的功能（都在 Start() 做，不用動 prefab 本身）：
// 3. snapToAnchorCenter：溫碗 prefab 裡的模型本身帶了 (-0.79, 1.6, 0.74) 的位移（原本是為了擺在
//    展桌上），直接拿來疊會讓碗出現在離 QR 兩公尺外的地方。這裡用 Renderer 的實際外框把
//    「碗底中心」移到父物件（BowlAnchor = QR 中心）的原點上，跟模型 pivot 在哪無關。
// 4. grabbable：自動加 BoxCollider + MRTK3 ObjectManipulator，讓玩家/工作人員可以直接用手
//    把虛擬碗捏住拖到真碗上 —— QR 掃不到時的保險。
//
// 不直接關掉整個 GameObject（用 SetActive），是因為這個物件自己的 Start() 要能正常執行
// 才有機會完成第 1 點的註冊；如果一開始就 inactive，Start() 根本不會被呼叫。
public class PhysicalBowlFilterOverlay : MonoBehaviour
{
    [Tooltip("疊放模型上的 Renderer，留空的話會自動抓這個物件（含子物件）底下所有 Renderer。")]
    [SerializeField] private Renderer[] overlayRenderers;

    [Tooltip("勾起來的話不管濾鏡有沒有開，碗都一直顯示（測試對位用）。正式流程要取消勾選。")]
    [SerializeField] private bool alwaysVisible = false;

    [Tooltip("Start() 時把模型的碗底中心移到父物件原點（QR 中心）。")]
    [SerializeField] private bool snapToAnchorCenter = true;

    [Tooltip("自動加 Collider + ObjectManipulator，讓虛擬碗可以用手捏住拖曳對位。")]
    [SerializeField] private bool grabbable = true;

    private Collider grabCollider;

    private void Start()
    {
        if (overlayRenderers == null || overlayRenderers.Length == 0)
        {
            overlayRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        }

        // 這兩步都要在 SetVisible 關掉 Renderer 之前做，關掉的 Renderer 抓不到正確外框。
        if (snapToAnchorCenter) SnapBottomCenterToParent();
        if (grabbable) MakeGrabbable();

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

    private bool TryGetWorldBounds(out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (var rend in overlayRenderers)
        {
            if (rend == null) continue;
            if (!found) { bounds = rend.bounds; found = true; }
            else bounds.Encapsulate(rend.bounds);
        }
        return found;
    }

    private void SnapBottomCenterToParent()
    {
        if (transform.parent == null || !TryGetWorldBounds(out var b)) return;

        var bottomCenter = new Vector3(b.center.x, b.min.y, b.center.z);
        transform.position += transform.parent.position - bottomCenter;
    }

    private void MakeGrabbable()
    {
        if (!TryGetWorldBounds(out var b)) return;

        // Collider 一定要比 ObjectManipulator 先加：XR Interactable 在自己的 Awake 收集 Collider。
        var box = gameObject.AddComponent<BoxCollider>();
        box.center = transform.InverseTransformPoint(b.center);
        var localSize = transform.InverseTransformVector(b.size);
        box.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
        grabCollider = box;

        if (GetComponent<ObjectManipulator>() == null)
        {
            gameObject.AddComponent<ObjectManipulator>();
        }
    }

    private void SetVisible(bool on)
    {
        on |= alwaysVisible;
        if (grabCollider != null) grabCollider.enabled = on;
        foreach (var rend in overlayRenderers)
        {
            if (rend != null) rend.enabled = on;
        }
    }
}
