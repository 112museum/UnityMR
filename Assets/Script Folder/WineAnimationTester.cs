using UnityEngine;

public class WineAnimationTester : MonoBehaviour
{
    [Header("物件")]
    // public Transform bowl;
    public Transform bottle;
    public Transform handAnchor;
    public Transform bowlTarget;
    public Transform drinkTarget;

    [Header("設定")]
    public float flySpeed = 3f;
    public Vector3 bottleBowlOffset = new Vector3(0, 0.05f, 0);
    public Vector3 bottleHandOffset = Vector3.zero;

    enum BottleState
    {
        Idle,
        MovingToBowl,
        InBowl,
        MovingToHand
    }

    BottleState state = BottleState.Idle;

    Rigidbody bottleRb;
    FloatingPickupItem pickup;

    void Start()
    {
        bottleRb = bottle.GetComponent<Rigidbody>();
        pickup = bottle.GetComponent<FloatingPickupItem>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.B)) TriggerBottleToBowl();
        if (Input.GetKeyDown(KeyCode.N)) TriggerBottleToHand();

        switch (state)
        {
            case BottleState.MovingToBowl:
                MoveTo(bottle, bowlTarget, bottleBowlOffset, OnReachBowl);
                break;

            case BottleState.MovingToHand:
                MoveTo(bottle, drinkTarget, bottleHandOffset, OnReachHand);
                break;
        }
    }

    // =====================
    // 碗
    // =====================
    public void TriggerBottleToBowl()
    {
        if (state != BottleState.Idle && state != BottleState.InBowl) return;

        LockBottle();

        // ⭐加這行（超關鍵）
        if (pickup != null)
            pickup.StopFloating();

        state = BottleState.MovingToBowl;
    }

    void OnReachBowl()
    {
        state = BottleState.InBowl;

        bottle.SetParent(bowlTarget);
        bottle.localPosition = bottleBowlOffset;
        // bottle.localRotation = Quaternion.identity;
        bottle.localRotation = Quaternion.Euler(-90, 0, 0);
    }

    // =====================
    // 手
    // =====================
    public void TriggerBottleToHand()
    {
        if (state != BottleState.InBowl) return;

        bottle.SetParent(null);
        LockBottle();

        // ⭐再保險一次
        if (pickup != null)
            pickup.StopFloating();

        state = BottleState.MovingToHand;
    }

    void OnReachHand()
    {
        state = BottleState.Idle;

        bottle.SetParent(drinkTarget);
        bottle.localPosition = bottleHandOffset;
        bottle.localRotation = Quaternion.identity;

        UnlockBottle();
    }

    // =====================
    // 移動
    // =====================
    void MoveTo(Transform obj, Transform target, Vector3 offset, System.Action onComplete)
    {
        Vector3 targetPos = target.TransformPoint(offset);

        obj.position = Vector3.Lerp(obj.position, targetPos, flySpeed * Time.deltaTime);
        obj.rotation = Quaternion.Slerp(obj.rotation, target.rotation, flySpeed * Time.deltaTime);

        if (Vector3.Distance(obj.position, targetPos) < 0.02f)
        {
            obj.position = targetPos;
            obj.rotation = target.rotation;
            onComplete?.Invoke();
        }
    }

    // =====================
    // 鎖定 / 解鎖
    // =====================
// 在 WineAnimationTester.cs 中修改 LockBottle 和 UnlockBottle
    void LockBottle()
    {
        // 在 LockBottle 時執行
        bottleRb.WakeUp();
        if (pickup != null) pickup.LockForAnimation();
        
        // 取得所有 Collider (包含子物件的)
        Collider[] cols = bottle.GetComponentsInChildren<Collider>();
        foreach (var col in cols)
        {
            // 移動時關閉物理碰撞，但如果需要持續偵測，可以留著 Trigger 那個
            // 但為了穩定，建議移動時全部關閉，等抵達 OnReachBowl 再處理
            col.enabled = false; 
        }
    }

    void UnlockBottle()
    {
        if (pickup != null) pickup.UnlockFromAnimation();

        Collider[] cols = bottle.GetComponentsInChildren<Collider>();
        foreach (var col in cols)
        {
            col.enabled = true;
        }
    }
}