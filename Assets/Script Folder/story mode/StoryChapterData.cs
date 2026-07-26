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
    public string freeChatSuccessKeyword; // 玩家答對後，NPC 固定回覆這句話，藉此結束自由問答、推進劇情
    public string freeChatAnswerKey; // 這一題實際的正確答案內容/判斷依據，NPC 用來判斷玩家是否答對（見 rag/llama_index.py _format_success_condition）
    public string freeChatHint; // 玩家答不出來時，AI 可自行判斷時機用來引導的線索；不會直接唸給玩家聽，也不能取代 freeChatAnswerKey（絕對不能直接洩題）

    [Header("Interaction Gate")]
    // 這句台詞講完後不會自動推進，要等外部呼叫 StoryModeManager.OnPlayerInteractSuccess() 才會換下一句。
    // 與 startsFreeChat 互斥：startsFreeChat 為 true 時這個欄位會被忽略。
    public bool requiresInteraction;

    [Header("Hint Panel (Player-Specific)")]
    // 依 PhotonNetwork.LocalPlayer.ActorNumber 分派：先進房間的人（1號）看 taskTextPlayer1，
    // 後進房間的人（2號）看 taskTextPlayer2，顯示在 hintPanel 上（見 StoryModeManager.ShowHintForLocalPlayer）。
    // 欄位名稱維持 taskTextPlayer1/2 沒改，避免已經填好的 StoryChapterData 資產因欄位改名而遺失資料。
    public string taskTextPlayer1;
    public string taskTextPlayer2;

    [Header("Object Visibility")]
    // 這句台詞講完後要顯示/隱藏的物件標籤。場景中掛 StoryObjectVisibility 的物件，
    // visibilityTag 對到這裡的字串才會反應（見 StoryModeManager.OnShowObjectTag/OnHideObjectTag）。
    public List<string> objectsToShow;
    public List<string> objectsToHide;
}

[CreateAssetMenu(fileName = "NewStoryChapter", menuName = "Story/Chapter")]
public class StoryChapterData : ScriptableObject
{
    public int chapterNumber; // 1 ~ 5
    public List<DialogueLine> dialogueLines;
}