using UnityEngine;

// 掛在 FireSeed_P1 / FireSeed_P2 上，讓底下的 Procedural fire（Fire 子物件）
// 隨著窯爐燒製進度從小長到大；PhotonKilnBurningManager.BurnProgress 本來就已經
// 透過 OnPhotonSerializeView 同步給房間裡所有人，這裡不需要額外處理網路同步。
// 中途放開、進度衰退時，火也會跟著等比例縮小，跟進度條反向連動一致。
public class FireGrowth : MonoBehaviour
{
    [SerializeField] private PhotonKilnBurningManager kilnManager;
    [SerializeField] private Transform fireVisual;
    [SerializeField] private float minScale = 0.2f; // 燒製進度 0 時的大小
    [SerializeField] private float maxScale = 1f;   // 燒製進度 1 時的大小

    private void Update()
    {
        if (kilnManager == null || fireVisual == null) return;

        float scale = Mathf.Lerp(minScale, maxScale, kilnManager.BurnProgress);
        fireVisual.localScale = Vector3.one * scale;
    }
}
