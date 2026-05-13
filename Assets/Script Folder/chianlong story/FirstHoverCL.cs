using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class FirstHoverCL : MonoBehaviour
{
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
    private bool hasTriggered = false;
    public Talker talker;
    public string dialogueCategory; // 你想播放的對話類別
    public string dialogueType; // 你想播放的對話檔名
    public Animator animator;
    public string animation;

    void Awake()
    {
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
    }

    void OnEnable()
    {
        grabInteractable.firstHoverEntered.AddListener(OnFirstHover);
    }

    void OnDisable()
    {
        grabInteractable.firstHoverEntered.RemoveListener(OnFirstHover);
    }

    private void OnFirstHover(HoverEnterEventArgs args)
    {
        if (hasTriggered) return;

        hasTriggered = true;
        Debug.Log("第一次看人物，播放開場白");

        // 呼叫 Talker 的 Speak()
        if (talker != null)
        {
            talker.Speak(dialogueCategory, dialogueType);
        }
        else
        {
            Debug.LogWarning("Talker 尚未指定!");
        }
        // 觸發動畫
        if (animator != null)
        {
            animator.SetTrigger(animation);
        }
        else
        {
            Debug.LogWarning("Animator 尚未指定!");
        }
    }
}