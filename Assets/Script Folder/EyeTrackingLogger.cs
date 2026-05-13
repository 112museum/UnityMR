using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.XR;

public class EyeTrackingLogger : MonoBehaviour
{
    [Header("Fixation Settings")]
    public float fixationThreshold = 0.3f;

    [Header("Ray Settings")]
    public float rayMaxDistance = 10f;

    [Header("CSV Settings")]
    public float writeInterval = 5f; // 每隔多久 flush CSV
    public bool enableSummaryCSV = true; // 是否生成 Summary CSV

    private string currentAOI = "None";
    private string currentObject = "None";
    private float currentFixationTime = 0f;

    private Dictionary<string, int> fixationCounts = new();
    private Dictionary<string, float> fixationDurations = new();

    private List<string> csvLines = new();
    private bool csvInitialized = false;

    private string rawCSVPath;
    private string summaryCSVPath;

    // Summary 專用統計結構
    private Dictionary<string, Dictionary<string, int>> zoneFixationCounts = new();
    private Dictionary<string, Dictionary<string, float>> zoneFixationDurations = new();


    void Start()
    {
        // 初始化 AOI 統計
        fixationCounts["AOI_NPC"] = 0;
        fixationCounts["AOI_Exhibit"] = 0;
        fixationCounts["AOI_Physical"] = 0;
        fixationCounts["AOI_Virtual"] = 0;
        fixationCounts["Untagged"] = 0;

        fixationDurations["AOI_NPC"] = 0f;
        fixationDurations["AOI_Exhibit"] = 0f;
        fixationDurations["AOI_Physical"] = 0f;
        fixationDurations["AOI_Virtual"] = 0f;
        fixationDurations["Untagged"] = 0f;

        // CSV 路徑
        rawCSVPath = Path.Combine(Application.persistentDataPath, "EyeTracking_Raw.csv");
        summaryCSVPath = Path.Combine(Application.persistentDataPath, "EyeTracking_Summary.csv");

        Debug.Log("Raw CSV path: " + rawCSVPath);
        if (enableSummaryCSV)
            Debug.Log("Summary CSV path: " + summaryCSVPath);

        // 初始化 Raw CSV（覆蓋舊檔）
        string csvHeader = "timestamp,AOI,ObjectName,fixationDuration";
        File.WriteAllText(rawCSVPath, csvHeader + "\n");
        csvInitialized = true;

        StartCoroutine(WriteCSVPeriodically());

        InitializeZone("Introvert");
        InitializeZone("Extrovert");
    }

    void InitializeZone(string zone)
    {
        zoneFixationCounts[zone] = new Dictionary<string, int>()
        {
            { "AOI_NPC", 0 },
            { "AOI_Exhibit", 0 }
        };

        zoneFixationDurations[zone] = new Dictionary<string, float>()
        {
            { "AOI_NPC", 0f },
            { "AOI_Exhibit", 0f }
        };
    }

    string GetZoneFromRootName(string rootName)
    {
        if (rootName.Contains("Introvert"))
            return "Introvert";
        if (rootName.Contains("Extrovert"))
            return "Extrovert";

        return null; // 其他不納入 Summary
    }


    void Update()
    {
        Vector3 gazeOrigin = Vector3.zero;
        Vector3 gazeDirection = Vector3.forward;

        // Hololens / MRTK3 Eye Tracking
        bool eyeDataValid = false;
        List<XRNodeState> nodeStates = new List<XRNodeState>();
        InputTracking.GetNodeStates(nodeStates);

        foreach (var nodeState in nodeStates)
        {
            if (nodeState.nodeType == XRNode.CenterEye)
            {
                if (nodeState.TryGetPosition(out Vector3 pos))
                    gazeOrigin = pos;
                if (nodeState.TryGetRotation(out Quaternion rot))
                    gazeDirection = rot * Vector3.forward;

                eyeDataValid = true;
            }
        }

        if (!eyeDataValid && Camera.main != null)
        {
            gazeOrigin = Camera.main.transform.position;
            gazeDirection = Camera.main.transform.forward;
        }

        Ray ray = new Ray(gazeOrigin, gazeDirection);
        string hitAOI = "None";
        string hitObject = "None";

        if (Physics.Raycast(ray, out RaycastHit hit, rayMaxDistance))
        {
            Transform t = hit.collider.transform;

            // 往上找 AOI Tag
            while (t != null)
            {
                if (t.tag.StartsWith("AOI_"))
                {
                    hitAOI = t.tag;
                    hitObject = t.root.name; // 使用 Root name
                    break;
                }
                t = t.parent;
            }

            if (hitAOI == "None")
            {
                hitAOI = hit.collider.tag;
                hitObject = hit.collider.transform.root.name;
            }
        }
        // 測試是否有'看到'物件
        // Debug.Log($"Hit AOI = {hitAOI}, Object = {hitObject}");

        // 同 AOI + 同物件 → 累積 fixation
        if (hitAOI == currentAOI && hitObject == currentObject)
        {
            currentFixationTime += Time.deltaTime;
        }
        else
        {
            FinalizeFixation();
            currentAOI = hitAOI;
            currentObject = hitObject;
            currentFixationTime = 0f;
        }
    }

    void FinalizeFixation()
    {
        if (currentFixationTime < fixationThreshold)
            return;

        // Raw CSV（完全照舊）
        if (fixationCounts.ContainsKey(currentAOI))
        {
            fixationCounts[currentAOI]++;
            fixationDurations[currentAOI] += currentFixationTime;

            csvLines.Add($"{Time.time:F3},{currentAOI},{currentObject},{currentFixationTime:F3}");
        }

        // ===== Summary（只處理 NPC / Exhibit）=====
        if (currentAOI != "AOI_NPC" && currentAOI != "AOI_Exhibit")
            return;

        string zone = GetZoneFromRootName(currentObject);
        if (zone == null)
            return;

        zoneFixationCounts[zone][currentAOI]++;
        zoneFixationDurations[zone][currentAOI] += currentFixationTime;
    }


    private IEnumerator WriteCSVPeriodically()
    {
        while (true)
        {
            yield return new WaitForSeconds(writeInterval);

            if (csvLines.Count > 0)
            {
                File.AppendAllLines(rawCSVPath, csvLines);
                csvLines.Clear();
                Debug.Log("Raw CSV updated: " + rawCSVPath);
            }

            if (enableSummaryCSV)
            {
                WriteSummaryCSV();
            }
        }
    }

    private void WriteSummaryCSV()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Zone,AOI,FixationCount,TotalFixationDuration");

        foreach (string zone in zoneFixationCounts.Keys)
        {
            int zoneTotalCount = 0;
            float zoneTotalDuration = 0f;

            foreach (string aoi in zoneFixationCounts[zone].Keys)
            {
                int count = zoneFixationCounts[zone][aoi];
                float duration = zoneFixationDurations[zone][aoi];

                sb.AppendLine($"{zone},{aoi},{count},{duration:F3}");

                zoneTotalCount += count;
                zoneTotalDuration += duration;
            }

            // 區域總計
            sb.AppendLine($"{zone},ALL,{zoneTotalCount},{zoneTotalDuration:F3}");

            // 🔹 空一行（Introvert / Extrovert 分隔）
            sb.AppendLine();
        }

        File.WriteAllText(summaryCSVPath, sb.ToString());
        Debug.Log("Summary CSV updated (Zone-based with spacing)");
    }

    private void OnDestroy()
    {
        FinalizeFixation();

        if (csvLines.Count > 0)
            File.AppendAllLines(rawCSVPath, csvLines);

        if (enableSummaryCSV)
            WriteSummaryCSV();
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            FinalizeFixation();
            if (csvLines.Count > 0)
                File.AppendAllLines(rawCSVPath, csvLines);

            if (enableSummaryCSV)
                WriteSummaryCSV();
        }
    }
}