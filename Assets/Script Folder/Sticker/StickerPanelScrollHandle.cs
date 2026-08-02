using UnityEngine;

// 掛在獨立於 Content 之外的 ScrollHandle 物件上（跟 ObjectManipulator + ConstraintManager +
// MoveAxisConstraint 同一個物件；MoveAxisConstraint 鎖 Y/Z，只留本地 X 軸能滑）。
//
// ScrollHandle 不是 Content 的 child，位置永遠固定在面板前緣同一個地方，不會因為 Content
// 捲動就跟著跑走。Handle 自己只在一小段固定範圍（handleMinLocalX ~ handleMaxLocalX）裡滑動，
// 這支腳本把 Handle 目前滑到的比例，對應套用到 Content 實際的捲動範圍
// （contentMinLocalX ~ contentMaxLocalX，貼紙越多這段要設越長）上——貼紙數量增加只影響
// Content 要捲的距離，玩家伸手抓的位置永遠不變。
//
// Content 本身不用再掛 ObjectManipulator / Collider（改由這支腳本直接設定 Content.localPosition），
// 也就不會再有 Content 的 Collider 誤收 Slot 的 Collider、或跑出桌子外撞到碗的問題。
public class StickerPanelScrollHandle : MonoBehaviour
{
    [SerializeField] private Transform content;

    [Header("Handle 自己的可滑動範圍（固定不變，不受貼紙數量影響）")]
    [SerializeField] private float handleMinLocalX = -0.05f;
    [SerializeField] private float handleMaxLocalX = 0.05f;

    [Header("Content 實際要捲動的範圍（貼紙越多這裡要設越大）")]
    [SerializeField] private float contentMinLocalX = -0.3f;
    [SerializeField] private float contentMaxLocalX = 0f;

    private void LateUpdate()
    {
        Vector3 handlePosition = transform.localPosition;
        float clampedHandleX = Mathf.Clamp(handlePosition.x, handleMinLocalX, handleMaxLocalX);
        if (!Mathf.Approximately(clampedHandleX, handlePosition.x))
        {
            transform.localPosition = new Vector3(clampedHandleX, handlePosition.y, handlePosition.z);
        }

        float scrollRatio = Mathf.InverseLerp(handleMinLocalX, handleMaxLocalX, clampedHandleX);
        float contentX = Mathf.Lerp(contentMinLocalX, contentMaxLocalX, scrollRatio);

        Vector3 contentPosition = content.localPosition;
        content.localPosition = new Vector3(contentX, contentPosition.y, contentPosition.z);
    }
}
