using UnityEngine;

// 掛在桌上每一顆顏色按鈕上。按鈕的按下事件（UI Button 的 OnClick、XRI Poke/Simple Interactable 的
// Select Entered、EventTrigger 的 PointerDown 都可以）在 Inspector 接到 Press()。
// 不用自己拉 manager：PhotonKilnBurningManager 在 Awake() 會依 buttons 陣列把自己跟索引綁進來，
// 所以按鈕只要放進 manager 的 buttons 陣列就好。
public class ColorSequenceButton : MonoBehaviour
{
    [SerializeField] private Color color = Color.red;

    [Tooltip("要自動染成這個顏色的外觀；留空就不染色（例如模型材質本身已經是這個顏色）")]
    [SerializeField] private Renderer[] tintRenderers;

    // 序列顯示器閃爍時用的就是這個顏色，跟按鈕外觀是同一個來源，不會對不上
    public Color Color => color;

    private PhotonKilnBurningManager _manager;
    private int _index;

    public void Bind(PhotonKilnBurningManager manager, int index)
    {
        _manager = manager;
        _index = index;

        if (tintRenderers == null) return;
        foreach (var r in tintRenderers)
        {
            if (r != null) r.material.color = color;
        }
    }

    public void Press()
    {
        if (_manager == null)
        {
            Debug.LogWarning($"[ColorSequenceButton] '{gameObject.name}' 沒有被放進 PhotonKilnBurningManager 的 buttons 陣列，按了不會有反應。");
            return;
        }
        _manager.SubmitPress(_index);
    }
}
