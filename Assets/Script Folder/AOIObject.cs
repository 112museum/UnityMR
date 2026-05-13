using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable))]
[RequireComponent(typeof(Collider))]
public class AOIObject : MonoBehaviour
{
    public string aoiName;

    private float enterTime;
    private float fixationThreshold = 0.2f;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable interactable;

    void Start()
    {
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);

        if (LoggingManager.Instance == null)
            Debug.LogWarning("[AOIObject] LoggingManager.Instance is null");

        Debug.Log($"[AOIObject] '{aoiName}' initialized on {gameObject.name}");
    }

    private void OnDisable()
    {
        if (interactable != null)
        {
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
        }
    }

    private void OnEnable()
    {
        if (interactable != null)
        {
            interactable.hoverEntered.AddListener(OnHoverEntered);
            interactable.hoverExited.AddListener(OnHoverExited);
        }
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        enterTime = Time.time;
        var origin = args.interactorObject.transform.position;
        var direction = args.interactorObject.transform.forward;

        Debug.Log($"[AOIObject] Gaze ENTER '{aoiName}'");
        LogGazeEvent("gaze_enter", 0f, origin, direction);
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        float duration = Time.time - enterTime;
        var origin = args.interactorObject.transform.position;
        var direction = args.interactorObject.transform.forward;

        Debug.Log($"[AOIObject] Gaze EXIT '{aoiName}' | duration={duration:F2}s");
        LogGazeEvent("gaze_exit", duration, origin, direction);

        if (duration >= fixationThreshold)
        {
            Debug.Log($"[AOIObject] FIXATION '{aoiName}' | duration={duration:F2}s");
            LogGazeEvent("fixation", duration, origin, direction);
        }
    }

    private void LogGazeEvent(string eventType, float duration, Vector3 origin, Vector3 direction)
    {
        if (LoggingManager.Instance == null) return;
        LoggingManager.Instance.LogEvent(new AOILogEvent
        {
            timestamp = DateTime.Now,
            event_type = eventType,
            aoi_name = aoiName,
            duration = duration,
            gaze_origin = origin,
            gaze_direction = direction
        });
    }
}