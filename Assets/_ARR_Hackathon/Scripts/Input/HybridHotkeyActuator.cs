using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using Unity.XR.CoreUtils;

namespace YataiTogether
{
    /// <summary>
    /// Hybrid Hotkey Actuator for Parent Sous-Chef physical keyboard controls & VR Actuation.
    /// Compliant with Meta Quest 3 & Unity 6.
    /// Implements:
    /// 1. VR POV height offset compensation (subtracting camera local Y).
    /// 2. Key 4 stretch confirmation and hand detach into BoilerPotSnapReceiver.
    /// </summary>
    [DisallowMultipleComponent]
    public class HybridHotkeyActuator : MonoBehaviour
    {
        public static HybridHotkeyActuator Instance { get; private set; }

        [Header("1. Dual Camera POV Anchors (Spacebar & Key V)")]
        [SerializeField] private Transform vrCameraRigRoot;
        [SerializeField] private Camera xrCamera;
        [SerializeField] private XROrigin xrOrigin;
        [SerializeField] private Transform pov1BikeAnchor;
        [SerializeField] private Transform pov2StallAnchor;
        [SerializeField] private Transform bikeCartWorldModel;
        [SerializeField] private float metersPerPedalClick = 10f;
        [SerializeField] private float targetRideDistance = 100f;

        [Header("2. Child Virtual Hand & Targets")]
        [SerializeField] private Transform childVirtualHandAttachPoint;
        [SerializeField] private Transform magicEggChuteSpawnPoint;
        [SerializeField] private Transform zoneCBowlDropTarget;
        [SerializeField] private BoilerPotSnapReceiver boilerPotReceiver;

        [Header("3. Ingredient & Tool Prefabs (Keys 1, 2, 3, 4, 5)")]
        [SerializeField] private GameObject chashuPorkPrefab;     // Key 1
        [SerializeField] private GameObject ajitamaEggPrefab;     // Key 2
        [SerializeField] private GameObject greenOnionsPrefab;    // Key 3
        [SerializeField] private GameObject rawNoodlesPrefab;     // Key 4
        [SerializeField] private GameObject brothLadleObject;     // Key 5

        [Header("4. Hold Timers (Keys W, B, F)")]
        [SerializeField] private float bandPullHoldDuration = 3.0f;   // Key W
        [SerializeField] private float boilerStretchDuration = 3.0f;  // Key B
        [SerializeField] private float handFlattenDuration = 5.0f;    // Key F

        [Header("5. Visual & Audio Events")]
        public UnityEvent<float> onBikeDistanceUpdated;           // Passes current meters (0-100)
        public UnityEvent<bool> onStallCurtainToggled;            // Key Q (True = Open, False = Closed)
        public UnityEvent<float> onBandPullProgress;              // Hold Key W (0.0 to 1.0)
        public UnityEvent onBandPullCompleted;
        public UnityEvent<float> onBoilerTimerProgress;           // Hold Key B (0.0 to 1.0)
        public UnityEvent onBoilerTimerFlashedGreen;
        public UnityEvent<float> onHandFlattenProgress;           // Hold Key F (0.0 to 1.0)
        public UnityEvent onGoldenNoriPressed;
        public UnityEvent onBrothPoured;                          // Key D
        public UnityEvent<int> onCustomerServedWithFireworks;     // Key S (Passes Customer 1-5)

        // Internal Runtime State
        private float _currentRideMeters = 0f;
        private bool _isAtStallPOV = false;
        private bool _isCurtainOpen = false;
        private GameObject _currentlyHeldProp = null;
        private float _bandHoldTimer = 0f;
        private float _boilerHoldTimer = 0f;
        private float _flattenHoldTimer = 0f;
        private int _customersServedCount = 0;
        private const int MaxCustomers = 5;

        public GameObject CurrentlyHeldProp => _currentlyHeldProp;
        public bool IsAtStallPOV => _isAtStallPOV;
        public float CurrentRideMeters => _currentRideMeters;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            EnsureReferences();
        }

        private void Start()
        {
            EnsureReferences();
            SnapCameraToPOV(false);
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            HandleBikeAndPOVHotkeys();
            HandleCurtainAndStretchHolds();
            HandleIngredientSpawnAndDropHotkeys();
            HandleLadleAndServingHotkeys();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void EnsureReferences()
        {
            if (xrOrigin == null) xrOrigin = FindFirstObjectByType<XROrigin>();
            if (vrCameraRigRoot == null)
            {
                if (xrOrigin != null) vrCameraRigRoot = xrOrigin.transform;
                else
                {
                    GameObject rig = GameObject.Find("[BuildingBlock] Camera Rig") ?? GameObject.Find("XR Origin (VR)");
                    if (rig != null) vrCameraRigRoot = rig.transform;
                }
            }

            if (xrCamera == null)
            {
                if (xrOrigin != null && xrOrigin.Camera != null) xrCamera = xrOrigin.Camera;
                else xrCamera = Camera.main;
            }

            if (pov1BikeAnchor == null)
            {
                GameObject b = GameObject.Find("POV_1_BikeSeatAnchor");
                if (b != null) pov1BikeAnchor = b.transform;
            }

            if (pov2StallAnchor == null)
            {
                GameObject s = GameObject.Find("POV_2_FoodStallCounterAnchor");
                if (s != null) pov2StallAnchor = s.transform;
            }

            if (childVirtualHandAttachPoint == null)
            {
                GameObject hand = GameObject.Find("RightHandAnchor") ?? GameObject.Find("RightControllerInHandAnchor");
                if (hand != null) childVirtualHandAttachPoint = hand.transform;
            }

            if (boilerPotReceiver == null)
            {
                boilerPotReceiver = BoilerPotSnapReceiver.Instance ?? FindFirstObjectByType<BoilerPotSnapReceiver>();
            }
        }

        /// <summary>
        /// REQUIREMENT 1: Fix VR POV Height Offset.
        /// When setting position to targetAnchor, handles tracking origin.
        /// Sets xrOrigin.RequestedTrackingOriginMode = Device (eye level)
        /// and offsets rig Y by subtracting camera local Y height.
        /// </summary>
        public void SnapCameraToPOV(bool toStallPOV)
        {
            EnsureReferences();
            _isAtStallPOV = toStallPOV;
            Transform targetAnchor = _isAtStallPOV ? pov2StallAnchor : pov1BikeAnchor;

            if (vrCameraRigRoot == null || targetAnchor == null) return;

            // Tracking origin mode eye-level enforcement
            if (xrOrigin != null)
            {
                xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            }
            if (OVRManager.instance != null)
            {
                OVRManager.instance.trackingOriginType = OVRManager.TrackingOrigin.EyeLevel;
            }

            // Offset rig Y position by subtracting camera's local Y height
            float camLocalY = xrCamera != null ? xrCamera.transform.localPosition.y : 0f;
            Vector3 targetPos = targetAnchor.position - new Vector3(0f, camLocalY, 0f);

            vrCameraRigRoot.SetPositionAndRotation(targetPos, targetAnchor.rotation);
            vrCameraRigRoot.SetParent(targetAnchor, true);

            Debug.Log($"[HybridHotkeyActuator] Snapped Camera Rig to {targetAnchor.name} at {targetPos:F3} (Height offset: -{camLocalY:F3}m)");
        }

        private void HandleBikeAndPOVHotkeys()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            // Spacebar: Bike +10m
            if (kb.spaceKey.wasPressedThisFrame && !_isAtStallPOV)
            {
                _currentRideMeters += metersPerPedalClick;
                if (bikeCartWorldModel != null)
                {
                    bikeCartWorldModel.position += bikeCartWorldModel.forward * (metersPerPedalClick * 0.2f);
                }
                onBikeDistanceUpdated?.Invoke(_currentRideMeters);

                if (_currentRideMeters >= targetRideDistance)
                {
                    SnapCameraToPOV(true);
                }
            }

            // Key V: Toggle POV
            if (kb.vKey.wasPressedThisFrame)
            {
                SnapCameraToPOV(!_isAtStallPOV);
            }
        }

        private void HandleCurtainAndStretchHolds()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            // Key Q: Toggle Curtain
            if (kb.qKey.wasPressedThisFrame)
            {
                _isCurtainOpen = !_isCurtainOpen;
                onStallCurtainToggled?.Invoke(_isCurtainOpen);
            }

            // Hold Key W: Band Pull Stretch (3.0s)
            if (kb.wKey.isPressed)
            {
                _bandHoldTimer += Time.deltaTime;
                onBandPullProgress?.Invoke(Mathf.Clamp01(_bandHoldTimer / bandPullHoldDuration));
                if (_bandHoldTimer >= bandPullHoldDuration)
                {
                    onBandPullCompleted?.Invoke();
                    _bandHoldTimer = 0f;
                }
            }

            // Hold Key B: Boiler Hold (3.0s)
            if (kb.bKey.isPressed)
            {
                _boilerHoldTimer += Time.deltaTime;
                onBoilerTimerProgress?.Invoke(Mathf.Clamp01(_boilerHoldTimer / boilerStretchDuration));
                if (_boilerHoldTimer >= boilerStretchDuration)
                {
                    onBoilerTimerFlashedGreen?.Invoke();
                    _boilerHoldTimer = 0f;
                    if (_currentlyHeldProp != null && IsNoodleProp(_currentlyHeldProp))
                    {
                        Destroy(_currentlyHeldProp);
                        _currentlyHeldProp = null;
                    }
                }
            }

            // Hold Key F: Hand Flatten / Golden Nori (5.0s)
            if (kb.fKey.isPressed)
            {
                _flattenHoldTimer += Time.deltaTime;
                onHandFlattenProgress?.Invoke(Mathf.Clamp01(_flattenHoldTimer / handFlattenDuration));
                if (_flattenHoldTimer >= handFlattenDuration)
                {
                    onGoldenNoriPressed?.Invoke();
                    _flattenHoldTimer = 0f;
                }
            }
        }

        private void HandleIngredientSpawnAndDropHotkeys()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            // Key 1: Pork
            if (kb.digit1Key.wasPressedThisFrame || (kb.numpad1Key != null && kb.numpad1Key.wasPressedThisFrame))
                AttachPropToChildHand(chashuPorkPrefab, "Chashu Pork");

            // Key 2: Egg
            if (kb.digit2Key.wasPressedThisFrame || (kb.numpad2Key != null && kb.numpad2Key.wasPressedThisFrame))
                AttachPropToChildHand(ajitamaEggPrefab, "Ajitama Egg");

            // Key 3: Onions
            if (kb.digit3Key.wasPressedThisFrame || (kb.numpad3Key != null && kb.numpad3Key.wasPressedThisFrame))
                AttachPropToChildHand(greenOnionsPrefab, "Green Onions");

            // REQUIREMENT 3: KEY 4 STRETCH CONFIRMATION & HAND DETACH
            if (kb.digit4Key.wasPressedThisFrame || (kb.numpad4Key != null && kb.numpad4Key.wasPressedThisFrame))
            {
                HandleKey4NoodleAction();
            }

            // Key A / Enter: Release into Bowl
            if (kb.aKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || (kb.numpadEnterKey != null && kb.numpadEnterKey.wasPressedThisFrame))
            {
                ReleaseHeldPropIntoBowl();
            }

            // Backspace: Reset held prop
            if (kb.backspaceKey.wasPressedThisFrame && _currentlyHeldProp != null)
            {
                Destroy(_currentlyHeldProp);
                _currentlyHeldProp = null;
            }
        }

        /// <summary>
        /// REQUIREMENT 3: Key 4 Stretch Confirmation & Hand Detach.
        /// If the player is currently holding the "noodle" prefab in their VR hand:
        /// 1. Force-release the grab interactor (or set parent to null).
        /// 2. Reference the active BoilerPotSnapReceiver instance.
        /// 3. Call boilerReceiver.SnapAndBoilNoodle(heldNoodle).
        /// 4. Set _currentlyHeldProp = null so the hand is completely free.
        /// </summary>
        public void HandleKey4NoodleAction()
        {
            // If already holding noodle, confirm stretch and snap to boiler
            if (_currentlyHeldProp != null && IsNoodleProp(_currentlyHeldProp))
            {
                ConfirmNoodleStretchAndBoil();
            }
            else
            {
                // First press: attach/spawn raw noodles to hand
                AttachPropToChildHand(rawNoodlesPrefab, "Raw Noodles");
            }
        }

        public void ConfirmNoodleStretchAndBoil()
        {
            if (_currentlyHeldProp == null) return;
            GameObject heldNoodle = _currentlyHeldProp;

            // 1. Force-release grab interactor (or set parent to null)
            heldNoodle.transform.SetParent(null, true);

            var grabbables = heldNoodle.GetComponentsInChildren<Oculus.Interaction.Grabbable>(true);
            for (int i = 0; i < grabbables.Length; i++) grabbables[i].enabled = false;

            var grabInteractables = heldNoodle.GetComponentsInChildren<Oculus.Interaction.GrabInteractable>(true);
            for (int i = 0; i < grabInteractables.Length; i++) grabInteractables[i].enabled = false;

            var handGrabs = heldNoodle.GetComponentsInChildren<Oculus.Interaction.HandGrab.HandGrabInteractable>(true);
            for (int i = 0; i < handGrabs.Length; i++) handGrabs[i].enabled = false;

            var xriGrabs = heldNoodle.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>(true);
            for (int i = 0; i < xriGrabs.Length; i++) xriGrabs[i].enabled = false;

            // 2. Reference the active BoilerPotSnapReceiver instance
            if (boilerPotReceiver == null)
            {
                boilerPotReceiver = BoilerPotSnapReceiver.Instance ?? FindFirstObjectByType<BoilerPotSnapReceiver>();
            }

            // 3. Call boilerReceiver.SnapAndBoilNoodle(heldNoodle)
            if (boilerPotReceiver != null)
            {
                boilerPotReceiver.SnapAndBoilNoodle(heldNoodle);
            }
            else if (BoilerStation.Instance != null)
            {
                BoilerStation.Instance.AcceptNoodleIntoBoiler(heldNoodle);
            }

            // 4. Set _currentlyHeldProp = null so the hand is completely free
            _currentlyHeldProp = null;

            Debug.Log("[HybridHotkeyActuator] Key 4 stretch confirmed! Noodle detached from VR hand and snapped to boiler.");
        }

        private void HandleLadleAndServingHotkeys()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            // Key 5: Ladle
            if (kb.digit5Key.wasPressedThisFrame || (kb.numpad5Key != null && kb.numpad5Key.wasPressedThisFrame))
            {
                if (brothLadleObject != null)
                {
                    AttachExistingObjectToHand(brothLadleObject);
                }
            }

            // Key D: Pour
            if (kb.dKey.wasPressedThisFrame)
            {
                onBrothPoured?.Invoke();
            }

            // Key S: Serve
            if (kb.sKey.wasPressedThisFrame)
            {
                _customersServedCount++;
                onCustomerServedWithFireworks?.Invoke(_customersServedCount);
            }
        }

        private void AttachPropToChildHand(GameObject prefab, string propName)
        {
            EnsureReferences();
            if (_currentlyHeldProp != null)
            {
                Destroy(_currentlyHeldProp);
                _currentlyHeldProp = null;
            }

            if (prefab != null)
            {
                Vector3 spawnPos = childVirtualHandAttachPoint != null ? childVirtualHandAttachPoint.position : transform.position;
                Quaternion spawnRot = childVirtualHandAttachPoint != null ? childVirtualHandAttachPoint.rotation : Quaternion.identity;
                _currentlyHeldProp = Instantiate(prefab, spawnPos, spawnRot);
            }
            else
            {
                // Fallback primitive
                _currentlyHeldProp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                _currentlyHeldProp.name = propName;
                _currentlyHeldProp.transform.localScale = new Vector3(0.04f, 0.04f, 0.15f);
                if (propName.Contains("Noodle"))
                {
                    _currentlyHeldProp.AddComponent<NoodleStretch>();
                    _currentlyHeldProp.tag = "noodle";
                }
            }

            if (childVirtualHandAttachPoint != null)
            {
                _currentlyHeldProp.transform.SetParent(childVirtualHandAttachPoint, true);
                _currentlyHeldProp.transform.localPosition = Vector3.zero;
            }

            Debug.Log($"[HybridHotkeyActuator] Attached {propName} to child's hand.");
        }

        private void AttachExistingObjectToHand(GameObject obj)
        {
            EnsureReferences();
            if (childVirtualHandAttachPoint != null && obj != null)
            {
                obj.transform.SetParent(childVirtualHandAttachPoint, true);
                obj.transform.localPosition = Vector3.zero;
            }
            _currentlyHeldProp = obj;
        }

        private void ReleaseHeldPropIntoBowl()
        {
            if (_currentlyHeldProp == null) return;

            Transform target = zoneCBowlDropTarget;
            if (target != null)
            {
                _currentlyHeldProp.transform.SetParent(target, true);
                _currentlyHeldProp.transform.localPosition = Vector3.zero;
            }
            else
            {
                _currentlyHeldProp.transform.SetParent(null, true);
            }

            _currentlyHeldProp = null;
            Debug.Log("[HybridHotkeyActuator] Released held prop into bowl.");
        }

        private bool IsNoodleProp(GameObject prop)
        {
            if (prop == null) return false;
            if (prop.GetComponent<NoodleStretch>() != null) return true;
            if (prop.GetComponent<StretchableNoodle>() != null) return true;
            try
            {
                if (prop.CompareTag("Noodle") || prop.CompareTag("noodle")) return true;
            }
            catch { }
            return prop.name.ToLower().Contains("noodle");
        }
    }
}
