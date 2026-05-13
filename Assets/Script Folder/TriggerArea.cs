using UnityEngine;
using System.Collections;

public class TriggerArea : MonoBehaviour
{
    // Adjust this to the tag of your player's camera or headset
    public string playerTag = "Player";
    // Assign the GameObject you want to activate/deactivate in the Inspector
    public GameObject objectToActivate;
    public float activateDuration = 5f;
    private Coroutine deactivationCoroutine;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            Debug.Log("Player entered the area.");
            if (objectToActivate != null)
            {
                objectToActivate.SetActive(true);
                if (deactivationCoroutine != null)
                {
                    StopCoroutine(deactivationCoroutine);
                }
                deactivationCoroutine = StartCoroutine(DeactivateAfterTime(activateDuration));
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            Debug.Log("Player exited the area.");
            if (objectToActivate != null)
            {
                if (deactivationCoroutine != null)
                {
                    StopCoroutine(deactivationCoroutine);
                }
                deactivationCoroutine = StartCoroutine(DeactivateAfterTime(activateDuration));
            }
        }
    }

    private IEnumerator DeactivateAfterTime(float time)
    {
        yield return new WaitForSeconds(time);
        objectToActivate.SetActive(false);
        deactivationCoroutine = null;
    }
}
