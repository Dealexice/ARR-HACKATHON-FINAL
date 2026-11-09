using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace YataiTogether
{
    /// <summary>
    /// Step 6: Noodle Boiler Station & Spoon Scoop Controller.
    /// - Trigger Collider on "boiler" pot detects incoming stretched "noodle".
    /// - Snaps noodle to boiling pot, disables physics, and counts down 3.0s boil time (with Key B hold backup).
    /// - Pot flashes cooked state (visual emission/color pulse + audio).
    /// - Spoon scoop trigger collider scoops cooked noodle, snapping it to "spoon_attach_point".
    /// - Hotkey backup: Holding Key B for 3.0s finishes boil and auto-transfers into bowl if spoon isn't used.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class BoilerStation : MonoBehaviour
    {
        public static BoilerStation Instance { get; private set; }

        [Header("Boiler Pot Snap Point & Timing")]
        [SerializeField] private Transform potSnapPoint;
        [SerializeField] private float boilDuration = 3.0f;

        [Header("Cooked Visuals")]
        [SerializeField] private Renderer potRenderer;
        [SerializeField] private Color rawColor = new Color(0.2f, 0.2f, 0.25f);
        [SerializeField] private Color cookedFlashColor = new Color(0.25f, 0.95f, 0.40f);
        [SerializeField] private ParticleSystem boilSteamParticles;

        [Header("State")]
        [SerializeField] private bool hasNoodleInPot = false;
        [SerializeField] private bool isCooked = false;
        [SerializeField] private float currentBoilTimer = 0f;

        [Header("Events")]
        public UnityEvent onBoilStarted = new UnityEvent();
        public UnityEvent onBoilFinished = new UnityEvent();
        public UnityEvent onNoodleScooped = new UnityEvent();

        private GameObject _containedNoodle;
        private Material _potMat;

        public bool IsCooked => isCooked;
        public bool HasNoodleInPot => hasNoodleInPot;
        // The director's B hold accumulator is the single boil timer (release pauses, completion latches once).
        public float BoilProgress => isCooked ? 1f
            : (YataiSessionDirector.Instance != null ? YataiSessionDirector.Instance.BHoldProgress
                : (boilDuration > 0f ? Mathf.Clamp01(currentBoilTimer / boilDuration) : 1f));
        public GameObject ContainedNoodle => _containedNoodle;

        public void Configure(Transform snap, Renderer rend)
        {
            potSnapPoint = snap;
            potRenderer = rend;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            if (potSnapPoint == null)
            {
                potSnapPoint = transform;
            }

            // Ensure child potSnapPoint has BoilerPotSnapReceiver
            if (potSnapPoint != null && potSnapPoint.GetComponent<BoilerPotSnapReceiver>() == null)
            {
                potSnapPoint.gameObject.AddComponent<BoilerPotSnapReceiver>();
            }

            if (potRenderer != null)
            {
                _potMat = potRenderer.material;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (_potMat != null)
            {
                Destroy(_potMat);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;

            // 1. Detect incoming noodle dropping into boiler
            if (!hasNoodleInPot && !isCooked)
            {
                if (IsNoodleObject(other.gameObject))
                {
                    AcceptNoodleIntoBoiler(other.gameObject);
                    return;
                }
            }

            // 2. Detect spoon scoop when noodle is cooked
            if (hasNoodleInPot && isCooked && _containedNoodle != null)
            {
                Transform spoonAttach = FindSpoonAttachPoint(other);
                if (spoonAttach != null)
                {
                    TransferNoodleToSpoon(spoonAttach);
                }
            }
        }

        private bool IsNoodleObject(GameObject go)
        {
            if (IngredientSpawnerBowl.IsStationaryDisplayMesh(go)) return false;
            string n = go.name.ToLower();
            return n.Contains("noodle") || go.CompareTag("Untagged") && n.Contains("spawned");
        }

        public void AcceptNoodleIntoBoiler(GameObject noodleObj)
        {
            _containedNoodle = noodleObj;
            hasNoodleInPot = true;
            isCooked = false;
            currentBoilTimer = 0f;

            // Snap noodle to pot and disable physics
            Rigidbody rb = _containedNoodle.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // ponytail: Zero velocity only while dynamic before setting kinematic
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            _containedNoodle.transform.SetParent(potSnapPoint != null ? potSnapPoint : transform, true);
            _containedNoodle.transform.position = potSnapPoint != null ? potSnapPoint.position : transform.position;

            if (BoilerPotSnapReceiver.Instance != null && !BoilerPotSnapReceiver.Instance.HasNoodleInPot)
            {
                BoilerPotSnapReceiver.Instance.SnapAndBoilNoodle(noodleObj);
            }

            if (boilSteamParticles != null && !boilSteamParticles.isPlaying)
            {
                boilSteamParticles.Play();
            }

            onBoilStarted?.Invoke();
            Debug.Log("[BoilerStation] Noodle placed in boiler. Hold [B] 3.0s to boil.");
        }

        /// <summary>Drops the pot's reference to its noodle without destroying it (it left the pot another way).</summary>
        public void ForgetContainedNoodle()
        {
            _containedNoodle = null;
            hasNoodleInPot = false;
        }

        public void FinishCooking(bool autoTransferToBowl = false)
        {
            if (isCooked) return;

            isCooked = true;
            currentBoilTimer = boilDuration;

            StartCoroutine(FlashCookedEffectRoutine());
            onBoilFinished?.Invoke();
            Debug.Log("[BoilerStation] Noodles fully boiled! Pot flashed green.");

            if (autoTransferToBowl)
            {
                TransferCookedNoodlesToBowl();
            }
        }

        /// <summary>
        /// Empties the pot without adding anything to the bowl (Backspace zero-penalty completion).
        /// </summary>
        public void ResetPot()
        {
            if (_containedNoodle != null)
            {
                Destroy(_containedNoodle);
                _containedNoodle = null;
            }

            hasNoodleInPot = false;
            isCooked = false;
            currentBoilTimer = 0f;

            if (boilSteamParticles != null && boilSteamParticles.isPlaying)
            {
                boilSteamParticles.Stop();
            }

            SetPotColor(rawColor);
        }

        public void TransferCookedNoodlesToBowl()
        {
            if (_containedNoodle != null)
            {
                Destroy(_containedNoodle);
                _containedNoodle = null;
            }
            hasNoodleInPot = false;

            if (RamenAssemblyReceiver.Instance != null)
            {
                RamenAssemblyReceiver.Instance.AddNoodles();
            }
            else if (YataiSessionDirector.Instance != null && YataiSessionDirector.Instance.CurrentStep <= CookingStep.Noodles_Transfer)
            {
                YataiSessionDirector.Instance.SetStep(CookingStep.Pork_Grab);
            }
        }

        private IEnumerator FlashCookedEffectRoutine()
        {
            if (_potMat == null) yield break;

            for (int i = 0; i < 3; i++)
            {
                SetPotColor(cookedFlashColor);
                yield return new WaitForSeconds(0.12f);
                SetPotColor(rawColor);
                yield return new WaitForSeconds(0.12f);
            }
            SetPotColor(cookedFlashColor);
        }

        private void SetPotColor(Color c)
        {
            if (_potMat == null) return;
            if (_potMat.HasProperty("_BaseColor")) _potMat.SetColor("_BaseColor", c);
            if (_potMat.HasProperty("_Color")) _potMat.SetColor("_Color", c);
            if (_potMat.HasProperty("_EmissionColor")) _potMat.SetColor("_EmissionColor", c * 0.5f);
        }

        private Transform FindSpoonAttachPoint(Collider col)
        {
            GameObject go = col.gameObject;
            if (go.name.ToLower().Contains("spoon") || go.transform.root.name.ToLower().Contains("spoon"))
            {
                Transform attach = go.transform.Find("spoon_attach_point");
                if (attach == null && go.transform.parent != null)
                {
                    attach = go.transform.parent.Find("spoon_attach_point");
                }
                if (attach == null)
                {
                    GameObject newAttach = new GameObject("spoon_attach_point");
                    newAttach.transform.SetParent(go.transform, false);
                    newAttach.transform.localPosition = new Vector3(0f, 0.05f, 0.15f);
                    attach = newAttach.transform;
                }
                return attach;
            }
            return null;
        }

        public void TransferNoodleToSpoon(Transform spoonAttachPoint)
        {
            if (_containedNoodle == null || spoonAttachPoint == null) return;

            _containedNoodle.transform.SetParent(spoonAttachPoint, true);
            _containedNoodle.transform.localPosition = Vector3.zero;
            _containedNoodle.transform.localRotation = Quaternion.identity;

            hasNoodleInPot = false;
            _containedNoodle = null;
            if (BoilerPotSnapReceiver.Instance != null) BoilerPotSnapReceiver.Instance.ForgetContainedNoodle();

            onNoodleScooped?.Invoke();
            Debug.Log("[BoilerStation] Cooked noodle scooped onto spoon tip. Ready to transfer to lamian_bowl.");
        }
    }
}
