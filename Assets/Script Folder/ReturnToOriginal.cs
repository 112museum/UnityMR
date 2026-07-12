using MRTK.Tutorials.MultiUserCapabilities;
using UnityEngine;

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

    void Start()
    {
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
        if (isColliding)
        {
            collisionTime += Time.deltaTime;
            if (collisionTime >= requiredCollisionTime)
            {
                ReturnToOriginalPosition();
                isColliding = false;
                collisionTime = 0f;
            }
        }
    }

    void ReturnToOriginalPosition()
    {
        var anchor = TableAnchor.Instance;
        if (anchor != null)
        {
            transform.position = anchor.transform.TransformPoint(originalAnchorLocalPosition);
            transform.rotation = anchor.transform.rotation * originalAnchorLocalRotation;
        }
        else
        {
            transform.position = originalAnchorLocalPosition;
            transform.rotation = originalAnchorLocalRotation;
        }
    }
}
