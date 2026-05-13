using UnityEngine;

public class StageSkipper : MonoBehaviour
{
    [Header("要把打勾取消的東西 (可以放很多個喔！)")]
    public GameObject[] objectsToHide; // 變成中括號，代表這是一個陣列(清單)
    
    [Header("第八階段的面板")]
    public GameObject phase8Panel;
    
    public bool isTestingPhase8 = true;

    void Start()
    {
        if (isTestingPhase8)
        {
            // 電腦會自動幫你把清單裡的東西一個一個關掉
            foreach (GameObject obj in objectsToHide)
            {
                if (obj != null) obj.SetActive(false);
            }
            
            // 最後打開第八階段
            if (phase8Panel != null) phase8Panel.SetActive(true);
        }
    }
}