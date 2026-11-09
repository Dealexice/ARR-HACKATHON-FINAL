using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace YataiTogether
{
    /// <summary>
    /// Step 12: Customer Serving & Celebration Station.
    /// Handles serving the completed ramen bowl to customer Panda across the counter table.
    /// Triggered either when the physical bowl is pushed/slid into the Panda counter zone,
    /// or when the parent taps Key 'S' (ParentAction.ServeCustomer).
    ///
    /// Requirements:
    /// - Slides/tweens "lamian_bowl" to customerServePoint in front of Panda.
    /// - Checks pandaAnimator: triggers eatOrCelebrateTrigger if valid, skips cleanly if null or parameter missing.
    /// - Plays confettiFX and fireworkFX particle systems.
    /// - Plays celebrationAudio (synthesizes soft attack celebratory fanfare if unassigned).
    /// - Notifies YataiSessionDirector (CookingStep.CustomerServed).
    /// - After delay (~2.5s), resets bowl ingredients and returns bowl to Zone C for the next customer.
    /// </summary>
    [DisallowMultipleComponent]
    public class CustomerServingStation : MonoBehaviour
    {
        public static CustomerServingStation Instance { get; private set; }

        [Header("Customer & Bowl References")]
        [Tooltip("Optional Animator on customer Panda / Po. Null-checked defensively.")]
        [SerializeField] private Animator pandaAnimator;

        [Tooltip("Animation trigger name for celebration/eating (default 'Celebrate').")]
        [SerializeField] private string eatOrCelebrateTrigger = "Celebrate";

        [Tooltip("Target Transform in front of Panda where the ramen bowl slides when served.")]
        [SerializeField] private Transform customerServePoint;

        [Tooltip("The ramen bowl GameObject to slide and reset.")]
        [SerializeField] private GameObject lamianBowl;

        [Header("Celebration FX")]
        [Tooltip("Confetti particle system celebrating customer order completion.")]
        [SerializeField] private ParticleSystem confettiFX;

        [Tooltip("Hanabi fireworks particle system above the festival stall.")]
        [SerializeField] private ParticleSystem fireworkFX;

        [Tooltip("Audio source for celebratory chime/fanfare.")]
        [SerializeField] private AudioSource celebrationAudio;

        [Header("Serving Parameters")]
        [SerializeField] private float slideDuration = 0.55f;
        [SerializeField] private float celebrationDuration = 2.5f;
        [SerializeField] private float returnDuration = 0.35f;

        [Header("Events")]
        public UnityEvent onServingStarted = new UnityEvent();
        public UnityEvent onCelebrationFinished = new UnityEvent();

        private bool _isServing = false;
        private Vector3 _bowlOriginalPosition;
        private Quaternion _bowlOriginalRotation;
        private Transform _bowlOriginalParent;
        private bool _hasBowlBaseline;
        private Coroutine _serveRoutine;
        private AudioClip _synthesizedCelebrationClip;

        public bool IsServing => _isServing;
        public Animator PandaAnimator => pandaAnimator;
        public ParticleSystem ConfettiFX => confettiFX;
        public ParticleSystem FireworkFX => fireworkFX;
        public AudioSource CelebrationAudio => celebrationAudio;
        public Transform CustomerServePoint => customerServePoint;
        public GameObject LamianBowl => lamianBowl;

        public void Configure(
            GameObject bowl,
            Transform servePoint,
            Animator animator = null,
            ParticleSystem confetti = null,
            ParticleSystem firework = null,
            AudioSource audio = null)
        {
            lamianBowl = bowl;
            customerServePoint = servePoint;
            pandaAnimator = animator;
            confettiFX = confetti;
            fireworkFX = firework;
            celebrationAudio = audio;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            EnsureReferences();
            EnsureCelebrationFX();
            CaptureBowlBaseline();
            SetupCelebrationAudio();
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

            EnsureReferences();
            CaptureBowlBaseline();
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
        }

        private void CaptureBowlBaseline()
        {
            // Zone C home is captured once. Re-capturing at serve time recorded wherever the bowl had
            // drifted/fallen to, so it was "returned" there instead of to the counter.
            if (_hasBowlBaseline || lamianBowl == null) return;
            _bowlOriginalPosition = lamianBowl.transform.position;
            _bowlOriginalRotation = lamianBowl.transform.rotation;
            _bowlOriginalParent = lamianBowl.transform.parent;
            _hasBowlBaseline = true;
        }

        private void EnsureReferences()
        {
            GameObject street03 = GameObject.Find("Test Street03") ?? GameObject.Find("Test Street");

            if (lamianBowl == null)
            {
                if (street03 != null)
                {
                    Transform t = street03.transform.Find("lamian_bowl");
                    if (t != null) lamianBowl = t.gameObject;
                }
                if (lamianBowl == null)
                {
                    lamianBowl = GameObject.Find("lamian_bowl");
                }
            }

            if (customerServePoint == null)
            {
                Transform found = transform.Find("customer_serve_point");
                if (found == null && street03 != null)
                {
                    found = street03.transform.Find("customer_serve_point");
                }

                if (found != null)
                {
                    customerServePoint = found;
                }
                else
                {
                    GameObject serveGo = new GameObject("customer_serve_point");
                    serveGo.transform.SetParent(street03 != null ? street03.transform : transform, false);
                    // Placed across the counter in front of Panda at (7.77, 1.38, 0.62)
                    serveGo.transform.position = new Vector3(7.35f, 1.11f, 0.52f);
                    customerServePoint = serveGo.transform;

                    // Trigger zone detecting physical bowl pushes into Panda's counter area
                    BoxCollider triggerBox = serveGo.AddComponent<BoxCollider>();
                    triggerBox.isTrigger = true;
                    triggerBox.size = new Vector3(0.50f, 0.35f, 0.50f);
                }
            }

            if (pandaAnimator == null)
            {
                // ponytail: Case-insensitive fallback lookup for customer Panda in stall
                GameObject pandaObj = GameObject.Find("panda") ?? GameObject.Find("Panda")
                    ?? (street03 != null ? (street03.transform.Find("panda")?.gameObject ?? street03.transform.Find("Panda")?.gameObject) : null);
                if (pandaObj != null)
                {
                    pandaAnimator = pandaObj.GetComponent<Animator>();
                    if (pandaAnimator == null)
                    {
                        pandaAnimator = pandaObj.GetComponentInChildren<Animator>();
                    }
                }
            }
        }

        private void EnsureCelebrationFX()
        {
            Transform street03 = GameObject.Find("Test Street03")?.transform ?? transform;

            // 1. Confetti Particle System
            if (confettiFX == null)
            {
                Transform foundConfetti = street03.Find("ConfettiFX") ?? transform.Find("ConfettiFX");
                if (foundConfetti != null)
                {
                    confettiFX = foundConfetti.GetComponent<ParticleSystem>();
                }
                else
                {
                    GameObject confettiGo = new GameObject("ConfettiFX");
                    confettiGo.transform.SetParent(street03, false);
                    confettiGo.transform.position = customerServePoint != null ? customerServePoint.position + Vector3.up * 0.4f : new Vector3(7.35f, 1.55f, 0.52f);
                    confettiFX = CreateProceduralConfetti(confettiGo);
                }
            }

            // 2. Firework Particle System
            if (fireworkFX == null)
            {
                Transform foundFirework = street03.Find("FireworkFX") ?? transform.Find("FireworkFX");
                if (foundFirework != null)
                {
                    fireworkFX = foundFirework.GetComponent<ParticleSystem>();
                }
                else
                {
                    GameObject fireworkGo = new GameObject("FireworkFX");
                    fireworkGo.transform.SetParent(street03, false);
                    fireworkGo.transform.position = new Vector3(7.50f, 2.50f, 0.52f);
                    fireworkFX = CreateProceduralFirework(fireworkGo);
                }
            }
        }

        private static ParticleSystem CreateProceduralConfetti(GameObject go)
        {
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.duration = 2.0f;
            main.loop = false;
            main.startLifetime = 1.8f;
            main.startSpeed = 2.5f;
            main.startSize = 0.045f;
            main.gravityModifier = 0.65f;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.0f, 45) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.15f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.85f, 0.2f), 0f), new GradientColorKey(new Color(0.95f, 0.35f, 0.45f), 0.5f), new GradientColorKey(new Color(0.3f, 0.85f, 0.95f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            ParticleSystemRenderer rend = go.GetComponent<ParticleSystemRenderer>();
            if (rend != null)
            {
                Material m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Standard"));
                m.name = "Confetti_Mat";
                rend.sharedMaterial = m;
            }

            return ps;
        }

        private static ParticleSystem CreateProceduralFirework(GameObject go)
        {
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.duration = 1.5f;
            main.loop = false;
            main.startLifetime = 1.2f;
            main.startSpeed = 3.2f;
            main.startSize = 0.035f;
            main.gravityModifier = 0.4f;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.05f, 35) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.5f, 0.1f), 0.6f), new GradientColorKey(new Color(1f, 0.2f, 0.3f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            ParticleSystemRenderer rend = go.GetComponent<ParticleSystemRenderer>();
            if (rend != null)
            {
                Material m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Standard"));
                m.name = "Firework_Mat";
                rend.sharedMaterial = m;
            }

            return ps;
        }

        private void SetupCelebrationAudio()
        {
            if (celebrationAudio == null)
            {
                celebrationAudio = GetComponent<AudioSource>();
                if (celebrationAudio == null)
                {
                    celebrationAudio = gameObject.AddComponent<AudioSource>();
                }
            }

            celebrationAudio.playOnAwake = false;
            celebrationAudio.spatialBlend = 0f;
            celebrationAudio.volume = 0.45f;

            // Synthesize joyful pentatonic fanfare (C5, E5, G5, C6) with soft attack envelopes
            // Compliant with AGENTS.md §5 Audio Safety
            int sampleRate = 44100;
            float duration = 1.10f;
            int count = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[count];

            float[] freqs = { 523.25f, 659.25f, 783.99f, 1046.50f };
            float[] startTimes = { 0.0f, 0.18f, 0.36f, 0.54f };

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;
                float sum = 0f;

                for (int note = 0; note < freqs.Length; note++)
                {
                    float noteT = t - startTimes[note];
                    if (noteT >= 0f)
                    {
                        float attack = Mathf.Clamp01(noteT / 0.04f);
                        float decay = Mathf.Exp(-4.5f * noteT);
                        sum += Mathf.Sin(2f * Mathf.PI * freqs[note] * noteT) * attack * decay * 0.28f;
                    }
                }

                samples[i] = sum;
            }

            _synthesizedCelebrationClip = AudioClip.Create("YataiCelebrationFanfare", count, 1, sampleRate, false);
            _synthesizedCelebrationClip.SetData(samples, 0);

            if (celebrationAudio.clip == null)
            {
                celebrationAudio.clip = _synthesizedCelebrationClip;
            }
        }

        private void HandleActionTapped(ParentAction action)
        {
            if (action == ParentAction.ServeCustomer)
            {
                TriggerServe();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;

            // Physical slide/push into Panda's counter area
            if (IsBowlCollider(other))
            {
                TriggerServe();
            }
        }

        private bool IsBowlCollider(Collider other)
        {
            if (lamianBowl != null && (other.gameObject == lamianBowl || other.transform.IsChildOf(lamianBowl.transform)))
            {
                return true;
            }

            return other.GetComponent<RamenAssemblyReceiver>() != null ||
                   other.GetComponentInParent<RamenAssemblyReceiver>() != null ||
                   other.gameObject.name.ToLower().Contains("lamian_bowl");
        }

        /// <summary>
        /// Initiates the serving sequence: slides bowl to Panda, triggers animation, plays FX/audio,
        /// notifies director, and resets the bowl after celebration delay.
        /// </summary>
        public void TriggerServe()
        {
            if (_isServing) return;

            YataiSessionDirector director = YataiSessionDirector.Instance;
            RamenAssemblyReceiver receiver = RamenAssemblyReceiver.Instance;

            bool isReady = (director != null && director.CurrentStep == CookingStep.ReadyToServe) ||
                           (receiver != null && receiver.IsBowlComplete);

            if (!isReady)
            {
                string msg = "Ramen is not complete yet! Finish toppings and broth before serving!";
                if (director != null)
                {
                    director.TriggerWarning(msg);
                }
                else
                {
                    Debug.LogWarning($"[CustomerServingStation] {msg}");
                }
                return;
            }

            if (_serveRoutine != null) StopCoroutine(_serveRoutine);
            _serveRoutine = StartCoroutine(ServeAndCelebrateRoutine());
        }

        private IEnumerator ServeAndCelebrateRoutine()
        {
            _isServing = true;
            EnsureReferences();
            CaptureBowlBaseline();
            onServingStarted?.Invoke();

            YataiSessionDirector director = YataiSessionDirector.Instance;
            if (director != null)
            {
                director.SetStep(CookingStep.CustomerServed);
            }

            // Force-release "lamian_bowl" from VR hand interactor if currently grabbed
            ForceReleaseBowlFromHands();

            // 1. Tween/slide "lamian_bowl" to customerServePoint
            Vector3 startPos = lamianBowl != null ? lamianBowl.transform.position : _bowlOriginalPosition;
            Vector3 targetPos = customerServePoint != null ? customerServePoint.position : startPos + new Vector3(0.65f, 0f, 0.20f);

            // Keep bowl flat on the table surface during slide
            targetPos.y = startPos.y;

            float elapsed = 0f;
            while (elapsed < slideDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / slideDuration);
                // Ease-out quad for natural gentle counter slide
                float easeOut = 1f - (1f - t) * (1f - t);

                if (lamianBowl != null)
                {
                    lamianBowl.transform.position = Vector3.Lerp(startPos, targetPos, easeOut);
                }
                yield return null;
            }

            if (lamianBowl != null)
            {
                lamianBowl.transform.position = targetPos;
            }

            Debug.Log("[CustomerServingStation] Bowl delivered to customer Panda! Starting celebration.");

            // 2. Check pandaAnimator: if assigned and parameter exists, trigger it; else skip cleanly
            TriggerPandaAnimation();

            // 3. Play confettiFX and fireworkFX particle systems
            if (confettiFX != null)
            {
                confettiFX.Play();
            }

            if (fireworkFX != null)
            {
                fireworkFX.Play();
            }

            // 4. Play celebrationAudio
            if (celebrationAudio != null)
            {
                if (celebrationAudio.clip != null)
                {
                    celebrationAudio.Play();
                }
                else if (_synthesizedCelebrationClip != null)
                {
                    celebrationAudio.PlayOneShot(_synthesizedCelebrationClip, 0.45f);
                }
            }

            // 5. Celebration delay (~2.5s)
            yield return new WaitForSeconds(celebrationDuration);

            // 6. Reset bowl (deactivate toppings/broth) and return bowl to Zone C
            if (RamenAssemblyReceiver.Instance != null)
            {
                RamenAssemblyReceiver.Instance.ResetBowlForNextCustomer();
            }

            if (lamianBowl != null)
            {
                // Smoothly return empty bowl back to chef's prep station (Zone C)
                elapsed = 0f;
                Vector3 currentDeliveredPos = lamianBowl.transform.position;

                while (elapsed < returnDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / returnDuration);
                    float smooth = t * t * (3f - 2f * t);
                    lamianBowl.transform.position = Vector3.Lerp(currentDeliveredPos, _bowlOriginalPosition, smooth);
                    yield return null;
                }

                lamianBowl.transform.SetParent(_bowlOriginalParent, true);
                lamianBowl.transform.position = _bowlOriginalPosition;
                lamianBowl.transform.rotation = _bowlOriginalRotation;
            }

            _isServing = false;
            _serveRoutine = null;
            onCelebrationFinished?.Invoke();

            // 7. Advance YataiSessionDirector loop (Customer progression 1/5 -> 5/5 or SessionComplete)
            if (director != null)
            {
                director.AdvanceToNextCustomerOrComplete();
            }

            Debug.Log("[CustomerServingStation] Celebration complete! Fresh empty bowl ready in Zone C.");
        }

        private void ForceReleaseBowlFromHands()
        {
            if (lamianBowl == null) return;

            lamianBowl.transform.SetParent(null, true);

            var grabbables = lamianBowl.GetComponentsInChildren<Oculus.Interaction.Grabbable>(true);
            for (int i = 0; i < grabbables.Length; i++) grabbables[i].enabled = false;

            var handGrabs = lamianBowl.GetComponentsInChildren<Oculus.Interaction.HandGrab.HandGrabInteractable>(true);
            for (int i = 0; i < handGrabs.Length; i++) handGrabs[i].enabled = false;

            var grabInteractors = lamianBowl.GetComponentsInChildren<Oculus.Interaction.GrabInteractable>(true);
            for (int i = 0; i < grabInteractors.Length; i++) grabInteractors[i].enabled = false;

            var xriGrabs = lamianBowl.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>(true);
            for (int i = 0; i < xriGrabs.Length; i++) xriGrabs[i].enabled = false;

            var rb = lamianBowl.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            Debug.Log("[CustomerServingStation] Force-released lamian_bowl from VR hands for serving animation.");
        }

        private void TriggerPandaAnimation()
        {
            if (pandaAnimator == null) return;
            if (!pandaAnimator.isActiveAndEnabled) return;

            if (!string.IsNullOrEmpty(eatOrCelebrateTrigger) && HasParameter(pandaAnimator, eatOrCelebrateTrigger))
            {
                pandaAnimator.SetTrigger(eatOrCelebrateTrigger);
                Debug.Log($"[CustomerServingStation] Triggered '{eatOrCelebrateTrigger}' on pandaAnimator.");
            }
        }

        private static bool HasParameter(Animator animator, string paramName)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            foreach (var p in animator.parameters)
            {
                if (p.name == paramName) return true;
            }
            return false;
        }
    }
}
