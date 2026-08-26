using Photon.Pun;
using UnityEngine;

// 掛在場景固定物件上（該物件需要有 PhotonView 元件，場景內固定擺放即可，不需要
// PhotonNetwork.Instantiate），本體或子物件要有一個 IsTrigger 的 Collider 當偵測範圍。
// 指定的物件（用 requiredTag 篩選，留空代表任何物件都算）一碰到這個 Trigger，
// 就透過 RPC 廣播給房間所有人，兩個玩家會同時呼叫到 StoryModeManager.OnPlayerInteractSuccess()，
// 不會有一人卡在原本進度、另一人已經推進的狀況（同樣的理由跟 StickerStepConfirm.cs 一致：
// 只在本機處理的話，另一位玩家不會同步）。
public class TriggerAdvanceStory : MonoBehaviourPun
{
    [Header("要偵測觸發的物件 Tag，留空即不限制")]
    [SerializeField] private string requiredTag = "";

    [Header("StoryModeManager")]
    [SerializeField] private StoryModeManager storyModeManager;

    private bool _hasTriggered;

    private void OnTriggerEnter(Collider other)
    {
        if (_hasTriggered) return;
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return;

        photonView.RPC(nameof(RpcNotifySuccess), RpcTarget.All);
    }

    [PunRPC]
    private void RpcNotifySuccess()
    {
        if (_hasTriggered) return; // 只推進一次，避免重複進出 Trigger 多呼叫幾次
        _hasTriggered = true;

        storyModeManager?.OnPlayerInteractSuccess();
    }
}
