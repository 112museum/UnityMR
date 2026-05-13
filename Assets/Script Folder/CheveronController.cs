using UnityEngine;

public class ChevronController : MonoBehaviour
{
    public GameObject chevron;

    public void ToggleChevron()
    {
        chevron.SetActive(!chevron.activeSelf);
    }
}
