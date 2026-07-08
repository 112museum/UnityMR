using UnityEngine;
using System.Collections;
using System.Reflection;

public class VirtualStoryManager : MonoBehaviour
{
    public static VirtualStoryManager Instance;
    public ProximityTrigger proximityTrigger;
    
        // 定義劇情狀態

    [Header("Current State")]
    public StoryState currentState;

    [Header("System References")]
    public WineAnimationTester wineAnimationTester;
    public ZeroGravityObject theBowl;
    public MuseumSurveyController museumSurveyController;
    public Transform npcHand;
    public Talker npc;
    public GameObject bowl;
    public GameObject bottle;
    public GameObject giantBowl;
    public Animator animator;
    // public Animation bottleAnimation;
    public Animation bowlAnimation;
    public ARUIController triggerButton;
    // public FloatingPickupItem item;
    // private bool allowLookBowlSuccessOnly = false;
    // private bool allowGazeBowlSuccessOnly = false;
    // private Coroutine lookLongerCoroutine;
    // private Coroutine gazeLongerCoroutine;
    private bool eventLocked = false;
    // private bool isPlaying = false; // 用於防止重複執行同一個任務
    
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
        // subtitleDisplayManager.ShowHint("請靠近桌子");
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
        // if (allowLookBowlSuccessOnly)
        // {
        //     if (eventType == EventType.LookBowlSuccess)
        //     {
        //         Notify(EventType.LookBowlSuccess);
        //     }
        //     return; // 擋掉所有其他事件
        // }
        // if (allowGazeBowlSuccessOnly)
        // {
        //     if (eventType == EventType.GazeBowlCloseSuccess)
        //     {
        //         StartCoroutine(NPCStartAppreciate());

        //     }
        //     return; // 擋掉所有其他事件
        // }

        switch (currentState)
        {
            case StoryState.NPCTalking:
                // NPC 講話階段，不接受任何事件
                Debug.Log("NPC is talking. Ignoring all events.");
                return; // 直接返回
            // EventType觸發劇情事件名稱不變
            case StoryState.WaitEnterZone:
                switch (eventType)
                {
                    case EventType.EnterStoryZone:
                        eventLocked = true;
                        StartCoroutine(DelayedStartIntro());
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
                        StartCoroutine(FindBowlWrong());
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
                    // case EventType.LookBowlFailed:
                    //     OnLookBowlFailed();
                    //     break;
                }
                break;

            // case StoryState.WaitBottleIntoBowl:
            //     switch (eventType)
            //     {
            //         case EventType.PutBottleIntoBowlSuccess:
            //             StartCoroutine(NPCDrinking());
            //             break;
            //         case EventType.PutBottleIntoBowlFailed:
            //             // animator.SetTrigger("flyingBottle");
            //             StartCoroutine(NPCDrinking());
            //             break;
            //     }
            //     break;

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
        Debug.Log("Player Press Start Button");

        // 確保乾隆(talker)先出現
        if (npc != null)
        {
            npc.gameObject.SetActive(true);
            proximityTrigger.ShowPrompt();
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

        // ===== 第一段 =====

        // SubtitleDisplayManager.Instance.DisplayStory("story1-1");
        
        // 第一階段皆維持同一動作(站立)
        // animator.SetTrigger("breathing");

        yield return npc.SpeakCoroutine("stories", "story1-1");


        // ===== 第二段 =====

        // SubtitleDisplayManager.Instance.DisplayStory("story1-2");

        animator.SetTrigger("look around1-2");

        yield return npc.SpeakCoroutine("stories", "story1-2");


        // ===== 第三段 =====

        // SubtitleDisplayManager.Instance.DisplayStory("story1-3");

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

        // 事件名稱不變(等待玩家抓碗)
        currentState = StoryState.WaitPlaceBowl;
        eventLocked = false;
        Debug.Log("[Story2] 等待玩家抓到溫碗");
        SubtitleDisplayManager.Instance.DisplayHintText("[系統提示] 找到三希堂中的溫碗後，請用手勢抓握溫碗");
        theBowl.SwitchToState(ZeroGravityObject.ObjectState.Floating_Grabable);

        // 等待抓碗trigger
        yield return new WaitForSeconds(20f);
        if (currentState == StoryState.WaitPlaceBowl) // 如果玩家沒抓到就進入超時
        {
            SubtitleDisplayManager.Instance.HideHint();
            StartCoroutine(FindBowlTimeout());
        }
    }
    public IEnumerator GoToLookBowlSequence()
    {
        // currentState = StoryState.NPCTalking;

        yield return new WaitForSeconds(2f); // 等待5秒
        
        // 顯示劇情與提示
        // SubtitleDisplayManager.Instance.DisplayStory("story3-1");
        // animator.SetTrigger("breathing");

        // 播放 NPC 台詞並等待完成
        yield return npc.SpeakCoroutine("stories", "story3-1");
        SubtitleDisplayManager.Instance.HideSubtitle();

        currentState = StoryState.WaitLookBowl;
        eventLocked = false;
        Debug.Log("[Story3] 玩家欣賞溫碗5秒");

        // 5秒後直接到下一個酒壺劇情
        yield return new WaitForSeconds(5f);
        Notify(EventType.LookBowlSuccess); // 直接觸發成功，進入下一段劇情
    }
        public IEnumerator GoToPutBottle()
    {
        currentState = StoryState.NPCTalking;
        
        bottle.SetActive(true);

        // SubtitleDisplayManager.Instance.DisplayStory("story4-1");
        yield return npc.SpeakCoroutine("stories", "story4-1");

        wineAnimationTester.TriggerBottleToBowl();
        // SubtitleDisplayManager.Instance.DisplayStory("story4-2");
        yield return npc.SpeakCoroutine("stories", "story4-2");
        SubtitleDisplayManager.Instance.HideSubtitle();

        theBowl.SwitchToState(ZeroGravityObject.ObjectState.Floating_Locked);
        animator.SetTrigger("back");
        // 到第四階段：NPC喝酒
        StartCoroutine(NPCDrinking());
    }
        public IEnumerator NPCDrinking()
    {
        // eventLocked = true;
        currentState = StoryState.NPCTalking;

        // SubtitleDisplayManager.Instance.DisplayStory("story4-3");
        yield return npc.SpeakCoroutine("stories", "story4-3");
        SubtitleDisplayManager.Instance.HideSubtitle();

        wineAnimationTester.TriggerBottleToHand();
        yield return new WaitForSeconds(1f);
        animator.SetTrigger("drinking4-3");
        yield return new WaitForSeconds(3f);
        bottle.SetActive(false);

        // 到第五階段
        StartCoroutine(BowlAppreciate());
    }
        public IEnumerator BowlAppreciate()
    {
        // SubtitleDisplayManager.Instance.DisplayStory("story5-1");
        // animator.SetTrigger("standing");
        yield return npc.SpeakCoroutine("stories", "story5-1");

        // 碗飛回乾隆手裡
        // bowlScript.StartBowlSequence();
        theBowl.SwitchToState(ZeroGravityObject.ObjectState.AttachedToNPC, npcHand);
        animator.SetTrigger("appreciating5-3");
        // SubtitleDisplayManager.Instance.DisplayStory("story5-2");
        yield return npc.SpeakCoroutine("stories", "story5-2");

        animator.SetTrigger("raising");
        SubtitleDisplayManager.Instance.HideSubtitle();

        currentState = StoryState.WaitGazeBowlClose;
        eventLocked = false;

        // 等5秒之後下一段看碗底劇情
        yield return new WaitForSeconds(7f);
        Notify(EventType.GazeBowlCloseSuccess); // 直接觸發成功，進入下一段劇情
    }
    public IEnumerator NPCStartAppreciate()
    {
        // eventLocked = true;
        currentState = StoryState.NPCTalking;

        // SubtitleDisplayManager.Instance.DisplayStory("story5-3");
        // animator.SetTrigger("breathing");
        yield return npc.SpeakCoroutine("stories", "story5-3");
        SubtitleDisplayManager.Instance.HideSubtitle();

        bowl.SetActive(false);
        // raising -> standing
        animator.SetTrigger("back");

        // 到第六階段
        StartCoroutine(TurningBowl());
    }
    public IEnumerator TurningBowl()
    {
        giantBowl.SetActive(true);
        // [動畫] 畫面左方溫碗翻轉成底部畫面
        bowlAnimation.GetComponent<Animation>().Play("bowl show bottom");
        
        // SubtitleDisplayManager.Instance.DisplayStory("story6-1");
        yield return npc.SpeakCoroutine("stories", "story6-1");

        // SubtitleDisplayManager.Instance.DisplayStory("story6-2");
        // animator.SetTrigger("指向虛擬溫碗");
        yield return npc.SpeakCoroutine("stories", "story6-2");
        SubtitleDisplayManager.Instance.HideSubtitle();
        // 到第七階段
        StartCoroutine(FinishStory());
    }
    public IEnumerator FinishStory()
    {
        giantBowl.SetActive(false);
        // SubtitleDisplayManager.Instance.DisplayStory("story7-1");
        animator.SetTrigger("standstill7-1");
        yield return npc.SpeakCoroutine("stories", "story7-1");
        SubtitleDisplayManager.Instance.HideSubtitle();

        SubtitleDisplayManager.Instance.DisplayHintText("[系統提示] 觀賞完，請點選「結束體驗」就能結束體驗！或等待30秒系統將自動結束體驗！");

        // 事件名稱依然不改(等待放碗 -> 等待點按鈕)
        currentState = StoryState.WaitBowlBack;
        eventLocked = false;
        // 等待trigger：玩家把溫碗點按鈕結束體驗
        triggerButton.EndExperienceButtonActive(true);
        yield return new WaitForSeconds(20f); // 等待30秒
        if (currentState == StoryState.WaitBowlBack) // 如果玩家完全沒反應就直接結束劇情
        {
            triggerButton.EndExperienceButtonActive(false);
            StartCoroutine(StoryEnding());
        }
    }
    public IEnumerator StoryEnding()
    {
        eventLocked = true;
        currentState = StoryState.NPCTalking;
        SubtitleDisplayManager.Instance.HideHint();
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
    public IEnumerator FindBowlWrong()
    {
        eventLocked = true;
        currentState = StoryState.NPCTalking;

        // SubtitleDisplayManager.Instance.DisplayTask("task2_fail_Virtual");
        yield return npc.SpeakCoroutine("tasks", "task2_fail_Virtual");

        // 隱藏 Task 面板
        SubtitleDisplayManager.Instance.HideTask();

        // 等待玩家再次嘗試
        currentState = StoryState.WaitPlaceBowl;
        eventLocked = false;
    }
            
        public IEnumerator FindBowlTimeout()
    {
        eventLocked = true;
        currentState = StoryState.NPCTalking;
        theBowl.SwitchToState(ZeroGravityObject.ObjectState.Floating_Locked);

        // SubtitleDisplayManager.Instance.DisplayTask("task2_overtime");
        // animator.SetTrigger("指向虛擬溫碗");
        animator.SetTrigger("point fake_F");

        // 播放 NPC 台詞並等待完成
        yield return npc.SpeakCoroutine("tasks", "task2_overtime");
        theBowl.SwitchToState(ZeroGravityObject.ObjectState.AttachedToNPC, npcHand);

        // 隱藏 Task 面板
        SubtitleDisplayManager.Instance.HideTask();

        // 到story3
        animator.SetTrigger("raising_virtual");
        yield return new WaitForSeconds(2f);
        yield return GoToLookBowlSequence();
    }
    public IEnumerator FindBowlSuccess()
    {
        eventLocked = true;
        currentState = StoryState.NPCTalking;
        theBowl.SwitchToState(ZeroGravityObject.ObjectState.Floating_Locked);

        // 顯示劇情與提示
        SubtitleDisplayManager.Instance.HideHint();
        // SubtitleDisplayManager.Instance.DisplayTask("task2_success");        
        animator.SetTrigger("point fake_S");

        // 播放 NPC 台詞並等待完成
        yield return npc.SpeakCoroutine("tasks", "task2_success");

        // 隱藏 Task 面板
        SubtitleDisplayManager.Instance.HideTask();

        // 溫碗飛到乾隆手上
        theBowl.SwitchToState(ZeroGravityObject.ObjectState.AttachedToNPC, npcHand);
        animator.SetTrigger("raising_virtual");
        yield return new WaitForSeconds(2f);
        yield return GoToLookBowlSequence();
    }
    // private IEnumerator LookLongerCoroutine()
    // {
    //     // eventLocked = false;
    //     allowLookBowlSuccessOnly = true; // 進入「只允許成功」

    //     SubtitleDisplayManager.Instance.DisplayTask("task3_fail");
    //     yield return npc.SpeakCoroutine("tasks", "task3_fail");

    //     // 隱藏 Task 面板
    //     SubtitleDisplayManager.Instance.HideTask();

    //     yield return new WaitForSeconds(5f);

    //     eventLocked = true;
    //     allowLookBowlSuccessOnly = false;

    //     StartCoroutine(GoToPutBottle());
    // }
    void OnLookBowlSuccess()
    {
        eventLocked = true;
        // allowLookBowlSuccessOnly = false;

        // if (lookLongerCoroutine != null)
        // {
        //     StopCoroutine(lookLongerCoroutine);
        //     lookLongerCoroutine = null;
        // }

        StartCoroutine(GoToPutBottle());
    }
    // void OnLookBowlFailed()
    // {
    //     // eventLocked = false;
    //     if (lookLongerCoroutine != null)
    //         StopCoroutine(lookLongerCoroutine);

    //     lookLongerCoroutine = StartCoroutine(LookLongerCoroutine());
    // }

    // private IEnumerator GazeLongerCoroutine()
    // {
    //     allowGazeBowlSuccessOnly = true; // 進入「只允許成功」

    //     SubtitleDisplayManager.Instance.DisplayTask("task5_fail");
    //     yield return npc.SpeakCoroutine("tasks", "task5_fail");

    //     // 隱藏 Task 面板
    //     SubtitleDisplayManager.Instance.HideTask();

    //     yield return new WaitForSeconds(5f);

    //     eventLocked = true;
    //     allowGazeBowlSuccessOnly = false;

    //     StartCoroutine(NPCStartAppreciate());
    // }
    void OnGazeBowlSuccess()
    {
        eventLocked = true;
        // allowGazeBowlSuccessOnly = false;

        // if (gazeLongerCoroutine != null)
        // {
        //     StopCoroutine(gazeLongerCoroutine);
        //     gazeLongerCoroutine = null;
        // }

        StartCoroutine(NPCStartAppreciate());
    }
    // void OnGazeBowlFailed()
    // {
    //     // eventLocked = false;
    //     if (gazeLongerCoroutine != null)
    //         StopCoroutine(gazeLongerCoroutine);

    //     gazeLongerCoroutine = StartCoroutine(GazeLongerCoroutine());
    // }
}