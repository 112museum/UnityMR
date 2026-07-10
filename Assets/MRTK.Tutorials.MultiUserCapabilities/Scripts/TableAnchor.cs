using UnityEngine;

namespace MRTK.Tutorials.MultiUserCapabilities
{
    public class TableAnchor : MonoBehaviour
    {
        public static TableAnchor Instance;

        // Assigned in Awake (not Start) because other scripts (TableAnchorAsParent,
        // GenericNetSync) read TableAnchor.Instance from their own Start(), and Unity
        // does not guarantee Start() ordering between objects. Awake() across the whole
        // scene always finishes before any Start() runs, so this guarantees Instance is
        // ready by the time those consumers check it.
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                if (Instance == this) return;
                Destroy(Instance.gameObject);
                Instance = this;
            }
        }
    }
}
