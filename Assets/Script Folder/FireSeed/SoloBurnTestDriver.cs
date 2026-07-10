using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 暫時測試用：不牽扯 Netcode，驗證「按住發光的火種 -> 進度條前進 -> 燒製完成」單人版本。
// 之後要測雙人網路版本時，把這支腳本移除，改用 KilnBurningManager 即可，邏輯是對應的。
public class SoloBurnTestDriver : MonoBehaviour
{
    [SerializeField] private FireSeedButton fireSeed;
    [SerializeField] private Slider progressSlider;

    [SerializeField] private float burnDurationSeconds = 15f;
    [SerializeField] private bool decayWhenReleased = true;
    [SerializeField] private float decayRate = 0.3f;

    public UnityEvent onBurnComplete;

    private float _progress;
    private bool _completeFired;

    private void Update()
    {
        if (_completeFired) return;

        if (fireSeed.IsHeld)
        {
            _progress += Time.deltaTime / burnDurationSeconds;
        }
        else if (decayWhenReleased)
        {
            _progress -= Time.deltaTime * decayRate;
        }

        _progress = Mathf.Clamp01(_progress);

        if (progressSlider != null)
        {
            progressSlider.value = _progress;
        }

        if (_progress >= 1f)
        {
            _completeFired = true;
            Debug.Log("[SoloBurnTestDriver] Burn complete (solo test).");
            onBurnComplete?.Invoke();
        }
    }
}
