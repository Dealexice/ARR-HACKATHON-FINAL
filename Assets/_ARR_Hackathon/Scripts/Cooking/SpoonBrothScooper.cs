using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Grab;

namespace YataiTogether
{
    /// <summary>
    /// Step 11: Broth Ladle & Wrist Pour Assembly Controller.
    /// Attached to the "spoon" prefab/GameObject in Test Street03.
    ///
    /// Enforces AGENTS.md §2, §4 (#10), & §7 (Meta Interaction SDK Physics Standard):
    /// - Ensures Rigidbody (0.2kg, ContinuousDynamic), Convex Collider, Grabbable, and HandGrabInteractable.
    /// - Tracks _hasBroth state and toggles spoonBrothFill mesh on the spoon tip.
    /// - OnTriggerEnter(Collider other):
    ///   * Touching "broth" pot -> _hasBroth = true; spoonBrothFill.SetActive(true);
    ///   * Touching "lamian_bowl" with _hasBroth == true -> activates "lamian_broth" child on bowl,
    ///     disables spoonBrothFill, resets _hasBroth = false, and advances YataiSessionDirector to ReadyToServe.
    ///   * Touching "lamian_bowl" with _hasBroth == false -> rejected (broth pot must be touched first).
    /// - Hybrid Parent Hotkey Backup:
    ///   * Key 5 (ParentAction.GrabLadle): Snaps/locks "spoon" to child virtual hand attach point.
    ///   * Key D (ParentAction.PourBroth): Tilts spoon 90° (with 2.5x visual-motor gain) and completes pour into lamian_bowl.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class SpoonBrothScooper : MonoBehaviour
    {
        public static SpoonBrothScooper Instance { get; private set; }

        [Header("Spoon Broth Visual & Attach References")]
        [Tooltip("Visual mesh on the spoon tip representing scooped golden broth (initially inactive).")]
        [SerializeField] private GameObject spoonBrothFill;

        [Tooltip("Optional child attach point on the spoon tip.")]
        [SerializeField] private Transform spoonTipPoint;

        [Header("Station References (Test Street03)")]
        [Tooltip("Reference to the 'broth' soup pot GameObject.")]
        [SerializeField] private GameObject brothPotObject;

        [Tooltip("Reference to the 'lamian_bowl' RamenAssemblyReceiver.")]
        [SerializeField] private RamenAssemblyReceiver ramenBowlReceiver;

        [Tooltip("Reference to the inactive 'lamian_broth' child inside 'lamian_bowl'.")]
        [SerializeField] private GameObject lamianBrothChild;

        [Header("Hand Tracking & Hotkey Fallback")]
        [Tooltip("Virtual hand attach point for Key 5 snap fallback.")]
        [SerializeField] private Transform handAttachPoint;

        [Tooltip("Tilt angle in degrees when pouring broth via Key D (default 90°).")]
        [SerializeField] private float pourTiltAngle = 90f;

        [Tooltip("Visual-motor rotation gain factor for child wrist pronation/supination (AGENTS.md §4 #10: 2.5x).")]
        [SerializeField] private float wristRotationGain = 2.5f;

        [Tooltip("Duration of the 90° pour tilt animation on Key D.")]
        [SerializeField] private float pourTiltDuration = 0.35f;

        [Header("Collider Identifiers")]
        [SerializeField] private string brothPotIdentifier = "broth";
        [SerializeField] private string ramenBowlIdentifier = "lamian_bowl";

        [Header("State (Read-Only in Inspector)")]
        [SerializeField] private bool _hasBroth = false;
        [SerializeField] private bool _isLadleInHand = false;
        [SerializeField] private bool _isPouring = false;

        [Header("Events")]
        public UnityEvent onLadleGrabbed = new UnityEvent();
        public UnityEvent onBrothScooped = new UnityEvent();
        public UnityEvent onBrothPoured = new UnityEvent();

        private Rigidbody _rb;
        private Grabbable _grabbable;
        private HandGrabInteractable _handGrabInteractable;
        private SpoonScoopController _noodleScoopController;

        private Vector3 _restWorldPosition;
        private Quaternion _restWorldRotation;
        private bool _capturedRestPose = false;
        private Coroutine _pourTiltCoroutine;

        private AudioSource _audioSource;
        private AudioClip _scoopChimeClip;
        private AudioClip _pourChimeClip;

        public bool HasBroth => _hasBroth;
        public bool IsLadleInHand => _isLadleInHand;
        public bool IsPouring => _isPouring;
        public GameObject SpoonBrothFill => spoonBrothFill;
        public GameObject BrothPotObject => brothPotObject;
        public GameObject LamianBrothChild => lamianBrothChild;

        public void Configure(
            GameObject brothFillVisual,
            GameObject brothPot,
            RamenAssemblyReceiver bowlReceiver,
            GameObject bowlBrothChild,
            Transform handAnchor = null)
        {
            spoonBrothFill = brothFillVisual;
            brothPotObject = brothPot;
            ramenBowlReceiver = bowlReceiver;
            lamianBrothChild = bowlBrothChild;
            if (handAnchor != null)
            {
                handAttachPoint = handAnchor;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            CaptureRestPose();
            EnsureMetaBuildingBlockComponents();
            EnsureSpoonBrothFillVisual();
            EnsureSceneReferences();
            SetupSynthesizedAudio();

            _hasBroth = false;
            if (spoonBrothFill != null)
            {
                spoonBrothFill.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (ParentHotkeyRouter.Instance != null)
            {
                ParentHotkeyRouter.Instance.ActionTapped += HandleActionTapped;
            }

            if (YataiSessionDirector.Instance != null)
            {
                YataiSessionDirector.Instance.StepChanged += HandleStepChanged;
            }

            if (_grabbable != null)
            {
                _grabbable.WhenPointerEventRaised -= HandleGrabbablePointerEvent;
                _grabbable.WhenPointerEventRaised += HandleGrabbablePointerEvent;
            }
        }

        private void Start()
        {
            if (ParentHotkeyRouter.Instance != null)
            {
                ParentHotkeyRouter.Instance.ActionTapped -= HandleActionTapped;
                ParentHotkeyRouter.Instance.ActionTapped += HandleActionTapped;
            }

            if (YataiSessionDirector.Instance != null)
            {
                YataiSessionDirector.Instance.StepChanged -= HandleStepChanged;
                YataiSessionDirector.Instance.StepChanged += HandleStepChanged;
            }

            if (_grabbable != null)
            {
                _grabbable.WhenPointerEventRaised -= HandleGrabbablePointerEvent;
                _grabbable.WhenPointerEventRaised += HandleGrabbablePointerEvent;
            }

            EnsureSceneReferences();
        }

        private void OnDisable()
        {
            if (ParentHotkeyRouter.Instance != null)
            {
                ParentHotkeyRouter.Instance.ActionTapped -= HandleActionTapped;
            }

            if (YataiSessionDirector.Instance != null)
            {
                YataiSessionDirector.Instance.StepChanged -= HandleStepChanged;
            }

            if (_grabbable != null)
            {
                _grabbable.WhenPointerEventRaised -= HandleGrabbablePointerEvent;
            }
        }

        private void OnDestroy()
        {
            if (_grabbable != null)
            {
                _grabbable.WhenPointerEventRaised -= HandleGrabbablePointerEvent;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void HandleGrabbablePointerEvent(PointerEvent evt)
        {
            if (evt.Type == PointerEventType.Select)
            {
                _isLadleInHand = true;
                onLadleGrabbed?.Invoke();

                if (ParentHotkeyRouter.Instance != null)
                {
                    ParentHotkeyRouter.Instance.CurrentlyHeldProp = gameObject;
                }

                if (YataiSessionDirector.Instance != null &&
                    YataiSessionDirector.Instance.CurrentStep == CookingStep.Broth_Ladle)
                {
                    YataiSessionDirector.Instance.SetStep(CookingStep.Broth_Pour);
                }
                Debug.Log("[SpoonBrothScooper] Spoon grabbed by VR Hand via Meta Interaction SDK!");
            }
            else if (evt.Type == PointerEventType.Unselect)
            {
                _isLadleInHand = false;
                if (ParentHotkeyRouter.Instance != null && ParentHotkeyRouter.Instance.CurrentlyHeldProp == gameObject)
                {
                    ParentHotkeyRouter.Instance.CurrentlyHeldProp = null;
                }
                Debug.Log("[SpoonBrothScooper] Spoon unselected/released by VR Hand.");
            }
        }

        private void Update()
        {
            // Follow virtual hand with 2.5x wrist rotation gain when snapped via Key 5 and not mid-pour animation
            if (_isLadleInHand && !_isPouring && handAttachPoint != null)
            {
                transform.position = handAttachPoint.position + (handAttachPoint.forward * 0.10f);

                // Apply 2.5x visual-motor gain on forearm/wrist roll (Z axis)
                Vector3 handEuler = handAttachPoint.localEulerAngles;
                float rollDelta = Mathf.DeltaAngle(0f, handEuler.z);
                float amplifiedRoll = rollDelta * wristRotationGain;
                transform.rotation = handAttachPoint.rotation * Quaternion.Euler(0f, 0f, amplifiedRoll);
            }
        }

        private void CaptureRestPose()
        {
            if (_capturedRestPose) return;
            _restWorldPosition = transform.position;
            _restWorldRotation = transform.rotation;
            _capturedRestPose = true;
        }

        /// <summary>
        /// Ensures Rigidbody, Convex Collider, Trigger Collider, Grabbable, and HandGrabInteractable
        /// are present on "spoon" per AGENTS.md §7.
        /// </summary>
        private void EnsureMetaBuildingBlockComponents()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb == null)
            {
                _rb = gameObject.AddComponent<Rigidbody>();
            }
            _rb.mass = 0.2f;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.isKinematic = true;

            // Solid convex collider for physical grabbing
            Collider solidCol = GetComponent<Collider>();
            if (solidCol == null)
            {
                MeshCollider mc = gameObject.AddComponent<MeshCollider>();
                mc.convex = true;
            }
            else if (solidCol is MeshCollider existingMc)
            {
                existingMc.convex = true;
            }

            // CRITICAL: Ensure dedicated solid BoxCollider on the body/handle of the spoon
            // Spans the handle (22cm length, 6cm width, 6cm height in world space) for effortless VR grip
            Transform bodyColT = transform.Find("spoon_body_collider");
            if (bodyColT == null)
            {
                GameObject bodyColGo = new GameObject("spoon_body_collider");
                bodyColGo.transform.SetParent(transform, false);
                bodyColGo.transform.localPosition = new Vector3(-0.0009f, -0.0002f, 0.00005f);
                bodyColGo.transform.localRotation = Quaternion.identity;
                bodyColGo.transform.localScale = Vector3.one;
                BoxCollider bc = bodyColGo.AddComponent<BoxCollider>();
                bc.isTrigger = false;
                bc.size = new Vector3(0.0022f, 0.0006f, 0.0006f);
            }
            else
            {
                BoxCollider bc = bodyColT.GetComponent<BoxCollider>();
                if (bc == null) bc = bodyColT.gameObject.AddComponent<BoxCollider>();
                bc.isTrigger = false;
                bc.size = new Vector3(0.0022f, 0.0006f, 0.0006f);
                bodyColT.localPosition = new Vector3(-0.0009f, -0.0002f, 0.00005f);
            }

            // Ensure trigger collider exists on child spoon_scoop_trigger (strictly sized to scoop head)
            Transform scoopTriggerTransform = transform.Find("spoon_scoop_trigger");
            if (scoopTriggerTransform == null)
            {
                GameObject trigGo = new GameObject("spoon_scoop_trigger");
                trigGo.transform.SetParent(transform, false);
                trigGo.transform.localPosition = new Vector3(0.00095f, 0.00009f, 0.00005f);
                SphereCollider sc = trigGo.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.radius = 0.00045f;
                scoopTriggerTransform = trigGo.transform;
            }
            else
            {
                SphereCollider sc = scoopTriggerTransform.GetComponent<SphereCollider>();
                if (sc != null)
                {
                    sc.isTrigger = true;
                    sc.radius = 0.00045f;
                }
                else
                {
                    Collider trigCol = scoopTriggerTransform.GetComponent<Collider>();
                    if (trigCol != null) trigCol.isTrigger = true;
                }
            }

            _grabbable = GetComponent<Grabbable>();
            if (_grabbable == null)
            {
                _grabbable = gameObject.AddComponent<Grabbable>();
            }
            _grabbable.InjectOptionalRigidbody(_rb);
            _grabbable.enabled = true;

            // Universal GrabInteractable for controllers and volume grab interactors
            GrabInteractable grabInteractable = GetComponent<GrabInteractable>();
            if (grabInteractable == null)
            {
                grabInteractable = gameObject.AddComponent<GrabInteractable>();
                grabInteractable.InjectRigidbody(_rb);
                grabInteractable.InjectOptionalPointableElement(_grabbable);
                grabInteractable.UseClosestPointAsGrabSource = true;
            }
            grabInteractable.enabled = true;

            // HandGrabInteractable for optical hand tracking (pinch and palm)
            _handGrabInteractable = GetComponent<HandGrabInteractable>();
            if (_handGrabInteractable == null)
            {
                _handGrabInteractable = gameObject.AddComponent<HandGrabInteractable>();
                _handGrabInteractable.InjectRigidbody(_rb);
                _handGrabInteractable.InjectOptionalPointableElement(_grabbable);
            }
            _handGrabInteractable.InjectSupportedGrabTypes(GrabTypeFlags.All);
            _handGrabInteractable.enabled = true;

            _grabbable.WhenPointerEventRaised -= HandleGrabbablePointerEvent;
            _grabbable.WhenPointerEventRaised += HandleGrabbablePointerEvent;

            _noodleScoopController = GetComponent<SpoonScoopController>();
        }

        /// <summary>
        /// Resolves or creates the "spoon_broth_fill" mesh on the spoon bowl tip (initially inactive).
        /// </summary>
        private void EnsureSpoonBrothFillVisual()
        {
            if (spoonTipPoint == null)
            {
                Transform foundAttach = transform.Find("spoon_attach_point");
                if (foundAttach != null)
                {
                    spoonTipPoint = foundAttach;
                }
            }

            if (spoonBrothFill == null)
            {
                Transform foundFill = transform.Find("spoon_broth_fill");
                if (foundFill == null && spoonTipPoint != null)
                {
                    foundFill = spoonTipPoint.Find("spoon_broth_fill");
                }

                if (foundFill != null)
                {
                    spoonBrothFill = foundFill.gameObject;
                }
                else
                {
                    GameObject fillGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    fillGo.name = "spoon_broth_fill";
                    fillGo.transform.SetParent(transform, false);
                    // Positioned inside the ladle cup at the +X tip of the Test Street03 spoon mesh
                    fillGo.transform.localPosition = new Vector3(0.00095f, 0.00009f, 0.00008f);
                    fillGo.transform.localRotation = Quaternion.identity;
                    fillGo.transform.localScale = new Vector3(0.00075f, 0.00075f, 0.00012f);

                    Collider fillCol = fillGo.GetComponent<Collider>();
                    if (fillCol != null)
                    {
                        if (Application.isPlaying) Destroy(fillCol);
                        else DestroyImmediate(fillCol);
                    }

                    Renderer fillRenderer = fillGo.GetComponent<Renderer>();
                    if (fillRenderer != null)
                    {
                        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                        if (sh != null)
                        {
                            Material brothMat = new Material(sh)
                            {
                                name = "SpoonBrothFill_Mat",
                                color = new Color(0.92f, 0.68f, 0.22f, 0.95f)
                            };
                            if (brothMat.HasProperty("_BaseColor"))
                            {
                                brothMat.SetColor("_BaseColor", new Color(0.92f, 0.68f, 0.22f, 0.95f));
                            }
                            if (brothMat.HasProperty("_EmissionColor"))
                            {
                                brothMat.EnableKeyword("_EMISSION");
                                brothMat.SetColor("_EmissionColor", new Color(0.45f, 0.30f, 0.06f));
                            }
                            fillRenderer.sharedMaterial = brothMat;
                        }
                    }

                    spoonBrothFill = fillGo;
                }
            }
        }

        /// <summary>
        /// Resolves references to Test Street03's "broth" pot, "lamian_bowl", "lamian_broth", and hand anchor.
        /// Also ensures the "broth" pot has a trigger collider so OnTriggerEnter fires reliably.
        /// </summary>
        private void EnsureSceneReferences()
        {
            GameObject street03 = GameObject.Find("Test Street03") ?? GameObject.Find("Test Street");

            if (brothPotObject == null)
            {
                if (street03 != null)
                {
                    Transform t = street03.transform.Find("broth");
                    if (t != null) brothPotObject = t.gameObject;
                }
                if (brothPotObject == null)
                {
                    brothPotObject = GameObject.Find("broth");
                }
            }

            // Guarantee the "broth" pot has a Trigger Collider for physical spoon dipping
            if (brothPotObject != null)
            {
                Collider potCol = brothPotObject.GetComponent<Collider>();
                if (potCol == null)
                {
                    BoxCollider box = brothPotObject.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                    box.center = new Vector3(-0.00043f, 0.00030f, 0.00050f);
                    box.size = new Vector3(0.0056f, 0.0046f, 0.0035f);
                }
                else
                {
                    potCol.isTrigger = true;
                }
            }

            if (ramenBowlReceiver == null)
            {
                ramenBowlReceiver = RamenAssemblyReceiver.Instance;
                if (ramenBowlReceiver == null && street03 != null)
                {
                    Transform bowlT = street03.transform.Find("lamian_bowl");
                    if (bowlT != null)
                    {
                        ramenBowlReceiver = bowlT.GetComponent<RamenAssemblyReceiver>();
                    }
                }
            }

            if (lamianBrothChild == null)
            {
                if (ramenBowlReceiver != null && ramenBowlReceiver.ChildBroth != null)
                {
                    lamianBrothChild = ramenBowlReceiver.ChildBroth;
                }
                else if (ramenBowlReceiver != null)
                {
                    Transform brothT = ramenBowlReceiver.transform.Find("lamian_broth")
                                    ?? ramenBowlReceiver.transform.Find("lamian_soup");
                    if (brothT != null)
                    {
                        lamianBrothChild = brothT.gameObject;
                    }
                }
            }

            if (handAttachPoint == null)
            {
                GameObject rig = GameObject.Find("[BuildingBlock] Camera Rig");
                if (rig != null)
                {
                    Transform tracking = rig.transform.Find("TrackingSpace");
                    if (tracking != null)
                    {
                        handAttachPoint = tracking.Find("RightHandAnchor");
                    }
                }

                if (handAttachPoint == null)
                {
                    GameObject pov2 = GameObject.Find("POV_2_FoodStallCounterAnchor");
                    if (pov2 != null)
                    {
                        handAttachPoint = pov2.transform;
                    }
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;

            // 1. Check if touching "broth" pot tag or name
            if (IsBrothPotCollider(other))
            {
                ScoopBrothFromPot();
                return;
            }

            // 2. Check if touching "lamian_bowl"
            if (IsLamianBowlCollider(other))
            {
                // Do not interfere if the spoon is currently carrying cooked noodles during Step 6
                if (_noodleScoopController != null && _noodleScoopController.HasNoodleOnSpoon)
                {
                    return;
                }

                if (_hasBroth)
                {
                    PourBrothIntoBowl(animateTilt: false);
                }
                else
                {
                    // Reject / do nothing when touching lamian_bowl with _hasBroth == false
                    Debug.LogWarning("[SpoonBrothScooper] Spoon touched lamian_bowl without broth (_hasBroth == false). Dip spoon into 'broth' pot first!");
                    if (YataiSessionDirector.Instance != null &&
                        (YataiSessionDirector.Instance.CurrentStep == CookingStep.Broth_Ladle ||
                         YataiSessionDirector.Instance.CurrentStep == CookingStep.Broth_Pour))
                    {
                        YataiSessionDirector.Instance.TriggerWarning("Ladle is empty! Dip spoon into the 'broth' pot first!");
                    }
                }
            }
        }

        private bool IsBrothPotCollider(Collider other)
        {
            if (brothPotObject != null &&
                (other.gameObject == brothPotObject || other.transform.IsChildOf(brothPotObject.transform)))
            {
                return true;
            }

            string objName = other.gameObject.name.ToLowerInvariant();
            if (objName == brothPotIdentifier || (objName.Contains(brothPotIdentifier) && !objName.Contains("lamian_broth") && !objName.Contains("spoon_broth")))
            {
                return true;
            }

            return SafeCompareTag(other.gameObject, brothPotIdentifier);
        }

        private bool IsLamianBowlCollider(Collider other)
        {
            if (ramenBowlReceiver != null &&
                (other.gameObject == ramenBowlReceiver.gameObject || other.transform.IsChildOf(ramenBowlReceiver.transform)))
            {
                return true;
            }

            if (other.GetComponent<RamenAssemblyReceiver>() != null || other.GetComponentInParent<RamenAssemblyReceiver>() != null)
            {
                return true;
            }

            string objName = other.gameObject.name.ToLowerInvariant();
            if (objName.Contains(ramenBowlIdentifier))
            {
                return true;
            }

            return SafeCompareTag(other.gameObject, ramenBowlIdentifier);
        }

        private static bool SafeCompareTag(GameObject go, string tagToCheck)
        {
            if (go == null || string.IsNullOrEmpty(tagToCheck)) return false;
            return string.Equals(go.tag, tagToCheck, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Scoops broth into the spoon tip: sets _hasBroth = true and activates spoonBrothFill.
        /// </summary>
        public void ScoopBrothFromPot()
        {
            EnsureSpoonBrothFillVisual();

            _hasBroth = true;
            if (spoonBrothFill != null)
            {
                spoonBrothFill.SetActive(true);
            }

            PlaySoftChime(_scoopChimeClip);
            onBrothScooped?.Invoke();
            Debug.Log("[SpoonBrothScooper] Scooped golden broth from 'broth' pot! (_hasBroth = true, spoonBrothFill active).");

            if (YataiSessionDirector.Instance != null &&
                YataiSessionDirector.Instance.CurrentStep == CookingStep.Broth_Ladle)
            {
                YataiSessionDirector.Instance.SetStep(CookingStep.Broth_Pour);
            }
        }

        /// <summary>
        /// Completes pouring broth from the spoon into lamian_bowl:
        /// 1. Activates child 'lamian_broth' on 'lamian_bowl'.
        /// 2. Disables 'spoonBrothFill'.
        /// 3. Resets '_hasBroth = false'.
        /// 4. Notifies YataiSessionDirector -> CookingStep.ReadyToServe.
        /// </summary>
        public void PourBrothIntoBowl(bool animateTilt = false)
        {
            EnsureSceneReferences();

            if (animateTilt)
            {
                if (_pourTiltCoroutine != null)
                {
                    StopCoroutine(_pourTiltCoroutine);
                }
                _pourTiltCoroutine = StartCoroutine(AnimatePourTiltRoutine());
            }

            // 1. Activate child lamian_broth on lamian_bowl
            if (lamianBrothChild != null)
            {
                lamianBrothChild.SetActive(true);
            }

            if (ramenBowlReceiver != null)
            {
                ramenBowlReceiver.AddBroth();
            }

            // 2. Disable spoonBrothFill
            if (spoonBrothFill != null)
            {
                spoonBrothFill.SetActive(false);
            }

            // 3. Reset _hasBroth flag
            _hasBroth = false;

            // 4. Trigger completion event & advance YataiSessionDirector to ReadyToServe
            PlaySoftChime(_pourChimeClip);
            onBrothPoured?.Invoke();

            if (YataiSessionDirector.Instance != null &&
                (YataiSessionDirector.Instance.CurrentStep == CookingStep.Broth_Ladle ||
                 YataiSessionDirector.Instance.CurrentStep == CookingStep.Broth_Pour))
            {
                YataiSessionDirector.Instance.SetStep(CookingStep.ReadyToServe);
            }

            Debug.Log("[SpoonBrothScooper] Broth poured into lamian_bowl! lamian_broth active, spoonBrothFill disabled, state -> ReadyToServe.");
        }

        private IEnumerator AnimatePourTiltRoutine()
        {
            _isPouring = true;

            // Position spoon directly above lamian_bowl before tilting 90 degrees
            if (ramenBowlReceiver != null)
            {
                transform.position = ramenBowlReceiver.transform.position + new Vector3(0f, 0.14f, 0f);
            }

            Quaternion startRot = transform.rotation;
            Quaternion tiltedRot = startRot * Quaternion.Euler(pourTiltAngle, 0f, 0f);

            float halfDuration = Mathf.Max(0.08f, pourTiltDuration * 0.5f);
            float elapsed = 0f;

            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                float smooth = t * t * (3f - 2f * t);
                transform.rotation = Quaternion.Slerp(startRot, tiltedRot, smooth);
                yield return null;
            }

            transform.rotation = tiltedRot;
            yield return new WaitForSeconds(0.10f);

            // Return spoon smoothly to rest pose on counter
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                float smooth = t * t * (3f - 2f * t);
                transform.rotation = Quaternion.Slerp(tiltedRot, _restWorldRotation, smooth);
                yield return null;
            }

            _isPouring = false;
            _isLadleInHand = false;
            ReturnSpoonToRestPose();
            _pourTiltCoroutine = null;
        }

        /// <summary>
        /// Key 5 Fallback: Snaps/locks "spoon" to the child's virtual hand attach point if not already grabbed.
        /// </summary>
        public void SnapSpoonToHand()
        {
            EnsureSceneReferences();
            CaptureRestPose();

            if (_rb != null)
            {
                // ponytail: Zero velocity only while dynamic before setting kinematic to prevent Unity 6 engine warnings
                if (!_rb.isKinematic)
                {
                    _rb.linearVelocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                }
                _rb.isKinematic = true;
            }

            if (handAttachPoint != null)
            {
                transform.position = handAttachPoint.position + (handAttachPoint.forward * 0.10f);
                transform.rotation = handAttachPoint.rotation;
            }
            else if (brothPotObject != null)
            {
                transform.position = brothPotObject.transform.position + new Vector3(0f, 0.12f, 0f);
            }

            _isLadleInHand = true;
            onLadleGrabbed?.Invoke();
            Debug.Log("[SpoonBrothScooper] Key 5: Spoon snapped to child virtual hand attach point.");
        }

        public void ReturnSpoonToRestPose()
        {
            if (!_capturedRestPose) return;

            if (_rb != null)
            {
                // ponytail: Zero velocity only while dynamic before setting kinematic to prevent Unity 6 engine warnings
                if (!_rb.isKinematic)
                {
                    _rb.linearVelocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                }
                _rb.isKinematic = true;
            }

            transform.position = _restWorldPosition;
            transform.rotation = _restWorldRotation;
        }

        public void ResetForNextCustomer()
        {
            if (_pourTiltCoroutine != null)
            {
                StopCoroutine(_pourTiltCoroutine);
                _pourTiltCoroutine = null;
            }

            _hasBroth = false;
            _isLadleInHand = false;
            _isPouring = false;

            if (spoonBrothFill != null)
            {
                spoonBrothFill.SetActive(false);
            }

            ReturnSpoonToRestPose();
        }

        private void HandleActionTapped(ParentAction action)
        {
            if (action == ParentAction.GrabLadle)
            {
                // Key 5: Only snap spoon to hand when in Broth_Ladle or Broth_Pour step
                if (YataiSessionDirector.Instance != null &&
                    (YataiSessionDirector.Instance.CurrentStep == CookingStep.Broth_Ladle ||
                     YataiSessionDirector.Instance.CurrentStep == CookingStep.Broth_Pour))
                {
                    SnapSpoonToHand();
                }
            }
            else if (action == ParentAction.PourBroth)
            {
                // Key D: If _hasBroth == true over bowl (or as assistive shortcut after Key 5),
                // tilts spoon 90° and completes pour into lamian_bowl
                if (YataiSessionDirector.Instance != null)
                {
                    CookingStep step = YataiSessionDirector.Instance.CurrentStep;
                    bool validAssistiveStep = (step == CookingStep.Broth_Pour ||
                                              (step == CookingStep.ReadyToServe && (_isLadleInHand || _hasBroth)));
                    bool validPhysicalScoopStep = (_hasBroth && step == CookingStep.Broth_Ladle);

                    if (validAssistiveStep || validPhysicalScoopStep)
                    {
                        if (!_hasBroth)
                        {
                            // Assistive shortcut: ensure broth fill visual flashes during tilt pour
                            _hasBroth = true;
                            if (spoonBrothFill != null) spoonBrothFill.SetActive(true);
                        }

                        PourBrothIntoBowl(animateTilt: true);
                    }
                }
            }
        }

        private void HandleStepChanged(CookingStep step)
        {
            if (step == CookingStep.Noodles_Grab || step == CookingStep.BikeRide)
            {
                ResetForNextCustomer();
            }
        }

        /// <summary>
        /// Synthesizes soft attack audio cues (>= 0.05s ramp, gentle resonance) per AGENTS.md §5 Audio Safety.
        /// </summary>
        private void SetupSynthesizedAudio()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
            _audioSource.volume = 0.30f;

            _scoopChimeClip = CreateSoftToneClip("SoftBrothScoop", 523.25f, 659.25f, 0.35f);
            _pourChimeClip = CreateSoftToneClip("SoftBrothPour", 587.33f, 880.0f, 0.50f);
        }

        private static AudioClip CreateSoftToneClip(string clipName, float freqA, float freqB, float duration)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float attack = Mathf.Clamp01(t / 0.06f);
                float decay = Mathf.Exp(-5.0f * t);
                float wave = Mathf.Sin(2f * Mathf.PI * freqA * t) * 0.6f
                           + Mathf.Sin(2f * Mathf.PI * freqB * t) * 0.4f;
                samples[i] = wave * attack * decay * 0.35f;
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void PlaySoftChime(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
            {
                _audioSource.PlayOneShot(clip, 0.30f);
            }
        }
    }
}
