using UnityEngine;

public class ZoneTriggerScript : MonoBehaviour
{
    public LineRenderer lineRenderer; // Reference to the LineRenderer

    void OnTriggerEnter(Collider other)
    {
        // Check if the collider belongs to the main camera
        if (other.CompareTag("MainCamera"))
        {
            // Disable the LineRenderer when the camera enters the zone
            lineRenderer.enabled = false;
        }
    }

    // void OnTriggerExit(Collider other)
    // {
    //     // Check if the collider belongs to the main camera
    //     if (other.CompareTag("MainCamera"))
    //     {
    //         // Enable the LineRenderer when the camera exits the zone
    //         lineRenderer.enabled = true;
    //     }
    // }
}
