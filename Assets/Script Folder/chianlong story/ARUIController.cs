using UnityEngine;

public class ARUIController : MonoBehaviour
{
    [Header("Buttons")]
    public GameObject startButton;        // 階段 0
    public GameObject foundBowlButton;    // 階段 2
    public GameObject endExperienceButton; // 階段 7
    
    void Start()
    {
        // startButton.SetActive(false);
        // foundBowlButton.SetActive(false);
        // endExperienceButton.SetActive(false);
    }
    // 階段 0：開始體驗
    public void OnStartExperience()
    {
        if (ARStoryManager.Instance != null)
        {
            ARStoryManager.Instance.Notify(EventType.EnterStoryZone);
            // 隱藏按鈕物件
            if (startButton != null) startButton.SetActive(false);
        }
        if (VirtualStoryManager.Instance != null)
        {
            VirtualStoryManager.Instance.Notify(EventType.EnterStoryZone);
            // 隱藏按鈕物件
            if (startButton != null) startButton.SetActive(false);
        }
    }

    // 階段 2：找到了
    public void OnFoundBowlClicked()
    {
        if (ARStoryManager.Instance != null)
        {
            ARStoryManager.Instance.Notify(EventType.PutBowlSuccess);
            
            // 成功觸發後隱藏，避免玩家反覆按壓
            if (foundBowlButton != null) foundBowlButton.SetActive(false);
        }
        if (VirtualStoryManager.Instance != null)
        {
            VirtualStoryManager.Instance.Notify(EventType.PutBowlSuccess);
            
            // 成功觸發後隱藏，避免玩家反覆按壓
            if (foundBowlButton != null) foundBowlButton.SetActive(false);
        }
    }

    // 階段 7：結束體驗
    public void OnEndExperienceClicked()
    {
        if (ARStoryManager.Instance != null)
        {
            ARStoryManager.Instance.Notify(EventType.PutBowlBackSuccess);
            
            if (endExperienceButton != null) endExperienceButton.SetActive(false);
        }
        if (VirtualStoryManager.Instance != null)
        {
            VirtualStoryManager.Instance.Notify(EventType.PutBowlBackSuccess);
            
            if (endExperienceButton != null) endExperienceButton.SetActive(false);
        }
    }
    // public void StartButtonActive()
    // {
    //     startButton.SetActive(true);
    // }
    public void FoundBowlButtonActive(bool active)
    {
        foundBowlButton.SetActive(active);
    }
    public void EndExperienceButtonActive(bool active)
    {
        endExperienceButton.SetActive(active);
    }
}