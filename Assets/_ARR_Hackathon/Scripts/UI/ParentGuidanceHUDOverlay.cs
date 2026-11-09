using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace YataiTogether
{
    /// <summary>
    /// Step 3 / 4 / 6 / 7 / 8: Screen Space - Overlay UI (Display 1) for Parent Guidance.
    /// Provides persistent TextMeshProUGUI displaying:
    /// - Current Step Number & Recipe Phase
    /// - Target Physical Movement & ARR VR Reframe
    /// - Parent Hotkey Backup
    /// - Warning Banner for out-of-order keys
    /// </summary>
    [DisallowMultipleComponent]
    public class ParentGuidanceHUDOverlay : MonoBehaviour
    {
        public static ParentGuidanceHUDOverlay Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private Canvas overlayCanvas;
        [SerializeField] private TextMeshProUGUI stepTitleText;
        [SerializeField] private TextMeshProUGUI targetMovementText;
        [SerializeField] private TextMeshProUGUI hotkeyBackupText;
        [SerializeField] private TextMeshProUGUI warningBannerText;
        [SerializeField] private GameObject warningPanel;

        private float _warningTimer = 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            EnsureCanvasAndTMP();
        }

        private void OnEnable()
        {
            if (YataiSessionDirector.Instance != null)
            {
                YataiSessionDirector.Instance.StepChanged += HandleStepChanged;
                YataiSessionDirector.Instance.SequenceWarning += HandleSequenceWarning;
            }
        }

        private void Start()
        {
            if (YataiSessionDirector.Instance != null)
            {
                YataiSessionDirector.Instance.StepChanged -= HandleStepChanged;
                YataiSessionDirector.Instance.StepChanged += HandleStepChanged;
                YataiSessionDirector.Instance.SequenceWarning -= HandleSequenceWarning;
                YataiSessionDirector.Instance.SequenceWarning += HandleSequenceWarning;
                UpdateHUD(YataiSessionDirector.Instance.CurrentStep);
            }
        }

        private void OnDisable()
        {
            if (YataiSessionDirector.Instance != null)
            {
                YataiSessionDirector.Instance.StepChanged -= HandleStepChanged;
                YataiSessionDirector.Instance.SequenceWarning -= HandleSequenceWarning;
            }
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
            if (_warningTimer > 0f)
            {
                _warningTimer -= Time.deltaTime;
                if (_warningTimer <= 0f && warningPanel != null)
                {
                    warningPanel.SetActive(false);
                }
            }
        }

        private void HandleStepChanged(CookingStep step)
        {
            UpdateHUD(step);
        }

        private void HandleSequenceWarning(string message)
        {
            ShowWarning(message);
        }

        public void UpdateHUD(CookingStep step)
        {
            string stepNum = "";
            string title = "";
            string movement = "";
            string backup = "";

            switch (step)
            {
                case CookingStep.BikeRide:
                    stepNum = "Step 2";
                    title = "Village Road Bike Ride";
                    movement = "Lower-limb seated cycling leg stretch";
                    backup = "Tap [Space] (+10m per pedal)";
                    break;

                case CookingStep.CurtainClosed:
                    stepNum = "Step 4";
                    title = "Noren Shop Curtain";
                    movement = "Overhead bimanual reach (shoulder flexion)";
                    backup = "Tap [Q] to open shop stall / Simmer Hold";
                    break;

                case CookingStep.Noodles_Grab:
                    stepNum = "Step 5";
                    title = "Pinch Fresh Noodles";
                    movement = "Pinch & pick up noodle element in Zone B";
                    backup = "Tap [4] (Noodles to Hand)";
                    break;

                case CookingStep.Noodles_Stretch:
                    stepNum = "Step 5";
                    title = "2-Hand Noodle Stretch";
                    movement = "Noodle ends in both hands, pull hands apart (bimanual abduction)";
                    backup = "Hold [W] for 3.0s";
                    break;

                case CookingStep.Noodles_Boil:
                    stepNum = "Step 6";
                    title = "Boil Noodles in Pot";
                    movement = "Elbow extension reach over pot & hold steady";
                    backup = "Hold [B] for 3.0s (release pauses)";
                    break;

                case CookingStep.Noodles_Transfer:
                    stepNum = "Step 6";
                    title = "Noodles from Pot to Bowl";
                    movement = "Reach into pot (or scoop with spoon), then release over ramen bowl";
                    backup = "Tap [4] (Noodles out of Pot) -> [A] / [Enter] (Drop into Bowl)";
                    break;

                case CookingStep.Pork_Grab:
                    stepNum = "Step 7";
                    title = "Pinch Chashu Pork";
                    movement = "Finger opening & pinch silicone pork in Zone B";
                    backup = "Tap [1] (Pork to Hand)";
                    break;

                case CookingStep.Pork_Release:
                    stepNum = "Step 7";
                    title = "Release Pork into Bowl";
                    movement = "Reach to ramen bowl & release fingers";
                    backup = "Tap [A] / [Enter] (Drop into Bowl)";
                    break;

                case CookingStep.Egg_Drop:
                    stepNum = "Step 8";
                    title = "Ajitama Egg Supination Drop";
                    movement = "Forearm supination (palm-up) above egg_bowl";
                    backup = "Tap [2] (Drop Magic Egg into Palm)";
                    break;

                case CookingStep.Egg_Release:
                    stepNum = "Step 8";
                    title = "Release Egg into Bowl";
                    movement = "Reach to ramen bowl & release egg";
                    backup = "Tap [A] / [Enter] (Drop into Bowl)";
                    break;

                case CookingStep.Onions_Grab:
                    stepNum = "Step 9";
                    title = "Pinch Green Onions";
                    movement = "Pinch & grasp onion toppings in Zone B";
                    backup = "Tap [3] (Onions to Hand)";
                    break;

                case CookingStep.Onions_Release:
                    stepNum = "Step 9";
                    title = "Scatter Onions in Bowl";
                    movement = "Release over ramen bowl";
                    backup = "Tap [A] / [Enter] (Drop into Bowl)";
                    break;

                case CookingStep.Nori_Hold:
                    stepNum = "Step 10";
                    title = "Warm Paw Golden Nori";
                    movement = "Parent-assisted palm flattening (wrist/finger flexor release)";
                    backup = "Hold [F] for 5.0s";
                    break;

                case CookingStep.Broth_Ladle:
                    stepNum = "Step 11";
                    title = "Grasp Broth Ladle";
                    movement = "Gross grasp ladle in Zone A";
                    backup = "Tap [5] (Ladle to Hand)";
                    break;

                case CookingStep.Broth_Pour:
                    stepNum = "Step 11";
                    title = "Pour Hot Broth";
                    movement = "Forearm/wrist rotation pour (2.5x gain)";
                    backup = "Tap [D] (Pour Broth)";
                    break;

                case CookingStep.ReadyToServe:
                    stepNum = "Step 12";
                    title = "Serve Ramen to Customer!";
                    movement = "Two-hand forward reach (push bowl)";
                    backup = "Tap [S] (Serve Customer!)";
                    break;

                case CookingStep.CustomerServed:
                    stepNum = "Celebration";
                    title = "Yippee! Customer Served!";
                    movement = "Relax hands & watch festival fireworks";
                    backup = "Next order loading...";
                    break;

                case CookingStep.SessionComplete:
                    stepNum = "Complete";
                    title = "Festival Session Complete!";
                    movement = "Rehab session successfully finished!";
                    backup = "Great work!";
                    break;
            }

            if (stepTitleText != null)
            {
                stepTitleText.text = $"{stepNum}: {title}";
            }

            if (targetMovementText != null)
            {
                targetMovementText.text = $"Movement: {movement}";
            }

            if (hotkeyBackupText != null)
            {
                bool canSkip = step < CookingStep.CustomerServed;
                hotkeyBackupText.text = canSkip
                    ? $"Parent Hotkey: {backup}   |   [Backspace] Skip step"
                    : $"Parent Hotkey: {backup}";
            }
        }

        public void ShowWarning(string message)
        {
            if (warningPanel != null) warningPanel.SetActive(true);
            if (warningBannerText != null) warningBannerText.text = message;
            _warningTimer = 3.0f;
        }

        private void EnsureCanvasAndTMP()
        {
            if (overlayCanvas == null)
            {
                GameObject canvasGo = new GameObject("ParentGuidanceCanvas");
                canvasGo.transform.SetParent(transform, false);

                overlayCanvas = canvasGo.AddComponent<Canvas>();
                overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                overlayCanvas.targetDisplay = 0; // Display 1

                CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasGo.AddComponent<GraphicRaycaster>();

                // Build Guidance Panel (Top-Left of screen)
                // ponytail: Moved to top-left (0, 1) to eliminate overlap with Kitchen Deck HUD on top-right
                GameObject panelGo = new GameObject("GuidancePanel");
                panelGo.transform.SetParent(canvasGo.transform, false);
                RectTransform panelRect = panelGo.AddComponent<RectTransform>();
                panelRect.anchorMin = new Vector2(0f, 1f);
                panelRect.anchorMax = new Vector2(0f, 1f);
                panelRect.pivot = new Vector2(0f, 1f);
                panelRect.anchoredPosition = new Vector2(20f, -20f);
                panelRect.sizeDelta = new Vector2(580f, 170f);

                Image panelImg = panelGo.AddComponent<Image>();
                panelImg.color = new Color(0.08f, 0.08f, 0.12f, 0.92f);

                // Step Title Text
                GameObject titleGo = new GameObject("StepTitleText");
                titleGo.transform.SetParent(panelGo.transform, false);
                RectTransform titleRect = titleGo.AddComponent<RectTransform>();
                titleRect.anchorMin = new Vector2(0f, 0.65f);
                titleRect.anchorMax = new Vector2(1f, 1f);
                titleRect.offsetMin = new Vector2(15f, 0f);
                titleRect.offsetMax = new Vector2(-15f, -10f);
                stepTitleText = titleGo.AddComponent<TextMeshProUGUI>();
                stepTitleText.fontSize = 24;
                stepTitleText.fontStyle = FontStyles.Bold;
                stepTitleText.color = new Color(1f, 0.88f, 0.4f);

                // Target Movement Text
                GameObject movGo = new GameObject("TargetMovementText");
                movGo.transform.SetParent(panelGo.transform, false);
                RectTransform movRect = movGo.AddComponent<RectTransform>();
                movRect.anchorMin = new Vector2(0f, 0.35f);
                movRect.anchorMax = new Vector2(1f, 0.65f);
                movRect.offsetMin = new Vector2(15f, 0f);
                movRect.offsetMax = new Vector2(-15f, 0f);
                targetMovementText = movGo.AddComponent<TextMeshProUGUI>();
                targetMovementText.fontSize = 17;
                targetMovementText.color = new Color(0.9f, 0.94f, 1f);

                // Hotkey Backup Text
                GameObject hotkeyGo = new GameObject("HotkeyBackupText");
                hotkeyGo.transform.SetParent(panelGo.transform, false);
                RectTransform hotkeyRect = hotkeyGo.AddComponent<RectTransform>();
                hotkeyRect.anchorMin = new Vector2(0f, 0.05f);
                hotkeyRect.anchorMax = new Vector2(1f, 0.35f);
                hotkeyRect.offsetMin = new Vector2(15f, 5f);
                hotkeyRect.offsetMax = new Vector2(-15f, 0f);
                hotkeyBackupText = hotkeyGo.AddComponent<TextMeshProUGUI>();
                hotkeyBackupText.fontSize = 16;
                hotkeyBackupText.fontStyle = FontStyles.Bold;
                hotkeyBackupText.color = new Color(0.4f, 0.95f, 0.55f);

                // Warning Panel (Center-Top of screen)
                warningPanel = new GameObject("WarningPanel");
                warningPanel.transform.SetParent(canvasGo.transform, false);
                RectTransform warnRect = warningPanel.AddComponent<RectTransform>();
                warnRect.anchorMin = new Vector2(0.5f, 1f);
                warnRect.anchorMax = new Vector2(0.5f, 1f);
                warnRect.pivot = new Vector2(0.5f, 1f);
                warnRect.anchoredPosition = new Vector2(0f, -25f);
                warnRect.sizeDelta = new Vector2(700f, 60f);

                Image warnImg = warningPanel.AddComponent<Image>();
                warnImg.color = new Color(0.85f, 0.15f, 0.15f, 0.95f);

                GameObject warnTextGo = new GameObject("WarningText");
                warnTextGo.transform.SetParent(warningPanel.transform, false);
                RectTransform wtRect = warnTextGo.AddComponent<RectTransform>();
                wtRect.anchorMin = Vector2.zero;
                wtRect.anchorMax = Vector2.one;
                wtRect.offsetMin = new Vector2(10f, 5f);
                wtRect.offsetMax = new Vector2(-10f, -5f);
                warningBannerText = warnTextGo.AddComponent<TextMeshProUGUI>();
                warningBannerText.fontSize = 20;
                warningBannerText.fontStyle = FontStyles.Bold;
                warningBannerText.alignment = TextAlignmentOptions.Center;
                warningBannerText.color = Color.white;

                warningPanel.SetActive(false);
            }
        }
    }
}
