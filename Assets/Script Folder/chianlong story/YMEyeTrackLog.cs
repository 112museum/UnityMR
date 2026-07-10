using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;


public class YMEyeTrackLog : MonoBehaviour
{
    [Header("Fixation Settings")]
    public float fixationThreshold = 0.3f;
    public float rayMaxDistance = 10f;


    [Header("CSV Settings")]
    public float writeInterval = 5f;
    public bool enableSummaryCSV = true;


    private bool isLogging = false;
    private string currentUserId = "None";
    private string currentAOI = "None";
    private string currentObject = "None";
    private float currentFixationTime = 0f;


    private List<string> targetTags = new List<string> {
        "AOI_VirtualScene", "AOI_Poster", "AOI_PhysicalExhibits"
    };


    private Dictionary<string, int> fixationCounts = new();
    private Dictionary<string, float> fixationDurations = new();
    private List<string> csvLines = new();


    private string rawCSVPath;
    private string summaryCSVPath;
   
    // 用來解決 Sharing violation 的鎖定物件
    private readonly object fileLock = new object();


    void Awake()
    {
        // Summary 是大表，路徑固定不變
        summaryCSVPath = Path.Combine(Application.persistentDataPath, "YMEyeTrack_Summary.csv");


        if (!File.Exists(summaryCSVPath))
        {
            StringBuilder header = new StringBuilder();
            header.Append("userid");
            foreach (var tag in targetTags)
            {
                header.Append($",{tag}_Count,{tag}_Duration");
            }
            header.Append(",waitTime");
            File.WriteAllText(summaryCSVPath, header.ToString() + "\n");
        }
    }


    public void StartNewUserWithCurrentTime()
    {
        string timeId = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        StartNewUser(timeId);
    }


    public void StartNewUser(string userId)
    {
        // 如果還在紀錄，先強制停止舊的
        if (isLogging) StopLogging(0f);


        currentUserId = userId;
        isLogging = true;


        // 【關鍵修改】：動態生成該使用者的專屬 Raw Data 路徑
        rawCSVPath = Path.Combine(Application.persistentDataPath, $"YMEyeTrack_Raw_{currentUserId}.csv");
        Debug.Log($"[Logger] 本次使用者 Raw Data 路徑: {rawCSVPath}");


        foreach (var tag in targetTags)
        {
            fixationCounts[tag] = 0;
            fixationDurations[tag] = 0f;
        }
        csvLines.Clear();


        // 建立新檔案並寫入標頭
        File.WriteAllText(rawCSVPath, "timestamp,AOI,event,fixationDuration\n");


        StartCoroutine(WriteCSVPeriodically());
    }


    public void StopLogging(float waitTime)
    {
        if (!isLogging) return;


        isLogging = false; // 先設為 false 讓 Coroutine 停止循環
        StopAllCoroutines();
       
        FinalizeFixation();  
        SaveFinalSummary(waitTime);
       
        Debug.Log($"[Logger] 使用者 {currentUserId} 已結算，等待時間：{waitTime}s。");
    }


    void Update()
    {
        if (!isLogging) return;


        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        string hitAOI = "None";
        string hitObject = "None";


        if (Physics.Raycast(ray, out RaycastHit hit, rayMaxDistance))
        {
            string tempTag = hit.collider.tag;
            if (targetTags.Contains(tempTag))
            {
                hitAOI = tempTag;
                hitObject = hit.collider.name;
            }
        }


        if (hitAOI == currentAOI && hitObject == currentObject)
        {
            currentFixationTime += Time.deltaTime;
        }
        else
        {
            bool aoiChanged = hitAOI != currentAOI;
            FinalizeFixation();
            currentAOI = hitAOI;
            currentObject = hitObject;
            currentFixationTime = 0f;
            if (aoiChanged && currentAOI != "None")
                csvLines.Add($"{Time.time:F3},{currentAOI},enter,");
        }
    }


    void FinalizeFixation()
    {
        if (currentFixationTime < fixationThreshold || currentAOI == "None")
            return;


        if (fixationCounts.ContainsKey(currentAOI))
        {
            fixationCounts[currentAOI]++;
            fixationDurations[currentAOI] += currentFixationTime;
            csvLines.Add($"{Time.time:F3},{currentAOI},exit,{currentFixationTime:F3}");
            Debug.Log($"[Logger] 紀錄：{currentAOI} 持續 {currentFixationTime:F2}s");
        }
    }


    private IEnumerator WriteCSVPeriodically()
    {
        while (isLogging)
        {
            yield return new WaitForSeconds(writeInterval);
            SaveRawDataOnly();
        }
    }


    private void SaveRawDataOnly()
    {
        if (csvLines.Count > 0)
        {
            // 使用 lock 避免同時寫入造成的 Sharing violation
            lock (fileLock)
            {
                try {
                    File.AppendAllLines(rawCSVPath, csvLines);
                    csvLines.Clear();
                } catch (IOException e) {
                    Debug.LogWarning($"[Logger] 寫入 RawData 失敗 (可能檔案被開啟中): {e.Message}");
                }
            }
        }
    }


    private void SaveFinalSummary(float waitTime)
    {
        SaveRawDataOnly();


        if (enableSummaryCSV)
        {
            lock (fileLock)
            {
                try {
                    StringBuilder sb = new StringBuilder();
                    sb.Append(currentUserId);
                    foreach (var tag in targetTags)
                    {
                        sb.Append($",{fixationCounts[tag]},{fixationDurations[tag]:F3}");
                    }
                    sb.Append($",{waitTime:F3}");


                    File.AppendAllText(summaryCSVPath, sb.ToString() + "\n");
                } catch (IOException e) {
                    Debug.LogError($"[Logger] 寫入 Summary 失敗: {e.Message}");
                }
            }
        }
    }
}







