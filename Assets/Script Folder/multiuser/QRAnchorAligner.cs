// QRAnchorAligner.cs — Aligns TableAnchor to a physical QR code so all
// Photon-synced content (which is transmitted relative to TableAnchor, see
// GenericNetSync.OnPhotonSerializeView) lands in the same real-world spot on
// every HoloLens 2 in the room.
using System;
using Microsoft.MixedReality.OpenXR;
using MRTK.Tutorials.MultiUserCapabilities;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARMarkerManager))]
public class QRAnchorAligner : MonoBehaviour
{
    [Tooltip("Must exactly match the text encoded in the printed QR code.")]
    [SerializeField] private string expectedQrText = "MRMuseum_Anchor_01";

    [Tooltip("If false, TableAnchor is set once on first detection and then locked. If true, TableAnchor keeps snapping to the latest detection (can jitter already-synced content).")]
    [SerializeField] private bool continuousAlignment = false;

    [Tooltip("Local-space offset (relative to the QR code's own orientation) applied when placing TableAnchor, for when the code isn't stuck exactly where content should be centered.")]
    [SerializeField] private Vector3 anchorPositionOffset = Vector3.zero;

    [Tooltip("Extra rotation (degrees) applied on top of the QR code's orientation.")]
    [SerializeField] private Vector3 anchorRotationOffsetEuler = Vector3.zero;

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

        // HoloLens caches detected QR codes at the OS/driver level, not per-app or per-Photon-
        // session, and can hand back a stale remembered pose the instant this app starts —
        // before the camera has re-seen the code at all. That's fatal if the physical code was
        // moved between test runs: we'd silently align to where it used to be. ARMarker.lastSeenTime
        // is scoped like Time.realtimeSinceStartup, so anything seen before this app session
        // began is necessarily a replayed/cached reading, not a fresh scan.
        sessionStartTime = Time.realtimeSinceStartup;
        lastRestartCheckTime = sessionStartTime;
    }

    private void Start()
    {
        if (verboseLogging)
        {
            Debug.Log($"[QRAnchorAligner] Start. subsystem running={markerManager.subsystem != null}, enabledMarkerTypes={string.Join(",", markerManager.enabledMarkerTypes)}, expecting text='{expectedQrText}'");
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
            Debug.Log($"[QRAnchorAligner] No markersChanged event in {restartIfNoDetectionAfterSeconds}s — restarting ARMarkerManager (mirrors the 'quit and relaunch' workaround for a QR watcher that wasn't ready at session start).");
        }

        markerManager.enabled = false;
        markerManager.enabled = true;
        lastRestartCheckTime = Time.realtimeSinceStartup;
    }

    private void OnMarkersChanged(ARMarkersChangedEventArgs args)
    {
        // Any callback at all — regardless of what it contains — proves the OS-level watcher
        // is alive, so the "not ready yet" restart above is no longer needed this session.
        watcherConfirmedAlive = true;

        if (verboseLogging)
        {
            Debug.Log($"[QRAnchorAligner] markersChanged: added={args.added.Count} updated={args.updated.Count} removed={args.removed.Count}");
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
            Debug.Log($"[QRAnchorAligner] marker id={marker.trackableId} decoded='{decoded}' trackingState={marker.trackingState} lastSeenTime={marker.lastSeenTime} sessionStartTime={sessionStartTime} pos={marker.transform.position} rot={marker.transform.rotation.eulerAngles}");
        }

        if (decoded != expectedQrText) return;

        if (marker.lastSeenTime < sessionStartTime)
        {
            if (verboseLogging)
            {
                Debug.Log($"[QRAnchorAligner] Ignoring marker id={marker.trackableId}: lastSeenTime={marker.lastSeenTime} predates this session (started {sessionStartTime}) — stale OS-cached reading, not a fresh scan.");
            }
            return;
        }

        // TrackingState.Tracking turned out to be rare/unreachable in practice on this
        // hardware (both test devices sat at Limited indefinitely), so this only skips the
        // worst case (not tracked at all) instead of hard-gating on Tracking.
        if (marker.trackingState == TrackingState.None)
        {
            if (verboseLogging)
            {
                Debug.Log($"[QRAnchorAligner] Ignoring marker id={marker.trackableId}: trackingState=None.");
            }
            return;
        }

        var anchor = TableAnchor.Instance;
        if (anchor == null) return;

        var markerTransform = marker.transform;
        var rotation = markerTransform.rotation * Quaternion.Euler(anchorRotationOffsetEuler);
        var position = markerTransform.position + markerTransform.TransformDirection(anchorPositionOffset);

        anchor.transform.SetPositionAndRotation(position, rotation);

        if (!IsAligned)
        {
            IsAligned = true;
            Aligned?.Invoke();
        }
    }
}
