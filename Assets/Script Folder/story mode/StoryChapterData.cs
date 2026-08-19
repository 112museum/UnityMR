using UnityEngine;
using System.Collections.Generic;

// 一個動作各自的 prompt + 對應的 Animator trigger。DialogueLine.beats 是這個的清單——
// 設計者自己決定每個動作要講什麼、順序為何，後端會在同一次呼叫裡針對每個 beat 的
// dialogueText 各自生成一段回覆，依序播放並各自觸發自己的 npcAnimationTrigger
// (StoryModeManager.RequestDialogueLine / HandleDialogueResponse)。
[System.Serializable]
public struct DialogueBeat
{
    public string dialogueText;        // 這個動作專屬的 prompt，送給後端 LLM
    public string npcAnimationTrigger; // 這個動作的 Animator trigger
}

[System.Serializable]
public struct DialogueLine
{
    public string speakerName;     // 誰說話 (NPC, 角色A, 角色B)
    // public AudioClip voiceOver;    // 語音檔 (如果有)
    public List<DialogueBeat> beats; // 空清單＝這行不呼叫後端，只套用下面的顯示/隱藏物件等效果

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