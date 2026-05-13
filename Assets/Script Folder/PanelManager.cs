using UnityEngine;

public class PanelManager : MonoBehaviour
{
    public GameObject nextPanel;

    private void OnDisable()
    {
        // When this panel is disabled, activate the next panel
        if (nextPanel != null)
        {
            nextPanel.SetActive(true);
        }
    }
}
