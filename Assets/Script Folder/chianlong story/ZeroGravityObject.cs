using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables; // 確保有引用 XRI 命名空間

public class ZeroGravityObject : MonoBehaviour
{
    public enum ObjectState { Floating_Grabable, Floating_Locked, AttachedToNPC, AttachedToTable }
    public ObjectState currentState = ObjectState.Floating_Grabable;

    [Header("漂浮設定")]
    public float floatAmplitude = 0.2f; 
    public float floatFrequency = 0.5f; 
    
    [Header("物理阻尼 (防止放手後亂飛)")]
    public float linearDrag = 5f;       
    public float angularDrag = 5f;

    [Header("吸附動畫設定")]
    public float lerpSpeed = 4f; // 數值越高，吸往 NPC 手上的速度越快

    private Rigidbody rb;
    private Vector3 startPos;
    public Transform targetHand; 
    public Transform targetTable; 
    private XRGrabInteractable grabInteractable;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabInteractable = GetComponent<XRGrabInteractable>();
        
        // 初始化物理設定
        rb.useGravity = false;
        startPos = transform.position;
    }

    void Start()
    {
        // 呼叫一次初始化狀態
        SwitchToState(currentState);
    }

    void Update()
    {
        switch (currentState)
        {
            case ObjectState.Floating_Grabable:
                bool isGrabbed = CheckIfGrabbed();

                if (!isGrabbed)
                {
                    // 沒被抓時，執行上下漂浮動畫
                    SimulateFloating();
                }
                else
                {
                    // 被抓著移動時，隨時更新 startPos，確保放手後在那裡漂浮
                    startPos = transform.position;
                }
                break;

            case ObjectState.Floating_Locked:
                SimulateFloating();
                break;

            case ObjectState.AttachedToNPC:
                FollowHand();
                break;

            case ObjectState.AttachedToTable:
                PutOnTable();
                break;
        }
    }

    // 判斷是否正被玩家選取/抓取
    bool CheckIfGrabbed()
    {
        if (grabInteractable != null)
        {
            return grabInteractable.isSelected;
        }
        return false;
    }

    void SimulateFloating()
    {
        // 使用正弦波計算偏移，加上 startPos 確保基準點正確
        float offset = Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
        
        // 注意：這裡使用 MovePosition 而不是直接改 transform，物理會更穩定
        Vector3 nextPos = new Vector3(transform.position.x, startPos.y + offset, transform.position.z);
        rb.MovePosition(nextPos);
    }

    // 更新後的功能：具備平滑吸附動畫，且保持直立
    // 放在乾隆手上
    void FollowHand()
    {
        if (targetHand != null)
        {
            // 移動座標至 NPC 手部
            transform.position = Vector3.Lerp(transform.position, targetHand.position, Time.deltaTime * lerpSpeed);
            
            // 旋轉修正：目標旋轉設為 Quaternion.identity (即 (0,0,0) 無旋轉狀態，保持直立)
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.identity, Time.deltaTime * lerpSpeed);
        }
    }
    // 放在展架上
    void PutOnTable()
    {
        if (targetTable != null)
        {
            // 移動座標至 NPC 手部
            transform.position = Vector3.Lerp(transform.position, targetTable.position, Time.deltaTime * lerpSpeed);
            
            // 旋轉修正：目標旋轉設為 Quaternion.identity (即 (0,0,0) 無旋轉狀態，保持直立)
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.identity, Time.deltaTime * lerpSpeed);
        }
    }

    // --- 外部切換狀態的接口 ---
    public void SwitchToState(ObjectState newState, Transform item = null)
    {
        currentState = newState;
        
        // 每次切換狀態時重新獲取 startPos
        startPos = transform.position;

        switch (newState)
        {
            case ObjectState.Floating_Grabable:
                rb.isKinematic = false; 
                rb.drag = linearDrag;
                rb.angularDrag = angularDrag;
                if (grabInteractable != null) grabInteractable.enabled = true;
                break;

            case ObjectState.Floating_Locked:
                rb.isKinematic = true; 
                if (grabInteractable != null) grabInteractable.enabled = false;
                break;

            case ObjectState.AttachedToNPC:
                rb.isKinematic = true;
                if (grabInteractable != null) grabInteractable.enabled = false;
                targetHand = item;
                break;

            case ObjectState.AttachedToTable:
                rb.isKinematic = true;
                if (grabInteractable != null) grabInteractable.enabled = false;
                targetTable = item;
                break;
        }
    }
}