using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace YataiTogether
{
    /// <summary>
    /// Step 6: Boiler Pot Snap Receiver.
    /// Attached to "potSnapPoint" child of "boiler".
    /// Detects physical incoming noodle colliders (tag "Noodle" or NoodleStretch component)
    /// and handles discrete hotkey Key 4 / Key B snap-and-boil execution.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class BoilerPotSnapReceiver : MonoBehaviour
    {
        public static BoilerPotSnapReceiver Instance { get; private set; }

        [Header("Snap & Boiling Configuration")]
        [SerializeField] private Transform snapTransform;
        [SerializeField] private float boilDuration = 3.0f;
        [SerializeField] private GameObject cookedNoodleVisual;

        [Header("Runtime State")]
        [SerializeField] private bool hasNoodleInPot = false;
        [SerializeField] private bool isCooked = false;
        [SerializeField] private float currentBoilTimer = 0f;

        [Header("Boiler Events")]
        public UnityEvent onBoilStarted = new UnityEvent();
        public UnityEvent onBoilFinished = new UnityEvent();
        public UnityEvent onNoodleScooped = new UnityEvent();

        private GameObject _containedNoodle;
        private Transform _rightHandAnchor;

        public bool IsCooked => isCooked;
        public bool HasNoodleInPot => hasNoodleInPot;
        public float BoilDuration => boilDuration;
        // The director's B hold accumulator is the single boil timer (release pauses, completion latches once).
        public float CurrentBoilTimer => YataiSessionDirector.Instance != null && !isCooked
            ? YataiSessionDirector.Instance.BHoldAccumulated
            : currentBoilTimer;
        public float BoilProgress => isCooked ? 1f
            : (YataiSessionDirector.Instance != null ? YataiSessionDirector.Instance.BHoldProgress
                : (boilDuration > 0f ? Mathf.Clamp01(currentBoilTimer / boilDuration) : 1f));
        /// <summary>True when cooked noodles are waiting in the pot (or the hotkey-only cooked visual is shown).</summary>
        public bool HasCookedNoodleReady => isCooked && (hasNoodleInPot ||
            (cookedNoodleVisual != null && cookedNoodleVisual.activeSelf));
        public GameObject ContainedNoodle => _containedNoodle;
        public Transform SnapTransform => snapTransform != null ? snapTransform : transform;
        public GameObject CookedNoodleVisual => cookedNoodleVisual;

        public void Configure(Transform snap, float duration, GameObject cookedVisual)
        {
            snapTransform = snap;
            boilDuration = duration;
            cookedNoodleVisual = cookedVisual;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            // Ensure collider is set to trigger
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            if (snapTransform == null)
            {
                snapTransform = transform;
            }

            EnsureCookedNoodleVisual();
        }

        private void OnEnable()
        {
            if (ParentHotkeyRouter.Instance != null)
            {
                ParentHotkeyRouter.Instance.HoldStarted += HandleHoldStarted;
            }
        }

        private void Start()
        {
            if (ParentHotkeyRouter.Instance != null)
            {
                ParentHotkeyRouter.Instance.HoldStarted -= HandleHoldStarted;
                ParentHotkeyRouter.Instance.HoldStarted += HandleHoldStarted;
            }
        }

        private void OnDisable()
        {
            if (ParentHotkeyRouter.Instance != null)
            {
                ParentHotkeyRouter.Instance.HoldStarted -= HandleHoldStarted;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;

            // 1. Cooked noodle waiting in the pot: spoon scoops it, or a real hand reaching in takes it
            if (HasCookedNoodleReady)
            {
                Transform spoonAttach = FindSpoonAttachPoint(other);
                if (spoonAttach != null)
                {
                    if (hasNoodleInPot) TransferNoodleToSpoon(spoonAttach);
                }
                else if (IngredientSpawnerBowl.IsVRHandCollider(other) &&
                         ParentHotkeyRouter.Instance != null && ParentHotkeyRouter.Instance.CurrentlyHeldProp == null)
                {
                    TakeCookedNoodleToHand();
                }
                return;
            }

            // 2. If pot is empty, check for incoming noodle to snap and boil
            if (!hasNoodleInPot && !isCooked)
            {
                if (IsNoodleMatch(other))
                {
                    SnapAndBoilNoodle(other.gameObject);
                }
            }
        }

        /// <summary>
        /// Checks if the collider belongs to a noodle via tag "Noodle", NoodleStretch component, or name.
        /// </summary>
        public bool IsNoodleMatch(Collider other)
        {
            if (other == null) return false;

            // Never snap the stationary noodle_bowl display mesh (its collider overlaps the boiler trigger)
            if (IngredientSpawnerBowl.IsStationaryDisplayMesh(other.gameObject)) return false;

            // Tag check ("Noodle" or "noodle")
            try
            {
                if (other.CompareTag("Noodle") || other.CompareTag("noodle"))
                {
                    return true;
                }
            }
            catch { }

            // Component check: NoodleStretch or StretchableNoodle
            if (other.GetComponent<NoodleStretch>() != null || other.GetComponentInParent<NoodleStretch>() != null)
            {
                return true;
            }

            if (other.GetComponent<StretchableNoodle>() != null || other.GetComponentInParent<StretchableNoodle>() != null)
            {
                return true;
            }

            // Fallback name check
            string n = other.gameObject.name.ToLower();
            return n.Contains("noodle");
        }

        /// <summary>
        /// Snaps the noodle into the boiler pot snap point, disables hand tracking / grab interactables,
        /// sets Rigidbody to kinematic, parents to snapTransform, and begins the 3.0s boiling countdown.
        /// </summary>
        public void SnapAndBoilNoodle(GameObject noodleObj)
        {
            if (noodleObj == null) return;

            // 1. Disable grabbable components or force release from VR hand interactor
            DisableGrabbableComponents(noodleObj);

            // 2. If Rigidbody exists, set isKinematic = true and useGravity = false
            Rigidbody rb = noodleObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // ponytail: Zero velocity only while dynamic before setting kinematic
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // 2b. The noodle is leaving the chef's hand: clear every hand-ownership reference so the
            //     hand is empty and later A/Enter or Key 4 presses can't destroy or duplicate it.
            ReleaseHandOwnership(noodleObj);

            // 3. Parent noodleObj to snapTransform, set localPosition to Vector3.zero
            Transform targetSnap = snapTransform != null ? snapTransform : transform;
            noodleObj.transform.SetParent(targetSnap, true);
            noodleObj.transform.localPosition = Vector3.zero;
            noodleObj.transform.localRotation = Quaternion.identity;

            _containedNoodle = noodleObj;
            hasNoodleInPot = true;
            isCooked = false;
            currentBoilTimer = 0f;

            if (cookedNoodleVisual != null)
            {
                cookedNoodleVisual.SetActive(false);
            }

            // Notify listeners. The boil itself only advances while the parent holds B (director timer).
            onBoilStarted?.Invoke();
            Debug.Log($"[BoilerPotSnapReceiver] Noodle snapped to '{targetSnap.name}'. Hold [B] {boilDuration:F1}s to boil.");

            // Coordinate with parent BoilerStation if present
            if (BoilerStation.Instance != null && !BoilerStation.Instance.HasNoodleInPot)
            {
                BoilerStation.Instance.AcceptNoodleIntoBoiler(noodleObj);
            }

            // Advance director to boil step if not already there
            if (YataiSessionDirector.Instance != null &&
                YataiSessionDirector.Instance.CurrentStep == CookingStep.Noodles_Stretch)
            {
                YataiSessionDirector.Instance.SetStep(CookingStep.Noodles_Boil);
            }
        }

        /// <summary>
        /// Flags boiling as finished (called by the director when the B hold reaches 3.0s), enables the cooked
        /// noodle visual if no real noodle is in the pot, and waits for the child to take it out (Key 4 / hand / spoon).
        /// </summary>
        public void FinishCooking()
        {
            if (isCooked) return;

            isCooked = true;
            currentBoilTimer = boilDuration;

            // 4. On completion, flag cooked and allow spoon pickup. Show cookedNoodleVisual ONLY when no real
            //    noodle is sitting in the pot (hotkey-only path); otherwise the pot showed two noodles.
            if (cookedNoodleVisual != null)
            {
                cookedNoodleVisual.SetActive(_containedNoodle == null || _containedNoodle == cookedNoodleVisual);
            }

            onBoilFinished?.Invoke();
            Debug.Log("[BoilerPotSnapReceiver] Boiling complete (3.0s)! Take the noodles out with [4] (or hand / spoon).");

            if (BoilerStation.Instance != null && !BoilerStation.Instance.IsCooked)
            {
                BoilerStation.Instance.FinishCooking(autoTransferToBowl: false);
            }
        }

        private static void ReleaseHandOwnership(GameObject noodleObj)
        {
            ParentHotkeyRouter router = ParentHotkeyRouter.Instance;
            if (router != null && router.CurrentlyHeldProp == noodleObj)
            {
                router.CurrentlyHeldProp = null;
            }

            IngredientSpawnerBowl[] spawners = FindObjectsByType<IngredientSpawnerBowl>(FindObjectsSortMode.None);
            for (int i = 0; i < spawners.Length; i++)
            {
                if (spawners[i] != null) spawners[i].ForgetDuplicate(noodleObj);
            }

            if (StretchableNoodle.Instance != null && StretchableNoodle.Instance.IsHeldInHand)
            {
                StretchableNoodle.Instance.OnNoodleDetachedToBoiler(noodleObj);
            }
        }

        /// <summary>
        /// Boiler step hand-off: when the parent starts holding Key B during Noodles_Boil while the chef
        /// still holds the noodle, the noodle leaves the VR hand and sits on top of the boiler.
        /// </summary>
        private void HandleHoldStarted(ParentAction action)
        {
            if (action != ParentAction.BoilHold || hasNoodleInPot) return;

            YataiSessionDirector director = YataiSessionDirector.Instance;
            if (director == null || director.CurrentStep != CookingStep.Noodles_Boil) return;

            ParentHotkeyRouter router = ParentHotkeyRouter.Instance;
            if (router != null && router.IsHoldingNoodle())
            {
                router.ConfirmNoodleStretchAndBoil();
            }
        }

        /// <summary>
        /// Empties the pot (Backspace zero-penalty completion, or a fresh bowl).
        /// </summary>
        public void ResetPot()
        {
            if (_containedNoodle != null && _containedNoodle != cookedNoodleVisual)
            {
                Destroy(_containedNoodle);
            }
            _containedNoodle = null;

            if (cookedNoodleVisual != null)
            {
                cookedNoodleVisual.SetActive(false);
            }

            hasNoodleInPot = false;
            isCooked = false;
            currentBoilTimer = 0f;
        }

        private void DisableGrabbableComponents(GameObject target)
        {
            // Meta Interaction SDK components
            var grabbables = target.GetComponentsInChildren<Oculus.Interaction.Grabbable>(true);
            for (int i = 0; i < grabbables.Length; i++) grabbables[i].enabled = false;

            var grabInteractables = target.GetComponentsInChildren<Oculus.Interaction.GrabInteractable>(true);
            for (int i = 0; i < grabInteractables.Length; i++) grabInteractables[i].enabled = false;

            var handGrabs = target.GetComponentsInChildren<Oculus.Interaction.HandGrab.HandGrabInteractable>(true);
            for (int i = 0; i < handGrabs.Length; i++) handGrabs[i].enabled = false;

            var distanceGrabs = target.GetComponentsInChildren<Oculus.Interaction.DistanceGrabInteractable>(true);
            for (int i = 0; i < distanceGrabs.Length; i++) distanceGrabs[i].enabled = false;

            // Unity XR Interaction Toolkit components if present
            var xriGrabs = target.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>(true);
            for (int i = 0; i < xriGrabs.Length; i++) xriGrabs[i].enabled = false;
        }

        private Transform FindSpoonAttachPoint(Collider col)
        {
            GameObject go = col.gameObject;
            string n = go.name.ToLower();
            string rootName = go.transform.root.name.ToLower();

            if (n.Contains("spoon") || rootName.Contains("spoon"))
            {
                Transform attach = go.transform.Find("spoon_attach_point");
                if (attach == null && go.transform.parent != null)
                {
                    attach = go.transform.parent.Find("spoon_attach_point");
                }
                if (attach == null)
                {
                    GameObject newAttach = new GameObject("spoon_attach_point");
                    newAttach.transform.SetParent(go.transform, false);
                    newAttach.transform.localPosition = new Vector3(0f, 0.05f, 0.15f);
                    attach = newAttach.transform;
                }
                return attach;
            }
            return null;
        }

        public void TransferNoodleToSpoon(Transform spoonAttachPoint)
        {
            if (spoonAttachPoint == null) return;
            if (_containedNoodle == null && cookedNoodleVisual == null) return;

            GameObject noodleToTransfer = _containedNoodle != null ? _containedNoodle : cookedNoodleVisual;
            noodleToTransfer.transform.SetParent(spoonAttachPoint, true);
            noodleToTransfer.transform.localPosition = Vector3.zero;
            noodleToTransfer.transform.localRotation = Quaternion.identity;

            hasNoodleInPot = false;
            _containedNoodle = null;
            if (cookedNoodleVisual != null && noodleToTransfer != cookedNoodleVisual) cookedNoodleVisual.SetActive(false);
            if (BoilerStation.Instance != null) BoilerStation.Instance.ForgetContainedNoodle();

            onNoodleScooped?.Invoke();
            Debug.Log("[BoilerPotSnapReceiver] Cooked noodle scooped onto spoon tip!");
        }

        /// <summary>Drops the pot's reference to its noodle without destroying it (it left the pot another way).</summary>
        public void ForgetContainedNoodle()
        {
            _containedNoodle = null;
            hasNoodleInPot = false;
        }

        /// <summary>
        /// Noodles_Transfer: takes the cooked noodle out of the pot into the chef's right hand (Key 4, or a real
        /// hand reaching into the pot). The child then drops it into lamian_bowl with A/Enter or by touching the bowl.
        /// </summary>
        public GameObject TakeCookedNoodleToHand(Transform hand = null)
        {
            if (!HasCookedNoodleReady) return null;

            if (hand == null) hand = ResolveRightHandAnchor();

            GameObject noodle = _containedNoodle;
            if (noodle == null || noodle == cookedNoodleVisual)
            {
                // Hotkey-only path: no real noodle reached the pot, so carry a copy of the cooked visual.
                noodle = Instantiate(cookedNoodleVisual, cookedNoodleVisual.transform.position, cookedNoodleVisual.transform.rotation);
                noodle.transform.localScale = cookedNoodleVisual.transform.lossyScale;
                noodle.name = "Cooked_noodle";
                noodle.SetActive(true);
            }

            if (cookedNoodleVisual != null) cookedNoodleVisual.SetActive(false);
            _containedNoodle = null;
            hasNoodleInPot = false;
            if (BoilerStation.Instance != null) BoilerStation.Instance.ForgetContainedNoodle();

            // Physics standard (AGENTS.md §7): kinematic while carried, solid collider so lamian_bowl's trigger sees it.
            Rigidbody rb = noodle.GetComponent<Rigidbody>();
            if (rb == null) rb = noodle.AddComponent<Rigidbody>();
            rb.mass = 0.12f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.isKinematic = true;
            rb.useGravity = false;
            Collider col = noodle.GetComponent<Collider>();
            if (col == null) col = noodle.AddComponent<BoxCollider>();
            col.isTrigger = false;

            if (hand != null)
            {
                noodle.transform.SetParent(hand, true);
                noodle.transform.position = hand.position + hand.forward * 0.08f;
            }
            else
            {
                noodle.transform.SetParent(null, true);
            }

            if (ParentHotkeyRouter.Instance != null) ParentHotkeyRouter.Instance.CurrentlyHeldProp = noodle;

            Debug.Log("[BoilerPotSnapReceiver] Cooked noodles taken out of the pot into the chef's hand. Drop into lamian_bowl with [A]/[Enter].");
            return noodle;
        }

        private Transform ResolveRightHandAnchor()
        {
            if (_rightHandAnchor != null) return _rightHandAnchor;
            GameObject rig = GameObject.Find("[BuildingBlock] Camera Rig") ?? GameObject.Find("OVRCameraRig");
            Transform tracking = rig != null ? rig.transform.Find("TrackingSpace") : null;
            if (tracking != null)
            {
                _rightHandAnchor = tracking.Find("RightHandAnchor") ?? tracking.Find("RightControllerAnchor");
            }
            return _rightHandAnchor;
        }

        private void EnsureCookedNoodleVisual()
        {
            EnsureCookedNoodleVisualExists();
            MatchRawNoodleWorldScale();
        }

        /// <summary>
        /// Gives the cooked visual the same world size as the raw Test Street03 'noodle'. Copying the raw
        /// noodle's local scale (x80) under the x80 boiler made it ~10 m wide.
        /// </summary>
        private void MatchRawNoodleWorldScale()
        {
            if (cookedNoodleVisual == null) return;
            GameObject rawNoodle = GameObject.Find("Test Street03/noodle")
                                ?? GameObject.Find("Test Street/noodle")
                                ?? GameObject.Find("noodle");
            Transform parent = cookedNoodleVisual.transform.parent;
            if (rawNoodle == null || parent == null) return;

            Vector3 world = rawNoodle.transform.lossyScale;
            Vector3 p = parent.lossyScale;
            cookedNoodleVisual.transform.localScale = new Vector3(
                p.x != 0f ? world.x / p.x : 1f,
                p.y != 0f ? world.y / p.y : 1f,
                p.z != 0f ? world.z / p.z : 1f);
        }

        private void EnsureCookedNoodleVisualExists()
        {
            if (cookedNoodleVisual != null) return;

            Transform found = transform.Find("cookedNoodleVisual");
            if (found != null)
            {
                // If it was an old primitive cylinder, destroy and replace with authentic noodle mesh
                if (found.GetComponent<MeshFilter>()?.sharedMesh?.name.Contains("Cylinder") == true)
                {
                    Destroy(found.gameObject);
                }
                else
                {
                    cookedNoodleVisual = found.gameObject;
                    cookedNoodleVisual.SetActive(false);
                    return;
                }
            }

            // Clone authentic noodle mesh from Test Street03 (never use a random yellow block or primitive cylinder)
            GameObject rawNoodle = GameObject.Find("Test Street03/noodle")
                                ?? GameObject.Find("Test Street/noodle")
                                ?? GameObject.Find("noodle");

            GameObject visual;
            if (rawNoodle != null)
            {
                visual = Instantiate(rawNoodle, snapTransform != null ? snapTransform : transform);
                visual.name = "cookedNoodleVisual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;

                Collider col = visual.GetComponent<Collider>();
                if (col != null) Destroy(col);

                Rigidbody rb = visual.GetComponent<Rigidbody>();
                if (rb != null) Destroy(rb);

                var grabbables = visual.GetComponentsInChildren<Oculus.Interaction.Grabbable>(true);
                for (int i = 0; i < grabbables.Length; i++) Destroy(grabbables[i]);

                var handGrabs = visual.GetComponentsInChildren<Oculus.Interaction.HandGrab.HandGrabInteractable>(true);
                for (int i = 0; i < handGrabs.Length; i++) Destroy(handGrabs[i]);
            }
            else
            {
                visual = new GameObject("cookedNoodleVisual");
                visual.transform.SetParent(snapTransform != null ? snapTransform : transform, false);
                visual.transform.localPosition = Vector3.zero;
            }

            cookedNoodleVisual = visual;
            cookedNoodleVisual.SetActive(false);
        }
    }
}
