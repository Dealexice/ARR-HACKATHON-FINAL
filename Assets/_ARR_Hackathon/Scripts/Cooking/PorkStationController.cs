using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

namespace YataiTogether
{
    /// <summary>
    /// Step 7: Chashu Pork Station Controller.
    /// Handles grabbing silicone pork in Zone B (Key 1 -> A / physical hand tracking).
    /// Fully compliant with Meta Interaction SDK:
    /// - Rigidbody (0.1kg, ContinuousDynamic)
    /// - Convex Collider
    /// - Grabbable & HandGrabInteractable
    /// - Key 1 snaps pork to hand; Key A / Enter drops into lamian_bowl.
    /// - Contact with lamian_bowl triggers RamenAssemblyReceiver.AddMeat().
    /// </summary>
    [DisallowMultipleComponent]
    public class PorkStationController : MonoBehaviour
    {
        public static PorkStationController Instance { get; private set; }

        [Header("Pork Prop & Spawn Settings")]
        [SerializeField] private GameObject meatProp;
        [SerializeField] private Transform trayRestPoint;
        [SerializeField] private Transform handTargetAnchor;

        [Header("State")]
        [SerializeField] private bool isPorkInHand = false;

        private Rigidbody _meatRb;
        private Grabbable _meatGrabbable;
        private HandGrabInteractable _meatHandGrab;
        private Vector3 _originalPos;
        private Quaternion _originalRot;

        public bool IsPorkInHand => isPorkInHand;
        public GameObject MeatProp => meatProp;

        public void Configure(GameObject meat, Transform tray, Transform hand)
        {
            meatProp = meat;
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
            ConfigurePorkPhysics();
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
            if (meatProp == null)
            {
                meatProp = GameObject.Find("meat");
            }

            if (trayRestPoint == null && meatProp != null)
            {
                _originalPos = meatProp.transform.position;
                _originalRot = meatProp.transform.rotation;
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
                    // Fallback to table height in front of counter anchor
                    GameObject anchor = GameObject.Find("POV_2_FoodStallCounterAnchor");
                    if (anchor != null)
                    {
                        handTargetAnchor = anchor.transform;
                    }
                }
            }
        }

        public void ConfigurePorkPhysics()
        {
            if (meatProp == null) return;

            meatProp.name = "meat";

            _meatRb = meatProp.GetComponent<Rigidbody>();
            if (_meatRb == null) _meatRb = meatProp.AddComponent<Rigidbody>();
            _meatRb.mass = 0.1f;
            _meatRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _meatRb.isKinematic = true;

            Collider col = meatProp.GetComponent<Collider>();
            if (col == null)
            {
                MeshCollider mc = meatProp.AddComponent<MeshCollider>();
                mc.convex = true;
            }

            _meatGrabbable = meatProp.GetComponent<Grabbable>();
            if (_meatGrabbable == null)
            {
                _meatGrabbable = meatProp.AddComponent<Grabbable>();
            }

            _meatHandGrab = meatProp.GetComponent<HandGrabInteractable>();
            if (_meatHandGrab == null)
            {
                _meatHandGrab = meatProp.AddComponent<HandGrabInteractable>();
                _meatHandGrab.InjectRigidbody(_meatRb);
                _meatHandGrab.InjectOptionalPointableElement(_meatGrabbable);
            }
        }

        private void HandleActionTapped(ParentAction action)
        {
            if (action == ParentAction.GrabPork)
            {
                if (YataiSessionDirector.Instance != null &&
                    (YataiSessionDirector.Instance.CurrentStep == CookingStep.Pork_Grab ||
                     YataiSessionDirector.Instance.CurrentStep == CookingStep.Pork_Release))
                {
                    SnapPorkToHand();
                }
            }
            else if (action == ParentAction.ReleaseIntoBowl)
            {
                if (YataiSessionDirector.Instance != null &&
                    (YataiSessionDirector.Instance.CurrentStep == CookingStep.Pork_Release ||
                     YataiSessionDirector.Instance.CurrentStep == CookingStep.Egg_Drop))
                {
                    ReleasePorkIntoBowl();
                }
            }
        }

        public void SnapPorkToHand()
        {
            if (meatProp == null) return;
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
                // Fallback table position near meat_bowl in Zone B
                targetPos = new Vector3(7.35f, 1.35f, 0.25f);
                targetRot = Quaternion.identity;
            }

            meatProp.transform.position = targetPos;
            meatProp.transform.rotation = targetRot;

            if (_meatRb != null)
            {
                // ponytail: Zero velocity only while dynamic before setting kinematic
                if (!_meatRb.isKinematic)
                {
                    _meatRb.linearVelocity = Vector3.zero;
                    _meatRb.angularVelocity = Vector3.zero;
                }
                _meatRb.isKinematic = true;
            }

            isPorkInHand = true;
            Debug.Log("[PorkStationController] Chashu pork snapped to hand! Ready to drop into bowl (Key A / Enter or physically touch bowl).");
        }

        public void ReleasePorkIntoBowl()
        {
            if (meatProp != null)
            {
                // If it's the loose meat in hand, hide or disable it
                meatProp.SetActive(false);
            }

            isPorkInHand = false;

            if (RamenAssemblyReceiver.Instance != null)
            {
                RamenAssemblyReceiver.Instance.AddMeat();
            }
        }
    }
}
