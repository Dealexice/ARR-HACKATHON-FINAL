using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace YataiTogether
{
    /// <summary>
    /// Step 10: Golden Nori Hand-Flatten Press Controller.
    /// Manages the parent-assisted palm flattening stretch on the table mat (Zone D).
    /// Enforces Anti-Uncanny Valley & Mutator Biofeedback Law (AGENTS.md §1 & Concept §4):
    /// - Never render a human parent avatar; renders Monkey / Po's warm glowing paw.
    /// - Key F hold for 5.0s charges the Golden Nori press.
    /// - Early release pauses; hold resumes smoothly.
    /// - Upon 5.0s completion: mini sparkler / chime fires, crispy Golden Nori snaps onto
    ///   the ramen bowl rim, and the director transitions to Step 11: Broth Ladle.
    /// </summary>
    [DisallowMultipleComponent]
    public class GoldenNoriController : MonoBehaviour
    {
        public static GoldenNoriController Instance { get; private set; }

        [Header("Timing")]
        [SerializeField] private float holdDuration = 5.0f;
        [SerializeField] private float currentHoldTimer = 0f;
        [SerializeField] private bool isNoriPressed = false;

        [Header("Visual Feedback (Warm Paw & Sparkles)")]
        [SerializeField] private GameObject warmPawVisual;
        [SerializeField] private ParticleSystem pressSparkles;
        [SerializeField] private ParticleSystem completeSparkler;
        [SerializeField] private GameObject noriMatProp;

        [Header("Events")]
        public UnityEvent<float> onHoldProgress = new UnityEvent<float>();
        public UnityEvent onNoriCompleted = new UnityEvent();

        public bool IsNoriPressed => isNoriPressed;
        public float HoldProgress => holdDuration > 0f ? Mathf.Clamp01(currentHoldTimer / holdDuration) : 1f;
        public float CurrentHoldTimer => currentHoldTimer;

        public void Configure(GameObject paw, ParticleSystem sparkles, ParticleSystem sparkler, GameObject nori)
        {
            warmPawVisual = paw;
            pressSparkles = sparkles;
            completeSparkler = sparkler;
            noriMatProp = nori;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            EnsureWarmPawAndNori();
        }

        private void OnEnable()
        {
            if (ParentHotkeyRouter.Instance != null)
            {
                ParentHotkeyRouter.Instance.HoldTicked += HandleHoldTicked;
            }
        }

        private void Start()
        {
            if (ParentHotkeyRouter.Instance != null)
            {
                ParentHotkeyRouter.Instance.HoldTicked -= HandleHoldTicked;
                ParentHotkeyRouter.Instance.HoldTicked += HandleHoldTicked;
            }
        }

        private void OnDisable()
        {
            if (ParentHotkeyRouter.Instance != null)
            {
                ParentHotkeyRouter.Instance.HoldTicked -= HandleHoldTicked;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void EnsureWarmPawAndNori()
        {
            if (warmPawVisual == null)
            {
                Transform foundPaw = transform.Find("WarmPawVisual");
                if (foundPaw != null)
                {
                    warmPawVisual = foundPaw.gameObject;
                }
                else
                {
                    warmPawVisual = CreateWarmPawVisual();
                }
            }

            if (warmPawVisual != null)
            {
                warmPawVisual.SetActive(false);
            }
        }

        private GameObject CreateWarmPawVisual()
        {
            GameObject pawGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pawGo.name = "WarmPawVisual";
            pawGo.transform.SetParent(transform, false);
            pawGo.transform.position = new Vector3(5.40f, 1.10f, 0.45f); // Near Zone D table mat
            pawGo.transform.localScale = new Vector3(0.18f, 0.04f, 0.18f);

            Collider col = pawGo.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer rend = pawGo.GetComponent<Renderer>();
            if (rend != null)
            {
                Material m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                m.name = "WarmPaw_Mat";
                m.color = new Color(1f, 0.78f, 0.28f, 0.85f);
                if (m.HasProperty("_EmissionColor"))
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", new Color(0.85f, 0.55f, 0.10f) * 1.5f);
                }
                rend.material = m;
            }

            return pawGo;
        }

        private void HandleHoldTicked(ParentAction action, float dt)
        {
            if (action == ParentAction.HandFlattenHold && !isNoriPressed)
            {
                if (YataiSessionDirector.Instance != null && YataiSessionDirector.Instance.CurrentStep == CookingStep.Nori_Hold &&
                    !YataiSessionDirector.Instance.IsSimmerHoldActive)
                {
                    currentHoldTimer += dt;
                    float progress = HoldProgress;
                    onHoldProgress?.Invoke(progress);

                    if (warmPawVisual != null && !warmPawVisual.activeSelf)
                    {
                        warmPawVisual.SetActive(true);
                    }

                    if (pressSparkles != null && !pressSparkles.isPlaying)
                    {
                        pressSparkles.Play();
                    }

                    if (currentHoldTimer >= holdDuration)
                    {
                        CompleteNoriPress();
                    }
                }
            }
        }

        public void CompleteNoriPress()
        {
            if (isNoriPressed) return;

            isNoriPressed = true;
            currentHoldTimer = holdDuration;

            if (warmPawVisual != null)
            {
                StartCoroutine(FadeOutPawRoutine());
            }

            if (pressSparkles != null && pressSparkles.isPlaying)
            {
                pressSparkles.Stop();
            }

            if (completeSparkler != null)
            {
                completeSparkler.Play();
            }

            if (noriMatProp != null)
            {
                noriMatProp.SetActive(false);
            }

            onNoriCompleted?.Invoke();
            Debug.Log("[GoldenNoriController] Golden Nori pressed flat! Snapping to ramen bowl rim.");

            if (RamenAssemblyReceiver.Instance != null)
            {
                RamenAssemblyReceiver.Instance.AddNori();
            }
            else if (YataiSessionDirector.Instance != null && YataiSessionDirector.Instance.CurrentStep == CookingStep.Nori_Hold)
            {
                YataiSessionDirector.Instance.SetStep(CookingStep.Broth_Ladle);
            }
        }

        private IEnumerator FadeOutPawRoutine()
        {
            yield return new WaitForSeconds(0.6f);
            if (warmPawVisual != null)
            {
                warmPawVisual.SetActive(false);
            }
        }

        public void ResetForNextCustomer()
        {
            isNoriPressed = false;
            currentHoldTimer = 0f;
            if (warmPawVisual != null) warmPawVisual.SetActive(false);
            if (noriMatProp != null) noriMatProp.SetActive(true);
        }
    }
}
