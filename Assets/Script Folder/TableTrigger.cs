using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class TableTrigger : MonoBehaviour
{
    public Talker npc;
    public Animator animator;
    public string animation1; // 觸發第一個動畫
    public string animation2; // 觸發第二個動畫

    // 記錄每個 TableItem 上次觸發時間
    private Dictionary<TableItem, float> lastTriggerTime = new Dictionary<TableItem, float>();

    public float triggerCooldown = 5f; // 冷卻時間，秒

    private void OnTriggerEnter(Collider other)
    {
        TableItem item = other.GetComponent<TableItem>();
        if (item == null) return;

        // 檢查冷卻時間
        if (lastTriggerTime.TryGetValue(item, out float lastTime))
        {
            if (Time.time - lastTime < triggerCooldown)
            {
                // 還沒超過冷卻，忽略這次碰撞
                return;
            }
        }

        // 更新上次觸發時間
        lastTriggerTime[item] = Time.time;

        // 觸發事件
        switch (item.itemType)
        {
            case ItemType.LianHuaWan:
                Debug.Log("蓮花碗放到桌上了！");
                // npc.Speak("", "LianHuaWan");
                // // 加上觸發動畫
                // StartCoroutine(PlayAnimationsSequentially());
                break;

            case ItemType.ZhiChuiPing:
                Debug.Log("紙槌瓶放到桌上了！");
                npc.Speak("", "ZhiChuiPing");
                break;
        }
    }
    private IEnumerator PlayAnimationsSequentially()
    {
        yield return new WaitForSeconds(3f); // 等待幾秒
        // 先播放第一個動畫
        animator.SetTrigger(animation1);

        // 等待動畫播放完
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        // 注意：這裡要先等一幀，確保 Animator 更新狀態
        yield return null;

        // 等到動畫播放結束
        while (animator.GetCurrentAnimatorStateInfo(0).IsName(animation1) &&
            animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
        {
            yield return null;
        }

        // 播放第二個動畫
        animator.SetTrigger(animation2);
    }
}
