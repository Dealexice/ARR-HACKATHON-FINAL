using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace YataiTogether
{
    /// <summary>
    /// Single source of truth for reading the Parent Sous-Chef physical keyboard cluster
    /// via UnityEngine.InputSystem (Keyboard.current).
    /// Zero per-frame GC allocations during idle Update polling.
    /// </summary>
    [DisallowMultipleComponent]
    public class ParentHotkeyRouter : MonoBehaviour
    {
        public static ParentHotkeyRouter Instance { get; private set; }

        [Header("Diagnostics")]
        [SerializeField] private bool logEventsToConsole = true;

        [Header("Inspector Events")]
        public UnityEvent<ParentAction> onActionTapped;
        public UnityEvent<ParentAction> onHoldStarted;
        public UnityEvent<ParentAction> onHoldStopped;

        // C# Events for zero-overhead subscription by YataiSessionDirector & HUD
        public event Action<ParentAction> ActionTapped;
        public event Action<ParentAction> HoldStarted;
        public event Action<ParentAction, float> HoldTicked;
        public event Action<ParentAction> HoldStopped;

        // Hold states (W = BandPullStretch, B = BoilHold, F = HandFlattenHold)
        private bool _isBandPullHeld;
        private bool _isBoilHeld;
        private bool _isHandFlattenHeld;

        // Simulated hold overrides for automated Editor Play Mode verification
        private bool _simBandPullHeld;
        private bool _simBoilHeld;
        private bool _simHandFlattenHeld;

        // Currently held prop reference for discrete hybrid actuation
        private GameObject _currentlyHeldProp = null;
        public GameObject CurrentlyHeldProp { get => _currentlyHeldProp; set => _currentlyHeldProp = value; }

        public ParentAction LastTappedAction { get; private set; } = ParentAction.None;
        public float LastTappedTime { get; private set; } = -1f;
        public int TotalTapCount { get; private set; }

        public bool IsHolding(ParentAction action)
        {
            switch (action)
            {
                case ParentAction.BandPullStretch:
                    return _isBandPullHeld;
                case ParentAction.BoilHold:
                    return _isBoilHeld;
                case ParentAction.HandFlattenHold:
                    return _isHandFlattenHeld;
                default:
                    return false;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[ParentHotkeyRouter] Duplicate router detected; keeping existing instance.", this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;

            // VR Headset Controller & Hand Pinch Fallbacks for on-device testing
            bool vrTogglePov = OVRInput.GetDown(OVRInput.Button.One) || OVRInput.GetDown(OVRInput.Button.Three);
            bool vrBikePedal = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger) || OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger);

            if (vrTogglePov)
            {
                RaiseTap(ParentAction.TogglePov, "VR Controller Button A/X (Toggle POV)");
            }

            if (vrBikePedal)
            {
                RaiseTap(ParentAction.BikePedalStretch, "VR Controller Trigger (Bike +10m)");
            }

            if (kb != null)
            {
                // 1. Phase 1 & Global Controls
                if (kb.spaceKey.wasPressedThisFrame)
                    RaiseTap(ParentAction.BikePedalStretch, "Spacebar (Bike +10m)");

                if (kb.vKey.wasPressedThisFrame)
                    RaiseTap(ParentAction.TogglePov, "Key V (Toggle POV)");

                if (kb.qKey.wasPressedThisFrame)
                    RaiseTap(ParentAction.ToggleCurtainRest, "Key Q (Curtain / Simmer Hold)");

                if (kb.backspaceKey.wasPressedThisFrame)
                    RaiseTap(ParentAction.ResetCurrentStep, "Backspace (Skip Step)");

                if (kb.eKey.wasPressedThisFrame)
                    RaiseTap(ParentAction.ElbowReachCue, "Key E (Elbow Reach Cue)");

                // 2. Ingredient & Tool Hotkeys (Keys 1 - 5)
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
                    RaiseTap(ParentAction.GrabPork, "Key 1 (Pinch Chashu Pork)");

                if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
                    RaiseTap(ParentAction.DropEggIntoPalm, "Key 2 (Palm-Up Egg Drop above egg_bowl)");

                if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)
                    RaiseTap(ParentAction.GrabOnions, "Key 3 (Pinch Green Onions)");

                if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame)
                {
                    // REQUIREMENT 3: If holding the RAW noodle, Key 4 confirms stretch and snaps into boiler.
                    // Never for the cooked noodle carried out of the pot (Noodles_Transfer): that goes to the bowl.
                    YataiSessionDirector director = YataiSessionDirector.Instance;
                    bool rawNoodleStep = director == null ||
                        director.CurrentStep == CookingStep.Noodles_Stretch ||
                        director.CurrentStep == CookingStep.Noodles_Boil;
                    if (rawNoodleStep && IsHoldingNoodle())
                    {
                        ConfirmNoodleStretchAndBoil();
                    }
                    else
                    {
                        RaiseTap(ParentAction.GrabNoodles, "Key 4 (Pinch Raw Noodles)");
                    }
                }

                if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame)
                    RaiseTap(ParentAction.GrabLadle, "Key 5 (Grasp Broth Ladle)");

                // 3. Release, Pour & Serve Hotkeys (A / Enter, D, S)
                if (kb.aKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                    RaiseTap(ParentAction.ReleaseIntoBowl, "Key A/Enter (Release into Bowl)");

                if (kb.dKey.wasPressedThisFrame)
                    RaiseTap(ParentAction.PourBroth, "Key D (Wrist Pour Broth)");

                if (kb.sKey.wasPressedThisFrame)
                    RaiseTap(ParentAction.ServeCustomer, "Key S (Serve Customer)");
            }

            // 4. Continuous Hold Keys (W, B, F) — combine physical keyboard + simulated test state
            bool wPressed = (kb != null && kb.wKey.isPressed) || _simBandPullHeld;
            bool bPressed = (kb != null && kb.bKey.isPressed) || _simBoilHeld;
            bool fPressed = (kb != null && kb.fKey.isPressed) || _simHandFlattenHeld;

            UpdateHoldChannel(ParentAction.BandPullStretch, wPressed, ref _isBandPullHeld, "Hold W (2-Hand Noodle Stretch)");
            UpdateHoldChannel(ParentAction.BoilHold, bPressed, ref _isBoilHeld, "Hold B (Boiler 3s Hold)");
            UpdateHoldChannel(ParentAction.HandFlattenHold, fPressed, ref _isHandFlattenHeld, "Hold F (5s Hand Flatten / Golden Nori)");
        }

        private void UpdateHoldChannel(ParentAction action, bool pressedNow, ref bool wasHeld, string label)
        {
            if (pressedNow && !wasHeld)
            {
                wasHeld = true;
                if (logEventsToConsole)
                {
                    Debug.Log($"[ParentHotkeyRouter] HOLD START -> {action} ({label})");
                }
                HoldStarted?.Invoke(action);
                onHoldStarted?.Invoke(action);
            }

            if (pressedNow)
            {
                HoldTicked?.Invoke(action, Time.deltaTime);
            }
            else if (wasHeld)
            {
                wasHeld = false;
                if (logEventsToConsole)
                {
                    Debug.Log($"[ParentHotkeyRouter] HOLD STOP (Paused) -> {action} ({label})");
                }
                HoldStopped?.Invoke(action);
                onHoldStopped?.Invoke(action);
            }
        }

        private void RaiseTap(ParentAction action, string label)
        {
            LastTappedAction = action;
            LastTappedTime = Time.time;
            TotalTapCount++;

            if (logEventsToConsole)
            {
                Debug.Log($"[ParentHotkeyRouter] TAP -> {action} ({label})");
            }

            ActionTapped?.Invoke(action);
            onActionTapped?.Invoke(action);
        }

        /// <summary>
        /// Programmatically simulates a discrete key tap (used by Editor Play Mode tests or companion UI).
        /// </summary>
        public void SimulateTap(ParentAction action)
        {
            if (action == ParentAction.None) return;
            RaiseTap(action, "Simulated Tap");
        }

        /// <summary>
        /// Programmatically sets the hold state of W, B, or F (used by Editor Play Mode tests).
        /// </summary>
        public void SimulateHold(ParentAction action, bool isHeld)
        {
            switch (action)
            {
                case ParentAction.BandPullStretch:
                    _simBandPullHeld = isHeld;
                    break;
                case ParentAction.BoilHold:
                    _simBoilHeld = isHeld;
                    break;
                case ParentAction.HandFlattenHold:
                    _simHandFlattenHeld = isHeld;
                    break;
            }
        }

        /// <summary>
        /// Programmatically triggers a hold tick with a specified delta time (used by automated tests).
        /// </summary>
        public void SimulateHoldTick(ParentAction action, float dt)
        {
            HoldTicked?.Invoke(action, dt);
        }

        #region Key 4 Stretch Confirmation & Hand Detach (Requirement 3)

        public bool IsHoldingNoodle()
        {
            if (_currentlyHeldProp != null && IsNoodleProp(_currentlyHeldProp))
            {
                return true;
            }

            if (StretchableNoodle.Instance != null && StretchableNoodle.Instance.IsHeldInHand)
            {
                return true;
            }

            NoodleSpawnerAndStretch spawner = FindFirstObjectByType<NoodleSpawnerAndStretch>();
            if (spawner != null && spawner.ActiveNoodleInstance != null)
            {
                return true;
            }

            return false;
        }

        public GameObject GetHeldNoodle()
        {
            if (_currentlyHeldProp != null && IsNoodleProp(_currentlyHeldProp))
            {
                return _currentlyHeldProp;
            }

            if (StretchableNoodle.Instance != null && StretchableNoodle.Instance.IsHeldInHand)
            {
                return StretchableNoodle.Instance.HeldDoughObject;
            }

            NoodleSpawnerAndStretch spawner = FindFirstObjectByType<NoodleSpawnerAndStretch>();
            if (spawner != null && spawner.ActiveNoodleInstance != null)
            {
                return spawner.ActiveNoodleInstance;
            }

            return null;
        }

        /// <summary>
        /// REQUIREMENT 3: KEY 4 STRETCH CONFIRMATION & HAND DETACH
        /// When parent presses hotkey 4 to confirm child's stretch:
        /// 1. Force-release the grab interactor (or set parent to null).
        /// 2. Reference the active BoilerPotSnapReceiver instance.
        /// 3. Call boilerReceiver.SnapAndBoilNoodle(heldNoodle).
        /// 4. Set _currentlyHeldProp = null so the hand is completely free.
        /// </summary>
        public void ConfirmNoodleStretchAndBoil()
        {
            GameObject heldNoodle = GetHeldNoodle();

            if (heldNoodle != null)
            {
                // 1. Force-release the grab interactor (or set parent to null)
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
                BoilerPotSnapReceiver boilerReceiver = BoilerPotSnapReceiver.Instance != null
                    ? BoilerPotSnapReceiver.Instance
                    : FindFirstObjectByType<BoilerPotSnapReceiver>();

                // 3. Call boilerReceiver.SnapAndBoilNoodle(heldNoodle)
                if (boilerReceiver != null)
                {
                    boilerReceiver.SnapAndBoilNoodle(heldNoodle);
                }
                else if (BoilerStation.Instance != null)
                {
                    BoilerStation.Instance.AcceptNoodleIntoBoiler(heldNoodle);
                }
            }

            // 4. Set _currentlyHeldProp = null so the hand is completely free
            _currentlyHeldProp = null;

            if (StretchableNoodle.Instance != null)
            {
                StretchableNoodle.Instance.OnNoodleDetachedToBoiler();
            }

            if (YataiSessionDirector.Instance != null &&
                YataiSessionDirector.Instance.CurrentStep == CookingStep.Noodles_Stretch)
            {
                YataiSessionDirector.Instance.SetStep(CookingStep.Noodles_Boil);
            }

            Debug.Log("[ParentHotkeyRouter] Key 4 stretch confirmed! Noodle detached from VR hand and snapped to boiler.");
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

        #endregion
    }
}
