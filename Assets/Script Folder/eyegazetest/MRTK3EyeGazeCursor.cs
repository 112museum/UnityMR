using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

public class MRTK3EyeGazeCursor : MonoBehaviour
{
    [Header("Cursor References")]
    public Transform cursorDot;        // 小圓點
    public Transform progressRing;     // dwell 進度圈

    [Header("Cursor Settings")]
    public float maxDistance = 2f;
    public float smoothSpeed = 20f;
    public float dwellTime = 2f;

    [Header("Eye Tracking Recording")]
    public bool recordGazeData = true;
    public Vector3 lastGazeOrigin;
    public Vector3 lastGazeDirection;
    public GameObject lastHitObject;

    private float dwellTimer = 0f;
    private GameObject currentTarget;

    void Start()
    {
        if (progressRing != null)
            progressRing.gameObject.SetActive(false);
    }

    void Update()
    {
        Vector3 origin;
        Vector3 direction;

        // HoloLens 2 Eye Gaze (MRTK3)
        // Editor fallback 使用 Camera.forward
#if UNITY_EDITOR
        origin = Camera.main.transform.position;
        direction = Camera.main.transform.forward;
#else
        // HoloLens 2 眼球追蹤 Ray
        var eyeGaze = OpenXRSettings.Instance?.GetFeature<EyeGazeInteraction>();
        if (eyeGaze != null && eyeGaze.enabled)
        {
            // 透過 HoloLens 2 XR SDK 取得 gaze
            var xrGaze = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.CenterEye);
            if (xrGaze.TryGetFeatureValue(UnityEngine.XR.CommonUsages.eyesData, out UnityEngine.XR.Eyes eyes))
            {
                if (eyes.TryGetFixationPoint(out Vector3 fixationPoint))
                {
                    origin = Camera.main.transform.position;
                    direction = (fixationPoint - origin).normalized;
                }
                else
                {
                    origin = Camera.main.transform.position;
                    direction = Camera.main.transform.forward;
                }
            }
            else
            {
                origin = Camera.main.transform.position;
                direction = Camera.main.transform.forward;
            }
        }
        else
        {
            origin = Camera.main.transform.position;
            direction = Camera.main.transform.forward;
        }
#endif

        // 紀錄眼球資料
        if (recordGazeData)
        {
            lastGazeOrigin = origin;
            lastGazeDirection = direction;
        }

        UpdateCursor(origin, direction);
    }

    private void UpdateCursor(Vector3 origin, Vector3 direction)
    {
        // 固定距離放置 cursor (始終跟視線)
        float cursorDistance = 2f; 
        Vector3 targetPos = origin + direction * cursorDistance;

        // 平滑跟隨視線
        if (cursorDot != null)
            cursorDot.position = Vector3.Lerp(cursorDot.position, targetPos, Time.deltaTime * smoothSpeed);

        cursorDot?.LookAt(Camera.main.transform);

        // Raycast 只用於 hover/dwell 判斷
        RaycastHit hit;
        GameObject targetObject = null;
        if (Physics.Raycast(origin, direction, out hit, maxDistance))
            targetObject = hit.collider.gameObject;

        HandleDwell(targetObject); // 控制 progressRing / dwell 顏色
    }

    private void HandleDwell(GameObject target)
    {
        if (target != null)
        {
            if (target == currentTarget)
            {
                dwellTimer += Time.deltaTime;

                if (progressRing != null)
                {
                    progressRing.gameObject.SetActive(true);
                    float scale = 1 + dwellTimer / dwellTime;
                    progressRing.localScale = Vector3.one * scale * 0.02f;
                }

                if (dwellTimer >= dwellTime)
                {
                    TriggerEvent(target);
                    dwellTimer = 0f;
                }
            }
            else
            {
                currentTarget = target;
                dwellTimer = 0f;
            }
        }
        else
        {
            currentTarget = null;
            dwellTimer = 0f;

            if (progressRing != null)
                progressRing.gameObject.SetActive(false);
        }
    }

    private void TriggerEvent(GameObject obj)
    {
        Debug.Log("Gaze Triggered: " + obj.name);

        var interact = obj.GetComponent<GazeInteractable>();
        if (interact != null)
            interact.OnGazeTriggered();
    }
}