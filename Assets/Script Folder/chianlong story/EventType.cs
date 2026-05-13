// 剛才瞬間發生了甚麼事件
public enum EventType
{
    // 1.玩家靠近觸發劇情
    EnterStoryZone,
    // 2.溫碗放展桌事件
    PutBowlFailed,
    PutBowlTimeout, //trigger不用寫!!!!story manager自己等時間到就執行
    PutBowlSuccess,
    // 3.欣賞溫碗事件
    LookBowlSuccess,
    // LookBowlTimeout, //無不同反應
    LookBowlFailed,
    // 4.放入酒瓶事件
    PutBottleIntoBowlSuccess,
    PutBottleIntoBowlFailed, //story manager自己等時間到就執行

    // 5.把碗給NPC事件(已刪除)
    // GiveBowlToNPCSuccess,
    // GiveBowlToNPCFailed,

    // 6.靠近欣賞溫碗事件
    GazeBowlCloseSuccess,
    // GazeBowlCloseFailed, //story manager自己等時間到就執行
    // 7.把碗放回原位事件
    PutBowlBackSuccess,
    // PutBowlBackFailed, //一分鐘後自動執行
    // 8.玩家離開劇情區域事件
    ExitStoryZone,
    StartPhase8Survey // 測試用的第八階段問卷事件
}