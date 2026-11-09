using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace YataiTogether
{
    /// <summary>
    /// Step 1: Dual-POV Camera Rig & Comfort Transition (Key V fade).
    /// Enforces AGENTS.md §1 Law:
    /// - Two fixed seated viewpoints only: POV 1 (BikeSeatAnchor) and POV 2 (FoodStallCounterAnchor).
    /// - NEVER translate, accelerate, tilt, or rotate the VR camera rig while the child can see.
    /// - Switches only at peak blackness behind a >= 0.5s comfort fade.
    /// - Parents the active [BuildingBlock] Camera Rig directly to the active POV anchor so both
    ///   anchors carry full VR camera + hand tracking functionality and stay 100% locked to their
    ///   anchor without FirstPersonLocomotor / CharacterController drift.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PovRigController : MonoBehaviour
    {
        public static PovRigController Instance { get; private set; }

        [Header("VR Camera Rig References")]
        [SerializeField] private Transform cameraRigRoot;
        [SerializeField] private Transform centerEyeAnchor;
        [SerializeField] private Camera xrCamera;
        [SerializeField] private Unity.XR.CoreUtils.XROrigin xrOrigin;
        [SerializeField] private bool handleTrackingOriginHeight = true;

        [Header("Fixed Seated POV Anchors")]
        [SerializeField] private Transform bikeSeatAnchor;
        [SerializeField] private Transform foodStallCounterAnchor;

        [Header("Comfort Transition Settings")]
        [Tooltip("Total fade duration in seconds (minimum 0.5s per comfort law).")]
        [SerializeField, Min(0.5f)] private float fadeDuration = 0.55f;
        [SerializeField] private bool startAtStallPov = false;

        [Header("Events")]
        [SerializeField] private UnityEvent<bool> onPovChanged = new UnityEvent<bool>();

        private bool _isAtStallPov;
        private bool _isTransitioning;
        private float _fadeAlpha;
        private Coroutine _transitionCoroutine;

        // Rig pose (local to the active anchor) captured at the last snap; held fixed between snaps.
        private Vector3 _lockedLocalPosition;
        private bool _needsHeightCalibration;

        // World-space fade quad parented to CenterEyeAnchor for HMD comfort fade
        private Renderer _hmdFadeRenderer;
        private Material _hmdFadeMaterial;
        private Texture2D _guiBlackTex;

        public bool IsAtStallPov => _isAtStallPov;
        public bool IsTransitioning => _isTransitioning;
        public float FadeAlpha => _fadeAlpha;
        public Transform CameraRigRoot => cameraRigRoot;
        public Transform CenterEyeAnchor => centerEyeAnchor;
        public Transform BikeSeatAnchor => bikeSeatAnchor;
        public Transform FoodStallCounterAnchor => foodStallCounterAnchor;
        public Transform CurrentAnchor => _isAtStallPov ? foodStallCounterAnchor : bikeSeatAnchor;
        public string CurrentPovLabel => _isAtStallPov
            ? "POV 2: FoodStallCounterAnchor (Stall Chef)"
            : "POV 1: BikeSeatAnchor (Bicycle Saddle)";

        public event System.Action<bool> PovChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            EnsureReferences();
            DisableRigLocomotorDrift();
            CreateHmdFadeOverlay();
            SnapToPovImmediate(startAtStallPov);
        }

        private void OnEnable()
        {
            if (ParentHotkeyRouter.Instance != null)
            {
                ParentHotkeyRouter.Instance.ActionTapped += HandleActionTapped;
            }
        }

        private void Start()
        {
            if (ParentHotkeyRouter.Instance != null)
            {
                ParentHotkeyRouter.Instance.ActionTapped -= HandleActionTapped;
                ParentHotkeyRouter.Instance.ActionTapped += HandleActionTapped;
            }
        }

        public void ApplyTrackingOriginMode()
        {
            if (xrOrigin != null)
            {
                xrOrigin.RequestedTrackingOriginMode = Unity.XR.CoreUtils.XROrigin.TrackingOriginMode.Device;
            }

            if (OVRManager.instance != null)
            {
                OVRManager.instance.trackingOriginType = OVRManager.TrackingOrigin.EyeLevel;
            }
        }

        private float GetCameraLocalY()
        {
            if (!handleTrackingOriginHeight) return 0f;
            if (xrCamera != null) return xrCamera.transform.localPosition.y;
            if (centerEyeAnchor != null) return centerEyeAnchor.localPosition.y;
            return 0f;
        }

        private void LateUpdate()
        {
            Transform targetAnchor = CurrentAnchor;
            if (cameraRigRoot == null || targetAnchor == null) return;

            // One-time seated height calibration: at Awake the headset often hasn't reported a head
            // height yet (camera local Y == 0). Apply the offset once, on the first valid tracking frame.
            if (_needsHeightCalibration && handleTrackingOriginHeight)
            {
                float camY = GetCameraLocalY();
                if (Mathf.Abs(camY) > 0.05f)
                {
                    _needsHeightCalibration = false;
                    LockRigToAnchor(targetAnchor, camY);
                    Debug.Log($"[PovRigController] Seated height calibrated once (camera local Y {camY:F3}m).");
                }
            }

            // Drift guard only: the rig is locked to the pose captured at the last snap. The live head
            // height is NOT re-subtracted every frame, so real head motion (incl. vertical) is preserved
            // and the rig never translates while the child can see (AGENTS.md §1).
            if (cameraRigRoot.parent != targetAnchor)
            {
                cameraRigRoot.SetParent(targetAnchor, false);
                cameraRigRoot.localPosition = _lockedLocalPosition;
                cameraRigRoot.localRotation = Quaternion.identity;
            }
            else if ((cameraRigRoot.localPosition - _lockedLocalPosition).sqrMagnitude > 1e-6f ||
                     cameraRigRoot.localRotation != Quaternion.identity)
            {
                cameraRigRoot.localPosition = _lockedLocalPosition;
                cameraRigRoot.localRotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// Parents the rig to the anchor and captures the locked local pose (anchor point minus the
        /// head height sampled at this instant). Only called at snap time (behind the fade) or once
        /// for the initial seated height calibration.
        /// </summary>
        private void LockRigToAnchor(Transform targetAnchor, float camLocalY)
        {
            Vector3 targetPos = targetAnchor.position - new Vector3(0f, camLocalY, 0f);
            cameraRigRoot.SetParent(targetAnchor, true);
            cameraRigRoot.SetPositionAndRotation(targetPos, targetAnchor.rotation);
            cameraRigRoot.localScale = Vector3.one;
            _lockedLocalPosition = cameraRigRoot.localPosition;
        }

        /// <summary>
        /// Re-samples the seated head height and re-locks the rig behind a comfort fade.
        /// </summary>
        public void RecalibrateSeatedHeight()
        {
            TransitionToPov(_isAtStallPov);
        }

        private void OnDisable()
        {
            if (ParentHotkeyRouter.Instance != null)
            {
                ParentHotkeyRouter.Instance.ActionTapped -= HandleActionTapped;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (_hmdFadeMaterial != null)
            {
                Destroy(_hmdFadeMaterial);
            }

            if (_guiBlackTex != null)
            {
                Destroy(_guiBlackTex);
            }
        }

        public void ConfigureReferences(
            Transform rigRoot,
            Transform eyeAnchor,
            Transform bikeAnchor,
            Transform stallAnchor)
        {
            cameraRigRoot = rigRoot;
            centerEyeAnchor = eyeAnchor;
            bikeSeatAnchor = bikeAnchor;
            foodStallCounterAnchor = stallAnchor;
        }

        private void HandleActionTapped(ParentAction action)
        {
            if (action == ParentAction.TogglePov)
            {
                TogglePov();
            }
        }

        /// <summary>
        /// Toggles between POV 1 (BikeSeatAnchor) and POV 2 (FoodStallCounterAnchor)
        /// behind a >= 0.5s comfort fade to black.
        /// </summary>
        public void TogglePov()
        {
            TransitionToPov(!_isAtStallPov);
        }

        /// <summary>
        /// Transitions to the requested POV behind a >= 0.5s comfort fade.
        /// </summary>
        public void TransitionToPov(bool toStallPov)
        {
            if (_isTransitioning)
            {
                return;
            }

            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
            }

            _transitionCoroutine = StartCoroutine(ComfortFadeAndSnapRoutine(toStallPov));
        }

        /// <summary>
        /// Immediately snaps and parents the VR camera rig to the target anchor without fading.
        /// Used during initialization or when already at peak blackness.
        /// </summary>
        public void SnapToPovImmediate(bool toStallPov)
        {
            EnsureReferences();
            DisableRigLocomotorDrift();
            ApplyTrackingOriginMode();
            _isAtStallPov = toStallPov;

            Transform targetAnchor = toStallPov ? foodStallCounterAnchor : bikeSeatAnchor;
            if (cameraRigRoot != null && targetAnchor != null)
            {
                float camLocalY = GetCameraLocalY();
                LockRigToAnchor(targetAnchor, camLocalY);
                // If tracking hasn't reported a head height yet, calibrate once when it does.
                _needsHeightCalibration = handleTrackingOriginHeight && Mathf.Abs(camLocalY) <= 0.05f;
            }

            Debug.Log($"[PovRigController] Snapped VR Rig to {CurrentPovLabel} at {(targetAnchor != null ? targetAnchor.position.ToString("F3") : "null")} (Local Y offset: {GetCameraLocalY():F3}m)");
            PovChanged?.Invoke(_isAtStallPov);
            onPovChanged?.Invoke(_isAtStallPov);
        }

        private IEnumerator ComfortFadeAndSnapRoutine(bool toStallPov)
        {
            _isTransitioning = true;
            float totalDuration = Mathf.Max(0.5f, fadeDuration);
            float halfDuration = totalDuration * 0.5f;

            // Phase 1: Fade out to full black (camera remains 100% stationary)
            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetFadeAlpha(Mathf.Clamp01(elapsed / halfDuration));
                yield return null;
            }

            SetFadeAlpha(1f);

            // Peak blackness: reparent/teleport rig instantaneously while child cannot see
            SnapToPovImmediate(toStallPov);
            yield return null;

            // Phase 2: Fade back in from black (camera is already stationary at new anchor)
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetFadeAlpha(1f - Mathf.Clamp01(elapsed / halfDuration));
                yield return null;
            }

            SetFadeAlpha(0f);
            _isTransitioning = false;
            _transitionCoroutine = null;
        }

        private void SetFadeAlpha(float alpha)
        {
            _fadeAlpha = Mathf.Clamp01(alpha);

            if (_hmdFadeRenderer != null && _hmdFadeMaterial != null)
            {
                bool active = _fadeAlpha > 0.001f;
                if (_hmdFadeRenderer.enabled != active)
                {
                    _hmdFadeRenderer.enabled = active;
                }

                Color c = new Color(0f, 0f, 0f, _fadeAlpha);
                if (_hmdFadeMaterial.HasProperty("_BaseColor"))
                {
                    _hmdFadeMaterial.SetColor("_BaseColor", c);
                }
                if (_hmdFadeMaterial.HasProperty("_Color"))
                {
                    _hmdFadeMaterial.SetColor("_Color", c);
                }
            }
        }

        private void EnsureReferences()
        {
            if (cameraRigRoot == null)
            {
                GameObject rigObj = GameObject.Find("[BuildingBlock] Camera Rig");
                if (rigObj != null)
                {
                    cameraRigRoot = rigObj.transform;
                }
            }

            if (centerEyeAnchor == null && cameraRigRoot != null)
            {
                Transform foundEye = cameraRigRoot.Find("TrackingSpace/CenterEyeAnchor");
                if (foundEye != null)
                {
                    centerEyeAnchor = foundEye;
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

            if (xrOrigin == null)
            {
                xrOrigin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            }

            if (xrCamera == null)
            {
                if (xrOrigin != null && xrOrigin.Camera != null)
                {
                    xrCamera = xrOrigin.Camera;
                }
                else if (centerEyeAnchor != null)
                {
                    xrCamera = centerEyeAnchor.GetComponent<Camera>();
                }
                if (xrCamera == null)
                {
                    xrCamera = Camera.main;
                }
            }

            if (foodStallCounterAnchor == null)
            {
                GameObject stallObj = GameObject.Find("POV_2_FoodStallCounterAnchor");
                if (stallObj != null)
                {
                    foodStallCounterAnchor = stallObj.transform;
                }
            }
        }

        /// <summary>
        /// Disables Meta Interaction SDK's FirstPersonLocomotor/Locomotor child on the Camera Rig
        /// so its capsule collider never pushes or drifts the seated VR camera rig away from the anchor.
        /// </summary>
        private void DisableRigLocomotorDrift()
        {
            if (cameraRigRoot == null)
            {
                return;
            }

            Transform locomotor = cameraRigRoot.Find("[BuildingBlock] OVRComprehensiveInteractionRig/Locomotor");
            if (locomotor != null && locomotor.gameObject.activeSelf)
            {
                locomotor.gameObject.SetActive(false);
            }
        }

        private void CreateHmdFadeOverlay()
        {
            if (centerEyeAnchor == null || _hmdFadeRenderer != null)
            {
                return;
            }

            Transform existing = centerEyeAnchor.Find("ARR_ComfortFadeQuad");
            GameObject quadObj;
            if (existing != null)
            {
                quadObj = existing.gameObject;
            }
            else
            {
                quadObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quadObj.name = "ARR_ComfortFadeQuad";
                Collider col = quadObj.GetComponent<Collider>();
                if (col != null)
                {
                    Destroy(col);
                }

                quadObj.transform.SetParent(centerEyeAnchor, false);
                quadObj.transform.localPosition = new Vector3(0f, 0f, 0.35f);
                quadObj.transform.localRotation = Quaternion.identity;
                quadObj.transform.localScale = new Vector3(2.5f, 2.5f, 1f);
            }

            _hmdFadeRenderer = quadObj.GetComponent<Renderer>();
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader != null)
            {
                _hmdFadeMaterial = new Material(shader)
                {
                    color = new Color(0f, 0f, 0f, 0f),
                    renderQueue = 5000
                };
                _hmdFadeRenderer.sharedMaterial = _hmdFadeMaterial;
            }

            _hmdFadeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _hmdFadeRenderer.receiveShadows = false;
            _hmdFadeRenderer.enabled = false;
        }

        private void OnGUI()
        {
            if (_fadeAlpha <= 0.001f)
            {
                return;
            }

            if (_guiBlackTex == null)
            {
                _guiBlackTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _guiBlackTex.SetPixel(0, 0, Color.white);
                _guiBlackTex.Apply();
            }

            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, _fadeAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _guiBlackTex);
            GUI.color = prev;
        }
    }
}
