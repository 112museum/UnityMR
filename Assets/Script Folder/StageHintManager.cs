using UnityEngine;

public class StageHintManager : MonoBehaviour
{
    // 這些是妳在 Unity 右邊面板會看到的「空格」
    [Header("把妳做好的 Canvas 拖到下面對應的格子裡")]
    public GameObject hintStage0;
    public GameObject hintStage6_1;
    public GameObject hintStage6_2;

    void Start()
    {
        // 遊戲一開始，先把所有提示都藏起來，保持畫面乾淨
        HideAll();
    }

    // 提供一個簡單的功能給同學呼叫
    public void ShowHint(int stageNumber)
    {
        HideAll(); // 先關掉全部

        if (stageNumber == 0 && hintStage0 != null) hintStage0.SetActive(true);
        if (stageNumber == 61 && hintStage6_1 != null) hintStage6_1.SetActive(true);
        if (stageNumber == 62 && hintStage6_2 != null) hintStage6_2.SetActive(true);
    }

    public void HideAll()
    {
        if (hintStage0 != null) hintStage0.SetActive(false);
        if (hintStage6_1 != null) hintStage6_1.SetActive(false);
        if (hintStage6_2 != null) hintStage6_2.SetActive(false);
    }
}
