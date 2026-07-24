using UnityEngine;

// 掛在 Sticker_Test 場景的 RoomLinker 物件上，場景一開始就自動連線加入房間，
// 做法跟 FireSeedTestStoryManager.Start() 一樣，方便直接 Play 測試不用手動按按鈕。
public class StickerTestBootstrap : MonoBehaviour
{
    [SerializeField] private RoomLinker roomLinker;

    private void Start()
    {
        roomLinker.JoinRoom();
    }
}
