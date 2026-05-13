using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class TouchReaction : MonoBehaviour
{
    [Header("偵測對象設定")]
    [Tooltip("勾選後：偵測標籤為 Player 的物件 (用於階段 5、7)\n不勾選：偵測名為 '酒壺' 或 '展架' 的物件")]
    public bool detectPlayer = true;

    [Header("階段事件設定")]
    [Tooltip("請根據掛載物件的階段選擇對應事件")]
    public EventType reactionEvent;

    /* * 階段標註說明：
     * ------------------------------------------------------------
     * 【階段 4】酒瓶入碗：選 PutBottleIntoBowlSuccess (detectPlayer 設為 false)
     * 【階段 5】靠近欣賞：選 GazeBowlCloseSuccess      (detectPlayer 設為 true)
     * 【階段 7】體驗結束：選 PutBowlBackSuccess       (detectPlayer 設為 false，偵測「展架」)
     * ------------------------------------------------------------
     */

    private void OnTriggerEnter(Collider other)
    {
        bool isTarget = false;

        if (detectPlayer)
        {
            // 模式 A：偵測玩家 (標籤為 Player)
            isTarget = other.CompareTag("Player");
        }
        else
        {
            // 模式 B：偵測特定物件
            // 偵測酒瓶 (名稱為 酒壺) 或 偵測放回位置 (名稱為 展架)
            if (other.name == "酒壺" || other.name == "碗放桌上觸發")
            {
                isTarget = true;
            }
        }

        if (isTarget)
        {
            Debug.Log($"<color=cyan>【觸發成功】</color> {gameObject.name} 偵測到 {other.name}，執行事件：{reactionEvent}");

            if (StoryManager.Instance != null)
            {
                StoryManager.Instance.Notify(reactionEvent);
            }
            else if (VirtualStoryManager.Instance != null)
            {
                VirtualStoryManager.Instance.Notify(reactionEvent);
            }
            else if (PhysicalStoryManager.Instance != null)
            {
                PhysicalStoryManager.Instance.Notify(reactionEvent);
            }
            else
            {
                Debug.LogError("找不到 StoryManager！請確認場景中有 StorySystem。");
            }
        }
    }
    // 當物件被抓取時，XR Grab Interactable 會呼叫這個方法
    public void OnGrabbed()
    {
        Debug.Log($"<color=cyan>【抓取成功】</color> 執行事件：{reactionEvent}");
        
        if (StoryManager.Instance != null)
            StoryManager.Instance.Notify(reactionEvent);
        else if (VirtualStoryManager.Instance != null)
            VirtualStoryManager.Instance.Notify(reactionEvent);
        else if (PhysicalStoryManager.Instance != null)
            PhysicalStoryManager.Instance.Notify(reactionEvent);
    }
    
}
