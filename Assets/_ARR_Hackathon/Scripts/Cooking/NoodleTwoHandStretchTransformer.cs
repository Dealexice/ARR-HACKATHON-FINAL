using Oculus.Interaction;
using UnityEngine;

namespace YataiTogether
{
    /// <summary>
    /// Step 5: Meta Interaction SDK transformer for the bimanual noodle stretch (AGENTS.md §4 #3).
    /// Injected as BOTH the one- and two-grab transformer of the spawned 'noodle' Grabbable.
    /// - One hand: the noodle follows that hand rigidly (no scaling).
    /// - Two hands: the noodle sits between both grab points, its long axis points hand-to-hand,
    ///   and its stretch ratio follows the hand distance relative to where both hands grabbed
    ///   (pull apart = longer/thinner, bring together = shorter), clamped to [1, maxStretch].
    /// It never writes scale itself: StretchableNoodle is the single scale writer and reads LiveStretchRatio.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NoodleTwoHandStretchTransformer : MonoBehaviour, ITransformer
    {
        [SerializeField] private float minStretch = 1.0f;
        [SerializeField] private float maxStretch = 2.5f;

        private IGrabbable _grabbable;
        private Grabbable _grabbableComponent;
        private int _activeGrabCount;
        private int _longAxis;

        // One-grab rigid offsets (world units, unaffected by scale changes)
        private Vector3 _oneGrabPosOffset;
        private Quaternion _oneGrabRotOffset;

        // Two-grab baseline
        private float _beginHandDistance;
        private float _beginRatio;
        private Vector3 _beginHandDir;
        private Vector3 _beginCenterOffset;
        private Quaternion _beginRotation;

        // Hand-anchor parent restored once both hands let go (noodle stays "in hand" for Key 4 / B)
        private Transform _restParent;

        public bool IsTwoHandGrabbed => _activeGrabCount >= 2;
        public float LiveStretchRatio { get; private set; } = 1f;
        public float MaxStretch => maxStretch;

        /// <summary>Lets StretchableNoodle seed the ratio (e.g. after a W hold) before the hands re-grab.</summary>
        public void SetCurrentRatio(float ratio)
        {
            if (!IsTwoHandGrabbed) LiveStretchRatio = Mathf.Clamp(ratio, minStretch, maxStretch);
        }

        public void Initialize(IGrabbable grabbable)
        {
            _grabbable = grabbable;
            _grabbableComponent = grabbable as Grabbable;
            _longAxis = ResolveLongAxis(gameObject);
        }

        private static Vector3 LocalAxisVector(int axis)
        {
            return axis == 1 ? Vector3.up : axis == 2 ? Vector3.forward : Vector3.right;
        }

        public void BeginTransform()
        {
            Transform target = _grabbable.Transform;
            _activeGrabCount = _grabbable.GrabPoints.Count;

            // Detach from the right-hand anchor while real hands hold it, so the anchor can't fight the grab.
            if (target.parent != null && _restParent == null)
            {
                _restParent = target.parent;
                target.SetParent(null, true);
            }

            if (_activeGrabCount >= 2)
            {
                Vector3 a = _grabbable.GrabPoints[0].position;
                Vector3 b = _grabbable.GrabPoints[1].position;
                Vector3 mid = (a + b) * 0.5f;
                _beginHandDistance = Mathf.Max(0.01f, Vector3.Distance(a, b));
                _beginHandDir = (b - a).sqrMagnitude > 1e-6f ? (b - a).normalized : target.rotation * LocalAxisVector(_longAxis);
                _beginRatio = LiveStretchRatio;

                // Lay the noodle's long axis along the hand-to-hand line, centred between both hands.
                Vector3 axisWorld = target.rotation * LocalAxisVector(_longAxis);
                if (Vector3.Dot(axisWorld, _beginHandDir) < 0f) axisWorld = -axisWorld;
                _beginRotation = Quaternion.FromToRotation(axisWorld, _beginHandDir) * target.rotation;
                _beginCenterOffset = Vector3.zero;
            }
            else if (_activeGrabCount == 1)
            {
                Pose g = _grabbable.GrabPoints[0];
                Quaternion inv = Quaternion.Inverse(g.rotation);
                _oneGrabPosOffset = inv * (target.position - g.position);
                _oneGrabRotOffset = inv * target.rotation;
            }
        }

        public void UpdateTransform()
        {
            Transform target = _grabbable.Transform;

            if (_activeGrabCount >= 2 && _grabbable.GrabPoints.Count >= 2)
            {
                Vector3 a = _grabbable.GrabPoints[0].position;
                Vector3 b = _grabbable.GrabPoints[1].position;
                Vector3 mid = (a + b) * 0.5f;
                Vector3 dir = (b - a).sqrMagnitude > 1e-6f ? (b - a).normalized : _beginHandDir;

                Quaternion delta = Quaternion.FromToRotation(_beginHandDir, dir);
                target.rotation = delta * _beginRotation;
                target.position = mid + delta * _beginCenterOffset;

                float distance = Vector3.Distance(a, b);
                LiveStretchRatio = Mathf.Clamp(_beginRatio * distance / _beginHandDistance, minStretch, maxStretch);
            }
            else if (_activeGrabCount == 1 && _grabbable.GrabPoints.Count >= 1)
            {
                Pose g = _grabbable.GrabPoints[0];
                target.rotation = g.rotation * _oneGrabRotOffset;
                target.position = g.position + g.rotation * _oneGrabPosOffset;
            }
        }

        public void EndTransform()
        {
            _activeGrabCount = 0;
        }

        private void LateUpdate()
        {
            if (_restParent == null) return;

            // Boiler snap / Backspace disabled the grab or re-parented the noodle: hand ownership is gone.
            if (_grabbableComponent == null || !_grabbableComponent.isActiveAndEnabled || transform.parent != null)
            {
                _restParent = null;
                return;
            }

            if (_grabbableComponent.SelectingPointsCount == 0)
            {
                transform.SetParent(_restParent, true);
                _restParent = null;
            }
        }

        /// <summary>Local axis (0=X, 1=Y, 2=Z) along which the mesh is longest: the noodle's stretch axis.</summary>
        public static int ResolveLongAxis(GameObject go)
        {
            MeshFilter mf = go != null ? go.GetComponentInChildren<MeshFilter>() : null;
            if (mf == null || mf.sharedMesh == null) return 0;
            Vector3 s = mf.sharedMesh.bounds.size;
            if (s.x >= s.y && s.x >= s.z) return 0;
            return s.y >= s.z ? 1 : 2;
        }
    }
}
