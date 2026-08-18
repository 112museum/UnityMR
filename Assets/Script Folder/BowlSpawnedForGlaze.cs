using UnityEngine;

// 掛在動態生成的碗 prefab 上（第二幕「瑪瑙入釉」用 PhotonNetwork.Instantiate 生成的碗）。
// Start() 在每個 client 自己那份 instance 生成完之後都會各自執行一次（跟同專案
// BowlSpawnedForStickers.cs 用 Start() 做「生成後接掛」的做法一致），把自己的 Renderer
// 註冊進場景中固定的 GlazeColorPalette，調色盤才知道「真正的碗」要改哪些 Renderer；
// 同時也註冊進 ColorBlindFilterToggle，這樣碗也會是色弱濾鏡的套用對象（碗是動態生成的，
// 沒辦法像場景裡固定的物件一樣直接在 Inspector 把 Renderer 拖進 targetRenderers）。
public class BowlSpawnedForGlaze : MonoBehaviour
{
    private void Start()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        var palette = FindFirstObjectByType<GlazeColorPalette>();
        palette?.RegisterActualBowl(renderers);

        ColorBlindFilterToggle.Instance?.RegisterTargetRenderers(renderers);
    }
}
