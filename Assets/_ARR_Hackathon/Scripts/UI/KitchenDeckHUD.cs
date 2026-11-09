using UnityEngine;

namespace YataiTogether
{
    /// <summary>
    /// Step 3: Kitchen Deck HUD (Parent Sous-Chef Companion Deck).
    /// Styled as Po's Festival Kitchen Deck, NOT a clinical or medical chart.
    /// Provides one-handed parent with instant visual feedback:
    /// - Current Customer (e.g. "Customer 1/5: Monkey / Po")
    /// - Current Recipe Step & Movement Reframe
    /// - Next Valid Hotkey Banner (Large prominent prompt)
    /// - Hold Ring Progress Bars (W: 2-Hand Stretch, B: Boiler Hold, F: Nori Press)
    /// - Sequence Locked Warning Banner (alerts on invalid/out-of-order keys)
    /// - Active Camera POV & Curtain State
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KitchenDeckHUD : MonoBehaviour
    {
        public static KitchenDeckHUD Instance { get; private set; }

        [Header("Display Toggle")]
        [SerializeField] private bool showHUD = true;

        private Texture2D _deckBgTex;
        private Texture2D _headerBgTex;
        private Texture2D _nextKeyBgTex;
        private Texture2D _warningBgTex;
        private Texture2D _barTrackTex;
        private Texture2D _barFillTex;

        private GUIStyle _deckStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _subHeaderStyle;
        private GUIStyle _nextKeyStyle;
        private GUIStyle _warningStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _statusStyle;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
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
            DestroyTextures();
        }

        private void EnsureStyles()
        {
            if (_deckStyle != null) return;

            _deckBgTex = MakeSolidTexture(new Color(0.09f, 0.08f, 0.11f, 0.95f));
            _headerBgTex = MakeSolidTexture(new Color(0.24f, 0.16f, 0.12f, 0.98f));
            _nextKeyBgTex = MakeSolidTexture(new Color(0.12f, 0.48f, 0.28f, 0.98f));
            _warningBgTex = MakeSolidTexture(new Color(0.72f, 0.16f, 0.14f, 0.98f));
            _barTrackTex = MakeSolidTexture(new Color(0.18f, 0.19f, 0.24f, 0.90f));
            _barFillTex = MakeSolidTexture(new Color(0.95f, 0.72f, 0.20f, 1.0f));

            _deckStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _deckBgTex },
                padding = new RectOffset(16, 16, 14, 14)
            };

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.90f, 0.50f) }
            };

            _subHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.92f, 0.94f, 1f) }
            };

            _nextKeyStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _nextKeyBgTex, textColor = Color.white },
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _warningStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _warningBgTex, textColor = Color.white },
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.85f, 0.88f, 0.95f) }
            };

            _statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.40f, 0.90f, 0.60f) }
            };
        }

        private static Texture2D MakeSolidTexture(Color color)
        {
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        private void DestroyTextures()
        {
            if (_deckBgTex != null) Destroy(_deckBgTex);
            if (_headerBgTex != null) Destroy(_headerBgTex);
            if (_nextKeyBgTex != null) Destroy(_nextKeyBgTex);
            if (_warningBgTex != null) Destroy(_warningBgTex);
            if (_barTrackTex != null) Destroy(_barTrackTex);
            if (_barFillTex != null) Destroy(_barFillTex);
        }

        private void OnGUI()
        {
            if (!showHUD) return;
            EnsureStyles();

            YataiSessionDirector director = YataiSessionDirector.Instance;
            PovRigController pov = PovRigController.Instance;
            BikeRideController bike = BikeRideController.Instance;

            const float width = 560f;
            const float height = 392f;
            // Docked on top-right of laptop screen
            Rect hudRect = new Rect(Screen.width - width - 16f, 16f, width, height);

            GUILayout.BeginArea(hudRect, _deckStyle);

            // Title Header
            GUILayout.BeginHorizontal();
            GUILayout.Label("🍜 YATAI TOGETHER — KITCHEN DECK", _headerStyle);
            GUILayout.FlexibleSpace();
            int currentCust = director != null ? director.CurrentCustomerIndex : 1;
            int maxCust = director != null ? director.MaxCustomers : 5;
            GUILayout.Label($"Customer {currentCust}/{maxCust}: Monkey / Po", _subHeaderStyle);
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);

            // POV & Curtain Status Row
            GUILayout.BeginHorizontal();
            string povLabel = pov != null ? pov.CurrentPovLabel : "POV: Unknown";
            GUILayout.Label($"Camera POV: {povLabel}", _labelStyle);
            GUILayout.FlexibleSpace();
            bool curtainOpen = director != null && director.IsCurtainOpen;
            bool simmerHold = director != null && director.IsSimmerHoldActive;
            string curtainStatus = simmerHold ? "⏸ SIMMER HOLD REST (Q to resume)" : (curtainOpen ? "🟢 STALL OPEN" : "⚪ STALL CLOSED");
            GUILayout.Label(curtainStatus, _statusStyle);
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);

            // Warning Banner (if sequence violation occurred)
            string warning = director != null ? director.WarningMessage : string.Empty;
            if (!string.IsNullOrEmpty(warning))
            {
                GUILayout.Box($"⚠️ {warning}", _warningStyle, GUILayout.Height(32f));
                GUILayout.Space(6f);
            }

            // Next Valid Hotkey Banner (The most important guidance for parent)
            string nextPrompt = director != null ? director.NextExpectedPrompt : "[SPACE] Pedal Bike (+10m)";
            GUILayout.Label("NEXT ACTION FOR PARENT / CHILD:", _subHeaderStyle);
            GUILayout.Box(nextPrompt, _nextKeyStyle, GUILayout.Height(40f));
            CookingStep stepNow = director != null ? director.CurrentStep : CookingStep.BikeRide;
            if (stepNow < CookingStep.CustomerServed)
            {
                GUILayout.Label("Too hard, or VR not responding?  Tap [Backspace] to skip this step (no penalty).", _statusStyle);
            }

            GUILayout.Space(10f);

            // Recipe Sequence Invariant Tracker
            GUILayout.Label("STRICT RAMEN ASSEMBLY SEQUENCE:", _subHeaderStyle);
            DrawRecipeSequenceTrack(director != null ? director.CurrentStep : CookingStep.BikeRide);

            GUILayout.Space(10f);

            // Active Stretch Hold Meters
            GUILayout.Label("ACTIVE HOLD PROGRESS (Release pauses, never resets):", _subHeaderStyle);
            float wProg = director != null ? director.WHoldProgress : 0f;
            float bProg = director != null ? director.BHoldProgress : 0f;
            float fProg = director != null ? director.FHoldProgress : 0f;

            DrawProgressBar($"[W] 2-Hand Band Stretch ({director?.WHoldAccumulated ?? 0f:F1}s / 3.0s)", wProg, new Color(0.95f, 0.75f, 0.25f));
            DrawProgressBar($"[B] Boiler Elbow Extension ({director?.BHoldAccumulated ?? 0f:F1}s / 3.0s)", bProg, new Color(0.35f, 0.85f, 0.45f));
            DrawProgressBar($"[F] Palm Flatten Golden Nori ({director?.FHoldAccumulated ?? 0f:F1}s / 5.0s)", fProg, new Color(0.95f, 0.55f, 0.25f));

            GUILayout.EndArea();
        }

        private void DrawRecipeSequenceTrack(CookingStep step)
        {
            GUILayout.BeginHorizontal();
            DrawStepPill("1. Noodles", step >= CookingStep.Pork_Grab, step >= CookingStep.Noodles_Grab && step < CookingStep.Pork_Grab);
            DrawStepPill("2. Pork", step >= CookingStep.Egg_Drop, step >= CookingStep.Pork_Grab && step < CookingStep.Egg_Drop);
            DrawStepPill("3. Egg", step >= CookingStep.Onions_Grab, step >= CookingStep.Egg_Drop && step < CookingStep.Onions_Grab);
            DrawStepPill("4. Onions", step >= CookingStep.Nori_Hold, step >= CookingStep.Onions_Grab && step < CookingStep.Nori_Hold);
            DrawStepPill("5. Nori", step >= CookingStep.Broth_Ladle, step == CookingStep.Nori_Hold);
            DrawStepPill("6. Broth", step >= CookingStep.ReadyToServe, step >= CookingStep.Broth_Ladle && step < CookingStep.ReadyToServe);
            DrawStepPill("7. Serve", step >= CookingStep.CustomerServed, step == CookingStep.ReadyToServe);
            GUILayout.EndHorizontal();
        }

        private void DrawStepPill(string label, bool completed, bool active)
        {
            Color color;
            if (completed)
                color = new Color(0.18f, 0.72f, 0.36f); // Green = done
            else if (active)
                color = new Color(0.95f, 0.80f, 0.25f); // Amber = current
            else
                color = new Color(0.40f, 0.42f, 0.50f); // Muted = locked

            Color prev = GUI.color;
            GUI.color = color;
            GUILayout.Box(label, GUI.skin.box, GUILayout.Width(84f), GUILayout.Height(24f));
            GUI.color = prev;
        }

        private void DrawProgressBar(string label, float progress, Color fillColor)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _labelStyle, GUILayout.Width(220f));

            Rect trackRect = GUILayoutUtility.GetRect(260f, 16f);
            GUI.DrawTexture(trackRect, _barTrackTex);

            if (progress > 0.001f)
            {
                Rect fillRect = new Rect(trackRect.x, trackRect.y, trackRect.width * progress, trackRect.height);
                Color prev = GUI.color;
                GUI.color = fillColor;
                GUI.DrawTexture(fillRect, _barFillTex);
                GUI.color = prev;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(2f);
        }
    }
}
