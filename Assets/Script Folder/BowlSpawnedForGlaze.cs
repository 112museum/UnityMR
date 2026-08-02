using UnityEngine;

// 掛在動態生成的碗 prefab 上（第二幕「瑪瑙入釉」用 PhotonNetwork.Instantiate 生成的碗）。
// Start() 在每個 client 自己那份 instance 生成完之後都會各自執行一次（跟同專案
// BowlSpawnedForStickers.cs 用 Start() 做「生成後接掛」的做法一致），把自己的 Renderer
// 註冊進場景中固定的 GlazeColorPalette，調色盤才知道「真正的碗」要改哪些 Renderer。
public class BowlSpawnedForGlaze : MonoBehaviour
{
    private void Start()
    {
        var palette = FindFirstObjectByType<GlazeColorPalette>();
        if (palette == null) return;

        palette.RegisterActualBowl(GetComponentsInChildren<Renderer>());
    }
}
