// 這個文件功能是讓多人在不同空間下也可以同步一樣場景，
// 但是 AnchorModuleScript 在 MRTK3 已經被刪除所以先全文件註解
#if false
using UnityEngine;

namespace MRTK.Tutorials.MultiUserCapabilities
{
    public class SharingModuleScript : MonoBehaviour
    {
        private AnchorModuleScript anchorModuleScript;

        private void Start()
        {
            anchorModuleScript = GetComponent<AnchorModuleScript>();
        }

        public void ShareAzureAnchor()
        {
            Debug.Log("\nSharingModuleScript.ShareAzureAnchor()");

            GenericNetworkManager.Instance.azureAnchorId = anchorModuleScript.currentAzureAnchorID;
            Debug.Log("GenericNetworkManager.Instance.azureAnchorId: " + GenericNetworkManager.Instance.azureAnchorId);

            var pvLocalUser = GenericNetworkManager.Instance.localUser.gameObject;
            var pu = pvLocalUser.gameObject.GetComponent<PhotonUser>();
            pu.ShareAzureAnchorId();
        }

        public void GetAzureAnchor()
        {
            Debug.Log("\nSharingModuleScript.GetAzureAnchor()");
            Debug.Log("GenericNetworkManager.Instance.azureAnchorId: " + GenericNetworkManager.Instance.azureAnchorId);

            anchorModuleScript.FindAzureAnchor(GenericNetworkManager.Instance.azureAnchorId);
        }
    }
}
#endif
