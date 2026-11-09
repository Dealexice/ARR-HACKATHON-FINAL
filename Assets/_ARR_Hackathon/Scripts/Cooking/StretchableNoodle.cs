using UnityEngine;

namespace YataiTogether
{
    /// <summary>
    /// Step 5: Stretchable Noodle Mechanic (Pinch Key 4 -> 2-Hand Stretch Hold W).
    /// Enforces AGENTS.md §4 CP Movement Mapping #3:
    /// - Pinch yarn noodle element in Zone B (Key 4) snaps 3D noodle element to primary hand.
    /// - Child brings second hand and pulls hands outward apart (bimanual abduction) or parent holds Key W.
    /// - In VR, the 3D noodle element dynamically stretches between both hands in real-time.
    /// - Accumulating 3.0s of hold W (or stretching past the abduction threshold) morphs the dough
    ///   into long, fresh, bouncy ramen noodles ready for the boiling pot (Hold B 3s).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StretchableNoodle : MonoBehaviour
    {
        public static StretchableNoodle Instance { get; private set; }

        [Header("Scene Object References")]
        [SerializeField] private Transform rawNoodleClusterInZoneB;
        [SerializeField] private Transform leftHandAnchor;
        [SerializeField] private Transform rightHandAnchor;

        [Header("Stretch Visuals")]
        [SerializeField] private Transform stretchDoughMesh;
        [SerializeField] private Transform cookedNoodlesMesh;
        [SerializeField] private float baseLength = 0.12f;
        [SerializeField] private float maxStretchedLength = 0.55f;

        [Header("State")]
        private bool _isHeldInHand = false;
        private bool _isFullyStretched = false;
        private Transform _primaryHand;
        private Transform _secondaryHand;
        private Vector3 _noodleBaseScale = new Vector3(0.003f, 0.003f, 0.003f);

        // The single noodle actually in the chef's right hand. When the noodle_bowl IngredientSpawnerBowl
        // owns the spawned prop we stretch THAT object; our own clone is only a fallback when no spawner exists.
        private Transform _stretchTarget;
        private Vector3 _stretchTargetBaseScale;
        private Transform _cachedFor;
        private MeshFilter _meshFilter;
        private float _baseWorldLength = 0.12f;
        private int _stretchAxis;

        public bool IsHeldInHand => _isHeldInHand;
        public bool IsFullyStretched => _isFullyStretched;
        public GameObject HeldDoughObject
        {
            get
            {
                if (_stretchTarget != null) return _stretchTarget.gameObject;
                if (!HasNoodleSpawner() && stretchDoughMesh != null && stretchDoughMesh.gameObject.activeSelf)
                    return stretchDoughMesh.gameObject;
                return null;
            }
        }

        /// <summary>
        /// Called once the held noodle has been snapped into the boiler pot. The hand is now empty.
        /// </summary>
        public void OnNoodleDetachedToBoiler(GameObject snappedNoodle = null)
        {
            // Back to its unstretched bundle size so it fits the pot (still world-space here: callers
            // unparent it from the hands before re-parenting to the pot with world scale kept).
            if (_stretchTarget != null && _cachedFor == _stretchTarget)
            {
                _stretchTarget.localScale = _stretchTargetBaseScale;
            }

            _isHeldInHand = false;
            _isFullyStretched = true;
            _stretchTarget = null;
            _cachedFor = null;

            if (stretchDoughMesh != null)
            {
                if (snappedNoodle != null && snappedNoodle == stretchDoughMesh.gameObject)
                {
                    // Fallback clone is now the noodle sitting in the pot: keep it visible there and
                    // build a fresh clone for the next customer.
                    stretchDoughMesh = null;
                    EnsureNoodleVisuals();
                }
                else
                {
                    stretchDoughMesh.gameObject.SetActive(false);
                }
            }
        }

        private static bool HasNoodleSpawner()
        {
            IngredientSpawnerBowl[] spawners = FindObjectsByType<IngredientSpawnerBowl>(FindObjectsSortMode.None);
            for (int i = 0; i < spawners.Length; i++)
            {
                if (spawners[i] != null && spawners[i].TriggeringHotkey == ParentAction.GrabNoodles) return true;
            }
            return false;
        }

        private static GameObject FindSpawnedHandNoodle()
        {
            IngredientSpawnerBowl[] spawners = FindObjectsByType<IngredientSpawnerBowl>(FindObjectsSortMode.None);
            for (int i = 0; i < spawners.Length; i++)
            {
                if (spawners[i] != null && spawners[i].TriggeringHotkey == ParentAction.GrabNoodles &&
                    spawners[i].ActiveSpawnedDuplicate != null)
                {
                    return spawners[i].ActiveSpawnedDuplicate;
                }
            }
            return null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            EnsureHandReferences();
            EnsureNoodleVisuals();
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
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void HandleActionTapped(ParentAction action)
        {
            if (action == ParentAction.GrabNoodles)
            {
                AttachNoodlesToHand();
            }
        }

        private void HandleStepChanged(CookingStep step)
        {
            if (step == CookingStep.Noodles_Boil)
            {
                OnStretchCompleted();
            }
            else if (step == CookingStep.Noodles_Grab)
            {
                ResetNoodlesForNewCustomer();
            }
        }

        private void Update()
        {
            if (!_isHeldInHand) return;

            UpdateStretchVisuals();
        }

        public void AttachNoodlesToHand()
        {
            // Single-noodle rule: ignore Key 4 when a noodle is already in hand or it is not the noodle step.
            if (_isHeldInHand) return;
            if (YataiSessionDirector.Instance != null)
            {
                CookingStep step = YataiSessionDirector.Instance.CurrentStep;
                if (step != CookingStep.Noodles_Grab && step != CookingStep.Noodles_Stretch) return;
            }

            EnsureHandReferences();
            _isHeldInHand = true;
            _isFullyStretched = false;
            _stretchTarget = null;
            _cachedFor = null;

            bool spawnerOwnsHandNoodle = HasNoodleSpawner();

            if (!spawnerOwnsHandNoodle)
            {
                // Fallback only: no noodle_bowl spawner, so our authentic-mesh clone is the one hand noodle.
                if (rawNoodleClusterInZoneB != null)
                {
                    rawNoodleClusterInZoneB.gameObject.SetActive(false);
                }

                if (stretchDoughMesh == null)
                {
                    EnsureNoodleVisuals();
                }

                if (stretchDoughMesh != null)
                {
                    stretchDoughMesh.gameObject.SetActive(true);
                    stretchDoughMesh.localScale = _noodleBaseScale;
                    _stretchTarget = stretchDoughMesh;
                    _cachedFor = null;
                }
            }
            else if (stretchDoughMesh != null)
            {
                // Never show a second noodle next to the spawner's hand noodle.
                stretchDoughMesh.gameObject.SetActive(false);
            }

            if (cookedNoodlesMesh != null)
            {
                cookedNoodlesMesh.gameObject.SetActive(false);
            }

            Debug.Log("[StretchableNoodle] Raw noodles picked up into chef's hand! Ready for 2-hand stretch (Hold W).");
        }

        /// <summary>
        /// Two-hand pinned stretch: the noodle's two ends stay on the two virtual hands. Its long axis points
        /// hand-to-hand and its length always equals the hand distance (W is only the hold timer).
        /// Tracking loss: the hand anchors hold their last good pose, so the noodle simply stays put.
        /// </summary>
        private void UpdateStretchVisuals()
        {
            // Resolve the spawner's hand noodle lazily (event order between listeners is not guaranteed).
            if (_stretchTarget == null)
            {
                GameObject spawned = FindSpawnedHandNoodle();
                if (spawned != null)
                {
                    _stretchTarget = spawned.transform;
                }
            }

            if (_stretchTarget == null) return;

            if (_cachedFor != _stretchTarget)
            {
                CacheStretchTarget();
            }

            EnsureHandReferences();
            Vector3 pR = HandPinchPoint(_primaryHand, new Vector3(7.20f, 1.35f, 0.45f));
            Vector3 pL = HandPinchPoint(_secondaryHand, pR + new Vector3(0f, 0f, 0.30f));
            if (_primaryHand == _secondaryHand) pL = pR + new Vector3(0f, 0f, 0.30f);

            Vector3 span = pL - pR;
            float handDistance = span.magnitude;
            Vector3 dir = handDistance > 1e-4f ? span / handDistance : _stretchTarget.rotation * LocalAxis(_stretchAxis);

            // 1. Orient the long mesh axis along the hand-to-hand line (minimal rotation, no 180 flips).
            Vector3 axisWorld = _stretchTarget.rotation * LocalAxis(_stretchAxis);
            if (Vector3.Dot(axisWorld, dir) < 0f) dir = -dir;
            _stretchTarget.rotation = Quaternion.FromToRotation(axisWorld, dir) * _stretchTarget.rotation;

            // 2. Length = hand distance (clamped), thinning the other two axes.
            float length = Mathf.Clamp(handDistance, _baseWorldLength, maxStretchedLength);
            float stretchRatio = _baseWorldLength > 1e-5f ? length / _baseWorldLength : 1f;
            float thinRatio = 1.0f / Mathf.Max(1.0f, Mathf.Sqrt(stretchRatio));
            int axis = _stretchAxis;
            Vector3 b = _stretchTargetBaseScale;
            _stretchTarget.localScale = new Vector3(
                b.x * (axis == 0 ? stretchRatio : thinRatio),
                b.y * (axis == 1 ? stretchRatio : thinRatio),
                b.z * (axis == 2 ? stretchRatio : thinRatio));

            // 3. Centre the mesh (not its pivot) between both hands so each end lands on a hand.
            Vector3 mid = (pL + pR) * 0.5f;
            _stretchTarget.position += mid - MeshWorldCenter();
        }

        private void CacheStretchTarget()
        {
            _cachedFor = _stretchTarget;

            // World-space driven while pinned: unparent from RightHandAnchor (keeps world scale) and keep the
            // Meta grab stack present but disabled (AGENTS.md §7) so its transformers can't fight the pinning.
            _stretchTarget.SetParent(null, true);
            SetGrabStackEnabled(_stretchTarget.gameObject, false);

            Rigidbody rb = _stretchTarget.GetComponent<Rigidbody>();
            if (rb != null)
            {
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            _stretchTargetBaseScale = _stretchTarget.localScale;
            _stretchAxis = NoodleTwoHandStretchTransformer.ResolveLongAxis(_stretchTarget.gameObject);
            _meshFilter = _stretchTarget.GetComponentInChildren<MeshFilter>();

            _baseWorldLength = baseLength;
            if (_meshFilter != null && _meshFilter.sharedMesh != null)
            {
                Vector3 size = _meshFilter.sharedMesh.bounds.size;
                Vector3 ls = _meshFilter.transform.lossyScale;
                _baseWorldLength = Mathf.Abs(size[_stretchAxis] * ls[_stretchAxis]);
            }
            _baseWorldLength = Mathf.Max(0.02f, _baseWorldLength);
        }

        private Vector3 MeshWorldCenter()
        {
            if (_meshFilter != null && _meshFilter.sharedMesh != null)
            {
                return _meshFilter.transform.TransformPoint(_meshFilter.sharedMesh.bounds.center);
            }
            return _stretchTarget.position;
        }

        private static Vector3 HandPinchPoint(Transform hand, Vector3 fallback)
        {
            // Same pinch offset the ingredient spawner uses for props in the hand.
            return hand != null ? hand.position + hand.forward * 0.08f : fallback;
        }

        private static Vector3 LocalAxis(int axis)
        {
            return axis == 1 ? Vector3.up : axis == 2 ? Vector3.forward : Vector3.right;
        }

        private static void SetGrabStackEnabled(GameObject target, bool enabled)
        {
            var grabbables = target.GetComponentsInChildren<Oculus.Interaction.Grabbable>(true);
            for (int i = 0; i < grabbables.Length; i++) grabbables[i].enabled = enabled;

            var grabInteractables = target.GetComponentsInChildren<Oculus.Interaction.GrabInteractable>(true);
            for (int i = 0; i < grabInteractables.Length; i++) grabInteractables[i].enabled = enabled;

            var handGrabs = target.GetComponentsInChildren<Oculus.Interaction.HandGrab.HandGrabInteractable>(true);
            for (int i = 0; i < handGrabs.Length; i++) handGrabs[i].enabled = enabled;
        }

        private void OnStretchCompleted()
        {
            _isFullyStretched = true;
            Debug.Log("[StretchableNoodle] Noodle stretch complete! Dough morphed into bouncy fresh ramen noodle bundle.");

            if (cookedNoodlesMesh != null)
            {
                cookedNoodlesMesh.gameObject.SetActive(true);
            }
        }

        public void ResetNoodlesForNewCustomer()
        {
            _isHeldInHand = false;
            _isFullyStretched = false;
            _stretchTarget = null;
            _cachedFor = null;

            if (rawNoodleClusterInZoneB != null)
            {
                rawNoodleClusterInZoneB.gameObject.SetActive(true);
            }

            if (stretchDoughMesh != null)
            {
                stretchDoughMesh.gameObject.SetActive(false);
            }

            if (cookedNoodlesMesh != null)
            {
                cookedNoodlesMesh.gameObject.SetActive(false);
            }
        }

        private void EnsureHandReferences()
        {
            if (rightHandAnchor == null || leftHandAnchor == null)
            {
                GameObject rig = GameObject.Find("[BuildingBlock] Camera Rig");
                if (rig != null)
                {
                    Transform tracking = rig.transform.Find("TrackingSpace");
                    if (tracking != null)
                    {
                        rightHandAnchor = tracking.Find("RightHandAnchor");
                        leftHandAnchor = tracking.Find("LeftHandAnchor");
                    }
                }
            }

            _primaryHand = rightHandAnchor != null ? rightHandAnchor : leftHandAnchor;
            _secondaryHand = leftHandAnchor != null ? leftHandAnchor : rightHandAnchor;
        }

        private void EnsureNoodleVisuals()
        {
            if (rawNoodleClusterInZoneB == null)
            {
                GameObject n = GameObject.Find("Test Street03/noodle")
                            ?? GameObject.Find("Test Street/noodle")
                            ?? GameObject.Find("noodle");
                if (n != null) rawNoodleClusterInZoneB = n.transform;
            }

            // Remove legacy yellow cylinder placeholder if present in scene
            GameObject oldCylinder = GameObject.Find("ARR_StretchDoughCylinder");
            if (oldCylinder != null)
            {
                Destroy(oldCylinder);
            }

            if (stretchDoughMesh == null)
            {
                GameObject existing = GameObject.Find("ARR_StretchingNoodleVisual");
                if (existing != null)
                {
                    stretchDoughMesh = existing.transform;
                    _noodleBaseScale = stretchDoughMesh.localScale;
                }
                else if (rawNoodleClusterInZoneB != null)
                {
                    // Clone authentic noodle mesh from Test Street03 (never use a random yellow block)
                    GameObject doughObj = Instantiate(rawNoodleClusterInZoneB.gameObject);
                    doughObj.name = "ARR_StretchingNoodleVisual";

                    Collider col = doughObj.GetComponent<Collider>();
                    if (col != null) Destroy(col);

                    Rigidbody rb = doughObj.GetComponent<Rigidbody>();
                    if (rb != null) Destroy(rb);

                    var grabbables = doughObj.GetComponentsInChildren<Oculus.Interaction.Grabbable>(true);
                    for (int i = 0; i < grabbables.Length; i++) Destroy(grabbables[i]);

                    var handGrabs = doughObj.GetComponentsInChildren<Oculus.Interaction.HandGrab.HandGrabInteractable>(true);
                    for (int i = 0; i < handGrabs.Length; i++) Destroy(handGrabs[i]);

                    stretchDoughMesh = doughObj.transform;
                    _noodleBaseScale = rawNoodleClusterInZoneB.localScale;
                    stretchDoughMesh.localScale = _noodleBaseScale;
                    stretchDoughMesh.gameObject.SetActive(false);
                }
            }
        }
    }
}
