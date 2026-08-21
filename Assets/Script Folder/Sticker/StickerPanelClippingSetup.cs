using Microsoft.MixedReality.GraphicsTools;
using UnityEngine;

// 掛在面板固定不動的背板／視窗框上（跟 ClippingBox 同一個物件；ClippingBox 的範圍要設成
// 面板可視窗口大小，Box 本身不能跟著 Content 一起滑動，否則裁切範圍會跟著跑）。
// Content 底下每個貼紙格子的 Renderer 開機時自動註冊給 ClippingBox，不用每加一個貼紙圖樣
// 就手動把它拖進 ClippingBox 的 Renderers 清單。
//
// ClippingBox 是逐 frame 拿 Renderer 的世界座標跟 Box 的範圍比對，跟這個 Renderer「目前實際
// 在哪裡」無關——貼紙一旦被玩家拉出面板去貼碗，位置一定會離開 Box 範圍，如果 Renderer 還留著
// 沒解除註冊，就會被裁到整顆消失。所以貼紙離開面板變成實際尺寸的當下要 RemoveRenderer，
// 放開彈回面板時要再 AddRenderer 加回去（對應 StickerDraggable 的 LiftedOffPanel / ReturnedToPanel）。
//
// 注意：ClippingBox 是靠材質裡的 shader 做裁切，貼紙材質要換成支援 _CLIPPING_BOX 的
// shader（例如 Graphics Tools/Standard），Unity 內建 shader 不會被裁切。
[RequireComponent(typeof(ClippingBox))]
public class StickerPanelClippingSetup : MonoBehaviour
{
    [SerializeField] private Transform content;

    private ClippingBox _clippingBox;

    private void Start()
    {
        _clippingBox = GetComponent<ClippingBox>();

        foreach (Transform slot in content)
        {
            foreach (Renderer slotRenderer in slot.GetComponentsInChildren<Renderer>())
            {
                _clippingBox.AddRenderer(slotRenderer);
            }

            if (slot.TryGetComponent(out StickerDraggable draggable))
            {
                draggable.LiftedOffPanel += HandleLiftedOffPanel;
                draggable.ReturnedToPanel += HandleReturnedToPanel;
            }
        }
    }

    private void HandleLiftedOffPanel(StickerDraggable sticker)
    {
        foreach (Renderer r in sticker.GetComponentsInChildren<Renderer>())
        {
            _clippingBox.RemoveRenderer(r);
        }
    }

    private void HandleReturnedToPanel(StickerDraggable sticker)
    {
        foreach (Renderer r in sticker.GetComponentsInChildren<Renderer>())
        {
            _clippingBox.AddRenderer(r);
        }
    }
}
