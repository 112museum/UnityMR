using UnityEngine;

public class CanvasController : MonoBehaviour
{
    public GameObject canvas; // 你的 Canvas（World Space）

    // Panel 關閉時呼叫
    public void OnPanelClosed()
    {
        canvas.SetActive(true);

        // 如果需要讓 Canvas 面向相機
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            canvas.transform.LookAt(mainCam.transform);
            canvas.transform.Rotate(0, 180f, 0); // 調整朝向
        }
    }
}