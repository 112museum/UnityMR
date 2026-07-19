using Photon.Pun;
using UnityEngine;

// 掛在 Assets/Resources/Sticker/StickerPlaced.prefab 上（該 prefab 需要有 PhotonView 元件）。
// 由 StickerDraggable 呼叫 PhotonNetwork.Instantiate 動態生成，生成當下透過
// PhotonView.InstantiationData 帶入顏色，讓房間裡所有玩家都看到同一顆貼紙、同一個顏色、
// 貼在碗的同一個位置。
public class StickerPlacedNetworked : MonoBehaviourPun, IPunInstantiateMagicCallback
{
    [SerializeField] private Renderer stickerRenderer;

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        object[] data = photonView.InstantiationData;
        if (data == null || data.Length < 3 || stickerRenderer == null) return;

        var color = new Color((float)data[0], (float)data[1], (float)data[2]);
        stickerRenderer.material.color = color;
    }
}
