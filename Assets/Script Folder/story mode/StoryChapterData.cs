using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct DialogueLine
{
    public string speakerName;     // 誰說話 (NPC, 角色A, 角色B)
    public string dialogueText;    // 送給後端 LLM 的 prompt/劇情稿（StoryModeManager.RequestDialogueLine）
    // public AudioClip voiceOver;    // 語音檔 (如果有)
    public string npcAnimationTrigger; // NPC 說這句話時要做的動作

    [Header("Free Chat")]
    public bool startsFreeChat; // 這句台詞講完後，開放玩家自由問答 speakerName 對應的 NPC
    public string freeChatSuccessKeyword; // 玩家的訊息含有這個關鍵詞，才結束自由問答、推進劇情
}

[CreateAssetMenu(fileName = "NewStoryChapter", menuName = "Story/Chapter")]
public class StoryChapterData : ScriptableObject
{
    public int chapterNumber; // 1 ~ 5
    public List<DialogueLine> dialogueLines;
    public string targetInteractivity; // 這一幕需要玩家互動的物件標籤或事件
}