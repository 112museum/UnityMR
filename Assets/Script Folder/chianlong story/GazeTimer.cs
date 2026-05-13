using UnityEngine;
using UnityEngine.UI; // 如果要用 Image 做進度條可加此行


public class GazeTimer : MonoBehaviour
{
    public float requiredTime = 5.0f;
    private float timer = 0f;
    private bool isGazing = false;
    public Transform cameraTransform;

    [Header("測試模式")]
    public bool useMouseHoverForTesting = true; // 👈 臨時測試用

    void Update()
    {
        if (isGazing)
        {
            timer += Time.deltaTime;

            if (timer >= requiredTime) {
                // 階段 3 成功：進入酒瓶階段
                if (StoryManager.Instance != null) StoryManager.Instance.Notify(EventType.LookBowlSuccess);
                if (PhysicalStoryManager.Instance != null) PhysicalStoryManager.Instance.Notify(EventType.LookBowlSuccess);
                timer = 0;
                isGazing = false;
                Debug.Log("[GazeTimer] 凝視完成");
            }
        }
    }

    // 這些方法會被 Camera 上的 Raycaster 呼叫
    public void StartGaze()
    {
        isGazing = true;
        Debug.Log("[GazeTimer] 開始凝視溫碗");
    }

    public void StopGaze()
    {
        isGazing = false;
        timer = 0;
    }

    // ===== 臨時測試用：用滑鼠 Hover 觸發 =====
    void OnMouseEnter()
    {
        if (useMouseHoverForTesting)
        {
            StartGaze();
        }
    }

    void OnMouseExit()
    {
        if (useMouseHoverForTesting)
        {
            StopGaze();
        }
    }
}