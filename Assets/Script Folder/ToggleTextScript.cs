using UnityEngine;

public class ToggleTextScript : MonoBehaviour
{
    public GameObject Text;

    public void ToggleText()
    {
        Text.SetActive(!Text.activeSelf);
    }
}
