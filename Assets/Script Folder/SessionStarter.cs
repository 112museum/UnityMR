using UnityEngine;

public class SessionStarter : MonoBehaviour
{
    void Start()
    {
        LoggingManager.Instance.SetSessionId("session_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss"));
    }
}