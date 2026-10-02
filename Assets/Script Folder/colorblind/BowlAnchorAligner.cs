// BowlAnchorAligner.cs — Aligns its own transform to a physical QR code placed
// next to the real 汝窯溫碗, independent of TableAnchor (confirmed 2026-09-23:
// the real bowl's table is not the same table TableAnchor/QRAnchorAligner
// aligns for multiplayer content, so it needs its own QR + its own anchor,
// not TableAnchorAsParent). Same detection/offset/restart logic as
// QRAnchorAligner, just targeting its own GameObject instead of
// TableAnchor.Instance.
//
// Usage: attach to an empty GameObject (e.g. "BowlAnchor") placed anywhere in
// the scene — its transform gets overwritten once the QR is seen, so starting
// position doesn't matter. Parent the overlay bowl instance (the one with
// PhysicalBowlFilterOverlay on it) under this GameObject in the Editor, then
// calibrate the overlay's local position/rotation/scale against the real bowl.
using System;
using Microsoft.MixedReality.OpenXR;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARMarkerManager))]
public class BowlAnchorAligner : MonoBehaviour
{
    [Tooltip("Must exactly match the text encoded in the printed/on-screen QR code.")]
    [SerializeField] private string expectedQrText = "MRMuseum_BowlAnchor_01";

    [Tooltip("If false, this transform is set once on first detection and then locked. If true, it keeps snapping to the latest detection (can jitter the overlay already sitting on it).")]
    [SerializeField] private bool continuousAlignment = false;

    [Tooltip("Offset (in the anchor's own upright frame when keepUpright is on, else the QR's frame) applied when placing this anchor, for when the code isn't stuck exactly where the overlay should be centered.")]
    [SerializeField] private Vector3 anchorPositionOffset = Vector3.zero;

    [Tooltip("Extra rotation (degrees) applied on top of the QR code's orientation.")]
    [SerializeField] private Vector3 anchorRotationOffsetEuler = Vector3.zero;

    [Tooltip("Keep this anchor upright in world space (only take the QR's yaw), so the bowl stands upright whether the QR lies flat on the table or is stuck on a vertical surface.")]
    [SerializeField] private bool keepUpright = true;

    [Tooltip("Logs every marker the subsystem detects (decoded text included), not just ones matching expectedQrText. Turn on while diagnosing why alignment isn't happening.")]
    [SerializeField] private bool verboseLogging = true;

    [Tooltip("On HoloLens, the OS-level QR watcher sometimes isn't ready yet the instant this component starts, so it silently misses every marker for the rest of the app session (the known workaround is quitting and relaunching the app). If no markersChanged event fires at all within this many seconds, the marker subsystem is restarted automatically to recover without a manual relaunch. Set to 0 to disable.")]
    [SerializeField] private float restartIfNoDetectionAfterSeconds = 10f;

    public bool IsAligned { get; private set; }
    public event Action Aligned;

    private ARMarkerManager markerManager;
    private float sessionStartTime;
    private float lastRestartCheckTime;
    private bool watcherConfirmedAlive;

    private void Awake()
    {
        markerManager = GetComponent<ARMarkerManager>();

        // Default (MostStable) puts the marker origin at the QR's corner; Center puts it at the
        // QR's geometric center, which is where the bowl should sit.
        markerManager.defaultTransformMode = TransformMode.Center;

        // Same reasoning as QRAnchorAligner: HoloLens caches detected QR codes at the
        // OS/driver level, so a reading from before this app session started must be
        // ignored as stale (e.g. the physical code moved between test runs).
        sessionStartTime = Time.realtimeSinceStartup;
        lastRestartCheckTime = sessionStartTime;
    }

    private void Start()
    {
        if (verboseLogging)
        {
            Debug.Log($"[BowlAnchorAligner] Start. subsystem running={markerManager.subsystem != null}, enabledMarkerTypes={string.Join(",", markerManager.enabledMarkerTypes)}, expecting text='{expectedQrText}'");
        }
    }

    private void Update()
    {
        if (restartIfNoDetectionAfterSeconds <= 0f || IsAligned || watcherConfirmedAlive) return;

        if (Time.realtimeSinceStartup - lastRestartCheckTime >= restartIfNoDetectionAfterSeconds)
        {
            RestartMarkerManager();
        }
    }

    private void OnEnable()
    {
        markerManager.markersChanged += OnMarkersChanged;
    }

    private void OnDisable()
    {
        markerManager.markersChanged -= OnMarkersChanged;
    }

    private void RestartMarkerManager()
    {
        if (verboseLogging)
        {
            Debug.Log($"[BowlAnchorAligner] No markersChanged event in {restartIfNoDetectionAfterSeconds}s — restarting ARMarkerManager (mirrors the 'quit and relaunch' workaround for a QR watcher that wasn't ready at session start).");
        }

        markerManager.enabled = false;
        markerManager.enabled = true;
        lastRestartCheckTime = Time.realtimeSinceStartup;
    }

    private void OnMarkersChanged(ARMarkersChangedEventArgs args)
    {
        watcherConfirmedAlive = true;

        if (verboseLogging)
        {
            Debug.Log($"[BowlAnchorAligner] markersChanged: added={args.added.Count} updated={args.updated.Count} removed={args.removed.Count}");
        }

        if (IsAligned && !continuousAlignment) return;

        foreach (var marker in args.added)
        {
            TryAlign(marker);
        }

        foreach (var marker in args.updated)
        {
            if (IsAligned && !continuousAlignment) break;
            TryAlign(marker);
        }
    }

    private void TryAlign(ARMarker marker)
    {
        var decoded = marker.GetDecodedString();

        if (verboseLogging)
        {
            Debug.Log($"[BowlAnchorAligner] marker id={marker.trackableId} decoded='{decoded}' trackingState={marker.trackingState} lastSeenTime={marker.lastSeenTime} sessionStartTime={sessionStartTime} pos={marker.transform.position} rot={marker.transform.rotation.eulerAngles}");
        }

        if (decoded != expectedQrText) return;

        if (marker.lastSeenTime < sessionStartTime)
        {
            if (verboseLogging)
            {
                Debug.Log($"[BowlAnchorAligner] Ignoring marker id={marker.trackableId}: lastSeenTime={marker.lastSeenTime} predates this session (started {sessionStartTime}) — stale OS-cached reading, not a fresh scan.");
            }
            return;
        }

        if (marker.trackingState == TrackingState.None)
        {
            if (verboseLogging)
            {
                Debug.Log($"[BowlAnchorAligner] Ignoring marker id={marker.trackableId}: trackingState=None.");
            }
            return;
        }

        var markerTransform = marker.transform;
        var baseRotation = keepUpright ? UprightYaw(markerTransform) : markerTransform.rotation;
        var rotation = baseRotation * Quaternion.Euler(anchorRotationOffsetEuler);
        var position = markerTransform.position + rotation * anchorPositionOffset;

        transform.SetPositionAndRotation(position, rotation);

        if (!IsAligned)
        {
            IsAligned = true;
            Aligned?.Invoke();
        }
    }

    // Whichever of the marker's in-plane/normal axes is most horizontal gives the yaw; world up stays up.
    private static Quaternion UprightYaw(Transform t)
    {
        var best = Vector3.zero;
        foreach (var axis in new[] { t.forward, t.up, t.right })
        {
            var flat = Vector3.ProjectOnPlane(axis, Vector3.up);
            if (flat.sqrMagnitude > best.sqrMagnitude) best = flat;
        }
        return best.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(best.normalized, Vector3.up) : Quaternion.identity;
    }
}
