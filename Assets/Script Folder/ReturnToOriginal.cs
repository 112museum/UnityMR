using MRTK.Tutorials.MultiUserCapabilities;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ReturnToOriginal : MonoBehaviour
{
    // Stored relative to TableAnchor, not raw world space: TableAnchor itself gets
    // moved later by QR-based alignment (QRAnchorAligner), after this object has
    // already settled at its authored spot. A cached world-space snapshot would go
    // stale the moment the anchor moves, sending the object back to where the table
    // used to be instead of where it is now.
    private Vector3 originalAnchorLocalPosition;
    private Quaternion originalAnchorLocalRotation;
    private float collisionTime = 0f;
    private bool isColliding = false;
    private float requiredCollisionTime = 3f;

    [SerializeField] private float maxDistanceFromOriginal = 30f;

    private Rigidbody _rigidbody;
    private XRGrabInteractable _grab;

    void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _grab = GetComponent<XRGrabInteractable>();

        var anchor = TableAnchor.Instance;
        if (anchor != null)
        {
            originalAnchorLocalPosition = anchor.transform.InverseTransformPoint(transform.position);
            originalAnchorLocalRotation = Quaternion.Inverse(anchor.transform.rotation) * transform.rotation;
        }
        else
        {
            originalAnchorLocalPosition = transform.position;
            originalAnchorLocalRotation = transform.rotation;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "Floor") // Replace with the tag of the other GameObject
        {
            isColliding = true;
            collisionTime = 0f;
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.tag == "Floor")
        {
            isColliding = false;
            collisionTime = 0f;
        }
    }

    void Update()
    {
        // 碗正被抓著的時候絕對不能動它的 Rigidbody：XRGrabInteractable 用自己的方式（VelocityTracking）
        // 每個 FixedUpdate 都在追蹤手的位置、並且在放開時把 useGravity/isKinematic 復原成抓取前記錄
        // 的值。如果這裡在還握著的時候硬改 position/rotation/velocity，等於在它的追蹤邏輯背後
        // 亂動 Rigidbody，會讓它抓取前後記錄的物理狀態跟著錯亂，導致放開後 useGravity/isKinematic
        // 跟抓之前不一樣。連碰撞計時、離原位太遠都要一起跳過，並把計時歸零，避免放開的瞬間馬上觸發。
        if (_grab != null && _grab.isSelected)
        {
            isColliding = false;
            collisionTime = 0f;
            return;
        }

        if (isColliding)
        {
            collisionTime += Time.deltaTime;
            if (collisionTime >= requiredCollisionTime)
            {
                ReturnToOriginalPosition();
                isColliding = false;
                collisionTime = 0f;
                return;
            }
        }

        // 不管有沒有在碰撞計時，只要離原本擺放的位置太遠（例如被人拿走亂丟），就直接拉回去。
        if (Vector3.Distance(transform.position, GetOriginalWorldPosition()) > maxDistanceFromOriginal)
        {
            ReturnToOriginalPosition();
            isColliding = false;
            collisionTime = 0f;
        }
    }

    // 換算成目前世界座標下的原始位置：跟 ReturnToOriginalPosition() 共用同一套邏輯，
    // 一樣要用 TableAnchor 目前的位置換算，才不會在 anchor 被 QR 校正移動之後算錯。
    Vector3 GetOriginalWorldPosition()
    {
        var anchor = TableAnchor.Instance;
        return anchor != null
            ? anchor.transform.TransformPoint(originalAnchorLocalPosition)
            : originalAnchorLocalPosition;
    }

    void ReturnToOriginalPosition()
    {
        var anchor = TableAnchor.Instance;
        Vector3 targetPosition = GetOriginalWorldPosition();
        Quaternion targetRotation = anchor != null
            ? anchor.transform.rotation * originalAnchorLocalRotation
            : originalAnchorLocalRotation;

        if (_rigidbody != null)
        {
            // 碗是完整物理模擬的 Rigidbody（非 kinematic、有重力），被丟飛/掉落時身上還帶著速度。
            // 只改 transform.position 不會動到這個速度，下一個 FixedUpdate 物理引擎照樣拿舊的
            // velocity 繼續算，碗回到定位的瞬間又會沿著原本的方向飛出去。改用 Rigidbody.position/
            // rotation 做瞬間移動（避免被physics引擎當成一幀內的超高速位移），並把殘留的線速度、
            // 角速度歸零，碗才會真的停在原地。
            _rigidbody.position = targetPosition;
            _rigidbody.rotation = targetRotation;
            _rigidbody.velocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }
        else
        {
            transform.position = targetPosition;
            transform.rotation = targetRotation;
        }
    }
}
