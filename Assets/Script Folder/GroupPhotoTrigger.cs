using UnityEngine;

// 讓「結尾對話講完」自動觸發大合照拍照，接法跟 ColorBlindChapterTrigger 一樣：訂閱
// StoryModeManager.OnShowObjectTag，收到指定 tag 時呼叫 GroupPhotoCapture.TakeGroupPhoto()。
//
// 用法：Assets/Stories/Ending.asset 最後一句台詞的 objectsToShow 已經加了 "GroupPhotoTrigger"
// 這個字串，跟這裡的 activationTag 對上即可，不用改 StoryModeManager 本身。
//
// 跟 StoryObjectVisibility／ColorBlindChapterTrigger 一樣：這個元件掛的 GameObject 要從頭到尾
// 保持 active，不能掛在會被關掉的物件上。
public class GroupPhotoTrigger : MonoBehaviour
{
    public string activationTag = "GroupPhotoTrigger";
    public GroupPhotoCapture photoCapture;

    private void Start()
    {
        if (StoryModeManager.Instance != null)
        {
            StoryModeManager.Instance.OnShowObjectTag += HandleShow;
        }
        else
        {
            Debug.LogError("[GroupPhotoTrigger] StoryModeManager.Instance is still null in Start() — is StoryModeManager in the scene?");
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
        if (photoCapture == null)
        {
            Debug.LogWarning("[GroupPhotoTrigger] tag 對到了，但沒有指定 Photo Capture。");
            return;
        }

        photoCapture.TakeGroupPhoto();
    }
}
