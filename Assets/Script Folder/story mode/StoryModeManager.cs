using UnityEngine;

public class StoryModeManager : MonoBehaviour
{
    public enum StoryState { Selection, Chapter1, Chapter2, Chapter3, Chapter4, Chapter5, Ending }
    
    // 網路同步變數 (以預想的同步邏輯為例)
    public StoryState currentStatus = StoryState.Selection;
    public int currentLineIndex = 0;
    
    public StoryChapterData[] allChapters; // 放你剛剛建立的 5 個章節資料
    
    // 玩家選角狀態
    private string player1Role = "";
    private string player2Role = "";

    public void StartStoryMode()
    {
        // 1. 檢查是否剛好兩個人
        // 2. 載入選角介面
        currentStatus = StoryState.Selection;
    }

    public void SelectRole(string roleName, bool isPlayer1)
    {
        if (isPlayer1) player1Role = roleName;
        else player2Role = roleName;

        // 當兩人都選好角色，進入 Chapter 1
        if (!string.IsNullOrEmpty(player1Role) && !string.IsNullOrEmpty(player2Role))
        {
            EnterChapter(1);
        }
    }

    public void EnterChapter(int chapterNum)
    {
        currentStatus = (StoryState)chapterNum;
        currentLineIndex = 0;
        PlayCurrentLine();
    }

    public void PlayCurrentLine()
    {
        StoryChapterData currentChapter = allChapters[(int)currentStatus - 1];
        
        if (currentLineIndex < currentChapter.dialogueLines.Count)
        {
            DialogueLine line = currentChapter.dialogueLines[currentLineIndex];
            
            // 驅動你原本的系統
            // TextManager.DisplaySubtitle(line.dialogueText);
            // TextToSpeech.Speak(line.dialogueText);
            // NPCRequestManager.SetNPCAnimation(line.npcAnimationTrigger);
        }
        else
        {
            // 這一幕的對話播完了，檢查是否有互動任務，或者直接進下一幕
            NextChapter();
        }
    }

    public void OnPlayerInteractSuccess()
    {
        // 當觸發了特定互動（例如上一題提到的：A 摸了某物件）
        // 呼叫此函式推進劇情
        currentLineIndex++;
        PlayCurrentLine();
    }

    private void NextChapter()
    {
        int nextNum = (int)currentStatus + 1;
        if (nextNum <= 5) EnterChapter(nextNum);
        else currentStatus = StoryState.Ending;
    }
}