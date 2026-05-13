using UnityEngine;

public class ReturnToOriginal : MonoBehaviour
{
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private float collisionTime = 0f;
    private bool isColliding = false;
    private float requiredCollisionTime = 3f;

    void Start()
    {
        // Store the original position and rotation
        originalPosition = transform.position;
        originalRotation = transform.rotation;
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
        transform.position = originalPosition;
        transform.rotation = originalRotation;
    }
}
