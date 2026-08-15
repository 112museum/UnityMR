using Photon.Pun;
using UnityEngine;
using UnityEngine.Events;

// 掛在場景固定物件上（該物件需要有 PhotonView 元件，場景內固定擺放即可，不需要
// PhotonNetwork.Instantiate）。做法跟 GlazeColorPalette.ConfirmSelection() 一樣：
// 貼貼紙環節的「貼好了！」按鈕不要直接掛 StoryModeManager.OnPlayerInteractSuccess——
// 那樣只會推進按下按鈕那個人自己的裝置，另一位玩家不會同步，兩人就會卡在不同進度
// （這正是調色環節原本踩到的那個 bug）。改成呼叫這裡的 ConfirmDone()，透過 RPC
// 廣播給房間所有人，兩人才會一起推進到下一段劇情。
public class StickerStepConfirm : MonoBehaviourPun
{
    [Header("貼紙面板")]
    [SerializeField] private GameObject stickerPanel;

    [Header("任一玩家按下「貼好了」後、兩人都會觸發一次，掛 StoryModeManager.OnPlayerInteractSuccess")]
    public UnityEvent onConfirmed;

    // 把這個方法掛到「貼好了」按鈕的 On Click()，取代原本直接掛的 OnPlayerInteractSuccess
    // 跟 GameObject.SetActive（那兩條都只會在本機生效，改成都走這裡的 RPC 才會兩人同步）。
    public void ConfirmDone()
    {
        photonView.RPC(nameof(RpcConfirmDone), RpcTarget.All);
    }

    [PunRPC]
    private void RpcConfirmDone()
    {
        if (stickerPanel != null) stickerPanel.SetActive(false);
        onConfirmed?.Invoke();
    }
}
