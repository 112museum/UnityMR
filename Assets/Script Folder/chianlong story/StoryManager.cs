using UnityEngine;
using System.Collections;
// using System.Diagnostics;


public class StoryManager : MonoBehaviour
{
    public static StoryManager Instance;
    public ProximityTrigger proximityTrigger;
    
        // 定義劇情狀態

    [Header("Current State")]
    public StoryState currentState;

    [Header("System References")]
    public MuseumSurveyController museumSurveyController;
    public Talker npc;
    public Animator animator;
    public FloatingPickupItem item;
    // public BowlFlyIn bowlScript;
    public WineAnimationTester wineAnimationTester;
    public ZeroGravityObject theBowl;
    public Transform npcHand;
    public Transform putBowlTable;
    private bool allowLookBowlSuccessOnly = false;
    private bool allowGazeBowlSuccessOnly = false;
    private Coroutine lookLongerCoroutine;
    private Coroutine gazeLongerCoroutine;
    private bool eventLocked = false;
    private bool isPlaying = false; // 用於防止重複執行同一個任務
    
    void Awake()
    {
        Instance = this;
    }

    void Start()
{
    StartStory(); 
}

    // 延遲指令
    // IEnumerator Delay(float time, IEnumerator routine)
    // {
    //     yield return new WaitForSeconds(time);
    //     yield return routine;
    // }

    // 觸發劇情開始
    void StartStory()
    {
        Debug.Log("Story Start");

        currentState = StoryState.WaitEnterZone;

        SubtitleDisplayManager.Instance.DisplayHint("hint0-1");
    }


    IEnumerator TestAnimation()
    {
        Debug.Log("Test Animation");

        // 顯示字幕
        // SubtitleDisplayManager.Instance.DisplayStory("story3-1");
        // 打動畫觸發的名字
        animator.SetTrigger("point real_S");
        // 播放 NPC 台詞並等待完成
        yield return npc.SpeakCoroutine("tasks", "task2_success_AR");

        //// 顯示字幕
        //SubtitleDisplayManager.Instance.DisplayStory("story3-1");
        //// 打動畫觸發的名字
        //animator.SetTrigger("point real");
        //// 播放 NPC 台詞並等待完成
        //yield return npc.SpeakCoroutine("tasks", "task2_overtime");
    }

    //!!!!!!!!!!!記得加上npc talking state!!!!!!!!!
    // npc talking包含上一階段的task failed/success
    // 改成一律npc talking 不分階段因為npc講話不會有其他觸發事件
    // 所有 Trigger 都會呼叫這裡
    public void Notify(EventType eventType)
    {
        if (currentState == StoryState.NPCTalking || eventLocked)
            return;
        
        Debug.Log("Event Received: " + eventType);

        // 如果進入「只允許成功」模式
        if (allowLookBowlSuccessOnly)
        {
            if (eventType == EventType.LookBowlSuccess)
            {
                Notify(EventType.LookBowlSuccess);
            }
            return; // 擋掉所有其他事件
        }
        if (allowGazeBowlSuccessOnly)
        {
            if (eventType == EventType.GazeBowlCloseSuccess)
            {
                StartCoroutine(NPCStartAppreciate());

            }
            return; // 擋掉所有其他事件
        }

        switch (currentState)
        {
            case StoryState.NPCTalking:
                // NPC 講話階段，不接受任何事件
                Debug.Log("NPC is talking. Ignoring all events.");
                return; // 直接返回
            case StoryState.WaitEnterZone:
                switch (eventType)
                {
                    case EventType.EnterStoryZone:
                        eventLocked = true;
                        // StartCoroutine(DelayedStartIntro());

                        // 各種測試
                        // StartCoroutine(BowlAppreciate());
                        // StartCoroutine(Phase8Survey());
                        StartCoroutine(GoToPlaceBowlSequence());
                        // StartCoroutine(TestAnimation());
                        break;
                }
                break;

            case StoryState.WaitPlaceBowl:
                switch (eventType)
                {
                    case EventType.PutBowlTimeout:
                       StartCoroutine(FindBowlTimeout());
                        break;
                    case EventType.PutBowlSuccess:
                       StartCoroutine(FindBowlSuccess());
                        break;
                    case EventType.PutBowlFailed:
                        StartCoroutine(FindBowlTooFar());
                        break;
                }
                break;

            case StoryState.WaitLookBowl:
                switch (eventType)
                {
                    case EventType.LookBowlSuccess:
                        OnLookBowlSuccess();
                        break;
                    // case EventType.LookBowlTimeout:
                    //     StartCoroutine(Delay(5f, GoToPutBottle()));
                    //     break;
                    case EventType.LookBowlFailed:
                        OnLookBowlFailed();
                        break;
                }
                break;

            case StoryState.WaitBottleIntoBowl:
                switch (eventType)
                {
                    case EventType.PutBottleIntoBowlSuccess:
                        wineAnimationTester.TriggerBottleToBowl();
                        StartCoroutine(NPCDrinking());
                        break;
                    case EventType.PutBottleIntoBowlFailed:
                        // 酒壺飛到溫碗裡
                        wineAnimationTester.TriggerBottleToBowl();
                        StartCoroutine(NPCDrinking());
                        break;
                }
                break;

            // case StoryState.WaitBowlToNPC:
            //     switch (eventType)
            //     {
            //         case EventType.GiveBowlToNPCSuccess:
            //             StartCoroutine(NPCStartAppreciate());
            //             break;
            //         case EventType.GiveBowlToNPCFailed:
            //             // animator.SetTrigger("flyingBowlAgain");
            //             StartCoroutine(NPCStartAppreciate());
            //             break;
            //     }
            //   break;

            case StoryState.WaitGazeBowlClose:
                switch (eventType)
                {
                    case EventType.GazeBowlCloseSuccess:
                        OnGazeBowlSuccess();
                        break;
                    // case EventType.GazeBowlCloseFailed:
                    //     OnGazeBowlFailed();
                    //     break;
                }
                break;

            case StoryState.WaitBowlBack:
                switch (eventType)
                {
                    case EventType.PutBowlBackSuccess:
                    // case EventType.PutBowlBackFailed:
                            StartCoroutine(StoryEnding());
                        break;
                }
                break;
            case StoryState.Finish:
                // 劇情結束，不接受任何事件，除非再次體驗
                Debug.Log("Story Finished.Start survey state.");
                return; // 直接返回

            case StoryState.Phase8Survey:
                switch (eventType)
                {
                    case EventType.StartPhase8Survey:
                        // 第八階段問卷
                        Debug.Log("TestPhase8Survey active. Ignoring all events except survey-related ones."); 
                        StartCoroutine(Phase8Survey());
                        break;
                }
                break;
        }
    }

    // =========================
    // 各劇情步驟(功能會放這裡?)
    // =========================

    IEnumerator DelayedStartIntro()
    {
        Debug.Log("Player Entered Zone");
        SubtitleDisplayManager.Instance.HideHint();

        // 確保乾隆(talker)先出現
        if (npc != null)
        {
            npc.gameObject.SetActive(true);
        }

        // 等 1~2 秒
        yield return new WaitForSeconds(1.5f);

        // 再開始劇情
        GoToNPCIntro();
    }
    void GoToNPCIntro() //之後要改，會改成一連串動畫，不會分三段，作為之後Coroutine的範例
    {
        StartCoroutine(NPCIntroSequence());
    }
    IEnumerator NPCIntroSequence()
    {
        currentState = StoryState.NPCTalking;
        // theBowl.SwitchToState(ZeroGravityObject.ObjectState.Floating_Locked);

        // ===== 第一段 =====

        // SubtitleDisplayManager.Instance.DisplayStory("story1-1");

        // animator.SetTrigger("breathing");

        yield return npc.SpeakCoroutine("stories", "story1-1");


        // ===== 第二段 =====

        // SubtitleDisplayManager.Instance.DisplayStory("story1-2");

        animator.SetTrigger("look around1-2");

        yield return npc.SpeakCoroutine("stories", "story1-2");


        // ===== 第三段 =====

        // SubtitleDisplayManager.Instance.DisplayStory("story1-3");

        animator.SetTrigger("looking exhibition1-3");

        yield return npc.SpeakCoroutine("stories", "story1-3");
        // 下一步：改用 yield return 等待完成
        yield return GoToPlaceBowlSequence();
    }
    public IEnumerator GoToPlaceBowlSequence()
    {
        currentState = StoryState.NPCTalking;

        // SubtitleDisplayManager.Instance.DisplayStory("story2-1");
        // 播放 NPC 台詞並等待完成
        yield return npc.SpeakCoroutine("stories", "story2-1");
        SubtitleDisplayManager.Instance.HideSubtitle();


        currentState = StoryState.WaitPlaceBowl;
        eventLocked = false;
        Debug.Log("[Story2] 等待玩家放置溫碗");
        // bowlScript.DetachAndFloat();
        theBowl.SwitchToState(ZeroGravityObject.ObjectState.Floating_Grabable);
        // 等待放碗trigger
        SubtitleDisplayManager.Instance.DisplayHint("hint2-1");
        yield return new WaitForSeconds(20f);
        SubtitleDisplayManager.Instance.HideHint();
        if (currentState == StoryState.WaitPlaceBowl) // 如果玩家完全沒反應就直接進入下一段
        {
            StartCoroutine(FindBowlTimeout());
        }
    }
    public IEnumerator GoToLookBowlSequence()
    {
        // currentState = StoryState.NPCTalking;
        // 顯示劇情與提示
        yield return new WaitForSeconds(3f);
        // SubtitleDisplayManager.Instance.DisplayStory("story3-1");
        // 播放 NPC 台詞並等待完成
        yield return npc.SpeakCoroutine("stories", "story3-1");
        SubtitleDisplayManager.Instance.HideSubtitle();

        currentState = StoryState.WaitLookBowl;
        eventLocked = false;
        Debug.Log("[Story3] 等待玩家凝視溫碗");
        // 等待視線trigger

        yield return new WaitForSeconds(10f);
        if (currentState == StoryState.WaitLookBowl) // 如果玩家完全沒反應就失敗
        {
            Notify(EventType.LookBowlFailed);
        }
    }
        public IEnumerator GoToPutBottle()
    {
        currentState = StoryState.NPCTalking;
        
        // SubtitleDisplayManager.Instance.DisplayStory("story4-1");
        yield return npc.SpeakCoroutine("stories", "story4-1");

        // SubtitleDisplayManager.Instance.DisplayStory("story4-2");
        yield return npc.SpeakCoroutine("stories", "story4-2");
        SubtitleDisplayManager.Instance.HideSubtitle();

        // 講完話後出現酒壺和提示
        SubtitleDisplayManager.Instance.DisplayHint("hint4-1");
        item.ShowItem();

        currentState = StoryState.WaitBottleIntoBowl;
        eventLocked = false;
        // 等待放酒壺trigger
        yield return new WaitForSeconds(30f);
        SubtitleDisplayManager.Instance.HideHint();
        if (currentState == StoryState.WaitBottleIntoBowl)
        {
            item.LockForAnimation();
            Notify(EventType.PutBottleIntoBowlFailed);
        }
    }
        public IEnumerator NPCDrinking()
    {
        eventLocked = true;
        currentState = StoryState.NPCTalking;

        // SubtitleDisplayManager.Instance.DisplayStory("story4-3");
        theBowl.SwitchToState(ZeroGravityObject.ObjectState.Floating_Locked);

        yield return npc.SpeakCoroutine("stories", "story4-3");
        SubtitleDisplayManager.Instance.HideSubtitle();

        // 酒壺飛到乾隆手上
        wineAnimationTester.TriggerBottleToHand();
        yield return new WaitForSeconds(1f); // 等酒壺飛行動畫結束
        animator.SetTrigger("drinking4-3");
        Debug.Log("乾隆喝酒");

        yield return new WaitForSeconds(4f);
        item.HideItem();
        // 到第五階段
        StartCoroutine(BowlAppreciate());
    }
        public IEnumerator BowlAppreciate()
    {
        // SubtitleDisplayManager.Instance.DisplayStory("story5-1");
        // animator.SetTrigger("taking5-1");
        yield return npc.SpeakCoroutine("stories", "story5-1");


        // SubtitleDisplayManager.Instance.DisplayStory("story5-2");

        // 碗飛回乾隆手裡
        // bowlScript.StartBowlSequence();
        theBowl.SwitchToState(ZeroGravityObject.ObjectState.AttachedToNPC, npcHand);
        animator.SetTrigger("appreciating5-3");

        yield return npc.SpeakCoroutine("stories", "story5-2");
        animator.SetTrigger("raising");

        SubtitleDisplayManager.Instance.HideSubtitle();

        currentState = StoryState.WaitGazeBowlClose;
        eventLocked = false;
        // 等待靠近欣賞trigger
        yield return new WaitForSeconds(3f);
        //溫碗離手
        // bowlScript.DetachAndFloat();
        // theBowl.SwitchToState(ZeroGravityObject.ObjectState.Floating_Locked);
        yield return new WaitForSeconds(12f);
        if (currentState == StoryState.WaitGazeBowlClose)
        {
            OnGazeBowlFailed();
        }
    }
    public IEnumerator NPCStartAppreciate()
    {
        // eventLocked = true;
        currentState = StoryState.NPCTalking;

        // SubtitleDisplayManager.Instance.DisplayStory("story5-3");
        // animator.SetTrigger("breathing");
        yield return npc.SpeakCoroutine("stories", "story5-3");
        SubtitleDisplayManager.Instance.HideSubtitle();

        // 到第六階段
        StartCoroutine(TurningBowl());
    }
    public IEnumerator TurningBowl()
    {
        // bowlScript.StartBowlSequence();
        // theBowl.SwitchToState(ZeroGravityObject.ObjectState.AttachedToNPC, npcHand);
        // SubtitleDisplayManager.Instance.DisplayStory("story6-1");
        yield return npc.SpeakCoroutine("stories", "story6-1");
        SubtitleDisplayManager.Instance.HideSubtitle();

        SubtitleDisplayManager.Instance.DisplayHint("hint6-1");
        // 切換成漂浮讓玩家可以拿
        // bowlScript.DetachAndFloat(); 
        animator.SetTrigger("taking6-1");
        theBowl.SwitchToState(ZeroGravityObject.ObjectState.Floating_Grabable);
        yield return new WaitForSeconds(6f);
        animator.SetTrigger("back");
        yield return new WaitForSeconds(2f);

        SubtitleDisplayManager.Instance.DisplayHint("hint6-2");
        yield return new WaitForSeconds(3f);
        SubtitleDisplayManager.Instance.HideHint();

        // SubtitleDisplayManager.Instance.DisplayStory("story6-2");
        animator.SetTrigger("point fake exhibit6-2");
        yield return npc.SpeakCoroutine("stories", "story6-2");
        SubtitleDisplayManager.Instance.HideSubtitle();

        // 到第七階段
        StartCoroutine(FinishStory());
    }
    public IEnumerator FinishStory()
    {
        // SubtitleDisplayManager.Instance.DisplayStory("story7-1");
        animator.SetTrigger("standstill7-1");
        yield return npc.SpeakCoroutine("stories", "story7-1");
        SubtitleDisplayManager.Instance.HideSubtitle();

        SubtitleDisplayManager.Instance.DisplayHint("hint7-1");

        currentState = StoryState.WaitBowlBack;
        eventLocked = false;
        // 等待trigger：玩家把溫碗放回原位
        // bowlScript.DetachAndFloat();
        // theBowl.SwitchToState(ZeroGravityObject.ObjectState.Floating_Grabable);

        yield return new WaitForSeconds(20f); // 等待30秒
        if (currentState == StoryState.WaitBowlBack) // 如果玩家完全沒反應就直接結束劇情
        {
            StartCoroutine(StoryEnding());
        }
    }
    public IEnumerator StoryEnding()
    {
        SubtitleDisplayManager.Instance.HideHint();

        theBowl.SwitchToState(ZeroGravityObject.ObjectState.AttachedToTable, putBowlTable);
        eventLocked = true;
        currentState = StoryState.NPCTalking;
        // SubtitleDisplayManager.Instance.DisplayStory("story7-2");
        // yield return npc.SpeakCoroutine("stories", "story7-2");
        // SubtitleDisplayManager.Instance.HideSubtitle();

        // 直接結束劇情並關閉乾隆場景
        currentState = StoryState.Finish;
        Finish();  

        yield return new WaitForSeconds(2f); // 等待2秒後開啟第八階段問卷
        currentState = StoryState.Phase8Survey;
        eventLocked = false;
        Notify(EventType.StartPhase8Survey);

        // // 等待玩家離開劇情區域
        // yield return new WaitForSeconds(60f); // 等待1分鐘
        // currentState = StoryState.WaitEnterZone; // 回到初始狀態，等待玩家再次進入
        // eventLocked = false;
    }
    public void Finish()
    {
        // 劇情結束 → 關閉乾隆場景
        if (proximityTrigger != null)
        {
            proximityTrigger.HidePrompt();
        }
        // 劇情結束，這裡可以放一些結束後的處理，例如顯示結局畫面、重置劇情等
        Debug.Log("Close Chianlong Scene.");
    }

    
    public IEnumerator Phase8Survey()
    {
        eventLocked = true;
        Debug.Log("Survey coroutine started");

        if (museumSurveyController == null)
        {
            Debug.LogError("museumSurveyController is NULL!");
        }
        else
        {
            Debug.Log("Calling museumSurveyController: StartPhase8Survey()");
            museumSurveyController.StartPhase8Survey();
        }

        yield return null;
    }






    // task success/failed methods
    public IEnumerator FindBowlTooFar()
    {
        if (isPlaying) yield break; // 已在執行就直接退出

        // eventLocked = false;
        isPlaying = true;

        // SubtitleDisplayManager.Instance.DisplayTask("task2_fail");
        yield return npc.SpeakCoroutine("tasks", "task2_fail");

        // 隱藏 Task 面板
        SubtitleDisplayManager.Instance.HideTask();

        isPlaying = false; // 結束後解除鎖
    }
            
        public IEnumerator FindBowlTimeout()
    {
        eventLocked = true;
        currentState = StoryState.NPCTalking;

        theBowl.SwitchToState(ZeroGravityObject.ObjectState.AttachedToTable, putBowlTable);
        // SubtitleDisplayManager.Instance.DisplayTask("task2_overtime");
        animator.SetTrigger("task 2 overtime");
        // 播放 NPC 台詞並等待完成
        yield return npc.SpeakCoroutine("tasks", "task2_overtime");

        // 隱藏 Task 面板
        SubtitleDisplayManager.Instance.HideTask();

        // animator.SetTrigger("flyingBowl");
        // 到story3
        // 欣賞碗的動畫
        // bowlScript.StartBowlSequence();
        theBowl.SwitchToState(ZeroGravityObject.ObjectState.AttachedToNPC, npcHand);
        animator.SetTrigger("16-17");
        yield return GoToLookBowlSequence();
    }
    public IEnumerator FindBowlSuccess()
    {
        eventLocked = true;
        currentState = StoryState.NPCTalking;

        // 顯示劇情與提示
        theBowl.SwitchToState(ZeroGravityObject.ObjectState.Floating_Locked);
        theBowl.SwitchToState(ZeroGravityObject.ObjectState.AttachedToTable, putBowlTable);
        // SubtitleDisplayManager.Instance.DisplayTask("task2_success");
        animator.SetTrigger("task 2 success");

        // 播放 NPC 台詞並等待完成
        yield return npc.SpeakCoroutine("tasks", "task2_success");
        yield return new WaitForSeconds(3f);

        // 隱藏 Task 面板
        SubtitleDisplayManager.Instance.HideTask();

        // animator.SetTrigger("flyingBowl");
        // 到story3
        // 欣賞碗的動畫
        // bowlScript.StartBowlSequence();
        theBowl.SwitchToState(ZeroGravityObject.ObjectState.AttachedToNPC, npcHand);
        animator.SetTrigger("16-17");
        yield return GoToLookBowlSequence();
    }
    private IEnumerator LookLongerCoroutine()
    {
        // eventLocked = false;
        currentState = StoryState.NPCTalking;

        // SubtitleDisplayManager.Instance.DisplayTask("task3_fail");
        yield return npc.SpeakCoroutine("tasks", "task3_fail");

        // 隱藏 Task 面板
        SubtitleDisplayManager.Instance.HideTask();
        currentState = StoryState.WaitLookBowl;

        allowLookBowlSuccessOnly = true; // 進入「只允許成功」

        yield return new WaitForSeconds(5f);

        eventLocked = true;
        allowLookBowlSuccessOnly = false;

        StartCoroutine(GoToPutBottle());
    }
    void OnLookBowlSuccess()
    {
        eventLocked = true;
        allowLookBowlSuccessOnly = false;

        if (lookLongerCoroutine != null)
        {
            StopCoroutine(lookLongerCoroutine);
            lookLongerCoroutine = null;
        }

        StartCoroutine(GoToPutBottle());
    }
    void OnLookBowlFailed()
    {
        // eventLocked = false;
        if (lookLongerCoroutine != null)
            StopCoroutine(lookLongerCoroutine);

        lookLongerCoroutine = StartCoroutine(LookLongerCoroutine());
    }

    private IEnumerator GazeLongerCoroutine()
    {
        currentState = StoryState.NPCTalking;
        // SubtitleDisplayManager.Instance.DisplayTask("task5_fail");
        yield return npc.SpeakCoroutine("tasks", "task5_fail");

        // 隱藏 Task 面板
        SubtitleDisplayManager.Instance.HideTask();
        currentState = StoryState.WaitGazeBowlClose;

        allowGazeBowlSuccessOnly = true; // 進入「只允許成功」

        yield return new WaitForSeconds(5f);

        eventLocked = true;
        allowGazeBowlSuccessOnly = false;

        StartCoroutine(NPCStartAppreciate());
    }
    void OnGazeBowlSuccess()
    {
        eventLocked = true;
        allowGazeBowlSuccessOnly = false;

        if (gazeLongerCoroutine != null)
        {
            StopCoroutine(gazeLongerCoroutine);
            gazeLongerCoroutine = null;
        }

        StartCoroutine(NPCStartAppreciate());
    }
    void OnGazeBowlFailed()
    {
        // eventLocked = false;
        if (gazeLongerCoroutine != null)
            StopCoroutine(gazeLongerCoroutine);

        gazeLongerCoroutine = StartCoroutine(GazeLongerCoroutine());
    }
}