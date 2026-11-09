using System;
using UnityEngine;
using UnityEngine.Events;

namespace YataiTogether
{
    public enum CookingStep
    {
        BikeRide = 0,
        CurtainClosed = 1,
        Noodles_Grab = 2,
        Noodles_Stretch = 3,
        Noodles_Boil = 4,
        Noodles_Transfer = 5,   // Cooked noodles in pot: Key 4 takes them to hand, A/Enter drops them into lamian_bowl
        Pork_Grab = 6,
        Pork_Release = 7,
        Egg_Drop = 8,
        Egg_Release = 9,
        Onions_Grab = 10,
        Onions_Release = 11,
        Nori_Hold = 12,
        Broth_Ladle = 13,
        Broth_Pour = 14,
        ReadyToServe = 15,
        CustomerServed = 16,
        SessionComplete = 17
    }

    /// <summary>
    /// Step 3: Strict Assembly Director.
    /// Enforces the Strict Assembly Sequence Law (AGENTS.md §2 & Concept §4.1):
    /// Noodles (4 -> W 3s -> B 3s -> 4 -> A) -> Chashu Pork (1 -> A) -> Ajitama Egg (2 -> A) ->
    /// Green Onions (3 -> A) -> Broth via Ladle (5 -> D) -> Serve (S).
    /// Rejects out-of-order or duplicate keys and alerts KitchenDeckHUD.
    /// V, Q, and Backspace are always valid.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class YataiSessionDirector : MonoBehaviour
    {
        public static YataiSessionDirector Instance { get; private set; }

        [Header("Session Configuration")]
        [SerializeField] private int maxCustomers = 5;
        [SerializeField] private CookingStep currentStep = CookingStep.BikeRide;

        [Header("Hold Timers (Accumulated seconds)")]
        [SerializeField] private float wHoldTarget = 3.0f;
        [SerializeField] private float bHoldTarget = 3.0f;
        [SerializeField] private float fHoldTarget = 5.0f;

        [Header("Events")]
        public UnityEvent<CookingStep> onStepChanged = new UnityEvent<CookingStep>();
        public UnityEvent<int, int> onCustomerChanged = new UnityEvent<int, int>();
        public UnityEvent<string> onSequenceWarning = new UnityEvent<string>();
        public UnityEvent onSessionCompleted = new UnityEvent();

        public event Action<CookingStep> StepChanged;
        public event Action<int, int> CustomerChanged;
        public event Action<string> SequenceWarning;
        public event Action SessionCompleted;

        private int _customerIndex = 1;
        private bool _isCurtainOpen = false;
        private bool _isSimmerHoldActive = false;

        private float _wHoldAccumulated = 0f;
        private float _bHoldAccumulated = 0f;
        private float _fHoldAccumulated = 0f;

        private string _warningMessage = string.Empty;
        private float _warningExpiryTime = 0f;

        public CookingStep CurrentStep => currentStep;
        public int CurrentCustomerIndex => _customerIndex;
        public int MaxCustomers => maxCustomers;
        public bool IsCurtainOpen => _isCurtainOpen;
        public bool IsSimmerHoldActive => _isSimmerHoldActive;
        public string WarningMessage => (Time.time < _warningExpiryTime) ? _warningMessage : string.Empty;

        public float WHoldProgress => Mathf.Clamp01(_wHoldAccumulated / wHoldTarget);
        public float BHoldProgress => Mathf.Clamp01(_bHoldAccumulated / bHoldTarget);
        public float FHoldProgress => Mathf.Clamp01(_fHoldAccumulated / fHoldTarget);
        public float WHoldAccumulated => _wHoldAccumulated;
        public float BHoldAccumulated => _bHoldAccumulated;
        public float FHoldAccumulated => _fHoldAccumulated;

        public string NextExpectedPrompt
        {
            get
            {
                switch (currentStep)
                {
                    case CookingStep.BikeRide:
                        return "[SPACE] Pedal Bike (+10m)";
                    case CookingStep.CurtainClosed:
                        return "[Q] Overhead Reach (Open Stall)";
                    case CookingStep.Noodles_Grab:
                        return "[4] Pinch Uncooked Noodles";
                    case CookingStep.Noodles_Stretch:
                        return $"Stretch noodle between both hands: hold [W] ({_wHoldAccumulated:F1}s / {wHoldTarget:F0}s)";
                    case CookingStep.Noodles_Boil:
                        return $"Hold [B] Elbow Extension Boil ({_bHoldAccumulated:F1}s / {bHoldTarget:F0}s, release pauses)";
                    case CookingStep.Noodles_Transfer:
                        return IsCarryingCookedNoodle()
                            ? "[A] / [Enter] Drop Noodles into Bowl"
                            : "[4] Take Cooked Noodles out of Pot";
                    case CookingStep.Pork_Grab:
                        return "[1] Finger Opening & Pinch Pork";
                    case CookingStep.Pork_Release:
                        return "[A] / [Enter] Drop Pork in Bowl";
                    case CookingStep.Egg_Drop:
                        return "[2] Palm-Up Magic Egg Drop";
                    case CookingStep.Egg_Release:
                        return "[A] / [Enter] Drop Egg in Bowl";
                    case CookingStep.Onions_Grab:
                        return "[3] Pinch Green Onions";
                    case CookingStep.Onions_Release:
                        return "[A] / [Enter] Drop Onions in Bowl";
                    case CookingStep.Nori_Hold:
                        return $"Hold [F] Warm Paw Golden Nori ({_fHoldAccumulated:F1}s / {fHoldTarget:F0}s)";
                    case CookingStep.Broth_Ladle:
                        return "[5] Gross Grasp Broth Ladle";
                    case CookingStep.Broth_Pour:
                        return "[D] Wrist Pour Broth into Bowl";
                    case CookingStep.ReadyToServe:
                        return "[S] Two-Hand Reach Forward (Serve!)";
                    case CookingStep.CustomerServed:
                        return "Serving celebration in progress...";
                    case CookingStep.SessionComplete:
                        return "Festival Session Complete! Great job!";
                    default:
                        return string.Empty;
                }
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
        }

        private void OnEnable()
        {
            if (ParentHotkeyRouter.Instance != null)
            {
                ParentHotkeyRouter.Instance.ActionTapped += HandleActionTapped;
                ParentHotkeyRouter.Instance.HoldTicked += HandleHoldTicked;
            }

            if (BikeRideController.Instance != null)
            {
                BikeRideController.Instance.RideCompleted += HandleBikeRideCompleted;
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

            if (BikeRideController.Instance != null)
            {
                BikeRideController.Instance.RideCompleted -= HandleBikeRideCompleted;
                BikeRideController.Instance.RideCompleted += HandleBikeRideCompleted;
            }
        }

        private void OnDisable()
        {
            if (ParentHotkeyRouter.Instance != null)
            {
                ParentHotkeyRouter.Instance.ActionTapped -= HandleActionTapped;
                ParentHotkeyRouter.Instance.HoldTicked -= HandleHoldTicked;
            }

            if (BikeRideController.Instance != null)
            {
                BikeRideController.Instance.RideCompleted -= HandleBikeRideCompleted;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void HandleBikeRideCompleted()
        {
            if (currentStep == CookingStep.BikeRide)
            {
                SetStep(CookingStep.CurtainClosed);
            }
        }

        public void HandleCurtainStateChanged(bool isOpen)
        {
            _isCurtainOpen = isOpen;
            _isSimmerHoldActive = !isOpen;

            if (currentStep == CookingStep.CurtainClosed && isOpen)
            {
                SetStep(CookingStep.Noodles_Grab);
            }
        }

        private void HandleHoldTicked(ParentAction action, float dt)
        {
            if (_isSimmerHoldActive)
            {
                return; // Simmer Hold rest freezes ALL hold progress (W, B and F)
            }

            switch (action)
            {
                case ParentAction.BandPullStretch:
                    if (currentStep == CookingStep.Noodles_Stretch)
                    {
                        _wHoldAccumulated += dt;
                        if (_wHoldAccumulated >= wHoldTarget)
                        {
                            _wHoldAccumulated = wHoldTarget;
                            SetStep(CookingStep.Noodles_Boil);
                        }
                    }
                    else if (currentStep < CookingStep.Noodles_Stretch)
                    {
                        TriggerWarning("Sequence locked: Pick up noodles first (Key 4)!");
                    }
                    break;

                case ParentAction.BoilHold:
                    if (currentStep == CookingStep.Noodles_Boil)
                    {
                        _bHoldAccumulated += dt;
                        if (_bHoldAccumulated >= bHoldTarget)
                        {
                            _bHoldAccumulated = bHoldTarget;
                            FinishBoil();
                        }
                    }
                    else if (currentStep < CookingStep.Noodles_Boil)
                    {
                        TriggerWarning("Sequence locked: Stretch noodles first (Hold W)!");
                    }
                    else if (currentStep == CookingStep.Noodles_Transfer)
                    {
                        TriggerWarning("Noodles are cooked! Take them out of the pot with [4].");
                    }
                    break;

                case ParentAction.HandFlattenHold:
                    if (currentStep == CookingStep.Nori_Hold)
                    {
                        _fHoldAccumulated += dt;
                        if (_fHoldAccumulated >= fHoldTarget)
                        {
                            _fHoldAccumulated = fHoldTarget;
                            if (GoldenNoriController.Instance != null)
                            {
                                GoldenNoriController.Instance.CompleteNoriPress();
                            }
                            else
                            {
                                SetStep(CookingStep.Broth_Ladle);
                            }
                        }
                    }
                    else if (currentStep < CookingStep.Nori_Hold)
                    {
                        TriggerWarning("Sequence locked: Add toppings before Golden Nori press!");
                    }
                    break;
            }
        }

        private void HandleActionTapped(ParentAction action)
        {
            // 1. Failsafes and universal keys (always allowed)
            if (action == ParentAction.TogglePov)
            {
                return; // Handled by PovRigController
            }

            if (action == ParentAction.ToggleCurtainRest)
            {
                ToggleCurtainRest();
                return;
            }

            if (action == ParentAction.ResetCurrentStep)
            {
                ResetCurrentStep();
                return;
            }

            if (action == ParentAction.ElbowReachCue)
            {
                Debug.Log("[YataiSessionDirector] Elbow Reach Alignment Cue triggered.");
                return;
            }

            // If shop curtain is closed and child is resting, block cooking keys
            if (!_isCurtainOpen && currentStep > CookingStep.CurtainClosed)
            {
                TriggerWarning("Stall paused in Simmer Hold rest. Tap [Q] to resume cooking!");
                return;
            }

            // 2. Strict State Machine Validation
            switch (action)
            {
                case ParentAction.BikePedalStretch:
                    if (currentStep != CookingStep.BikeRide)
                    {
                        TriggerWarning("Bike ride completed! Now assemble ramen at the stall.");
                    }
                    break;

                case ParentAction.GrabNoodles:
                    if (currentStep == CookingStep.Noodles_Grab)
                    {
                        SetStep(CookingStep.Noodles_Stretch);
                    }
                    else if (currentStep == CookingStep.Noodles_Stretch)
                    {
                        // Key 4 stretch confirmation: advance to boiling
                        SetStep(CookingStep.Noodles_Boil);
                    }
                    else if (currentStep == CookingStep.Noodles_Boil &&
                             ParentHotkeyRouter.Instance != null && ParentHotkeyRouter.Instance.IsHoldingNoodle())
                    {
                        // Raw noodle still in hand (tap arrived without the keyboard intercept): drop it into the pot.
                        ParentHotkeyRouter.Instance.ConfirmNoodleStretchAndBoil();
                    }
                    else if (currentStep == CookingStep.Noodles_Transfer)
                    {
                        // Cooked noodles out of the pot into the chef's hand (ignored if already carried).
                        if (!IsCarryingCookedNoodle() && BoilerPotSnapReceiver.Instance != null)
                        {
                            BoilerPotSnapReceiver.Instance.TakeCookedNoodleToHand();
                        }
                    }
                    else
                    {
                        RejectOutOfOrderKey("[4] Noodles", "Noodles are already cooked or not expected!");
                    }
                    break;

                case ParentAction.GrabPork:
                    if (currentStep == CookingStep.Pork_Grab)
                    {
                        SetStep(CookingStep.Pork_Release);
                    }
                    else
                    {
                        RejectOutOfOrderKey("[1] Pork", "Finish noodles before adding Chashu pork!");
                    }
                    break;

                case ParentAction.DropEggIntoPalm:
                    if (currentStep == CookingStep.Egg_Drop)
                    {
                        SetStep(CookingStep.Egg_Release);
                    }
                    else
                    {
                        RejectOutOfOrderKey("[2] Egg", "Add Chashu pork before dropping Ajitama egg!");
                    }
                    break;

                case ParentAction.GrabOnions:
                    if (currentStep == CookingStep.Onions_Grab)
                    {
                        SetStep(CookingStep.Onions_Release);
                    }
                    else
                    {
                        RejectOutOfOrderKey("[3] Onions", "Add egg before topping with green onions!");
                    }
                    break;

                case ParentAction.ReleaseIntoBowl:
                    if (currentStep == CookingStep.Noodles_Transfer)
                    {
                        if (IsCarryingCookedNoodle())
                        {
                            ParentHotkeyRouter router = ParentHotkeyRouter.Instance;
                            Destroy(router.CurrentlyHeldProp);
                            router.CurrentlyHeldProp = null;
                            if (RamenAssemblyReceiver.Instance != null) RamenAssemblyReceiver.Instance.AddNoodles();
                            else SetStep(CookingStep.Pork_Grab);
                        }
                        else
                        {
                            TriggerWarning("Take the cooked noodles out of the pot first [4]!");
                        }
                    }
                    else if (currentStep == CookingStep.Pork_Release)
                    {
                        SetStep(CookingStep.Egg_Drop);
                    }
                    else if (currentStep == CookingStep.Egg_Release)
                    {
                        SetStep(CookingStep.Onions_Grab);
                    }
                    else if (currentStep == CookingStep.Onions_Release)
                    {
                        SetStep(CookingStep.Nori_Hold);
                    }
                    else
                    {
                        TriggerWarning("Nothing currently held to release into the ramen bowl!");
                    }
                    break;

                case ParentAction.GrabLadle:
                    if (currentStep == CookingStep.Broth_Ladle)
                    {
                        SetStep(CookingStep.Broth_Pour);
                    }
                    else
                    {
                        RejectOutOfOrderKey("[5] Broth Ladle", "Add toppings before pouring broth!");
                    }
                    break;

                case ParentAction.PourBroth:
                    if (currentStep == CookingStep.Broth_Pour)
                    {
                        SetStep(CookingStep.ReadyToServe);
                    }
                    else if (currentStep == CookingStep.ReadyToServe)
                    {
                        // Broth already poured this frame by SpoonBrothScooper (listener order); silently ignore.
                    }
                    else
                    {
                        RejectOutOfOrderKey("[D] Pour Broth", "Grab ladle with [5] before pouring!");
                    }
                    break;

                case ParentAction.ServeCustomer:
                    if (currentStep == CookingStep.ReadyToServe)
                    {
                        ServeCurrentCustomer();
                    }
                    else
                    {
                        TriggerWarning("Ramen is not complete yet! Follow the recipe to finish.");
                    }
                    break;
            }
        }

        private void RejectOutOfOrderKey(string keyLabel, string detail)
        {
            string message = $"Sequence Locked! Cannot press {keyLabel}. {detail}";
            TriggerWarning(message);
        }

        public void TriggerWarning(string message)
        {
            _warningMessage = message;
            _warningExpiryTime = Time.time + 3.0f;
            Debug.LogWarning($"[YataiSessionDirector] {message}");
            SequenceWarning?.Invoke(message);
            onSequenceWarning?.Invoke(message);
        }

        /// <summary>
        /// B hold reached 3.0s: the pot flashes green, the cooked noodles stay in the pot, and the child takes
        /// them out (Key 4 / hand / spoon) and drops them into lamian_bowl (A / Enter / touch).
        /// </summary>
        private void FinishBoil()
        {
            // The raw noodle must never stay in the hand past the boil (it would be dropped raw into the bowl).
            ParentHotkeyRouter router = ParentHotkeyRouter.Instance;
            if (router != null && router.IsHoldingNoodle())
            {
                router.ConfirmNoodleStretchAndBoil();
            }

            if (BoilerPotSnapReceiver.Instance != null) BoilerPotSnapReceiver.Instance.FinishCooking();
            if (BoilerStation.Instance != null) BoilerStation.Instance.FinishCooking(autoTransferToBowl: false);

            if (BoilerPotSnapReceiver.Instance == null && BoilerStation.Instance == null)
            {
                // No boiler in the scene: nothing to take out, so the bowl gets the noodles directly.
                if (RamenAssemblyReceiver.Instance != null) RamenAssemblyReceiver.Instance.AddNoodles();
                else SetStep(CookingStep.Pork_Grab);
                return;
            }

            SetStep(CookingStep.Noodles_Transfer);
        }

        /// <summary>True when the chef's hand currently carries the cooked noodles taken out of the pot.</summary>
        public bool IsCarryingCookedNoodle()
        {
            ParentHotkeyRouter router = ParentHotkeyRouter.Instance;
            return currentStep == CookingStep.Noodles_Transfer && router != null && router.CurrentlyHeldProp != null;
        }

        private static void ResetBoilerPots()
        {
            if (BoilerPotSnapReceiver.Instance != null) BoilerPotSnapReceiver.Instance.ResetPot();
            if (BoilerStation.Instance != null) BoilerStation.Instance.ResetPot();
        }

        public void SetStep(CookingStep newStep)
        {
            if (newStep == CookingStep.CustomerServed)
            {
                Debug.Log($"[STACKTRACE] SetStep(CustomerServed) from:\n{System.Environment.StackTrace}");
            }
            if (newStep == CookingStep.Noodles_Grab)
            {
                // Fresh bowl: empty and un-cook the pot so the next customer's noodle can boil.
                ResetBoilerPots();
            }
            currentStep = newStep;
            Debug.Log($"[YataiSessionDirector] === STEP ADVANCED: {currentStep} | Customer {_customerIndex}/{maxCustomers} ===");
            StepChanged?.Invoke(currentStep);
            onStepChanged?.Invoke(currentStep);
        }

        public void ToggleCurtainRest()
        {
            _isCurtainOpen = !_isCurtainOpen;
            _isSimmerHoldActive = !_isCurtainOpen;

            if (NorenCurtain.Instance != null)
            {
                NorenCurtain.Instance.SetCurtainOpen(_isCurtainOpen);
            }

            HandleCurtainStateChanged(_isCurtainOpen);
        }

        public void ResetSession()
        {
            _customerIndex = 1;
            _wHoldAccumulated = 0f;
            _bHoldAccumulated = 0f;
            _fHoldAccumulated = 0f;
            SetStep(CookingStep.Noodles_Grab);
            CustomerChanged?.Invoke(_customerIndex, maxCustomers);
            onCustomerChanged?.Invoke(_customerIndex, maxCustomers);
        }

        /// <summary>
        /// Backspace failsafe (Concept §4): when an action is too hard for the child or the VR tracking is not
        /// working, skips the CURRENT step and moves on to the next one. Clears any stuck held item and cleanly
        /// completes the step's ingredient into the bowl, with zero penalty and without resetting recipe progress.
        /// </summary>
        public void ResetCurrentStep()
        {
            Debug.Log($"[YataiSessionDirector] Backspace: zero-penalty completion of {currentStep}.");
            RamenAssemblyReceiver bowl = RamenAssemblyReceiver.Instance;

            switch (currentStep)
            {
                case CookingStep.BikeRide:
                    // Too hard / pedal tracking failing: arrive at the stall now (comfort fade handled by PovRigController).
                    if (BikeRideController.Instance != null) BikeRideController.Instance.SkipToArrival();
                    AdvanceIfStillIn(CookingStep.CurtainClosed, CookingStep.BikeRide);
                    break;

                case CookingStep.CurtainClosed:
                    // Overhead reach skipped: open the Noren curtain (advances to Noodles_Grab).
                    if (!_isCurtainOpen) ToggleCurtainRest();
                    AdvanceIfStillIn(CookingStep.Noodles_Grab, CookingStep.CurtainClosed);
                    break;

                case CookingStep.ReadyToServe:
                    // Two-hand push skipped: serve the customer anyway.
                    ServeCurrentCustomer();
                    break;

                case CookingStep.Noodles_Grab:
                case CookingStep.Noodles_Stretch:
                case CookingStep.Noodles_Boil:
                case CookingStep.Noodles_Transfer:
                    ClearHeldItems();
                    ResetBoilerPots();
                    _wHoldAccumulated = wHoldTarget;
                    _bHoldAccumulated = bHoldTarget;
                    if (bowl != null) bowl.AddNoodles();
                    AdvanceIfStillIn(CookingStep.Pork_Grab, CookingStep.Noodles_Grab, CookingStep.Noodles_Stretch, CookingStep.Noodles_Boil, CookingStep.Noodles_Transfer);
                    break;

                case CookingStep.Pork_Grab:
                case CookingStep.Pork_Release:
                    ClearHeldItems();
                    if (PorkStationController.Instance != null) PorkStationController.Instance.ReleasePorkIntoBowl();
                    if (bowl != null) bowl.AddMeat();
                    AdvanceIfStillIn(CookingStep.Egg_Drop, CookingStep.Pork_Grab, CookingStep.Pork_Release);
                    break;

                case CookingStep.Egg_Drop:
                case CookingStep.Egg_Release:
                    ClearHeldItems();
                    if (EggStationController.Instance != null) EggStationController.Instance.ReleaseEggIntoBowl();
                    if (bowl != null) bowl.AddEgg();
                    AdvanceIfStillIn(CookingStep.Onions_Grab, CookingStep.Egg_Drop, CookingStep.Egg_Release);
                    break;

                case CookingStep.Onions_Grab:
                case CookingStep.Onions_Release:
                    ClearHeldItems();
                    if (OnionStationController.Instance != null) OnionStationController.Instance.ReleaseOnionsIntoBowl();
                    if (bowl != null) bowl.AddOnions();
                    AdvanceIfStillIn(CookingStep.Nori_Hold, CookingStep.Onions_Grab, CookingStep.Onions_Release);
                    break;

                case CookingStep.Nori_Hold:
                    _fHoldAccumulated = fHoldTarget;
                    if (GoldenNoriController.Instance != null) GoldenNoriController.Instance.CompleteNoriPress();
                    else if (bowl != null) bowl.AddNori();
                    AdvanceIfStillIn(CookingStep.Broth_Ladle, CookingStep.Nori_Hold);
                    break;

                case CookingStep.Broth_Ladle:
                case CookingStep.Broth_Pour:
                    if (bowl != null) bowl.AddBroth();
                    if (SpoonBrothScooper.Instance != null) SpoonBrothScooper.Instance.ResetForNextCustomer();
                    AdvanceIfStillIn(CookingStep.ReadyToServe, CookingStep.Broth_Ladle, CookingStep.Broth_Pour);
                    break;

                default:
                    ClearHeldItems();
                    break;
            }
        }

        private void AdvanceIfStillIn(CookingStep next, params CookingStep[] fromSteps)
        {
            for (int i = 0; i < fromSteps.Length; i++)
            {
                if (currentStep == fromSteps[i])
                {
                    SetStep(next);
                    return;
                }
            }
        }

        /// <summary>
        /// Removes any ingredient still in the chef's hand (spawned duplicates and the stretch noodle).
        /// </summary>
        private static void ClearHeldItems()
        {
            IngredientSpawnerBowl[] spawners = FindObjectsByType<IngredientSpawnerBowl>(FindObjectsSortMode.None);
            for (int i = 0; i < spawners.Length; i++)
            {
                if (spawners[i] != null) spawners[i].DiscardHeldDuplicate();
            }

            ParentHotkeyRouter router = ParentHotkeyRouter.Instance;
            if (router != null && router.CurrentlyHeldProp != null)
            {
                Destroy(router.CurrentlyHeldProp);
                router.CurrentlyHeldProp = null;
            }

            if (StretchableNoodle.Instance != null && StretchableNoodle.Instance.IsHeldInHand)
            {
                StretchableNoodle.Instance.OnNoodleDetachedToBoiler();
            }
        }

        private void ServeCurrentCustomer()
        {
            if (CustomerServingStation.Instance != null)
            {
                CustomerServingStation.Instance.TriggerServe();
                return;
            }

            AdvanceToNextCustomerOrComplete();
        }

        public void AdvanceToNextCustomerOrComplete()
        {
            SetStep(CookingStep.CustomerServed);
            Debug.Log($"[YataiSessionDirector] Customer {_customerIndex}/{maxCustomers} served! Hanabi fireworks & celebration!");

            if (_customerIndex >= maxCustomers)
            {
                SetStep(CookingStep.SessionComplete);
                SessionCompleted?.Invoke();
                onSessionCompleted?.Invoke();
            }
            else
            {
                _customerIndex++;
                CustomerChanged?.Invoke(_customerIndex, maxCustomers);
                onCustomerChanged?.Invoke(_customerIndex, maxCustomers);

                // Reset bowl state for next identical bowl loop
                _wHoldAccumulated = 0f;
                _bHoldAccumulated = 0f;
                _fHoldAccumulated = 0f;
                SetStep(CookingStep.Noodles_Grab);
            }
        }
    }
}
