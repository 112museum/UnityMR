using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class FloatingPickupItem : MonoBehaviour
{
    Rigidbody rb;
    XRGrabInteractable grab;
    Renderer[] renderers;

    [Header("漂浮設定")]
    public float floatSpeed = 1.5f;
    public float floatHeight = 0.04f; // 稍微調小一點點，漂浮感會更細緻

    Vector3 startPos;
    bool isFloating = true;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
        renderers = GetComponentsInChildren<Renderer>();

        // 初始化物理：徹底禁用重力
        rb.useGravity = false;
        rb.isKinematic = true;

        startPos = transform.position;

        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);

        HideItem(); // 依照你需求決定是否一開始隱藏
    }

    void Update()
    {
        if (!isFloating) return;

        // 使用 Mathf.Sin 進行上下平滑漂浮
        float newY = startPos.y + Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        
        // Kinematic 狀態下直接對 transform 或 rb.MovePosition 操作
        transform.position = new Vector3(startPos.x, newY, startPos.z);
    }

    // 當玩家抓取時
    void OnGrab(SelectEnterEventArgs args)
    {
        isFloating = false;
        
        // 抓取時關閉 Kinematic，這樣物件在手上撞到東西才會有物理反應
        rb.isKinematic = false;
        rb.useGravity = false; // 確保抓取時也不受重力影響
    }

    // 當玩家放手時
    void OnRelease(SelectExitEventArgs args)
    {
        // 1. 更新漂浮基準點為「放手的位置」
        startPos = transform.position;

        // 2. 停止物理物理慣性，防止放手後噴掉
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // 3. 恢復為 Kinematic 模式，交給 Update 的漂浮邏輯接管
        rb.isKinematic = true;

        // 4. 恢復漂浮狀態
        isFloating = true;
    }

    // --- 劇情系統呼叫的對外接口 ---

    public void StopFloating()
    {
        isFloating = false;
    }

    public void LockForAnimation()
    {
        // 先歸零物理速度（這時 isKinematic 還是 false）
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // 再設定為 Kinematic 並關閉漂浮
        rb.isKinematic = true;
        rb.useGravity = false;
        isFloating = false;

        // 禁止抓取
        if (grab != null) grab.enabled = false;
    }

    public void UnlockFromAnimation()
    {
        grab.enabled = true;
        // 移除下面這兩行，不要在這裡強制恢復漂浮
        // startPos = transform.position;
        // isFloating = true; 
    }

    public void ShowItem()
    {
        foreach (Renderer r in renderers) r.enabled = true;
    }

    public void HideItem()
    {
        foreach (Renderer r in renderers) r.enabled = false;
    }
}