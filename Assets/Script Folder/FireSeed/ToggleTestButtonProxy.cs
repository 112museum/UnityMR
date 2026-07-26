using UnityEngine;
using UnityEngine.EventSystems;

// 掛在 P1_TestButton / P2_TestButton 這種 UI 測試按鈕上，取代 EventTrigger 原本的
// Pointer Down / Pointer Up（按住式）行為，改成「點一下切換」：點第一下等同按住火種
// （呼叫 target.OnPressStart()），再點一下等同放開（呼叫 target.OnPressEnd()）。
// 單人測試雙人燒製時，兩顆按鈕不需要真的同時按住，依序點兩下讓兩邊都保持「持有」狀態即可。
//
// 使用方式：把這顆腳本掛到按鈕物件上，Target 欄位接對應的 FireSeedButton（FireSeed_P1 /
// FireSeed_P2 物件上的那個），然後把 EventTrigger 元件裡原本的 Pointer Down / Pointer Up
// 這兩筆事件刪掉，避免跟這裡的切換邏輯互相打架。
public class ToggleTestButtonProxy : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private FireSeedButton target;

    private bool _isHeld;

    public void OnPointerClick(PointerEventData eventData)
    {
        _isHeld = !_isHeld;

        if (_isHeld)
        {
            target.OnPressStart();
        }
        else
        {
            target.OnPressEnd();
        }
    }
}
