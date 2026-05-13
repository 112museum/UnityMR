using UnityEngine;
using MixedReality.Toolkit.SpatialManipulation; // 必須引用 MRTK 命名空間

public class GrabDistanceChecker : MonoBehaviour
{
    public Transform playerCamera; // 拖入 Main Camera

    // 這個方法會在被抓取時由 Event 呼叫
    public void CheckDistanceOnGrab()
    {
        float distance = Vector3.Distance(playerCamera.position, transform.position);
        
        if (distance > 1f) // 如果超過 1 公尺
        {
            // 階段 2 失敗：站太遠了！
            Debug.Log("太遠了！距離為: " + distance);
            if (StoryManager.Instance != null) StoryManager.Instance.Notify(EventType.PutBowlFailed);
            if (PhysicalStoryManager.Instance != null) PhysicalStoryManager.Instance.Notify(EventType.PutBowlFailed);
            } else {
            // 階段 2 成功：太棒了！你找到了！
            if (StoryManager.Instance != null) StoryManager.Instance.Notify(EventType.PutBowlSuccess);
            if (PhysicalStoryManager.Instance != null) PhysicalStoryManager.Instance.Notify(EventType.PutBowlSuccess);
        }
    }
    public void GazeBowlCloseSuccess()
    {
        if (StoryManager.Instance != null) StoryManager.Instance.Notify(EventType.GazeBowlCloseSuccess);
        if (PhysicalStoryManager.Instance != null) PhysicalStoryManager.Instance.Notify(EventType.GazeBowlCloseSuccess);
    }
}
