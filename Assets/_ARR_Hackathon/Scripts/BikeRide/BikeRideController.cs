using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace YataiTogether
{
    /// <summary>
    /// Step 2: Bike Active Pedaling Ride (100m Progress).
    /// Enforces AGENTS.md §1 & §4 Law:
    /// - Child performs seated active cycling leg stretch; parent taps Spacebar on each good cycle.
    /// - NEVER translate, accelerate, tilt, or rotate the VR camera rig or food cart while the child can see.
    /// - Instead, the festival village street scrolls smoothly past the stationary cart/bike with eased motion.
    /// - Each Spacebar tap advances +10m (10 taps = 100m).
    /// - At 100m (10th tap), automatically transitions to POV 2 (FoodStallCounterAnchor) behind a >= 0.5s comfort fade,
    ///   and snaps the street back to its festival stall anchor position while the screen is black.
    /// - Extra Spacebar taps after arrival at 100m are ignored (until ResetRide is called).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BikeRideController : MonoBehaviour
    {
        public static BikeRideController Instance { get; private set; }

        [Header("Scrolling Street Reference (Rig & Cart Stay Stationary)")]
        [SerializeField] private Transform scrollingStreetTransform;
        [SerializeField] private Transform bikeSeatAnchor;

        [Header("Bike Stretch Parameters")]
        [SerializeField] private float metersPerTap = 10f;
        [SerializeField] private float targetDistanceMeters = 100f;
        [Tooltip("Visual world-space scroll distance per 10m tap so the child remains inside the street corridor.")]
        [SerializeField] private float visualScrollMetersPerStep = 1.4f;
        [SerializeField] private float stepScrollDuration = 0.65f;

        [Header("3D Milestone Sign in Front of Handlebars")]
        [SerializeField] private Transform milestoneSignRoot;
        [SerializeField] private TextMeshPro milestoneTextTMP;
        [SerializeField] private TextMesh milestoneTextLegacy;
        [SerializeField] private Transform progressBarFill;

        [Header("Events")]
        [SerializeField] private UnityEvent<float, float> onDistanceChanged = new UnityEvent<float, float>();
        [SerializeField] private UnityEvent onRideCompleted = new UnityEvent();

        private Vector3 _streetInitialWorldPos;
        private bool _capturedStreetInitialPos;
        private Vector3 _scrollDirectionWorld;

        private int _pedalCount;
        private float _currentDistanceMeters;
        private float _displayedVisualOffset;
        private float _targetVisualOffset;
        private bool _rideCompleted;

        private Coroutine _scrollCoroutine;
        private AudioSource _bellAudioSource;
        private AudioClip _bellClip;

        public int PedalCount => _pedalCount;
        public float CurrentDistanceMeters => _currentDistanceMeters;
        public float TargetDistanceMeters => targetDistanceMeters;
        public float NormalizedProgress => targetDistanceMeters > 0f
            ? Mathf.Clamp01(_currentDistanceMeters / targetDistanceMeters)
            : 1f;
        public bool IsRideCompleted => _rideCompleted;

        public event System.Action<float, float> DistanceChanged;
        public event System.Action RideCompleted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            EnsureReferences();
            CaptureStreetBaseline();
            SetupSynthesizedBellAudio();
            EnsureMilestoneSignVisuals();
            UpdateMilestoneVisuals();
        }

        private void OnEnable()
        {
            if (ParentHotkeyRouter.Instance != null)
            {
                ParentHotkeyRouter.Instance.ActionTapped += HandleActionTapped;
            }

            if (PovRigController.Instance != null)
            {
                PovRigController.Instance.PovChanged += HandlePovChanged;
            }
        }

        private void Start()
        {
            if (ParentHotkeyRouter.Instance != null)
            {
                ParentHotkeyRouter.Instance.ActionTapped -= HandleActionTapped;
                ParentHotkeyRouter.Instance.ActionTapped += HandleActionTapped;
            }

            if (PovRigController.Instance != null)
            {
                PovRigController.Instance.PovChanged -= HandlePovChanged;
                PovRigController.Instance.PovChanged += HandlePovChanged;
            }
        }

        private void OnDisable()
        {
            if (ParentHotkeyRouter.Instance != null)
            {
                ParentHotkeyRouter.Instance.ActionTapped -= HandleActionTapped;
            }

            if (PovRigController.Instance != null)
            {
                PovRigController.Instance.PovChanged -= HandlePovChanged;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void ConfigureReferences(Transform streetTransform, Transform bikeAnchor)
        {
            scrollingStreetTransform = streetTransform;
            bikeSeatAnchor = bikeAnchor;
            CaptureStreetBaseline();
            EnsureMilestoneSignVisuals();
            UpdateMilestoneVisuals();
        }

        private void HandleActionTapped(ParentAction action)
        {
            if (action == ParentAction.BikePedalStretch)
            {
                AdvancePedalStretch();
            }
        }

        private void HandlePovChanged(bool isAtStallPov)
        {
            if (milestoneSignRoot != null)
            {
                // Keep the 3D milestone sign visible while at the Bike Seat POV
                milestoneSignRoot.gameObject.SetActive(!isAtStallPov);
            }

            // When arriving at the Food Stall POV (at peak blackness), restore the street to its home alignment
            // around the cart so the stall sits centered in the village street.
            if (isAtStallPov && scrollingStreetTransform != null && _capturedStreetInitialPos)
            {
                if (_scrollCoroutine != null)
                {
                    StopCoroutine(_scrollCoroutine);
                    _scrollCoroutine = null;
                }

                scrollingStreetTransform.position = _streetInitialWorldPos;
                _displayedVisualOffset = 0f;
            }
        }

        /// <summary>
        /// Triggered on Spacebar tap: advances +10m along the festival path, scrolls the street smoothly
        /// past the stationary bike/cart, rings the soft chime bell, and auto-transitions to the Stall POV at 100m.
        /// Extra taps after 100m are ignored.
        /// </summary>
        public void AdvancePedalStretch()
        {
            if (_rideCompleted)
            {
                Debug.Log("[BikeRideController] Already arrived at 100m Festival Stall — extra Spacebar tap ignored.");
                return;
            }

            // If parent accidentally tapped Spacebar while at Stall POV, ignore
            if (PovRigController.Instance != null && PovRigController.Instance.IsAtStallPov)
            {
                return;
            }

            EnsureReferences();
            CaptureStreetBaseline();

            _pedalCount++;
            _currentDistanceMeters = Mathf.Min(targetDistanceMeters, _pedalCount * metersPerTap);
            _targetVisualOffset = _pedalCount * visualScrollMetersPerStep;

            PlaySoftBellChime(_pedalCount);
            UpdateMilestoneVisuals();

            if (_scrollCoroutine != null)
            {
                StopCoroutine(_scrollCoroutine);
            }
            _scrollCoroutine = StartCoroutine(SmoothScrollStreetRoutine(_targetVisualOffset));

            Debug.Log($"[BikeRideController] Bike Pedal Stretch #{_pedalCount}: {_currentDistanceMeters:F0}m / {targetDistanceMeters:F0}m");
            DistanceChanged?.Invoke(_currentDistanceMeters, targetDistanceMeters);
            onDistanceChanged?.Invoke(_currentDistanceMeters, targetDistanceMeters);

            if (_currentDistanceMeters >= targetDistanceMeters)
            {
                _rideCompleted = true;
                Debug.Log("[BikeRideController] Reached 100m! Auto-transitioning to POV 2 (FoodStallCounterAnchor).");
                RideCompleted?.Invoke();
                onRideCompleted?.Invoke();

                if (PovRigController.Instance != null)
                {
                    PovRigController.Instance.TransitionToPov(true);
                }
            }
        }

        /// <summary>
        /// Backspace skip: jumps straight to 100m arrival (no chimes for the skipped pedals) and
        /// transitions to the Stall POV behind the usual comfort fade.
        /// </summary>
        public void SkipToArrival()
        {
            if (_rideCompleted) return;

            if (_scrollCoroutine != null)
            {
                StopCoroutine(_scrollCoroutine);
                _scrollCoroutine = null;
            }

            _pedalCount = Mathf.CeilToInt(targetDistanceMeters / Mathf.Max(0.01f, metersPerTap));
            _currentDistanceMeters = targetDistanceMeters;
            _targetVisualOffset = _pedalCount * visualScrollMetersPerStep;
            _rideCompleted = true;
            UpdateMilestoneVisuals();

            Debug.Log("[BikeRideController] Backspace skip: arriving at 100m Festival Stall.");
            DistanceChanged?.Invoke(_currentDistanceMeters, targetDistanceMeters);
            onDistanceChanged?.Invoke(_currentDistanceMeters, targetDistanceMeters);
            RideCompleted?.Invoke();
            onRideCompleted?.Invoke();

            if (PovRigController.Instance != null && !PovRigController.Instance.IsAtStallPov)
            {
                PovRigController.Instance.TransitionToPov(true);
            }
        }

        /// <summary>
        /// Zero-penalty reset of the bike ride back to 0m / 100m.
        /// </summary>
        public void ResetRide()
        {
            if (_scrollCoroutine != null)
            {
                StopCoroutine(_scrollCoroutine);
                _scrollCoroutine = null;
            }

            _pedalCount = 0;
            _currentDistanceMeters = 0f;
            _displayedVisualOffset = 0f;
            _targetVisualOffset = 0f;
            _rideCompleted = false;

            if (scrollingStreetTransform != null && _capturedStreetInitialPos)
            {
                scrollingStreetTransform.position = _streetInitialWorldPos;
            }

            UpdateMilestoneVisuals();
            DistanceChanged?.Invoke(_currentDistanceMeters, targetDistanceMeters);
            onDistanceChanged?.Invoke(_currentDistanceMeters, targetDistanceMeters);
        }

        private IEnumerator SmoothScrollStreetRoutine(float destinationOffset)
        {
            float startOffset = _displayedVisualOffset;
            float duration = Mathf.Max(0.15f, stepScrollDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // SmoothStep easing (zero sudden acceleration spikes for CP vestibular comfort)
                float eased = t * t * (3f - 2f * t);
                _displayedVisualOffset = Mathf.Lerp(startOffset, destinationOffset, eased);

                if (scrollingStreetTransform != null && _capturedStreetInitialPos)
                {
                    scrollingStreetTransform.position = _streetInitialWorldPos + (_scrollDirectionWorld * _displayedVisualOffset);
                }

                yield return null;
            }

            _displayedVisualOffset = destinationOffset;
            if (scrollingStreetTransform != null && _capturedStreetInitialPos)
            {
                scrollingStreetTransform.position = _streetInitialWorldPos + (_scrollDirectionWorld * _displayedVisualOffset);
            }

            _scrollCoroutine = null;
        }

        private void EnsureReferences()
        {
            if (scrollingStreetTransform == null)
            {
                GameObject streetObj = GameObject.Find("Test Street03/street")
                                    ?? GameObject.Find("Test Street/street")
                                    ?? GameObject.Find("street");
                if (streetObj != null)
                {
                    scrollingStreetTransform = streetObj.transform;
                }
            }

            if (bikeSeatAnchor == null)
            {
                GameObject bikeObj = GameObject.Find("POV_1_BikeSeatAnchor");
                if (bikeObj != null)
                {
                    bikeSeatAnchor = bikeObj.transform;
                }
            }
        }

        private void CaptureStreetBaseline()
        {
            if (_capturedStreetInitialPos || scrollingStreetTransform == null)
            {
                return;
            }

            _streetInitialWorldPos = scrollingStreetTransform.position;
            // Scroll backwards relative to the bike's forward heading along the horizontal plane
            Vector3 bikeForward = Vector3.forward;
            if (bikeSeatAnchor != null)
            {
                bikeForward = Vector3.ProjectOnPlane(bikeSeatAnchor.forward, Vector3.up).normalized;
                if (bikeForward.sqrMagnitude < 0.001f)
                {
                    bikeForward = Vector3.forward;
                }
            }

            _scrollDirectionWorld = -bikeForward;
            _capturedStreetInitialPos = true;
        }

        private void EnsureMilestoneSignVisuals()
        {
            if (milestoneSignRoot == null)
            {
                GameObject existing = GameObject.Find("ARR_BikeMilestoneSign");
                if (existing != null)
                {
                    milestoneSignRoot = existing.transform;
                }
            }

            if (milestoneSignRoot == null)
            {
                GameObject signObj = new GameObject("ARR_BikeMilestoneSign");
                milestoneSignRoot = signObj.transform;

                // Position right above the bicycle handlebars (Z ≈ 2.85, X ≈ 7.09, Y ≈ 1.42) facing the rider
                Vector3 signPos = new Vector3(7.09f, 1.44f, 2.88f);
                Quaternion signRot = Quaternion.Euler(0f, 4.33f, 0f);
                if (bikeSeatAnchor != null)
                {
                    Vector3 flatFwd = Vector3.ProjectOnPlane(bikeSeatAnchor.forward, Vector3.up).normalized;
                    signPos = bikeSeatAnchor.position + (flatFwd * 0.78f) + new Vector3(0f, -0.10f, 0f);
                    signRot = Quaternion.LookRotation(flatFwd, Vector3.up);
                }

                milestoneSignRoot.SetPositionAndRotation(signPos, signRot);

                // Backing board
                GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
                board.name = "MilestoneBoard";
                Collider boardCol = board.GetComponent<Collider>();
                if (boardCol != null) Destroy(boardCol);
                board.transform.SetParent(milestoneSignRoot, false);
                board.transform.localPosition = Vector3.zero;
                board.transform.localScale = new Vector3(0.46f, 0.19f, 0.02f);
                Renderer boardRenderer = board.GetComponent<Renderer>();
                if (boardRenderer != null)
                {
                    Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    if (sh != null)
                    {
                        Material m = new Material(sh) { color = new Color(0.14f, 0.10f, 0.07f, 1f) };
                        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.14f, 0.10f, 0.07f, 1f));
                        boardRenderer.sharedMaterial = m;
                    }
                }

                // Progress bar track
                GameObject barBg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                barBg.name = "ProgressBarTrack";
                Collider bgCol = barBg.GetComponent<Collider>();
                if (bgCol != null) Destroy(bgCol);
                barBg.transform.SetParent(milestoneSignRoot, false);
                barBg.transform.localPosition = new Vector3(0f, -0.055f, -0.012f);
                barBg.transform.localScale = new Vector3(0.38f, 0.026f, 0.01f);
                Renderer bgRenderer = barBg.GetComponent<Renderer>();
                if (bgRenderer != null)
                {
                    Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    if (sh != null)
                    {
                        Material m = new Material(sh) { color = new Color(0.25f, 0.25f, 0.28f, 1f) };
                        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.25f, 0.25f, 0.28f, 1f));
                        bgRenderer.sharedMaterial = m;
                    }
                }

                // Progress bar fill
                GameObject barFill = GameObject.CreatePrimitive(PrimitiveType.Cube);
                barFill.name = "ProgressBarFill";
                Collider fillCol = barFill.GetComponent<Collider>();
                if (fillCol != null) Destroy(fillCol);
                barFill.transform.SetParent(milestoneSignRoot, false);
                barFill.transform.localPosition = new Vector3(-0.19f, -0.055f, -0.016f);
                barFill.transform.localScale = new Vector3(0.001f, 0.024f, 0.012f);
                Renderer fillRenderer = barFill.GetComponent<Renderer>();
                if (fillRenderer != null)
                {
                    Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    if (sh != null)
                    {
                        Material m = new Material(sh) { color = new Color(0.25f, 0.88f, 0.42f, 1f) };
                        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.25f, 0.88f, 0.42f, 1f));
                        fillRenderer.sharedMaterial = m;
                    }
                }
                progressBarFill = barFill.transform;

                // 3D Text label
                GameObject textObj = new GameObject("MilestoneLabel");
                textObj.transform.SetParent(milestoneSignRoot, false);
                textObj.transform.localPosition = new Vector3(0f, 0.018f, -0.015f);
                textObj.transform.localRotation = Quaternion.identity;

                milestoneTextTMP = textObj.AddComponent<TextMeshPro>();
                milestoneTextTMP.alignment = TextAlignmentOptions.Center;
                milestoneTextTMP.fontSize = 0.36f;
                milestoneTextTMP.color = new Color(1f, 0.92f, 0.55f, 1f);
                RectTransform rt = milestoneTextTMP.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.sizeDelta = new Vector2(0.44f, 0.14f);
                }
            }
            else
            {
                if (milestoneTextTMP == null)
                {
                    milestoneTextTMP = milestoneSignRoot.GetComponentInChildren<TextMeshPro>(true);
                }
                if (milestoneTextLegacy == null)
                {
                    milestoneTextLegacy = milestoneSignRoot.GetComponentInChildren<TextMesh>(true);
                }
                if (progressBarFill == null)
                {
                    Transform foundFill = milestoneSignRoot.Find("ProgressBarFill");
                    if (foundFill != null) progressBarFill = foundFill;
                }
            }
        }

        private void UpdateMilestoneVisuals()
        {
            string statusLine = _rideCompleted
                ? $"FESTIVAL ARRIVED!\n{_currentDistanceMeters:F0}m / {targetDistanceMeters:F0}m"
                : $"YATAI BIKE RIDE\n{_currentDistanceMeters:F0}m / {targetDistanceMeters:F0}m";

            if (milestoneTextTMP != null)
            {
                milestoneTextTMP.text = statusLine;
            }

            if (milestoneTextLegacy != null)
            {
                milestoneTextLegacy.text = statusLine;
            }

            if (progressBarFill != null)
            {
                const float maxBarWidth = 0.38f;
                float fillWidth = Mathf.Max(0.004f, maxBarWidth * NormalizedProgress);
                progressBarFill.localScale = new Vector3(fillWidth, 0.024f, 0.012f);
                progressBarFill.localPosition = new Vector3(-0.19f + (fillWidth * 0.5f), -0.055f, -0.016f);
            }
        }

        /// <summary>
        /// Synthesizes a soft, warm bicycle bell chime with a gentle attack envelope (AGENTS.md §5 Audio Safety).
        /// </summary>
        private void SetupSynthesizedBellAudio()
        {
            _bellAudioSource = GetComponent<AudioSource>();
            if (_bellAudioSource == null)
            {
                _bellAudioSource = gameObject.AddComponent<AudioSource>();
            }

            _bellAudioSource.playOnAwake = false;
            _bellAudioSource.spatialBlend = 0f;
            _bellAudioSource.volume = 0.35f;

            int sampleRate = 44100;
            float duration = 0.45f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                // Soft attack envelope (50ms ramp) + smooth exponential decay
                float attack = Mathf.Clamp01(t / 0.05f);
                float decay = Mathf.Exp(-6.5f * t);
                float tone = Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.7f
                           + Mathf.Sin(2f * Mathf.PI * 1318.5f * t) * 0.3f;
                samples[i] = tone * attack * decay * 0.4f;
            }

            _bellClip = AudioClip.Create("SoftYataiBikeBell", sampleCount, 1, sampleRate, false);
            _bellClip.SetData(samples, 0);
        }

        private void PlaySoftBellChime(int stepIndex)
        {
            if (_bellAudioSource == null || _bellClip == null)
            {
                return;
            }

            _bellAudioSource.pitch = 0.95f + (Mathf.Clamp01(stepIndex / 10f) * 0.20f);
            _bellAudioSource.PlayOneShot(_bellClip, 0.35f);
        }
    }
}
