using UnityEngine;
using UnityEngine.Events;

// 火種的發光與按住偵測。不綁死框架，OnPressStart()/OnPressEnd() 開放給
// Inspector 手動接 XRI 的 Select Entered/Exited 或 MRTK3 的 Touch/Click 事件。
[RequireComponent(typeof(Renderer))]
public class FireSeedButton : MonoBehaviour
{
    [Header("外觀")]
    [SerializeField] private Color idleColor = new Color(1f, 0.4f, 0f) * 1f;      // 待機微光
    [SerializeField] private Color heldColor = new Color(1f, 0.6f, 0.1f) * 4f;    // 按住時的強光 (HDR 強度可自行調高)
    [SerializeField] private float idlePulseSpeed = 1.5f;
    [SerializeField] private float idlePulseMin = 0.3f;
    [SerializeField] private float idlePulseMax = 0.8f;
    [SerializeField] private float colorLerpSpeed = 8f;

    [Header("事件")]
    public UnityEvent onPressStart;
    public UnityEvent onPressEnd;

    private Renderer _renderer;
    private MaterialPropertyBlock _mpb;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public bool IsHeld { get; private set; }
    public float HoldTime { get; private set; } // 這顆火種被按住的累積秒數(本地端量測，網路同步交給另一支腳本)

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();

        // MaterialPropertyBlock 只能改 _EmissionColor 的數值，無法開關 shader 的 _EMISSION
        // keyword。材質沒在 Inspector 勾選 Emission 的話，這裡改色不會有任何視覺效果且不會報錯。
        Material sharedMat = _renderer.sharedMaterial;
        if (sharedMat != null && !sharedMat.IsKeywordEnabled("_EMISSION"))
        {
            Debug.LogWarning($"[FireSeedButton] '{gameObject.name}' 的材質 '{sharedMat.name}' 未啟用 Emission，" +
                              "請到材質 Inspector 勾選 Emission，否則發光效果不會顯示。", this);
        }
    }

    private void Update()
    {
        Color target;

        if (IsHeld)
        {
            HoldTime += Time.deltaTime;
            target = heldColor;
        }
        else
        {
            HoldTime = 0f;
            float t = (Mathf.Sin(Time.time * idlePulseSpeed) + 1f) * 0.5f;
            float pulse = Mathf.Lerp(idlePulseMin, idlePulseMax, t);
            target = idleColor * pulse;
        }

        // 用 MaterialPropertyBlock 改 Emission，避免每顆火種各自 Instance 一份材質
        _renderer.GetPropertyBlock(_mpb);
        Color current = _mpb.GetVector(EmissionColorId);
        Color lerped = Color.Lerp(current, target, Time.deltaTime * colorLerpSpeed);
        _mpb.SetColor(EmissionColorId, lerped);
        _renderer.SetPropertyBlock(_mpb);
    }

    // 掛到 XRI 的 Select Entered 或 MRTK3 的 Touch/Click 開始事件
    public void OnPressStart()
    {
        if (IsHeld) return;
        IsHeld = true;
        Debug.Log($"[FireSeedButton] '{gameObject.name}' pressed.");
        onPressStart?.Invoke();
    }

    // 掛到 XRI 的 Select Exited 或 MRTK3 的 Touch/Click 結束事件
    public void OnPressEnd()
    {
        if (!IsHeld) return;
        IsHeld = false;
        Debug.Log($"[FireSeedButton] '{gameObject.name}' released. HoldTime={HoldTime:F2}s");
        onPressEnd?.Invoke();
    }
}
