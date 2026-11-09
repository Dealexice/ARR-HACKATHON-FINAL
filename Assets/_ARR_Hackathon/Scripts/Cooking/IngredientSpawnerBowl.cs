using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Grab;

namespace YataiTogether
{
    /// <summary>
    /// Step 6-9: Infinite Ingredient Spawner Bowl.
    /// Attached to stationary ingredient bowls ("meat_bowl", "noodle_bowl", "onion_bowl", "egg_bowl").
    ///
    /// Requirements:
    /// - Keeps the internal display mesh inside each bowl stationary and non-grabbable (isKinematic = true).
    /// - Detects hand pinch / trigger enter from tracked VR hands, or discrete hotkeys (Keys 1-4).
    /// - On grab/hotkey: instantiates a duplicate of ingredientPrefab at the user's tracked VR hand attach point.
    /// - The duplicate is equipped with Rigidbody (ContinuousDynamic), Convex Collider, Grabbable, and HandGrabInteractable.
    /// - The duplicate follows the hand until released / dropped into boiler or lamian_bowl.
    /// - The stationary display mesh inside the bowl remains untouched for infinite pickups.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class IngredientSpawnerBowl : MonoBehaviour
    {
        [Header("Ingredient Setup")]
        [Tooltip("The template prefab or GameObject to clone when the player grabs this ingredient.")]
        [SerializeField] private GameObject ingredientPrefab;

        [Tooltip("The static display mesh resting inside the bowl (must remain stationary and untouched).")]
        [SerializeField] private GameObject displayMesh;

        [Tooltip("Ingredient identifier tag/name ('meat', 'noodle', 'onion', 'egg').")]
        [SerializeField] private string ingredientType = "meat";

        [Tooltip("Associated parent hotkey action (e.g. GrabPork for meat, GrabNoodles for noodles, etc.).")]
        [SerializeField] private ParentAction triggeringHotkey = ParentAction.None;

        [Header("Hand Tracking Anchors")]
        [Tooltip("Attach point on the tracked VR hand (defaults to RightHandAnchor / pinch transform).")]
        [SerializeField] private Transform primaryHandAttach;

        [Header("Events")]
        public UnityEvent onIngredientSpawned = new UnityEvent();
        public UnityEvent onIngredientReleased = new UnityEvent();

        private GameObject _activeSpawnedDuplicate;
        private Rigidbody _activeDuplicateRb;
        private Collider _triggerCollider;
        private bool _suppressDirectorAdvance;

        public GameObject IngredientPrefab => ingredientPrefab;
        public GameObject DisplayMesh => displayMesh;
        public string IngredientType => ingredientType;
        public ParentAction TriggeringHotkey => triggeringHotkey;
        public GameObject ActiveSpawnedDuplicate => _activeSpawnedDuplicate;

        public void Configure(GameObject prefab, GameObject display, string type, ParentAction hotkey, Transform handAttach = null)
        {
            ingredientPrefab = prefab;
            displayMesh = display;
            ingredientType = type;
            triggeringHotkey = hotkey;
            primaryHandAttach = handAttach;
        }

        private void Awake()
        {
            _triggerCollider = GetComponent<Collider>();
            if (_triggerCollider != null)
            {
                _triggerCollider.isTrigger = true;
            }

            EnsureDisplayMeshIsStationary();
            ResolveHandAttachPoint();
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

            EnsureDisplayMeshIsStationary();
            ResolveHandAttachPoint();
        }

        private void OnDisable()
        {
            if (ParentHotkeyRouter.Instance != null)
            {
                ParentHotkeyRouter.Instance.ActionTapped -= HandleActionTapped;
            }
        }

        /// <summary>
        /// Ensures the display mesh inside the bowl is strictly stationary and non-grabbable.
        /// </summary>
        public void EnsureDisplayMeshIsStationary()
        {
            if (displayMesh == null && transform.parent != null)
            {
                // Try finding child with ingredient name
                Transform found = transform.Find(ingredientType) ?? transform.parent.Find(ingredientType);
                if (found != null) displayMesh = found.gameObject;
            }

            if (displayMesh != null)
            {
                Rigidbody rb = displayMesh.GetComponent<Rigidbody>();
                if (rb == null) rb = displayMesh.AddComponent<Rigidbody>();
                rb.isKinematic = true;

                // Disable any direct grab interactables on the stationary display mesh
                var grabbables = displayMesh.GetComponentsInChildren<Grabbable>(true);
                for (int i = 0; i < grabbables.Length; i++) grabbables[i].enabled = false;

                var handGrabs = displayMesh.GetComponentsInChildren<HandGrabInteractable>(true);
                for (int i = 0; i < handGrabs.Length; i++) handGrabs[i].enabled = false;

                displayMesh.SetActive(true);
            }
        }

        private void ResolveHandAttachPoint()
        {
            if (primaryHandAttach != null) return;

            GameObject rig = GameObject.Find("[BuildingBlock] Camera Rig") ?? GameObject.Find("OVRCameraRig");
            if (rig != null)
            {
                Transform tracking = rig.transform.Find("TrackingSpace");
                if (tracking != null)
                {
                    primaryHandAttach = tracking.Find("RightHandAnchor") ?? tracking.Find("RightControllerAnchor");
                }
            }

            if (primaryHandAttach == null)
            {
                GameObject pov2 = GameObject.Find("POV_2_FoodStallCounterAnchor");
                if (pov2 != null) primaryHandAttach = pov2.transform;
            }
        }

        private void HandleActionTapped(ParentAction action)
        {
            if (action == triggeringHotkey && triggeringHotkey != ParentAction.None)
            {
                if (CanSpawnForCurrentStep())
                {
                    // Hotkey path: YataiSessionDirector advances the step itself from the same tap.
                    // Advancing here too would double-step (e.g. Noodles_Grab -> Stretch -> Boil, skipping W).
                    _suppressDirectorAdvance = true;
                    try { SpawnDuplicateToHand(); }
                    finally { _suppressDirectorAdvance = false; }
                }
            }
            else if (action == ParentAction.ReleaseIntoBowl && _activeSpawnedDuplicate != null &&
                     !ingredientType.ToLower().Contains("noodle"))
            {
                // The raw noodle never drops straight into the bowl: it must be boiled first
                // (W -> B -> 4 -> A). A/Enter during the stretch/boil must not destroy it.
                ReleaseHeldDuplicateIntoBowl();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;

            // Detect VR Hand entering the bowl trigger zone (Meta Quest 3 hand tracking or controllers)
            if (IsVRHandCollider(other))
            {
                // If user doesn't already have an active prop in hand, spawn duplicate
                if (_activeSpawnedDuplicate == null && CanSpawnForCurrentStep())
                {
                    // Always attach to the right-hand anchor, never to whichever finger/bone collider
                    // touched the bowl (left-hand or bone-scaled parents gave oversized, misplaced props).
                    SpawnDuplicateToHand();
                }
            }
        }

        /// <summary>
        /// True if the collider belongs to a tracked VR hand or controller (Meta Quest 3 hands, interactors, rig).
        /// </summary>
        public static bool IsVRHandCollider(Collider other)
        {
            if (other == null) return false;
            string otherName = other.gameObject.name.ToLower();
            return otherName.Contains("pinch") ||
                   otherName.Contains("grab") ||
                   otherName.Contains("hand") ||
                   otherName.Contains("finger") ||
                   otherName.Contains("interactor") ||
                   otherName.Contains("controller") ||
                   other.CompareTag("Player") ||
                   other.GetComponentInParent<OVRCameraRig>() != null ||
                   other.GetComponentInParent<HandVisual>() != null ||
                   other.GetComponentInParent<HandGrabInteractor>() != null;
        }

        /// <summary>
        /// Instantiates a duplicate of the ingredient and attaches it to the player's VR hand.
        /// </summary>
        public GameObject SpawnDuplicateToHand(Transform targetHand = null)
        {
            Transform attachTarget = targetHand != null ? targetHand : primaryHandAttach;
            Vector3 spawnPos;
            Quaternion spawnRot;

            if (attachTarget != null)
            {
                spawnPos = attachTarget.position + attachTarget.forward * 0.08f;
                spawnRot = attachTarget.rotation;
            }
            else if (displayMesh != null)
            {
                spawnPos = displayMesh.transform.position + Vector3.up * 0.10f;
                spawnRot = displayMesh.transform.rotation;
            }
            else
            {
                spawnPos = transform.position + Vector3.up * 0.12f;
                spawnRot = transform.rotation;
            }

            GameObject template = ingredientPrefab != null ? ingredientPrefab : displayMesh;
            if (template == null)
            {
                Debug.LogWarning($"[IngredientSpawnerBowl] No prefab or display mesh configured for {gameObject.name}!");
                return null;
            }

            // Clean up old duplicate if it was never placed
            if (_activeSpawnedDuplicate != null)
            {
                Destroy(_activeSpawnedDuplicate);
            }

            // Instantiate duplicate
            _activeSpawnedDuplicate = Instantiate(template, spawnPos, spawnRot);
            _activeSpawnedDuplicate.name = $"Spawned_{ingredientType}";
            _activeSpawnedDuplicate.SetActive(true);

            // Set tag safely
            try
            {
                _activeSpawnedDuplicate.tag = ingredientType;
            }
            catch { }

            // Ensure physical components per AGENTS.md §7
            _activeDuplicateRb = _activeSpawnedDuplicate.GetComponent<Rigidbody>();
            if (_activeDuplicateRb == null) _activeDuplicateRb = _activeSpawnedDuplicate.AddComponent<Rigidbody>();
            _activeDuplicateRb.mass = 0.12f;
            _activeDuplicateRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _activeDuplicateRb.isKinematic = true; // Kept kinematic while attached to hand

            Collider col = _activeSpawnedDuplicate.GetComponent<Collider>();
            if (col == null)
            {
                if (ingredientType.ToLower().Contains("egg"))
                {
                    SphereCollider sc = _activeSpawnedDuplicate.AddComponent<SphereCollider>();
                    sc.radius = 0.00045f;
                    col = sc;
                }
                else
                {
                    MeshCollider mc = _activeSpawnedDuplicate.AddComponent<MeshCollider>();
                    mc.convex = true;
                    col = mc;
                }
            }
            col.isTrigger = false;

            // Ensure Grabbable
            Grabbable grabbable = _activeSpawnedDuplicate.GetComponent<Grabbable>();
            if (grabbable == null) grabbable = _activeSpawnedDuplicate.AddComponent<Grabbable>();
            grabbable.InjectOptionalRigidbody(_activeDuplicateRb);
            grabbable.enabled = true;

            // Universal GrabInteractable for controllers and volume grab interactors
            GrabInteractable grabInteractable = _activeSpawnedDuplicate.GetComponent<GrabInteractable>();
            if (grabInteractable == null)
            {
                grabInteractable = _activeSpawnedDuplicate.AddComponent<GrabInteractable>();
                grabInteractable.InjectRigidbody(_activeDuplicateRb);
                grabInteractable.InjectOptionalPointableElement(grabbable);
                grabInteractable.UseClosestPointAsGrabSource = true;
            }
            grabInteractable.enabled = true;

            // HandGrabInteractable for optical hand tracking (pinch and palm)
            HandGrabInteractable handGrab = _activeSpawnedDuplicate.GetComponent<HandGrabInteractable>();
            if (handGrab == null)
            {
                handGrab = _activeSpawnedDuplicate.AddComponent<HandGrabInteractable>();
                handGrab.InjectRigidbody(_activeDuplicateRb);
                handGrab.InjectOptionalPointableElement(grabbable);
            }
            handGrab.InjectSupportedGrabTypes(GrabTypeFlags.All);
            handGrab.enabled = true;

            bool isNoodle = ingredientType.ToLower().Contains("noodle");

            // Parent to hand attach point so it follows hand movement
            if (attachTarget != null)
            {
                _activeSpawnedDuplicate.transform.SetParent(attachTarget, true);

                // Hotkey-attached props are driven by the hand anchor (Quest tracks WHERE, parent decides WHEN).
                // Keep the Meta grab stack present (AGENTS.md §7) but disabled while attached: otherwise the
                // Grabbable's default GrabFreeTransformer also moves AND scales the prop with hand distance.
                // The noodle too: StretchableNoodle pins its two ends to both hand anchors deterministically.
                grabbable.enabled = false;
                grabInteractable.enabled = false;
                handGrab.enabled = false;
            }

            // Specific setup for noodles
            if (isNoodle)
            {
                // Bimanual stretch (AGENTS.md §4 #3) is driven by StretchableNoodle: ends pinned to both hands.
                if (_activeSpawnedDuplicate.GetComponent<NoodleStretch>() == null)
                {
                    _activeSpawnedDuplicate.AddComponent<NoodleStretch>();
                }

                // A physical hand grab (OnTriggerEnter) never fires the Key 4 tap, so arm the stretch
                // driver here too; otherwise the grabbed noodle stays rigid. Idempotent on the hotkey path.
                if (StretchableNoodle.Instance != null)
                {
                    StretchableNoodle.Instance.AttachNoodlesToHand();
                }
            }

            // Register with ParentHotkeyRouter
            if (ParentHotkeyRouter.Instance != null)
            {
                ParentHotkeyRouter.Instance.CurrentlyHeldProp = _activeSpawnedDuplicate;
            }

            // Notify YataiSessionDirector (physical hand grabs only; hotkey taps are advanced by the director)
            if (!_suppressDirectorAdvance)
            {
                AdvanceDirectorStepOnGrab();
            }

            // Display mesh remains completely untouched in the bowl!
            EnsureDisplayMeshIsStationary();

            onIngredientSpawned?.Invoke();
            Debug.Log($"[IngredientSpawnerBowl] Spawned infinite duplicate for '{ingredientType}' into chef hand. Original in bowl untouched.");

            return _activeSpawnedDuplicate;
        }

        /// <summary>
        /// Strict Sequence Law + single-prop rule: only spawn while this ingredient's grab step is active
        /// (or its release step, which the director may have just entered this same frame), and never
        /// while a duplicate from this bowl is already in the chef's hand.
        /// </summary>
        private bool CanSpawnForCurrentStep()
        {
            if (_activeSpawnedDuplicate != null) return false;

            YataiSessionDirector director = YataiSessionDirector.Instance;
            if (director == null) return true;

            CookingStep step = director.CurrentStep;
            string t = ingredientType.ToLower();
            if (t.Contains("noodle")) return step == CookingStep.Noodles_Grab || step == CookingStep.Noodles_Stretch;
            if (t.Contains("meat")) return step == CookingStep.Pork_Grab || step == CookingStep.Pork_Release;
            if (t.Contains("egg")) return step == CookingStep.Egg_Drop || step == CookingStep.Egg_Release;
            if (t.Contains("onion")) return step == CookingStep.Onions_Grab || step == CookingStep.Onions_Release;
            return true;
        }

        /// <summary>
        /// True if the object (or one of its parents) is the stationary display mesh of any ingredient bowl.
        /// Stations must never snap these (e.g. the noodle_bowl display overlaps the boiler trigger).
        /// </summary>
        public static bool IsStationaryDisplayMesh(GameObject go)
        {
            if (go == null) return false;
            IngredientSpawnerBowl[] spawners = FindObjectsByType<IngredientSpawnerBowl>(FindObjectsSortMode.None);
            for (int i = 0; i < spawners.Length; i++)
            {
                GameObject display = spawners[i] != null ? spawners[i].DisplayMesh : null;
                if (display != null && (go == display || go.transform.IsChildOf(display.transform))) return true;
            }
            return false;
        }

        /// <summary>
        /// Drops this bowl's reference to its spawned duplicate without destroying it
        /// (e.g. the noodle now lives in the boiler pot and must survive later A/Enter releases).
        /// </summary>
        public void ForgetDuplicate(GameObject duplicate)
        {
            if (duplicate == null || _activeSpawnedDuplicate != duplicate) return;

            _activeSpawnedDuplicate = null;
            _activeDuplicateRb = null;

            if (ParentHotkeyRouter.Instance != null && ParentHotkeyRouter.Instance.CurrentlyHeldProp == duplicate)
            {
                ParentHotkeyRouter.Instance.CurrentlyHeldProp = null;
            }
        }

        /// <summary>
        /// Zero-penalty Backspace: removes any duplicate still in the chef's hand without adding it to the bowl.
        /// </summary>
        public void DiscardHeldDuplicate()
        {
            if (_activeSpawnedDuplicate == null) return;

            GameObject prop = _activeSpawnedDuplicate;
            ForgetDuplicate(prop);
            Destroy(prop);
        }

        private void AdvanceDirectorStepOnGrab()
        {
            YataiSessionDirector director = YataiSessionDirector.Instance;
            if (director == null) return;

            string t = ingredientType.ToLower();
            if (t.Contains("meat") && director.CurrentStep == CookingStep.Pork_Grab)
            {
                director.SetStep(CookingStep.Pork_Release);
            }
            else if (t.Contains("egg") && director.CurrentStep == CookingStep.Egg_Drop)
            {
                director.SetStep(CookingStep.Egg_Release);
            }
            else if (t.Contains("onion") && director.CurrentStep == CookingStep.Onions_Grab)
            {
                director.SetStep(CookingStep.Onions_Release);
            }
            else if (t.Contains("noodle") && director.CurrentStep == CookingStep.Noodles_Grab)
            {
                director.SetStep(CookingStep.Noodles_Stretch);
            }
        }

        /// <summary>
        /// Drops or releases the currently held duplicate into the lamian_bowl.
        /// </summary>
        public void ReleaseHeldDuplicateIntoBowl()
        {
            if (_activeSpawnedDuplicate == null) return;

            GameObject prop = _activeSpawnedDuplicate;
            _activeSpawnedDuplicate = null;

            if (ParentHotkeyRouter.Instance != null && ParentHotkeyRouter.Instance.CurrentlyHeldProp == prop)
            {
                ParentHotkeyRouter.Instance.CurrentlyHeldProp = null;
            }

            prop.transform.SetParent(null, true);

            // Notify RamenAssemblyReceiver
            RamenAssemblyReceiver receiver = RamenAssemblyReceiver.Instance;
            if (receiver != null)
            {
                string t = ingredientType.ToLower();
                if (t.Contains("meat")) receiver.AddMeat();
                else if (t.Contains("egg")) receiver.AddEgg();
                else if (t.Contains("onion")) receiver.AddOnions();
            }

            // Destroy the loose duplicate prop
            Destroy(prop);
            onIngredientReleased?.Invoke();
            Debug.Log($"[IngredientSpawnerBowl] Released '{ingredientType}' duplicate into bowl.");
        }
    }
}
