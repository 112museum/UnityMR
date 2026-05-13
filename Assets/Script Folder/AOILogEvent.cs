using System;
using UnityEngine;

[System.Serializable]
public class AOILogEvent
{
    public string session_id;
    public DateTime timestamp;
    public string event_type;
    public string aoi_name;
    public float duration;

    public Vector3 gaze_origin;
    public Vector3 gaze_direction;
}