using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

namespace YataiTogether
{
    /// <summary>
    /// Step 9: Green Scallions / Onions Station Controller.
    /// Handles grabbing scallions in Zone B (Key 3 -> A / physical hand tracking).
    /// Fully compliant with Meta Interaction SDK Building Blocks standards:
    /// - Rigidbody (0.1kg, ContinuousDynamic, kinematic while resting/held)
    /// - Convex MeshCollider
    /// - Grabbable & HandGrabInteractable
    /// - Key 3 snaps onions to hand; Key A / Enter releases into lamian_bowl.
    /// - Physical contact with lamian_bowl triggers RamenAssemblyReceiver.AddOnions().
    /// </summary>
    [DisallowMultipleComponent]
    public class OnionStationController : MonoBehaviour
    {
        public static OnionStationController Instance { get; private set; }

        [Header("Onion Prop & Spawn Settings")]
        [SerializeField] private GameObject onionProp;
        [SerializeField] private Transform trayRestPoint;
        [SerializeField] private Transform handTargetAnchor;

        [Header("State")]
        [SerializeField] private bool isOnionsInHand = false;

        private Rigidbody _onionRb;
        private Grabbable _onionGrabbable;
        private HandGrabInteractable _onionHandGrab;

        public bool IsOnionsInHand => isOnionsInHand;
        public GameObject OnionProp => onionProp;

        public void Configure(GameObject onion, Transform tray, Transform hand)
        {
            onionProp = onion;
            trayRestPoint = tray;
            handTargetAnchor = hand;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            FindReferencesIfNull();
            ConfigureOnionPhysics();
        }

        private void OnEnable()
        {
            if (ParentHotkeyRouter.Instance != null)
            {
                ParentHotkeyRouter.Instance.ActionTapped += HandleActionTapped;
            }
        }

        private void Start()
        {
            if (ParentHotkeyRouter.Instance != null)
            {
                ParentHotkeyRouter.Instance.ActionTapped -= HandleActionTapped;
                ParentHotkeyRouter.Instance.ActionTapped += HandleActionTapped;
            }
            FindHandAnchor();
        }

        private void OnDisable()
        {
            if (ParentHotkeyRouter.Instance != null)
            {
                ParentHotkeyRouter.Instance.ActionTapped -= HandleActionTapped;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void FindReferencesIfNull()
        {
            if (onionProp == null)
            {
                onionProp = GameObject.Find("onion");
            }

            if (trayRestPoint == null && onionProp != null)
            {
                trayRestPoint = onionProp.transform.parent;
            }
        }

        private void FindHandAnchor()
        {
            if (handTargetAnchor == null)
            {
                GameObject rig = GameObject.Find("[BuildingBlock] Camera Rig");
                if (rig != null)
                {
                    Transform tracking = rig.transform.Find("TrackingSpace");
                    if (tracking != null)
                    {
                        handTargetAnchor = tracking.Find("RightHandAnchor");
                    }
                }

                if (handTargetAnchor == null)
                {
                    GameObject anchor = GameObject.Find("POV_2_FoodStallCounterAnchor");
                    if (anchor != null)
                    {
                        handTargetAnchor = anchor.transform;
                    }
                }
            }
        }

        public void ConfigureOnionPhysics()
        {
            if (onionProp == null) return;

            onionProp.name = "onion";

            _onionRb = onionProp.GetComponent<Rigidbody>();
            if (_onionRb == null) _onionRb = onionProp.AddComponent<Rigidbody>();
            _onionRb.mass = 0.1f;
            _onionRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _onionRb.isKinematic = true;

            Collider col = onionProp.GetComponent<Collider>();
            if (col == null)
            {
                MeshCollider mc = onionProp.AddComponent<MeshCollider>();
                mc.convex = true;
            }

            _onionGrabbable = onionProp.GetComponent<Grabbable>();
            if (_onionGrabbable == null)
            {
                _onionGrabbable = onionProp.AddComponent<Grabbable>();
            }

            _onionHandGrab = onionProp.GetComponent<HandGrabInteractable>();
            if (_onionHandGrab == null)
            {
                _onionHandGrab = onionProp.AddComponent<HandGrabInteractable>();
                _onionHandGrab.InjectRigidbody(_onionRb);
                _onionHandGrab.InjectOptionalPointableElement(_onionGrabbable);
            }
        }

        private void HandleActionTapped(ParentAction action)
        {
            if (action == ParentAction.GrabOnions)
            {
                if (YataiSessionDirector.Instance != null &&
                    (YataiSessionDirector.Instance.CurrentStep == CookingStep.Onions_Grab ||
                     YataiSessionDirector.Instance.CurrentStep == CookingStep.Onions_Release))
                {
                    SnapOnionsToHand();
                }
            }
            else if (action == ParentAction.ReleaseIntoBowl)
            {
                if (YataiSessionDirector.Instance != null &&
                    (YataiSessionDirector.Instance.CurrentStep == CookingStep.Onions_Release ||
                     YataiSessionDirector.Instance.CurrentStep == CookingStep.Nori_Hold))
                {
                    ReleaseOnionsIntoBowl();
                }
            }
        }

        public void SnapOnionsToHand()
        {
            if (onionProp == null) return;
            FindHandAnchor();

            Vector3 targetPos;
            Quaternion targetRot;

            if (handTargetAnchor != null)
            {
                targetPos = handTargetAnchor.position + handTargetAnchor.forward * 0.12f;
                targetRot = handTargetAnchor.rotation;
            }
            else
            {
                // Fallback table position near onion tray in Zone B
                targetPos = new Vector3(7.35f, 1.35f, 0.35f);
                targetRot = Quaternion.identity;
            }

            onionProp.transform.position = targetPos;
            onionProp.transform.rotation = targetRot;

            if (_onionRb != null)
            {
                // ponytail: Zero velocity only while dynamic before setting kinematic
                if (!_onionRb.isKinematic)
                {
                    _onionRb.linearVelocity = Vector3.zero;
                    _onionRb.angularVelocity = Vector3.zero;
                }
                _onionRb.isKinematic = true;
            }

            isOnionsInHand = true;
            Debug.Log("[OnionStationController] Green onions snapped to hand! Ready to drop into bowl (Key A / Enter or physically touch bowl).");
        }

        public void ReleaseOnionsIntoBowl()
        {
            if (onionProp != null)
            {
                onionProp.SetActive(false);
            }

            isOnionsInHand = false;

            if (RamenAssemblyReceiver.Instance != null)
            {
                RamenAssemblyReceiver.Instance.AddOnions();
            }
        }
    }
}
