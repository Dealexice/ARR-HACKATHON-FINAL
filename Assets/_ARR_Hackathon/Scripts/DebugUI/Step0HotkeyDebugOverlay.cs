using System.Collections.Generic;
using UnityEngine;

namespace YataiTogether
{
    /// <summary>
    /// Step 0 diagnostic overlay for verifying ParentHotkeyRouter in Editor Play Mode.
    /// Renders the Parent One-Handed Keyboard Cluster and recent event feed on the Game View.
    /// Can be toggled via inspector or disabled once Step 3 KitchenDeckHUD takes over.
    /// </summary>
    [RequireComponent(typeof(ParentHotkeyRouter))]
    public class Step0HotkeyDebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool showOverlay = true;

        private ParentHotkeyRouter _router;
        private readonly Queue<string> _recentLogs = new Queue<string>(10);
        private readonly Dictionary<ParentAction, float> _lastTriggeredTimestamp = new Dictionary<ParentAction, float>(16);

        private float _wHoldAccumulated;
        private float _bHoldAccumulated;
        private float _fHoldAccumulated;

        private GUIStyle _panelStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _keyIdleStyle;
        private GUIStyle _keyActiveStyle;
        private GUIStyle _logStyle;
        private Texture2D _panelTex;
        private Texture2D _idleTex;
        private Texture2D _activeTex;

        private const int MaxLogEntries = 8;
        private const float FlashDuration = 0.35f;

        private void Awake()
        {
            _router = GetComponent<ParentHotkeyRouter>();
        }

        private void OnEnable()
        {
            if (_router == null) _router = GetComponent<ParentHotkeyRouter>();
            if (_router == null) return;

            _router.ActionTapped += HandleActionTapped;
            _router.HoldStarted += HandleHoldStarted;
            _router.HoldTicked += HandleHoldTicked;
            _router.HoldStopped += HandleHoldStopped;
        }

        private void OnDisable()
        {
            if (_router == null) return;

            _router.ActionTapped -= HandleActionTapped;
            _router.HoldStarted -= HandleHoldStarted;
            _router.HoldTicked -= HandleHoldTicked;
            _router.HoldStopped -= HandleHoldStopped;
        }

        private void HandleActionTapped(ParentAction action)
        {
            _lastTriggeredTimestamp[action] = Time.time;
            AppendLog($"[{Time.time:F2}s] TAP -> {action}");
        }

        private void HandleHoldStarted(ParentAction action)
        {
            _lastTriggeredTimestamp[action] = Time.time;
            AppendLog($"[{Time.time:F2}s] HOLD START -> {action}");
        }

        private void HandleHoldTicked(ParentAction action, float dt)
        {
            _lastTriggeredTimestamp[action] = Time.time;
            switch (action)
            {
                case ParentAction.BandPullStretch:
                    _wHoldAccumulated += dt;
                    break;
                case ParentAction.BoilHold:
                    _bHoldAccumulated += dt;
                    break;
                case ParentAction.HandFlattenHold:
                    _fHoldAccumulated += dt;
                    break;
            }
        }

        private void HandleHoldStopped(ParentAction action)
        {
            AppendLog($"[{Time.time:F2}s] HOLD PAUSED -> {action}");
        }

        private void AppendLog(string entry)
        {
            if (_recentLogs.Count >= MaxLogEntries)
            {
                _recentLogs.Dequeue();
            }
            _recentLogs.Enqueue(entry);
        }

        private void EnsureStyles()
        {
            if (_panelStyle != null) return;

            _panelTex = MakeSolidTexture(new Color(0.08f, 0.09f, 0.12f, 0.90f));
            _idleTex = MakeSolidTexture(new Color(0.20f, 0.22f, 0.28f, 0.95f));
            _activeTex = MakeSolidTexture(new Color(0.18f, 0.72f, 0.36f, 0.98f));

            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _panelTex },
                padding = new RectOffset(14, 14, 12, 12)
            };

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.35f) }
            };

            _keyIdleStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _idleTex, textColor = Color.white },
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };

            _keyActiveStyle = new GUIStyle(_keyIdleStyle)
            {
                normal = { background = _activeTex, textColor = Color.white }
            };

            _logStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.85f, 0.92f, 1f) }
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

        private bool IsActionHighlighted(ParentAction action)
        {
            if (_router != null && _router.IsHolding(action)) return true;
            if (_lastTriggeredTimestamp.TryGetValue(action, out float t))
            {
                return (Time.time - t) <= FlashDuration;
            }
            return false;
        }

        private void OnGUI()
        {
            if (!showOverlay) return;
            EnsureStyles();

            const float width = 620f;
            const float height = 445f;
            Rect panelRect = new Rect(16f, 16f, width, height);

            GUILayout.BeginArea(panelRect, _panelStyle);
            GUILayout.Label("YATAI TOGETHER — STEPS 0, 1 & 2: Hotkeys, Dual-POV & 100m Bike Ride", _headerStyle);
            string povText = PovRigController.Instance != null
                ? $"{PovRigController.Instance.CurrentPovLabel} {(PovRigController.Instance.IsTransitioning ? "[FADING...]" : "")}"
                : "POV: (PovRigController not initialized)";
            GUILayout.Label($"Active Camera POV: {povText}", _logStyle);
            string bikeText = BikeRideController.Instance != null
                ? $"{BikeRideController.Instance.CurrentDistanceMeters:F0}m / {BikeRideController.Instance.TargetDistanceMeters:F0}m (Pedals: {BikeRideController.Instance.PedalCount}/10) {(BikeRideController.Instance.IsRideCompleted ? "[ARRIVED AT STALL]" : "")}"
                : "Bike Ride: (BikeRideController not initialized)";
            GUILayout.Label($"Bike Stretch Progress: {bikeText}", _logStyle);
            GUILayout.Space(6f);

            // Row 1: 1, 2, 3, 4, 5, Backspace
            GUILayout.BeginHorizontal();
            DrawKeyCap("[1] Pork", ParentAction.GrabPork, 88f);
            DrawKeyCap("[2] Egg", ParentAction.DropEggIntoPalm, 88f);
            DrawKeyCap("[3] Onions", ParentAction.GrabOnions, 88f);
            DrawKeyCap("[4] Noodles", ParentAction.GrabNoodles, 88f);
            DrawKeyCap("[5] Ladle", ParentAction.GrabLadle, 88f);
            GUILayout.FlexibleSpace();
            DrawKeyCap("[Bksp] Skip", ParentAction.ResetCurrentStep, 110f);
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            // Row 2: Q, W, E, B
            GUILayout.BeginHorizontal();
            GUILayout.Space(18f);
            DrawKeyCap("[Q] Curtain", ParentAction.ToggleCurtainRest, 100f);
            DrawKeyCap($"[W] Pull ({_wHoldAccumulated:F1}s)", ParentAction.BandPullStretch, 120f);
            DrawKeyCap("[E] Elbow Cue", ParentAction.ElbowReachCue, 110f);
            DrawKeyCap($"[B] Boil ({_bHoldAccumulated:F1}s)", ParentAction.BoilHold, 120f);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            // Row 3: A, S, D, F, Enter
            GUILayout.BeginHorizontal();
            GUILayout.Space(36f);
            DrawKeyCap("[A] Drop", ParentAction.ReleaseIntoBowl, 95f);
            DrawKeyCap("[S] Serve", ParentAction.ServeCustomer, 95f);
            DrawKeyCap("[D] Pour", ParentAction.PourBroth, 95f);
            DrawKeyCap($"[F] Nori ({_fHoldAccumulated:F1}s)", ParentAction.HandFlattenHold, 125f);
            GUILayout.FlexibleSpace();
            DrawKeyCap("[Enter] Drop", ParentAction.ReleaseIntoBowl, 110f);
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            // Row 4: Spacebar, V
            GUILayout.BeginHorizontal();
            GUILayout.Space(90f);
            DrawKeyCap("[SPACEBAR] Bike Pedal (+10m)", ParentAction.BikePedalStretch, 260f);
            GUILayout.Space(16f);
            DrawKeyCap("[V] Switch POV", ParentAction.TogglePov, 130f);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            GUILayout.Label($"Recent Hotkey Events (Total Taps: {(_router != null ? _router.TotalTapCount : 0)}):", _headerStyle);

            if (_recentLogs.Count == 0)
            {
                GUILayout.Label("  (Press any key in the Parent Cluster to test...)", _logStyle);
            }
            else
            {
                foreach (string entry in _recentLogs)
                {
                    GUILayout.Label("  • " + entry, _logStyle);
                }
            }

            GUILayout.EndArea();
        }

        private void DrawKeyCap(string label, ParentAction action, float width)
        {
            GUIStyle style = IsActionHighlighted(action) ? _keyActiveStyle : _keyIdleStyle;
            GUILayout.Box(label, style, GUILayout.Width(width), GUILayout.Height(34f));
        }
    }
}
