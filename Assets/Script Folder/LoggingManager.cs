using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using UnityEngine;

public class LoggingManager : MonoBehaviour
{
    public static LoggingManager Instance;

    private List<AOILogEvent> buffer = new List<AOILogEvent>();
    private string sessionId;
    private string fileGuid;
    private string logPath;
    private bool headerWritten = false;

    private const string CsvHeader =
        "session_id,timestamp,event_type,aoi_name,duration," +
        "gaze_origin_x,gaze_origin_y,gaze_origin_z," +
        "gaze_direction_x,gaze_direction_y,gaze_direction_z";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Set the session id externally and use it directly as the filename.
    // A GUID is generated per session file to avoid filename collisions.
    public void SetSessionId(string id)
    {
        sessionId = id;
        fileGuid = Guid.NewGuid().ToString("N");
        headerWritten = false;
        logPath = Path.Combine(Application.persistentDataPath, $"{sessionId}_{fileGuid}.csv");
        Debug.Log("[LoggingManager] Log path: " + logPath);
    }

    public void LogEvent(AOILogEvent e)
    {
        if (logPath == null) return; // participant not selected yet
        e.session_id = sessionId;
        buffer.Add(e);

        if (buffer.Count >= 5)
            Flush();
    }

    public void Flush()
    {
        if (buffer.Count == 0) return;

        var sb = new StringBuilder();

        if (!headerWritten)
        {
            sb.AppendLine(CsvHeader);
            headerWritten = true;
        }

        foreach (var e in buffer)
        {
            sb.AppendLine(
                $"{e.session_id},{e.timestamp:yyyy-MM-dd HH:mm:ss.fff},{e.event_type},{e.aoi_name},{e.duration:F4}," +
                $"{e.gaze_origin.x:F4},{e.gaze_origin.y:F4},{e.gaze_origin.z:F4}," +
                $"{e.gaze_direction.x:F4},{e.gaze_direction.y:F4},{e.gaze_direction.z:F4}"
            );
        }

        File.AppendAllText(logPath, sb.ToString());
        buffer.Clear();
    }

    void OnApplicationQuit()
    {
        Flush();
    }
}
