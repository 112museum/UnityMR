using UnityEngine;

public class GazeInteractable : MonoBehaviour
{
    public Color triggeredColor = Color.green;
    private Color originalColor;
    private Renderer rend;

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (rend != null)
            originalColor = rend.material.color;
    }

    public void OnGazeTriggered()
    {
        if (rend != null)
        {
            rend.material.color = triggeredColor;
            Invoke(nameof(ResetColor), 1f);
        }
        Debug.Log("GazeInteractable triggered: " + gameObject.name);
    }

    private void ResetColor()
    {
        if (rend != null)
            rend.material.color = originalColor;
    }
}