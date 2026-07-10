// DebugLogHud.cs — Temporary on-screen log viewer for on-device debugging
// (e.g. HoloLens2 QR alignment testing) where there's no PC/cable nearby.
// Attach to a world-space TextMeshProUGUI that's parented to the camera.
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class DebugLogHud : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI logText;
    [SerializeField] private int maxLines = 15;

    private readonly Queue<string> lines = new Queue<string>();
    private readonly StringBuilder builder = new StringBuilder();

    private void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    private void HandleLog(string message, string stackTrace, LogType type)
    {
        var color = type switch
        {
            LogType.Error or LogType.Exception => "red",
            LogType.Warning => "yellow",
            _ => "white",
        };

        lines.Enqueue($"<color={color}>{message}</color>");
        while (lines.Count > maxLines)
        {
            lines.Dequeue();
        }

        builder.Clear();
        foreach (var line in lines)
        {
            builder.AppendLine(line);
        }

        if (logText != null)
        {
            logText.text = builder.ToString();
        }
    }
}
