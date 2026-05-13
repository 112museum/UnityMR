using UnityEngine;

public class ToggleTextToogle : MonoBehaviour
{
    public GameObject cameraText;

    public void ToggleText()
    {
        cameraText.SetActive(!cameraText.activeSelf);
    }
}
