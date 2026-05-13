// using UnityEngine;
// using UnityEngine.XR.Interaction.Toolkit;
// using UnityEngine.XR.Interaction.Toolkit.Interactables;

// [RequireComponent(typeof(Animator))]
// public class BowlFlyIn : MonoBehaviour
// {
//     [Header("物件指派")]
//     public Transform bowl;         
//     public Transform handAnchor;  
//     private Animator characterAnim; 
//     private Rigidbody bowlRigidbody; 

//     [Header("飛行設定")]
//     public float flySpeed = 2f;
//     public float delayTime = 0.5f; 

//     [Header("漂浮設定")]
//     public float floatAmplitude = 0.05f; 
//     public float floatFrequency = 1f;    
//     private Vector3 floatBasePosition;   

//     private bool isSequenceStarted = false;
//     private bool isAttached = false;
//     private bool isFloating = false;     
//     private float timer = 0f;
//     private Quaternion fixedRotation = Quaternion.Euler(0, 0, 0);
//     XRGrabInteractable grab;

//     void Start()
//     {
//         characterAnim = GetComponent<Animator>();
//         if (bowl != null)
//         {
//             bowlRigidbody = bowl.GetComponent<Rigidbody>();
//             grab = bowl.GetComponent<XRGrabInteractable>();
//         }

//         if (grab != null)
//         {
//             grab.selectEntered.AddListener(OnGrab);
//         }
//     }

//     void Update()
//     {
//         // 如果正在被抓取，不執行腳本位移邏輯
//         if (grab != null && grab.isSelected) return;

//         // 1. 飛行中邏輯... (保留你原本的代碼)
//         if (isSequenceStarted && !isAttached && !isFloating)
//         {
//             timer += Time.deltaTime;
//             if (timer >= delayTime)
//             {
//                 bowl.position = Vector3.Lerp(bowl.position, handAnchor.position, flySpeed * Time.deltaTime);
//                 bowl.rotation = Quaternion.Slerp(bowl.rotation, handAnchor.rotation, flySpeed * Time.deltaTime);
//                 if (Vector3.Distance(bowl.position, handAnchor.position) < 0.02f) AttachToHand();
//             }
//         }

//         // 2. 吸附於手上邏輯...
//         if (isAttached)
//         {
//             bowl.position = handAnchor.position;
//             bowl.rotation = fixedRotation;
//         }

//         // 3. 空中漂浮邏輯
//         if (isFloating)
//         {
//             float newY = floatBasePosition.y + Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
//             bowl.position = new Vector3(floatBasePosition.x, newY, floatBasePosition.z);
//             bowl.rotation = fixedRotation;
//         }
//     }

//     // ==========================================
//     // 新增功能 1：使碗漂浮在空中且玩家不可以拿 (其他腳本呼叫)
//     // ==========================================
//     public void EnableFloatingLocked()
//     {
//         isFloating = true;
//         isAttached = false;
//         isSequenceStarted = false;
//         floatBasePosition = bowl.position;

//         // 鎖定物理與抓取
//         if (bowlRigidbody != null) bowlRigidbody.isKinematic = true;
//         if (grab != null) grab.enabled = false; // 禁用抓取功能

//         Debug.Log("碗進入「鎖定漂浮」模式：玩家無法抓取");
//     }

//     // ==========================================
//     // 功能 2：解除吸附並開始漂浮 (你原本的邏輯，但加上可抓取開關)
//     // ==========================================
//     public void DetachAndFloat()
//     {
//         isAttached = false;
//         isFloating = true;
//         isSequenceStarted = false;
//         bowl.SetParent(null); 
//         floatBasePosition = bowl.position; 

//         if (bowlRigidbody != null) bowlRigidbody.isKinematic = true;
//         if (grab != null) grab.enabled = true; // 確保漂浮時玩家可以去拿

//         Debug.Log("碗進入「自由漂浮」模式：玩家可以抓取");
//     }

//     // ==========================================
//     // 關鍵回調：玩家拿了之後的處理
//     // ==========================================
//     void OnGrab(SelectEnterEventArgs args)
//     {
//         // 1. 停止所有腳本位移（包含漂浮）
//         isFloating = false;
//         isAttached = false;
//         isSequenceStarted = false;
//         bowl.SetParent(null);

//         // 2. 滿足你的需求：取消重力，但關閉 Kinematic 讓手感正常，且會停在空中
//         if (bowlRigidbody != null)
//         {
//             bowlRigidbody.isKinematic = false; // 關閉 Kinematic 手感才不會死板
//             bowlRigidbody.useGravity = false;  // 關閉重力，放手後會停滯在空中
//         }

//         Debug.Log("玩家抓取：取消漂浮，關閉重力 (放手後將停滯)");
//     }

//     // --- 其他原有方法 ---
//     public void StartBowlSequence()
//     {
//         if (isSequenceStarted) return;
//         isSequenceStarted = true;
//         isAttached = false;
//         isFloating = false; 
//         timer = 0f;
//         if (bowlRigidbody != null) bowlRigidbody.isKinematic = true; 
//         // if (characterAnim != null) characterAnim.SetTrigger("16-17");
//     }

//     void AttachToHand()
//     {
//         isAttached = true;
//         isFloating = false;
//         bowl.SetParent(handAnchor);
//     }
// }

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class BowlFlyIn : MonoBehaviour
{
    [Header("物件指派")]
    public Transform bowl;
    public Transform handAnchor;
    private Rigidbody bowlRigidbody;
    private XRGrabInteractable grab;

    [Header("設定")]
    public float flySpeed = 2f;
    public float floatAmplitude = 0.05f;
    public float floatFrequency = 1f;
    
    private Vector3 floatBasePosition;
    private Quaternion fixedRotation;
    private bool isSequenceStarted = false; 
    private bool isAttached = false;        
    private bool isFloating = false;        

    void Awake() // 建議在 Awake 抓取組件
    {
        if (bowl != null)
        {
            bowlRigidbody = bowl.GetComponent<Rigidbody>();
            grab = bowl.GetComponent<XRGrabInteractable>();

            if (grab != null)
            {
                grab.selectEntered.AddListener(OnGrab);
                grab.selectExited.AddListener(OnRelease);
            }
        }
    }

    void Update()
    {
        // 核心邏輯：如果碗正被抓著，腳本直接「下班」，不准動 Transform
        if (grab != null && grab.isSelected)
        {
            return; 
        }

        // 1. 飛向 NPC 手心
        if (isSequenceStarted && !isAttached)
        {
            bowl.position = Vector3.Lerp(bowl.position, handAnchor.position, flySpeed * Time.deltaTime);
            bowl.rotation = Quaternion.Slerp(bowl.rotation, handAnchor.rotation, flySpeed * Time.deltaTime);

            if (Vector3.Distance(bowl.position, handAnchor.position) < 0.01f)
            {
                AttachToHand();
            }
        }
        // 2. 自由漂浮狀態
        else if (isFloating && !isAttached)
        {
            float newY = floatBasePosition.y + Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
            bowl.position = new Vector3(floatBasePosition.x, newY, floatBasePosition.z);
            bowl.rotation = fixedRotation;
        }
    }

    void LateUpdate()
    {
        // 3. 強制黏在 NPC 手上（無視動畫偏移）
        if (isAttached && (grab == null || !grab.isSelected))
        {
            bowl.position = handAnchor.position;
            // 使用你之前調好的角度補償
            bowl.rotation = handAnchor.rotation * Quaternion.Euler(72, 156, 109);
        }
    }

    public void StartBowlSequence()
    {
        isSequenceStarted = true;
        isAttached = false;
        isFloating = false;
        if (grab != null) grab.enabled = false; // 飛行中禁止抓取
        bowl.SetParent(null); 
    }

    private void AttachToHand()
    {
        isSequenceStarted = false;
        isAttached = true;
    }

    public void DetachAndFloat()
    {
        isAttached = false;
        isSequenceStarted = false;
        isFloating = true;

        floatBasePosition = bowl.position;
        fixedRotation = bowl.rotation;

        if (grab != null) grab.enabled = true; // 進入可抓取狀態
        if (bowlRigidbody != null) bowlRigidbody.isKinematic = true; 
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        isFloating = false;
        isAttached = false;
        // 抓取時物理狀態交給 XR Toolkit 管理
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        // 放手後，以放手的位置為準繼續漂浮
        floatBasePosition = bowl.position;
        fixedRotation = bowl.rotation;
        isFloating = true;
    }
}