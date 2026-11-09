using System.Collections;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

namespace YataiTogether
{
    /// <summary>
    /// Step 8: Ajitama Egg Drop & Assembly Controller.
    /// Handles palm-up supination over egg_bowl (Keys 2 -> A / physical hand tracking).
    /// Fully compliant with Meta Interaction SDK:
    /// - egg_bowl station with overhead chute drop point (timed ~0.18s gravity drop into palm)
    /// - egg prop with Rigidbody (0.1kg, ContinuousDynamic), convex Collider, Grabbable, and HandGrabInteractable
    /// - Key 2 drops egg from chute onto palm; Key A / Enter drops egg into lamian_bowl
    /// - Contact with lamian_bowl triggers RamenAssemblyReceiver.AddEgg()
    /// </summary>
    [DisallowMultipleComponent]
    public class EggStationController : MonoBehaviour
    {
        public static EggStationController Instance { get; private set; }

        [Header("Egg Prop & Chute")]
        [SerializeField] private GameObject eggProp;
        [SerializeField] private Transform chuteDropPoint;
        [SerializeField] private Transform handTargetAnchor;
        [SerializeField] private float dropDuration = 0.18f;

        [Header("State")]
        [SerializeField] private bool isEggDropped = false;
        [SerializeField] private bool isEggInHand = false;

        private Rigidbody _eggRb;
        private Grabbable _eggGrabbable;
        private HandGrabInteractable _eggHandGrab;
        private Coroutine _dropRoutine;

        public bool IsEggDropped => isEggDropped;
        public bool IsEggInHand => isEggInHand;
        public GameObject EggProp => eggProp;

        public void Configure(GameObject egg, Transform chute, Transform hand)
        {
            eggProp = egg;
            chuteDropPoint = chute;
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
            ConfigureEggPhysics();
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
            if (eggProp == null)
            {
                eggProp = GameObject.Find("egg");
            }

            if (chuteDropPoint == null)
            {
                Transform found = transform.Find("chute_drop_point");
                if (found != null)
                {
                    chuteDropPoint = found;
                }
                else
                {
                    GameObject chuteGo = new GameObject("chute_drop_point");
                    chuteGo.transform.SetParent(transform, false);
                    chuteGo.transform.localPosition = new Vector3(0f, 0.22f, 0f);
                    chuteDropPoint = chuteGo.transform;
                }
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

        public void ConfigureEggPhysics()
        {
            if (eggProp == null) return;

            eggProp.name = "egg";

            _eggRb = eggProp.GetComponent<Rigidbody>();
            if (_eggRb == null) _eggRb = eggProp.AddComponent<Rigidbody>();
            _eggRb.mass = 0.1f;
            _eggRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _eggRb.isKinematic = true;

            Collider col = eggProp.GetComponent<Collider>();
            if (col == null)
            {
                SphereCollider sc = eggProp.AddComponent<SphereCollider>();
                sc.radius = 0.010f;
            }
            else if (col is SphereCollider existingSc)
            {
                existingSc.radius = 0.010f;
            }

            _eggGrabbable = eggProp.GetComponent<Grabbable>();
            if (_eggGrabbable == null)
            {
                _eggGrabbable = eggProp.AddComponent<Grabbable>();
            }

            _eggHandGrab = eggProp.GetComponent<HandGrabInteractable>();
            if (_eggHandGrab == null)
            {
                _eggHandGrab = eggProp.AddComponent<HandGrabInteractable>();
                _eggHandGrab.InjectRigidbody(_eggRb);
                _eggHandGrab.InjectOptionalPointableElement(_eggGrabbable);
            }
        }

        private void HandleActionTapped(ParentAction action)
        {
            if (action == ParentAction.DropEggIntoPalm)
            {
                if (YataiSessionDirector.Instance != null &&
                    (YataiSessionDirector.Instance.CurrentStep == CookingStep.Egg_Drop ||
                     YataiSessionDirector.Instance.CurrentStep == CookingStep.Egg_Release))
                {
                    DropEggFromChute();
                }
            }
            else if (action == ParentAction.ReleaseIntoBowl)
            {
                if (YataiSessionDirector.Instance != null &&
                    (YataiSessionDirector.Instance.CurrentStep == CookingStep.Egg_Release ||
                     YataiSessionDirector.Instance.CurrentStep == CookingStep.Onions_Grab))
                {
                    ReleaseEggIntoBowl();
                }
            }
        }

        public void DropEggFromChute()
        {
            if (eggProp == null) return;
            FindHandAnchor();

            Vector3 startPos = chuteDropPoint != null ? chuteDropPoint.position : transform.position + Vector3.up * 0.22f;
            Vector3 endPos;

            if (handTargetAnchor != null)
            {
                endPos = handTargetAnchor.position + handTargetAnchor.forward * 0.12f;
            }
            else
            {
                // Fallback table position near egg_bowl
                endPos = new Vector3(7.35f, 1.35f, 0.05f);
            }

            eggProp.SetActive(true);
            eggProp.transform.position = startPos;

            if (_dropRoutine != null) StopCoroutine(_dropRoutine);
            _dropRoutine = StartCoroutine(EggDropRoutine(startPos, endPos));
        }

        private IEnumerator EggDropRoutine(Vector3 start, Vector3 end)
        {
            float elapsed = 0f;
            isEggDropped = true;

            if (eggProp == null) yield break;

            if (_eggRb != null)
            {
                _eggRb.isKinematic = true;
            }

            while (elapsed < dropDuration)
            {
                if (eggProp == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dropDuration);
                // Ease in quad for gravity drop feel
                float easeT = t * t;
                eggProp.transform.position = Vector3.Lerp(start, end, easeT);
                yield return null;
            }

            if (eggProp != null)
            {
                eggProp.transform.position = end;
                if (_eggRb != null)
                {
                    // ponytail: Zero velocity only while dynamic before setting kinematic
                    if (!_eggRb.isKinematic)
                    {
                        _eggRb.linearVelocity = Vector3.zero;
                    }
                    _eggRb.isKinematic = true;
                }
            }

            isEggInHand = true;
            _dropRoutine = null;
            Debug.Log("[EggStationController] Ajitama egg dropped into palm! Ready to place in ramen bowl (Key A / Enter or touch bowl).");
        }

        public void ReleaseEggIntoBowl()
        {
            if (_dropRoutine != null)
            {
                StopCoroutine(_dropRoutine);
                _dropRoutine = null;
            }

            if (eggProp != null)
            {
                eggProp.SetActive(false);
            }

            isEggInHand = false;

            if (RamenAssemblyReceiver.Instance != null)
            {
                RamenAssemblyReceiver.Instance.AddEgg();
            }
        }
    }
}
