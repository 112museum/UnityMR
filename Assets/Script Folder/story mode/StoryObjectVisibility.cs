using UnityEngine;

// Toggles `target`'s active state when a story line's objectsToShow/objectsToHide (see
// DialogueLine in StoryChapterData.cs) lists this component's visibilityTag.
//
// This script's own GameObject must stay active the whole time — do NOT put it on the
// object being shown/hidden. Unity never runs Start()/OnEnable() on a GameObject that
// begins inactive, so a script living on that object could never subscribe in the first
// place; and even if it started active, disabling itself would trigger its own OnDisable
// and unsubscribe it, so it could never be shown again. Put this on an always-active
// holder (a parent, or a dedicated empty object) and point `target` at the real object.
public class StoryObjectVisibility : MonoBehaviour
{
    public string visibilityTag;

    [Tooltip("Object to show/hide. Defaults to this GameObject if left empty — only safe if this GameObject itself starts active and nothing else disables it.")]
    public GameObject target;

    private void Awake()
    {
        target ??= gameObject;
    }

    // StoryModeManager.Instance is set in ITS OWN Awake(), and Unity doesn't guarantee
    // this object's OnEnable() runs after that — so subscribing here instead of OnEnable()
    // is required: Unity always finishes every object's Awake() before any object's Start().
    private void Start()
    {
        if (StoryModeManager.Instance != null)
        {
            StoryModeManager.Instance.OnShowObjectTag += HandleShow;
            StoryModeManager.Instance.OnHideObjectTag += HandleHide;
        }
        else
        {
            Debug.LogError("[StoryObjectVisibility] StoryModeManager.Instance is still null in Start() — is StoryModeManager in the scene?");
        }
    }

    // Unsubscribing on destroy (not disable) since this component's own GameObject is
    // meant to stay active for the scene's lifetime — see the class comment above.
    private void OnDestroy()
    {
        if (StoryModeManager.Instance != null)
        {
            StoryModeManager.Instance.OnShowObjectTag -= HandleShow;
            StoryModeManager.Instance.OnHideObjectTag -= HandleHide;
        }
    }

    private void HandleShow(string tag)
    {
        if (tag == visibilityTag) target.SetActive(true);
    }

    private void HandleHide(string tag)
    {
        if (tag == visibilityTag) target.SetActive(false);
    }
}
