using UnityEngine;

public class ProximityTrigger : MonoBehaviour
{
    public GameObject uiPrompt;

    // --- 1. 在這裡新增開關變數 ---
    private bool hasTriggered = false;

    void Start()
    {
        if (uiPrompt != null) uiPrompt.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        // --- 2. 判斷是否為玩家且尚未觸發 ---
        if (other.CompareTag("Player") && !hasTriggered)
        {
            hasTriggered = true; // --- 3. 立即鎖定，確保後續重複進入無效 ---

            Debug.Log("【觸發成功】執行開場劇情與 UI 顯示");

            // 通知 StoryManager
            if (StoryManager.Instance != null)
                StoryManager.Instance.Notify(EventType.EnterStoryZone);
            if (PhysicalStoryManager.Instance != null)
                PhysicalStoryManager.Instance.Notify(EventType.EnterStoryZone);

            ShowPrompt();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // 如果你希望玩家離開後，下次進來還能觸發，就取消註解下面這行：
            // hasTriggered = false; 

            if (StoryManager.Instance != null)
                StoryManager.Instance.Notify(EventType.ExitStoryZone);
            if (PhysicalStoryManager.Instance != null)
                PhysicalStoryManager.Instance.Notify(EventType.ExitStoryZone);
        }
    }

    public void ShowPrompt()
    {
        if (uiPrompt != null)
            uiPrompt.SetActive(true);
    }

    public void HidePrompt()
    {
        if (uiPrompt != null)
            uiPrompt.SetActive(false);
    }
}