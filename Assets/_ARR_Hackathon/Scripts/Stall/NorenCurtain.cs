using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace YataiTogether
{
    /// <summary>
    /// Step 4: Noren Stall Curtain & Simmer Hold rest break.
    /// Enforces AGENTS.md §2 & §4 Law:
    /// - Overhead reach (shoulder flexion) by child triggers parent Key Q tap.
    /// - First Q tap rolls up the traditional Japanese Noren curtain with a soft festival drum roll, opening the stall.
    /// - Tapping Q anytime during cooking drops the curtain for a zero-penalty "Simmer Hold" rest break.
    /// - During Simmer Hold, all timers and cooking are frozen without resetting progress.
    /// - Tapping Q again smoothly rolls up the curtain and resumes cooking.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NorenCurtain : MonoBehaviour
    {
        public static NorenCurtain Instance { get; private set; }

        [Header("Curtain Visual Components")]
        [SerializeField] private Transform curtainRoot;
        [SerializeField] private Transform[] curtainFabricPanels;
        [SerializeField] private float rollDuration = 0.55f;

        [Header("Curtain Open/Close Positions")]
        [SerializeField] private Vector3 closedLocalScale = new Vector3(1f, 1f, 1f);
        [SerializeField] private Vector3 openLocalScale = new Vector3(1f, 0.12f, 1f);

        [Header("Events")]
        public UnityEvent<bool> onCurtainToggled = new UnityEvent<bool>();
        public event System.Action<bool> CurtainToggled;

        private bool _isOpen = false;
        private bool _isAnimating = false;
        private Coroutine _rollCoroutine;
        private AudioSource _drumAudioSource;
        private AudioClip _drumClip;

        public bool IsOpen => _isOpen;
        public bool IsAnimating => _isAnimating;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            EnsureCurtainVisuals();
            SetupSoftDrumAudio();
            ApplyCurtainStateImmediate(_isOpen);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void ToggleCurtain()
        {
            SetCurtainOpen(!_isOpen);
        }

        public void SetCurtainOpen(bool open)
        {
            if (_isOpen == open && !_isAnimating) return;

            _isOpen = open;
            PlaySoftDrumCue(_isOpen);

            if (_rollCoroutine != null)
            {
                StopCoroutine(_rollCoroutine);
            }
            _rollCoroutine = StartCoroutine(AnimateCurtainRoutine(_isOpen));

            CurtainToggled?.Invoke(_isOpen);
            onCurtainToggled?.Invoke(_isOpen);
        }

        private void ApplyCurtainStateImmediate(bool open)
        {
            _isOpen = open;
            Vector3 targetScale = open ? openLocalScale : closedLocalScale;
            if (curtainRoot != null)
            {
                curtainRoot.localScale = targetScale;
            }
        }

        private IEnumerator AnimateCurtainRoutine(bool targetOpen)
        {
            _isAnimating = true;
            Vector3 startScale = curtainRoot != null ? curtainRoot.localScale : (targetOpen ? closedLocalScale : openLocalScale);
            Vector3 destScale = targetOpen ? openLocalScale : closedLocalScale;

            float elapsed = 0f;
            float duration = Mathf.Max(0.2f, rollDuration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smooth = t * t * (3f - 2f * t);

                if (curtainRoot != null)
                {
                    curtainRoot.localScale = Vector3.Lerp(startScale, destScale, smooth);
                }
                yield return null;
            }

            if (curtainRoot != null)
            {
                curtainRoot.localScale = destScale;
            }

            _isAnimating = false;
            _rollCoroutine = null;
            Debug.Log($"[NorenCurtain] Noren stall curtain is now {(targetOpen ? "ROLLED UP (STALL OPEN)" : "ROLLED DOWN (SIMMER HOLD REST)")}.");
        }

        private void EnsureCurtainVisuals()
        {
            if (curtainRoot == null)
            {
                GameObject existing = GameObject.Find("ARR_NorenCurtainRoot");
                if (existing != null)
                {
                    curtainRoot = existing.transform;
                }
            }

            if (curtainRoot == null)
            {
                GameObject rootObj = new GameObject("ARR_NorenCurtainRoot");
                curtainRoot = rootObj.transform;

                // Position across the upper front counter of the food cart
                // Counter center is around X=7.0, Y=1.16m. Food cart roof underside is at Y~1.80m here.
                // Seated chef POV2 is at (7.78, 1.58, 0.45) looking across X=7.0 at customer monkey at X=5.72
                // The root pivot IS the curtain rim, flush with the roof underside, so the rim stays touching
                // the ceiling both when closed and when rolled up (open scale shrinks toward the pivot).
                curtainRoot.position = new Vector3(7.15f, 1.80f, 0.50f);
                curtainRoot.rotation = Quaternion.Euler(0f, -85.67f, 0f);

                // Create a wooden curtain pole
                GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "NorenPole";
                Collider poleCol = pole.GetComponent<Collider>();
                if (poleCol != null) Destroy(poleCol);
                pole.transform.SetParent(curtainRoot, false);
                pole.transform.localPosition = new Vector3(0f, -0.02f, 0f); // top edge (radius 0.02) at the pivot
                pole.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                pole.transform.localScale = new Vector3(0.04f, 0.70f, 0.04f);

                Renderer poleRenderer = pole.GetComponent<Renderer>();
                if (poleRenderer != null)
                {
                    Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    if (sh != null)
                    {
                        Material m = new Material(sh) { color = new Color(0.25f, 0.14f, 0.08f) };
                        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.25f, 0.14f, 0.08f));
                        poleRenderer.sharedMaterial = m;
                    }
                }

                // Create 3 traditional Japanese fabric slits (panels)
                curtainFabricPanels = new Transform[3];
                Color norenIndigo = new Color(0.10f, 0.16f, 0.32f, 1f);

                for (int i = 0; i < 3; i++)
                {
                    GameObject slit = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    slit.name = $"NorenPanel_{i}";
                    Collider slitCol = slit.GetComponent<Collider>();
                    if (slitCol != null) Destroy(slitCol);

                    slit.transform.SetParent(curtainRoot, false);
                    float xOffset = -0.42f + (i * 0.42f);
                    slit.transform.localPosition = new Vector3(xOffset, -0.39f, 0f);
                    slit.transform.localScale = new Vector3(0.38f, 0.65f, 0.015f);

                    Renderer slitRenderer = slit.GetComponent<Renderer>();
                    if (slitRenderer != null)
                    {
                        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                        if (sh != null)
                        {
                            Material m = new Material(sh) { color = norenIndigo };
                            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", norenIndigo);
                            slitRenderer.sharedMaterial = m;
                        }
                    }
                    curtainFabricPanels[i] = slit.transform;
                }
            }
        }

        private void SetupSoftDrumAudio()
        {
            _drumAudioSource = GetComponent<AudioSource>();
            if (_drumAudioSource == null)
            {
                _drumAudioSource = gameObject.AddComponent<AudioSource>();
            }

            _drumAudioSource.playOnAwake = false;
            _drumAudioSource.spatialBlend = 0f;
            _drumAudioSource.volume = 0.45f;

            // Synthesize warm taiko festival drum cue (gentle attack, soft resonance)
            int sampleRate = 44100;
            float duration = 0.60f;
            int count = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;
                float attack = Mathf.Clamp01(t / 0.03f);
                float decay = Mathf.Exp(-5.0f * t);
                float tone = Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.7f
                           + Mathf.Sin(2f * Mathf.PI * 165f * t) * 0.3f;
                samples[i] = tone * attack * decay * 0.5f;
            }

            _drumClip = AudioClip.Create("SoftTaikoNorenDrum", count, 1, sampleRate, false);
            _drumClip.SetData(samples, 0);
        }

        private void PlaySoftDrumCue(bool opening)
        {
            if (_drumAudioSource != null && _drumClip != null)
            {
                _drumAudioSource.pitch = opening ? 1.05f : 0.85f;
                _drumAudioSource.PlayOneShot(_drumClip, 0.45f);
            }
        }
    }
}
