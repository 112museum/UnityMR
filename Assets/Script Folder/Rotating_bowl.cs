using UnityEngine;



public class Rotating_bowl : MonoBehaviour

{

    public Vector3 totalRotation = new Vector3(0, 360, 0); // 設定總共要轉幾度

    public float duration = 3.0f; // 設定要在幾秒內轉完



    private bool isAnimating = false;

    private float elapsedTime = 0;

    private Quaternion startRotation;

    private Quaternion endRotation;



    // 你可以從其他程式呼叫這個 Function 來啟動旋轉

    [ContextMenu("Start Rotation")] // 這行能讓你點擊 Inspector 右上角三點來測試

    public void StartRotating()

    {

        if (isAnimating) return;



        startRotation = transform.localRotation;

        endRotation = startRotation * Quaternion.Euler(totalRotation);

        elapsedTime = 0;

        isAnimating = true;

    }



    void Update()

    {

        Debug.Log("程式正在執行中！"); // 這行會讓你在下方 Console 看到字

        transform.Rotate(Vector3.up * 20 * Time.deltaTime);



        if (isAnimating)

        {

            elapsedTime += Time.deltaTime;

            float percentage = elapsedTime / duration;



            // 平滑插值旋轉

            transform.localRotation = Quaternion.Slerp(startRotation, endRotation, percentage);



            if (percentage >= 1.0f)

            {

                isAnimating = false; // 轉完就停

            }

        }



        // 測試用：按下 R 鍵轉一次

        if (Input.GetKeyDown(KeyCode.R)) StartRotating();

    }

}