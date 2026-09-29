using Photon.Pun;
using UnityEngine;

// 掛在動態生成的碗 prefab 上（第二幕「瑪瑙入釉」用 PhotonNetwork.Instantiate 生成的碗，
// 本身就需要有 PhotonView 元件才能被 PhotonNetwork.Instantiate，RPC 不用另外加元件）。
// Start() 在每個 client 自己那份 instance 生成完之後都會各自執行一次（跟同專案
// BowlSpawnedForStickers.cs 用 Start() 做「生成後接掛」的做法一致），把自己的 Renderer
// 註冊進場景中固定的 GlazeColorPalette，調色盤才知道「真正的碗」要改哪些 Renderer；
// 同時也註冊進 ColorBlindFilterToggle，這樣碗也會是色弱濾鏡的套用對象（碗是動態生成的，
// 沒辦法像場景裡固定的物件一樣直接在 Inspector 把 Renderer 拖進 targetRenderers）。
//
// 碗碰到 pot1/liquid（用 liquidTag 篩選）時，會先把碗的材質暫時換成跟 liquid 目前一樣的材質，
// 當作「沾到釉料」的預覽效果；但這只是預覽，優先程度比 GlazeColorPalette.ApplyColorToBowl()
// 套的正式顏色低——真正套色之後就不再處理碰撞，也不會被這裡的預覽蓋回去（IsAppliedToBowl
// 兩處都檢查一次：送 RPC 前先擋一次，RPC 真正執行時再擋一次，避免碰撞跟正式套色前後腳發生
// 時漏檢查）。碰撞偵測本身是本機各自的物理事件，所以透過 RPC 廣播讓兩個玩家同時看到同一次
// 沾釉預覽，不會有一人看到碗變色、另一人沒看到的情況。
public class BowlSpawnedForGlaze : MonoBehaviourPun
{
    [Header("釉料液體")]
    [SerializeField] private string liquidTag = "GlazeLiquid";

    private Renderer[] _bowlRenderers;
    private GlazeColorPalette _palette;
    private Renderer _liquidRenderer;

    private void Start()
    {
        _bowlRenderers = GetComponentsInChildren<Renderer>();

        _palette = FindFirstObjectByType<GlazeColorPalette>();
        _palette?.RegisterActualBowl(_bowlRenderers);

        ColorBlindFilterToggle.Instance?.RegisterTargetRenderers(_bowlRenderers);

        // 在 Start() 就各自本機找一次 liquid 的 Renderer 存起來（跟找 GlazeColorPalette 的做法一樣），
        // 不要等 OnTriggerEnter 才抓，因為碰撞事件不保證兩個 client 都會各自觸發一次。
        // 這只是先搶快取，如果失敗（例如雙人連線時，這顆碗的 Start() 在自己這端執行的當下，
        // liquid 物件剛好還沒準備好），RpcDipIntoLiquid 真正要用到的時候還會再找一次。
        CacheLiquidRenderer();
    }

    private void CacheLiquidRenderer()
    {
        if (_liquidRenderer != null) return;

        GameObject liquidObject = GameObject.FindWithTag(liquidTag);
        if (liquidObject != null) _liquidRenderer = liquidObject.GetComponent<Renderer>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_palette != null && _palette.IsAppliedToBowl) return; // 正式顏色已經套上去了，預覽效果不再處理
        if (!other.CompareTag(liquidTag)) return;

        photonView.RPC(nameof(RpcDipIntoLiquid), RpcTarget.All);
    }

    [PunRPC]
    private void RpcDipIntoLiquid()
    {
        if (_palette != null && _palette.IsAppliedToBowl) return; // RPC 送達時可能已經套過正式顏色，再擋一次

        // 保險：Start() 當下如果沒找到 liquid（雙人連線時序問題），這裡碰撞當下已經確定
        // liquid 物件存在了，再找一次補救，避免永遠卡在 null 而不換色。
        CacheLiquidRenderer();
        if (_liquidRenderer == null || _bowlRenderers == null) return;

        foreach (Renderer rend in _bowlRenderers)
        {
            if (rend == null) continue;
            rend.material = new Material(_liquidRenderer.material);
        }
    }
}
