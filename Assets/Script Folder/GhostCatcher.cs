using UnityEngine;

public class GhostCatcher : MonoBehaviour
{
    // 當這個物件被「關掉（變成淺色字）」的時候，這段程式就會觸發！
    void OnDisable()
    {
        // 印出紅色的警告，並且把「是誰呼叫了關閉指令」的追蹤紀錄全部印出來
        Debug.LogError("🚨 抓到鬼了！Canvas 被關掉了！凶手藏在下面的紀錄裡：\n" + StackTraceUtility.ExtractStackTrace());
    }
}