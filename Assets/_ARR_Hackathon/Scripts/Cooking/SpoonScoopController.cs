using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Grab;

namespace YataiTogether
{
    /// <summary>
    /// Step 6: Spoon Scoop Utensil Controller.
    /// Attached to "spoon" in Zone A / counter.
    /// Fully compliant with Meta Interaction SDK:
    /// - Rigidbody (0.2kg, ContinuousDynamic)
    /// - Convex Collider
    /// - Grabbable & HandGrabInteractable
    /// - spoon_scoop_trigger (Trigger Collider) detecting boiler noodles and transferring to spoon_attach_point
    /// - Depositing cooked noodles into lamian_bowl
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class SpoonScoopController : MonoBehaviour
    {
        [Header("Attach & Scoop Points")]
        [SerializeField] private Transform spoonAttachPoint;
        [SerializeField] private Collider scoopTrigger;

        [Header("State")]
        [SerializeField] private bool hasNoodleOnSpoon = false;

        public bool HasNoodleOnSpoon => hasNoodleOnSpoon;
        public Transform SpoonAttachPoint => spoonAttachPoint;

        private Rigidbody _rb;
        private Grabbable _grabbable;
        private HandGrabInteractable _handGrabInteractable;

        private void Awake()
        {
            EnsurePhysicsAndGrab();
            EnsureAttachAndTrigger();
        }

        private void EnsurePhysicsAndGrab()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
            _rb.mass = 0.2f;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            Collider col = GetComponent<Collider>();
            if (col == null)
            {
                MeshCollider mc = gameObject.AddComponent<MeshCollider>();
                mc.convex = true;
            }

            // CRITICAL: Ensure dedicated solid BoxCollider on the body/handle of the spoon
            Transform bodyColT = transform.Find("spoon_body_collider");
            if (bodyColT == null)
            {
                GameObject bodyColGo = new GameObject("spoon_body_collider");
                bodyColGo.transform.SetParent(transform, false);
                bodyColGo.transform.localPosition = new Vector3(-0.0009f, -0.0002f, 0.00005f);
                bodyColGo.transform.localRotation = Quaternion.identity;
                bodyColGo.transform.localScale = Vector3.one;
                BoxCollider bc = bodyColGo.AddComponent<BoxCollider>();
                bc.isTrigger = false;
                bc.size = new Vector3(0.0022f, 0.0006f, 0.0006f);
            }

            _grabbable = GetComponent<Grabbable>();
            if (_grabbable == null)
            {
                _grabbable = gameObject.AddComponent<Grabbable>();
            }
            _grabbable.InjectOptionalRigidbody(_rb);
            _grabbable.enabled = true;

            GrabInteractable grabInteractable = GetComponent<GrabInteractable>();
            if (grabInteractable == null)
            {
                grabInteractable = gameObject.AddComponent<GrabInteractable>();
                grabInteractable.InjectRigidbody(_rb);
                grabInteractable.InjectOptionalPointableElement(_grabbable);
                grabInteractable.UseClosestPointAsGrabSource = true;
            }
            grabInteractable.enabled = true;

            _handGrabInteractable = GetComponent<HandGrabInteractable>();
            if (_handGrabInteractable == null)
            {
                _handGrabInteractable = gameObject.AddComponent<HandGrabInteractable>();
                _handGrabInteractable.InjectRigidbody(_rb);
                _handGrabInteractable.InjectOptionalPointableElement(_grabbable);
            }
            _handGrabInteractable.InjectSupportedGrabTypes(GrabTypeFlags.All);
            _handGrabInteractable.enabled = true;
        }

        private void EnsureAttachAndTrigger()
        {
            if (spoonAttachPoint == null)
            {
                Transform found = transform.Find("spoon_attach_point");
                if (found != null)
                {
                    spoonAttachPoint = found;
                }
                else
                {
                    GameObject attachGo = new GameObject("spoon_attach_point");
                    attachGo.transform.SetParent(transform, false);
                    attachGo.transform.localPosition = new Vector3(0.00095f, 0.00009f, 0.00005f);
                    spoonAttachPoint = attachGo.transform;
                }
            }

            if (scoopTrigger == null)
            {
                Transform triggerT = transform.Find("spoon_scoop_trigger");
                if (triggerT != null)
                {
                    scoopTrigger = triggerT.GetComponent<Collider>();
                }
                else
                {
                    GameObject triggerGo = new GameObject("spoon_scoop_trigger");
                    triggerGo.transform.SetParent(transform, false);
                    triggerGo.transform.localPosition = new Vector3(0.00095f, 0.00009f, 0.00005f);
                    SphereCollider sc = triggerGo.AddComponent<SphereCollider>();
                    sc.isTrigger = true;
                    sc.radius = 0.00045f;
                    scoopTrigger = sc;
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;

            // 1. Scoop cooked noodles from boiler
            if (!hasNoodleOnSpoon)
            {
                BoilerStation boiler = other.GetComponent<BoilerStation>() ?? other.GetComponentInParent<BoilerStation>();
                if (boiler != null && boiler.IsCooked && boiler.HasNoodleInPot)
                {
                    boiler.TransferNoodleToSpoon(spoonAttachPoint);
                    hasNoodleOnSpoon = true;
                    Debug.Log("[SpoonScoopController] Scooped cooked noodle from boiler pot!");
                    return;
                }
            }

            // 2. Deposit noodle into lamian_bowl
            if (hasNoodleOnSpoon)
            {
                RamenAssemblyReceiver bowl = other.GetComponent<RamenAssemblyReceiver>() ?? other.GetComponentInParent<RamenAssemblyReceiver>();
                if (bowl != null)
                {
                    DepositNoodleIntoBowl(bowl);
                }
            }
        }

        public void DepositNoodleIntoBowl(RamenAssemblyReceiver bowl)
        {
            if (spoonAttachPoint != null)
            {
                for (int i = spoonAttachPoint.childCount - 1; i >= 0; i--)
                {
                    Destroy(spoonAttachPoint.GetChild(i).gameObject);
                }
            }

            hasNoodleOnSpoon = false;
            bowl.AddNoodles();
            Debug.Log("[SpoonScoopController] Successfully transferred noodles to ramen bowl!");
        }
    }
}
