using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ZoneController : MonoBehaviour
{
    public Image loadingBar;
    public GameObject objectToActivate;
    public float loadingDuration = 3f;
    public float fadeDuration = 1f;
    public string visitorID = "0afa90eb-38c4-47dd-b756-cd6d113b51ec";  // Replace with actual visitor ID
    public string exhibitID = "9ff1f1e8-0b66-4013-9745-6fa28f127f45";  // Replace with actual exhibit ID

    private CanvasGroup canvasGroup;
    private bool isPlayerInside = false;
    private float timeSpentInZone = 0f;
    private ExhibitZoneTime exhibitZoneTime;

    private void Awake()
    {
        if (objectToActivate != null)
        {
            objectToActivate.SetActive(false);
        }
    }

    private void Start()
    {
        loadingBar.fillAmount = 0f;
        canvasGroup = objectToActivate.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = objectToActivate.AddComponent<CanvasGroup>();
        }
        canvasGroup.alpha = 0f;

        exhibitZoneTime = FindObjectOfType<ExhibitZoneTime>();  // Assuming Zonetime is attached to a GameObject in the scene
    }

    private void Update()
    {
        if (isPlayerInside)
        {
            timeSpentInZone += Time.deltaTime;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (!isPlayerInside)
            {
                isPlayerInside = true;
                timeSpentInZone = 0f;
                Debug.Log("Player entered the area. Activating panel from TriggerArea.");
                if (objectToActivate != null)
                {
                    objectToActivate.SetActive(true);
                    StartCoroutine(FillLoadingBar(loadingDuration));
                    StartCoroutine(FadeIn());
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (isPlayerInside)
            {
                isPlayerInside = false;
                Debug.Log("Player exited the area. Deactivating panel from TriggerArea.");
                if (objectToActivate != null)
                {
                    objectToActivate.SetActive(false);
                    StopAllCoroutines();
                    // StartCoroutine(FadeOut());

                    string duration = $"{timeSpentInZone:F2} seconds";
                    StartCoroutine(exhibitZoneTime.PostDuration(visitorID, exhibitID, duration));
                }
            }
        }
    }

    private IEnumerator FillLoadingBar(float duration)
    {
        Debug.Log("Starting to fill loading bar.");
        loadingBar.fillAmount = 0f;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            loadingBar.fillAmount = elapsedTime / duration;
            yield return null;
        }

        Debug.Log("Finished filling loading bar. Deactivating object.");
        loadingBar.fillAmount = 0f;
        objectToActivate.SetActive(false);
    }

    private IEnumerator FadeIn()
    {
        Debug.Log("Starting fade in.");
        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsedTime / fadeDuration);
            yield return null;
        }
    }

    private IEnumerator FadeOut()
    {
        Debug.Log("Starting fade out.");
        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(1f - (elapsedTime / fadeDuration));
            yield return null;
        }

        Debug.Log("Finished fade out. Deactivating object.");
        objectToActivate.SetActive(false);
    }
}
