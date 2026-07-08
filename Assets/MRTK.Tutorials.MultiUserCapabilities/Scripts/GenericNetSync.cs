using Photon.Pun;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MRTK.Tutorials.MultiUserCapabilities
{
    public class GenericNetSync : MonoBehaviourPun, IPunObservable
    {
        [SerializeField] private bool isUser = default;

        private Camera mainCamera;
        private XRBaseInteractable interactable;

        private Vector3 networkLocalPosition;
        private Quaternion networkLocalRotation;

        private Vector3 startingLocalPosition;
        private Quaternion startingLocalRotation;

        void IPunObservable.OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            // Sync position/rotation relative to TableAnchor explicitly, rather than trusting
            // transform.localPosition. XRGrabInteractable un-parents the object from TableAnchor for
            // the duration of a grab (see retainTransformParent) and re-parents it on release, so
            // transform.localPosition briefly means "relative to scene root" instead of "relative to
            // TableAnchor" while held. Sending that raw value made the object appear to teleport to a
            // wrong (often very high) spot on every other client until the grabber let go.
            var anchor = TableAnchor.Instance != null ? TableAnchor.Instance.transform : null;

            if (stream.IsWriting)
            {
                if (anchor != null)
                {
                    stream.SendNext(anchor.InverseTransformPoint(transform.position));
                    stream.SendNext(Quaternion.Inverse(anchor.rotation) * transform.rotation);
                }
                else
                {
                    stream.SendNext(transform.position);
                    stream.SendNext(transform.rotation);
                }
            }
            else
            {
                networkLocalPosition = (Vector3) stream.ReceiveNext();
                networkLocalRotation = (Quaternion) stream.ReceiveNext();
            }
        }

        private void Start()
        {
            mainCamera = Camera.main;
            interactable = GetComponent<XRBaseInteractable>();

            if (isUser)
            {
                if (TableAnchor.Instance != null) transform.parent = FindObjectOfType<TableAnchor>().transform;
            }

            var trans = transform;
            startingLocalPosition = trans.localPosition;
            startingLocalRotation = trans.localRotation;

            networkLocalPosition = startingLocalPosition;
            networkLocalRotation = startingLocalRotation;
        }

        // private void FixedUpdate()
        private void Update()
        {
            // While this client's own interactor is holding the object, don't snap it back to the
            // last-received network pose. Ownership is requested on grab (see OwnershipHandler) but
            // the transfer round-trip isn't instant; fighting the grab during that window is what
            // caused the object to jump away and only settle into the grabber's hand a moment later.
            var isLocallyGrabbed = interactable != null && interactable.isSelected;

            if (!photonView.IsMine && !isLocallyGrabbed)
            {
                var trans = transform;
                var anchor = TableAnchor.Instance != null ? TableAnchor.Instance.transform : null;

                if (anchor != null)
                {
                    trans.position = anchor.TransformPoint(networkLocalPosition);
                    trans.rotation = anchor.rotation * networkLocalRotation;
                }
                else
                {
                    trans.position = networkLocalPosition;
                    trans.rotation = networkLocalRotation;
                }
            }

            if (photonView.IsMine && isUser)
            {
                var trans = transform;
                var mainCameraTransform = mainCamera.transform;
                trans.position = mainCameraTransform.position;
                trans.rotation = mainCameraTransform.rotation;
            }
        }
    }
}
