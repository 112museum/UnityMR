using UnityEngine;
using UnityEngine.AI;

public class PathGuideScript : MonoBehaviour
{
    public Transform user; // The user's current position (Main Camera)
    public Transform destination; // The destination position
    private LineRenderer lineRenderer;
    private NavMeshPath navMeshPath;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        navMeshPath = new NavMeshPath();
        lineRenderer.enabled = false; // Initially disable the LineRenderer
    }

    public void OnGuideButtonClick()
    {
        // Enable the LineRenderer and update the path when the button is clicked
        lineRenderer.enabled = true;
        UpdatePath();
    }

    void Update()
    {
        if (lineRenderer.enabled)
        {
            // Update the path continuously while the LineRenderer is enabled
            UpdatePath();
        }
    }

    void UpdatePath()
    {
        if (user != null && destination != null)
        {
            // Calculate the path
            Vector3 userPosition = new Vector3(user.position.x, user.position.y, user.position.z);
            NavMesh.CalculatePath(userPosition, destination.position, NavMesh.AllAreas, navMeshPath);

            // Set the points to the LineRenderer
            lineRenderer.positionCount = navMeshPath.corners.Length;
            lineRenderer.SetPositions(navMeshPath.corners);
        }
    }
}
