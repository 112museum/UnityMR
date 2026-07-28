using UnityEngine;
using UnityEngine.Events;

// 火種的按住偵測。不綁死框架，OnPressStart()/OnPressEnd() 開放給
// Inspector 手動接 UI Button 的 EventTrigger PointerDown/PointerUp 事件。
// 視覺呈現交給場景裡的 Procedural fire 特效物件，這支腳本只負責狀態與事件。
public class FireSeedButton : MonoBehaviour
{
    [Header("事件")]
    public UnityEvent onPressStart;
    public UnityEvent onPressEnd;

    public bool IsHeld { get; private set; }
    public float HoldTime { get; private set; } // 這顆火種被按住的累積秒數(本地端量測，網路同步交給另一支腳本)

    private void Update()
    {
        HoldTime = IsHeld ? HoldTime + Time.deltaTime : 0f;
    }

    // 掛到按鈕的按下事件
    public void OnPressStart()
    {
        if (IsHeld) return;
        IsHeld = true;
        Debug.Log($"[FireSeedButton] '{gameObject.name}' pressed.");
        onPressStart?.Invoke();
    }

    // 掛到按鈕的放開事件
    public void OnPressEnd()
    {
        if (!IsHeld) return;
        IsHeld = false;
        Debug.Log($"[FireSeedButton] '{gameObject.name}' released. HoldTime={HoldTime:F2}s");
        onPressEnd?.Invoke();
    }

    // 給網路同步腳本呼叫，用來反映「另一位玩家」在他們裝置上按住/放開的狀態。
    // 不會觸發 onPressStart/onPressEnd，避免遠端同步回來的狀態被誤當本地輸入又送一次網路封包。
    public void SetHeldExternally(bool held)
    {
        IsHeld = held;
    }
}
