using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

namespace YataiTogether
{
    /// <summary>
    /// Step 5: Noodle Duplication & Stretch Controller.
    /// - Spawns a duplicate grabbable noodle from the noodle station bowl on grab / key 4.
    /// - Stretches localScale along Z (length) and scales X/Y (thickness) based on hand distance
    ///   or parent hotkey input (Hold W), clamped between 1.0x and 2.5x.
    /// - Compliant with Meta Interaction SDK (Rigidbody, Collider, Grabbable, GrabInteractable).
    /// </summary>
    [DisallowMultipleComponent]
    public class NoodleSpawnerAndStretch : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [Tooltip("Prefab of the stretchable noodle to duplicate when grabbed.")]
        [SerializeField] private GameObject noodlePrefab;

        [Tooltip("Spawn origin point inside the ingredient station bowl.")]
        [SerializeField] private Transform spawnPoint;

        [Tooltip("Parent container for spawned noodles (optional).")]
        [SerializeField] private Transform spawnedContainer;

        [Header("Hands Tracking References")]
        [SerializeField] private Transform primaryHandAnchor;
        [SerializeField] private Transform secondaryHandAnchor;

        [Header("Stretch Parameters")]
        [SerializeField] private Vector3 baseLocalScale = Vector3.one;
        [SerializeField, Range(1.0f, 1.5f)] private float minStretchMultiplier = 1.0f;
        [SerializeField, Range(2.0f, 3.5f)] private float maxStretchMultiplier = 2.5f;
        [SerializeField] private float stretchRate = 0.5f;

        [Header("State Tracking")]
        [SerializeField] private bool isSpawned = false;
        [SerializeField] private bool isStretched = false;
        [SerializeField] private float currentStretchFactor = 1.0f;

        // Active spawned instance references
        private GameObject _activeNoodleInstance;
        private Rigidbody _activeRigidbody;
        private Grabbable _activeGrabbable;
        private HandGrabInteractable _activeHandGrabInteractable;

        public bool IsStretched => isStretched;
        public float CurrentStretchFactor => currentStretchFactor;
        public GameObject ActiveNoodleInstance => _activeNoodleInstance;

        private void Awake()
        {
            if (spawnPoint == null)
            {
                spawnPoint = transform;
            }

            EnsureHandAnchors();
        }

        private void OnEnable()
        {
            if (ParentHotkeyRouter.Instance != null)
            {
                ParentHotkeyRouter.Instance.ActionTapped += HandleActionTapped;
                ParentHotkeyRouter.Instance.HoldTicked += HandleHoldTicked;
            }
        }

        private void Start()
        {
            if (ParentHotkeyRouter.Instance != null)
            {
                ParentHotkeyRouter.Instance.ActionTapped -= HandleActionTapped;
                ParentHotkeyRouter.Instance.ActionTapped += HandleActionTapped;
                ParentHotkeyRouter.Instance.HoldTicked -= HandleHoldTicked;
                ParentHotkeyRouter.Instance.HoldTicked += HandleHoldTicked;
            }
        }

        private void OnDisable()
        {
            if (ParentHotkeyRouter.Instance != null)
            {
                ParentHotkeyRouter.Instance.ActionTapped -= HandleActionTapped;
                ParentHotkeyRouter.Instance.HoldTicked -= HandleHoldTicked;
            }
        }

        private void Update()
        {
            if (_activeNoodleInstance == null) return;

            // Distance-based bimanual stretch calculation if both hands are tracked
            if (primaryHandAnchor != null && secondaryHandAnchor != null)
            {
                float distance = Vector3.Distance(primaryHandAnchor.position, secondaryHandAnchor.position);
                // Base hand distance ~0.15m up to full reach ~0.50m
                if (distance > 0.18f)
                {
                    float factor = Mathf.InverseLerp(0.18f, 0.50f, distance);
                    ApplyStretch(Mathf.Lerp(minStretchMultiplier, maxStretchMultiplier, factor));
                }
            }
        }

        private void HandleActionTapped(ParentAction action)
        {
            if (action == ParentAction.GrabNoodles)
            {
                SpawnNoodleToHand();
            }
        }

        private void HandleHoldTicked(ParentAction action, float dt)
        {
            if (action == ParentAction.BandPullStretch && _activeNoodleInstance != null)
            {
                float newFactor = Mathf.MoveTowards(currentStretchFactor, maxStretchMultiplier, stretchRate * dt);
                ApplyStretch(newFactor);
            }
        }

        /// <summary>
        /// Spawns a duplicate noodle prefab into the child's hand while the original stays in the bowl.
        /// Configures Rigidbody, Collider, Grabbable, and HandGrabInteractable.
        /// </summary>
        public GameObject SpawnNoodleToHand()
        {
            EnsureHandAnchors();

            Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
            Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

            if (noodlePrefab != null)
            {
                _activeNoodleInstance = Instantiate(noodlePrefab, spawnPos, spawnRot, spawnedContainer);
            }
            else
            {
                // Fallback procedural visual if prefab is not assigned in inspector
                _activeNoodleInstance = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                _activeNoodleInstance.name = "noodle_spawned";
                _activeNoodleInstance.transform.position = spawnPos;
                _activeNoodleInstance.transform.localScale = new Vector3(0.04f, 0.04f, 0.15f);

                Renderer r = _activeNoodleInstance.GetComponent<Renderer>();
                if (r != null)
                {
                    Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    if (sh != null)
                    {
                        Material mat = new Material(sh);
                        mat.color = new Color(0.96f, 0.85f, 0.40f, 1f);
                        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.96f, 0.85f, 0.40f, 1f));
                        r.sharedMaterial = mat;
                    }
                }
            }

            // Tag as noodle for downstream collider recognition
            _activeNoodleInstance.tag = "Untagged"; // Can set to "noodle" if tag exists in project

            // Ensure Meta Building Blocks components
            _activeRigidbody = _activeNoodleInstance.GetComponent<Rigidbody>();
            if (_activeRigidbody == null)
            {
                _activeRigidbody = _activeNoodleInstance.AddComponent<Rigidbody>();
            }
            _activeRigidbody.mass = 0.1f;
            _activeRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            Collider col = _activeNoodleInstance.GetComponent<Collider>();
            if (col == null)
            {
                col = _activeNoodleInstance.AddComponent<CapsuleCollider>();
            }

            _activeGrabbable = _activeNoodleInstance.GetComponent<Grabbable>();
            if (_activeGrabbable == null)
            {
                _activeGrabbable = _activeNoodleInstance.AddComponent<Grabbable>();
            }

            _activeHandGrabInteractable = _activeNoodleInstance.GetComponent<HandGrabInteractable>();
            if (_activeHandGrabInteractable == null)
            {
                _activeHandGrabInteractable = _activeNoodleInstance.AddComponent<HandGrabInteractable>();
                _activeHandGrabInteractable.InjectRigidbody(_activeRigidbody);
                _activeHandGrabInteractable.InjectOptionalPointableElement(_activeGrabbable);
            }

            // Snap directly to hand anchor if available
            Transform targetHand = primaryHandAnchor != null ? primaryHandAnchor : transform;
            _activeNoodleInstance.transform.position = targetHand.position;
            _activeNoodleInstance.transform.rotation = targetHand.rotation;

            currentStretchFactor = minStretchMultiplier;
            baseLocalScale = _activeNoodleInstance.transform.localScale;
            isSpawned = true;
            isStretched = false;

            Debug.Log("[NoodleSpawnerAndStretch] Duplicate noodle spawned into hand. Original stays in station bowl.");
            return _activeNoodleInstance;
        }

        public void ApplyStretch(float factor)
        {
            if (_activeNoodleInstance == null) return;

            currentStretchFactor = Mathf.Clamp(factor, minStretchMultiplier, maxStretchMultiplier);

            // Stretch along length (Z), thins slightly along X/Y
            float lengthScale = baseLocalScale.z * currentStretchFactor;
            float thicknessScale = baseLocalScale.x / Mathf.Sqrt(currentStretchFactor);

            _activeNoodleInstance.transform.localScale = new Vector3(
                thicknessScale,
                thicknessScale,
                lengthScale
            );

            if (currentStretchFactor >= maxStretchMultiplier - 0.05f)
            {
                isStretched = true;
            }
        }

        private void EnsureHandAnchors()
        {
            if (primaryHandAnchor == null || secondaryHandAnchor == null)
            {
                GameObject rig = GameObject.Find("[BuildingBlock] Camera Rig");
                if (rig != null)
                {
                    Transform tracking = rig.transform.Find("TrackingSpace");
                    if (tracking != null)
                    {
                        if (primaryHandAnchor == null) primaryHandAnchor = tracking.Find("RightHandAnchor");
                        if (secondaryHandAnchor == null) secondaryHandAnchor = tracking.Find("LeftHandAnchor");
                    }
                }
            }
        }
    }
}
