using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct DialogueLine
{
    public string speakerName;     // 誰說話 (NPC, 角色A, 角色B)
    public string dialogueText;    // 送給後端 LLM 的 prompt/劇情稿（StoryModeManager.RequestDialogueLine）
    public AudioClip voiceOver;    // 語音檔 (如果有)
    public string npcAnimationTrigger; // NPC 說這句話時要做的動作
}

[CreateAssetMenu(fileName = "NewStoryChapter", menuName = "Story/Chapter")]
public class StoryChapterData : ScriptableObject
{
    public int chapterNumber; // 1 ~ 5
    public List<DialogueLine> dialogueLines;
    public string targetInteractivity; // 這一幕需要玩家互動的物件標籤或事件
}