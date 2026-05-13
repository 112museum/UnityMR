// 目前劇情走到哪裡、現在正在等甚麼(等玩家的動作達標(?))
public enum StoryState
{
    WaitEnterZone,
    // NPCIntroTalking,
    NPCTalking,
    WaitPlaceBowl,
    WaitLookBowl,
    WaitBottleIntoBowl,
    WaitBowlToNPC,
    WaitGazeBowlClose,
    WaitBowlBack,
    Finish,
    Phase8Survey // 測試用的第八階段問卷狀態
}

