using UnityEngine;

// 讓「第二幕開場」自動打開色弱矯正濾鏡，接法跟 StoryObjectVisibility 完全一樣——訂閱
// StoryModeManager.OnShowObjectTag，只是不是拿來顯示/隱藏物件，而是拿來觸發
// ColorBlindFilterToggle.ActivateFromChapter2()。
//
// 用法：在第二幕（Chapter2）第一句台詞的 StoryChapterData.dialogueLines[0].objectsToShow
// 加入一個字串（例如 "ColorBlindFilterOn"），跟這裡的 activationTag 對上即可，不用改
// StoryModeManager 本身。
//
// 跟 StoryObjectVisibility 一樣：這個元件掛的 GameObject 要從頭到尾保持 active，
// 不能掛在被開關的物件上（inactive 的物件不會執行 Start()，OnDisable 也會把自己取消訂閱）。
public class ColorBlindChapterTrigger : MonoBehaviour
{
    public string activationTag = "ColorBlindFilterOn";

    private void Start()
    {
        if (StoryModeManager.Instance != null)
        {
            StoryModeManager.Instance.OnShowObjectTag += HandleShow;
        }
        else
        {
            Debug.LogError("[ColorBlindChapterTrigger] StoryModeManager.Instance is still null in Start() — is StoryModeManager in the scene?");
        }
    }

    private void OnDestroy()
    {
        if (StoryModeManager.Instance != null)
        {
            StoryModeManager.Instance.OnShowObjectTag -= HandleShow;
        }
    }

    private void HandleShow(string tag)
    {
        if (tag != activationTag) return;
        if (ColorBlindFilterToggle.Instance == null)
        {
            Debug.LogWarning("[ColorBlindChapterTrigger] tag 對到了，但場景裡找不到 ColorBlindFilterToggle。");
            return;
        }

        ColorBlindFilterToggle.Instance.ActivateFromChapter2();
    }
}
