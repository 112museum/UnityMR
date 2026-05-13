using UnityEngine;

public class BowlFlipper : MonoBehaviour
{
    [Header("翻轉設定")]
    public Vector3 flipRotation = new Vector3(180, 0, 0); // 繞 X 軸翻轉 180 度
    public float duration = 6.1f; // 翻轉秒數，數字越大越慢

    private bool isAnimating = false;
    private float elapsedTime = 0;
    private Quaternion startRotation;
    private Quaternion endRotation;

    void Start()
    {
        // 遊戲一開始就自動啟動翻轉
        StartFlipping();
    }

    [ContextMenu("Start Flip")]
    public void StartFlipping()
    {
        if (isAnimating) return;

        // 紀錄當下角度作為起點
        startRotation = transform.localRotation;
        // 計算翻轉後的目標角度
        endRotation = startRotation * Quaternion.Euler(flipRotation);

        elapsedTime = 0;
        isAnimating = true;
    }

    void Update()
    {
        if (isAnimating)
        {
            elapsedTime += Time.deltaTime;
            float percentage = elapsedTime / duration;

            // 使用 Slerp 球面線性插值，確保旋轉時軸心絕對固定，不會產生位移
            transform.localRotation = Quaternion.Slerp(startRotation, endRotation, percentage);

            // 達到 100% 進度時停止
            if (percentage >= 1.0f)
            {
                transform.localRotation = endRotation; // 精準校正到最終角度
                isAnimating = false;
                Debug.Log("翻轉完成！");
            }
        }

        // 測試用：按下鍵盤 F 鍵可以再次翻轉
        if (Input.GetKeyDown(KeyCode.F)) StartFlipping();
    }
}