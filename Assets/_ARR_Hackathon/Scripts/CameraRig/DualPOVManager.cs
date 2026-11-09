using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Unity.XR.CoreUtils;

namespace YataiTogether
{
    /// <summary>
    /// Dual POV Manager with VR height offset compensation.
    /// Handles seated eye-level vs floor-level tracking offset when snapping to target anchors.
    /// Enforces AGENTS.md §1:
    /// - Two fixed seated viewpoints only: POV 1 (BikeSeatAnchor) and POV 2 (FoodStallCounterAnchor).
    /// - Sets xrOrigin.RequestedTrackingOriginMode = Device (eye level) or offsets rig Y by subtracting camera local Y height.
    /// </summary>
    [DisallowMultipleComponent]
    public class DualPOVManager : MonoBehaviour
    {
        public static DualPOVManager Instance { get; private set; }

        [Header("VR Rig & Tracking References")]
        [SerializeField] private Transform xrOriginRoot;
        [SerializeField] private Camera xrCamera;
        [SerializeField] private XROrigin xrOrigin;

        [Header("Seated POV Anchors")]
        [SerializeField] private Transform pov1BikeAnchor;
        [SerializeField] private Transform pov2StallAnchor;

        [Header("Comfort Transition Settings")]
        [SerializeField, Min(0.5f)] private float fadeDuration = 0.55f;
        [SerializeField] private bool autoCompensateHeight = true;

        [Header("Events")]
        public UnityEvent<bool> onPovChanged = new UnityEvent<bool>();

        private bool _isAtStallPOV = false;
        private bool _isTransitioning = false;

        public bool IsAtStallPOV => _isAtStallPOV;
        public bool IsTransitioning => _isTransitioning;
        public Transform XrOriginRoot => xrOriginRoot;
        public Camera XrCamera => xrCamera;
        public Transform BikeAnchor => pov1BikeAnchor;
        public Transform StallAnchor => pov2StallAnchor;
        public Transform CurrentAnchor => _isAtStallPOV ? pov2StallAnchor : pov1BikeAnchor;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            EnsureReferences();
            ApplyEyeLevelTrackingMode();
        }

        private void Start()
        {
            // Initial seated anchor snap
            SnapToAnchor(CurrentAnchor);
            StartCoroutine(EnsureHandVisualsRoutine());
        }

        private IEnumerator EnsureHandVisualsRoutine()
        {
            EnsureVRHandVisualsVisible();
            yield return new WaitForSeconds(0.5f);
            EnsureVRHandVisualsVisible();
        }

        /// <summary>
        /// Enforces visible VR Hand Visuals with shadow casting and opaque URP material.
        /// </summary>
        public void EnsureVRHandVisualsVisible()
        {
            GameObject rig = xrOriginRoot != null ? xrOriginRoot.gameObject : GameObject.Find("[BuildingBlock] Camera Rig");
            if (rig == null) return;

            var renderers = rig.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            foreach (var smr in renderers)
            {
                smr.enabled = true;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                smr.receiveShadows = true;

                if (smr.sharedMaterial == null || smr.sharedMaterial.shader == null || smr.sharedMaterial.shader.name.Contains("Hidden"))
                {
                    Material handMat = new Material(litShader);
                    handMat.name = "VRHandOpaque_Mat";
                    Color handColor = new Color(0.92f, 0.80f, 0.70f, 1.0f);
                    handMat.color = handColor;
                    if (handMat.HasProperty("_BaseColor")) handMat.SetColor("_BaseColor", handColor);
                    smr.sharedMaterial = handMat;
                }
            }
        }

        private void LateUpdate()
        {
            // Maintain seated anchor lock with camera local Y offset
            Transform targetAnchor = CurrentAnchor;
            if (xrOriginRoot != null && targetAnchor != null)
            {
                if (xrOriginRoot.parent != targetAnchor)
                {
                    xrOriginRoot.SetParent(targetAnchor, true);
                }

                float camLocalY = (xrCamera != null && autoCompensateHeight) ? xrCamera.transform.localPosition.y : 0f;
                Vector3 targetPos = targetAnchor.position - new Vector3(0f, camLocalY, 0f);
                xrOriginRoot.SetPositionAndRotation(targetPos, targetAnchor.rotation);
            }
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
            if (xrOrigin == null)
            {
                xrOrigin = FindFirstObjectByType<XROrigin>();
            }

            if (xrOriginRoot == null)
            {
                if (xrOrigin != null)
                {
                    xrOriginRoot = xrOrigin.transform;
                }
                else
                {
                    GameObject rigObj = GameObject.Find("[BuildingBlock] Camera Rig") ?? GameObject.Find("XR Origin (VR)");
                    if (rigObj != null) xrOriginRoot = rigObj.transform;
                }
            }

            if (xrCamera == null)
            {
                if (xrOrigin != null && xrOrigin.Camera != null)
                {
                    xrCamera = xrOrigin.Camera;
                }
                else
                {
                    xrCamera = Camera.main;
                }
            }

            if (pov1BikeAnchor == null)
            {
                GameObject bikeObj = GameObject.Find("POV_1_BikeSeatAnchor");
                if (bikeObj != null) pov1BikeAnchor = bikeObj.transform;
            }

            if (pov2StallAnchor == null)
            {
                GameObject stallObj = GameObject.Find("POV_2_FoodStallCounterAnchor");
                if (stallObj != null) pov2StallAnchor = stallObj.transform;
            }
        }

        /// <summary>
        /// Ensures tracking origin mode is Device (eye level) to prevent double elevation in seated VR.
        /// </summary>
        public void ApplyEyeLevelTrackingMode()
        {
            if (xrOrigin != null)
            {
                xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            }

            if (OVRManager.instance != null)
            {
                OVRManager.instance.trackingOriginType = OVRManager.TrackingOrigin.EyeLevel;
            }
        }

        /// <summary>
        /// Snaps position to targetAnchor with height compensation:
        /// targetPos = targetAnchor.position - new Vector3(0, xrCamera.transform.localPosition.y, 0);
        /// xrOriginRoot.SetPositionAndRotation(targetPos, targetAnchor.rotation);
        /// </summary>
        public void SnapToAnchor(Transform targetAnchor)
        {
            if (targetAnchor == null) return;
            EnsureReferences();
            ApplyEyeLevelTrackingMode();

            if (xrOriginRoot == null) return;

            float camLocalY = (xrCamera != null && autoCompensateHeight) ? xrCamera.transform.localPosition.y : 0f;
            Vector3 targetPos = targetAnchor.position - new Vector3(0f, camLocalY, 0f);

            xrOriginRoot.SetPositionAndRotation(targetPos, targetAnchor.rotation);
            xrOriginRoot.SetParent(targetAnchor, true);

            Debug.Log($"[DualPOVManager] Snapped VR rig to '{targetAnchor.name}' at {targetPos:F3} (Cam local Y: {camLocalY:F3}m)");
        }

        public void TogglePOV()
        {
            SwitchPOV(!_isAtStallPOV);
        }

        public void SwitchPOV(bool toStallPOV)
        {
            _isAtStallPOV = toStallPOV;
            Transform targetAnchor = _isAtStallPOV ? pov2StallAnchor : pov1BikeAnchor;
            SnapToAnchor(targetAnchor);
            onPovChanged?.Invoke(_isAtStallPOV);
        }

        public void SnapToBike() => SwitchPOV(false);
        public void SnapToStall() => SwitchPOV(true);
    }
}
