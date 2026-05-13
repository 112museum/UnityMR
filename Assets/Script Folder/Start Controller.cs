using UnityEngine;
using UnityEngine.UI;

public class PanelController : MonoBehaviour
{
    public GameObject startPage;

    public void HideStartButton()
    {
        startPage.gameObject.SetActive(false);
    }
}
