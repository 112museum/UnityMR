using UnityEngine;
using UnityEngine.Events;
public class TalkSpace : MonoBehaviour
{
    public GameObject npc;
    private Animator npcAnimator;
    public UnityEvent OnEnter;

    private void Start()
    {
        // Get the Animator component of the NPC
        npcAnimator = npc.GetComponent<Animator>();
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if the entering collider is the camera (player)
        if (other.CompareTag("Player"))
        {
            // Trigger the animation
            OnEnter.Invoke();
        }
    }
}