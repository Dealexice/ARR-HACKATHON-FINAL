using System;
using UnityEngine;
using UnityEngine.Events;

namespace YataiTogether
{
    /// <summary>
    /// Steps 6 to 10: Collider-Based Bowl Assembly Receiver.
    /// Attached to "lamian_bowl".
    /// Detects physical ingredients entering the bowl trigger collider, activates the corresponding
    /// fixed child mesh inside the bowl, destroys/hides the incoming loose prop, and advances recipe state.
    /// Fully compliant with Meta Interaction SDK & Strict Sequence Law.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class RamenAssemblyReceiver : MonoBehaviour
    {
        public static RamenAssemblyReceiver Instance { get; private set; }

        [Header("Assembled Child Meshes (Inside lamian_bowl)")]
        [SerializeField] private GameObject childLamianNoodles;
        [SerializeField] private GameObject childLamianMeat;
        [SerializeField] private GameObject childLamianEgg;
        [SerializeField] private GameObject childLamianOnions;
        [SerializeField] private GameObject childLamianNori;
        [SerializeField] private GameObject childLamianBroth;

        [Header("Ingredient Collider Tag / Name Identifiers")]
        [SerializeField] private string noodleIdentifier = "noodle";
        [SerializeField] private string meatIdentifier = "meat";
        [SerializeField] private string eggIdentifier = "egg";
        [SerializeField] private string onionIdentifier = "onion";
        [SerializeField] private string noriIdentifier = "nori";
        [SerializeField] private string brothIdentifier = "broth";
        [SerializeField] private string spoonIdentifier = "spoon";

        [Header("VR Hand Grab on Bowl Completion (Step 12)")]
        [Tooltip("Meta Interaction SDK Grabbable component, unlocked when bowl is complete.")]
        [SerializeField] private Oculus.Interaction.Grabbable bowlGrabbable;

        [Tooltip("Meta Interaction SDK HandGrabInteractable, unlocked when bowl is complete.")]
        [SerializeField] private Oculus.Interaction.HandGrab.HandGrabInteractable bowlHandGrab;

        [Tooltip("Rigidbody on lamian_bowl (always kinematic, no gravity: its only collider is a trigger).")]
        [SerializeField] private Rigidbody bowlRigidbody;

        [Header("Events")]
        public UnityEvent onNoodlesAdded = new UnityEvent();
        public UnityEvent onMeatAdded = new UnityEvent();
        public UnityEvent onEggAdded = new UnityEvent();
        public UnityEvent onOnionsAdded = new UnityEvent();
        public UnityEvent onNoriAdded = new UnityEvent();
        public UnityEvent onBrothAdded = new UnityEvent();
        public UnityEvent onBowlCompleted = new UnityEvent();

        public bool HasNoodles => childLamianNoodles != null && childLamianNoodles.activeSelf;
        public bool HasMeat => childLamianMeat != null && childLamianMeat.activeSelf;
        public bool HasEgg => childLamianEgg != null && childLamianEgg.activeSelf;
        public bool HasOnions => childLamianOnions != null && childLamianOnions.activeSelf;
        public bool HasNori => childLamianNori != null && childLamianNori.activeSelf;
        public bool HasBroth => childLamianBroth != null && childLamianBroth.activeSelf;
        public bool IsBowlComplete => HasNoodles && HasMeat && HasEgg && HasOnions && (childLamianNori == null || HasNori) && HasBroth;

        public GameObject ChildNoodles => childLamianNoodles;
        public GameObject ChildMeat => childLamianMeat;
        public GameObject ChildEgg => childLamianEgg;
        public GameObject ChildOnions => childLamianOnions;
        public GameObject ChildNori => childLamianNori;
        public GameObject ChildBroth => childLamianBroth;

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

            FindChildMeshesIfNull();
            InitializeChildMeshes(false);

            if (bowlRigidbody == null) bowlRigidbody = GetComponent<Rigidbody>();
            if (bowlGrabbable == null) bowlGrabbable = GetComponent<Oculus.Interaction.Grabbable>();
            if (bowlHandGrab == null) bowlHandGrab = GetComponent<Oculus.Interaction.HandGrab.HandGrabInteractable>();
            SetBowlGrabbable(false);
        }

        private void OnEnable()
        {
            if (ParentHotkeyRouter.Instance != null)
            {
                ParentHotkeyRouter.Instance.ActionTapped += HandleActionTapped;
            }

            if (YataiSessionDirector.Instance != null)
            {
                YataiSessionDirector.Instance.StepChanged += HandleStepChanged;
            }
        }

        private void Start()
        {
            if (ParentHotkeyRouter.Instance != null)
            {
                ParentHotkeyRouter.Instance.ActionTapped -= HandleActionTapped;
                ParentHotkeyRouter.Instance.ActionTapped += HandleActionTapped;
            }

            if (YataiSessionDirector.Instance != null)
            {
                YataiSessionDirector.Instance.StepChanged -= HandleStepChanged;
                YataiSessionDirector.Instance.StepChanged += HandleStepChanged;
            }
        }

        private void OnDisable()
        {
            if (ParentHotkeyRouter.Instance != null)
            {
                ParentHotkeyRouter.Instance.ActionTapped -= HandleActionTapped;
            }

            if (YataiSessionDirector.Instance != null)
            {
                YataiSessionDirector.Instance.StepChanged -= HandleStepChanged;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void HandleStepChanged(CookingStep step)
        {
            if (step == CookingStep.Noodles_Grab && IsBowlComplete)
            {
                ResetBowlForNextCustomer();
            }
        }

        private void HandleActionTapped(ParentAction action)
        {
            // Fallback only if SpoonBrothScooper is not present in scene and director is in Broth_Pour
            if (action == ParentAction.PourBroth && SpoonBrothScooper.Instance == null)
            {
                if (YataiSessionDirector.Instance != null &&
                    (YataiSessionDirector.Instance.CurrentStep == CookingStep.Broth_Pour ||
                     YataiSessionDirector.Instance.CurrentStep == CookingStep.ReadyToServe))
                {
                    AddBroth();
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;

            string objName = other.gameObject.name.ToLower();
            string rootName = other.transform.root.name.ToLower();

            // Noodles only go into the bowl once cooked and taken out of the pot (Strict Sequence Law):
            // a raw noodle swinging past the bowl during the stretch must not count.
            bool noodlesCanEnter = YataiSessionDirector.Instance == null ||
                                   YataiSessionDirector.Instance.CurrentStep == CookingStep.Noodles_Transfer;

            // 1. Spoon with noodle touches bowl
            if (noodlesCanEnter && (objName.Contains(spoonIdentifier) || rootName.Contains(spoonIdentifier)))
            {
                Transform spoonAttach = other.transform.Find("spoon_attach_point");
                if (spoonAttach == null && other.transform.parent != null)
                {
                    spoonAttach = other.transform.parent.Find("spoon_attach_point");
                }

                if (spoonAttach != null && spoonAttach.childCount > 0)
                {
                    for (int i = spoonAttach.childCount - 1; i >= 0; i--)
                    {
                        Destroy(spoonAttach.GetChild(i).gameObject);
                    }
                    AddNoodles();
                    return;
                }
            }

            // Direct noodle mesh dropped into bowl
            // ponytail: Defensive name and tag comparison preventing engine tag exceptions
            if (!HasNoodles && MatchesIdentifierOrTag(other, noodleIdentifier, "noodle"))
            {
                if (!noodlesCanEnter) return;

                ParentHotkeyRouter router = ParentHotkeyRouter.Instance;
                if (router != null && router.CurrentlyHeldProp == other.gameObject) router.CurrentlyHeldProp = null;
                Destroy(other.gameObject);
                AddNoodles();
                return;
            }

            // 2. Meat touches bowl
            if (!HasMeat && MatchesIdentifierOrTag(other, meatIdentifier, "meat"))
            {
                // Verify strict order: only accept if noodles are in bowl
                if (!HasNoodles)
                {
                    if (YataiSessionDirector.Instance != null)
                    {
                        YataiSessionDirector.Instance.TriggerWarning("Sequence locked: Cook noodles before adding Chashu pork!");
                    }
                    return;
                }

                other.gameObject.SetActive(false);
                AddMeat();
                return;
            }

            // 3. Egg touches bowl
            if (!HasEgg && MatchesIdentifierOrTag(other, eggIdentifier, "egg"))
            {
                // Verify strict order: only accept if meat is in bowl
                if (!HasMeat)
                {
                    if (YataiSessionDirector.Instance != null)
                    {
                        YataiSessionDirector.Instance.TriggerWarning("Sequence locked: Add Chashu pork before Ajitama egg!");
                    }
                    return;
                }

                other.gameObject.SetActive(false);
                AddEgg();
                return;
            }

            // 4. Onions touch bowl
            if (!HasOnions && MatchesIdentifierOrTag(other, onionIdentifier, "onion"))
            {
                if (!HasEgg)
                {
                    if (YataiSessionDirector.Instance != null)
                    {
                        YataiSessionDirector.Instance.TriggerWarning("Sequence locked: Add Ajitama egg before green onions!");
                    }
                    return;
                }

                other.gameObject.SetActive(false);
                AddOnions();
                return;
            }

            // 5. Broth ladle touches bowl
            if (!HasBroth && MatchesIdentifierOrTag(other, brothIdentifier, "broth"))
            {
                if (!HasOnions)
                {
                    if (YataiSessionDirector.Instance != null)
                    {
                        YataiSessionDirector.Instance.TriggerWarning("Sequence locked: Complete toppings before broth!");
                    }
                    return;
                }

                AddBroth();
                return;
            }
        }

        private static bool MatchesIdentifierOrTag(Collider col, string identifier, string tag)
        {
            if (col == null) return false;
            string n = col.gameObject.name.ToLower();
            if (!string.IsNullOrEmpty(identifier) && n.Contains(identifier.ToLower())) return true;
            try
            {
                return col.CompareTag(tag);
            }
            catch
            {
                return false;
            }
        }

        public void AddNoodles()
        {
            if (HasNoodles) return;

            if (childLamianNoodles != null) childLamianNoodles.SetActive(true);
            onNoodlesAdded?.Invoke();
            Debug.Log("[RamenAssemblyReceiver] Noodles placed in bowl!");

            if (YataiSessionDirector.Instance != null && YataiSessionDirector.Instance.CurrentStep <= CookingStep.Noodles_Transfer)
            {
                YataiSessionDirector.Instance.SetStep(CookingStep.Pork_Grab);
            }

            CheckBowlCompletion();
        }

        public void AddMeat()
        {
            if (HasMeat) return;

            if (childLamianMeat != null) childLamianMeat.SetActive(true);
            onMeatAdded?.Invoke();
            Debug.Log("[RamenAssemblyReceiver] Chashu pork placed in bowl!");

            if (YataiSessionDirector.Instance != null && (YataiSessionDirector.Instance.CurrentStep == CookingStep.Pork_Grab || YataiSessionDirector.Instance.CurrentStep == CookingStep.Pork_Release))
            {
                YataiSessionDirector.Instance.SetStep(CookingStep.Egg_Drop);
            }

            CheckBowlCompletion();
        }

        public void AddEgg()
        {
            if (HasEgg) return;

            if (childLamianEgg != null) childLamianEgg.SetActive(true);
            onEggAdded?.Invoke();
            Debug.Log("[RamenAssemblyReceiver] Ajitama egg placed in bowl!");

            if (YataiSessionDirector.Instance != null && (YataiSessionDirector.Instance.CurrentStep == CookingStep.Egg_Drop || YataiSessionDirector.Instance.CurrentStep == CookingStep.Egg_Release))
            {
                YataiSessionDirector.Instance.SetStep(CookingStep.Onions_Grab);
            }

            CheckBowlCompletion();
        }

        public void AddOnions()
        {
            if (HasOnions) return;

            if (childLamianOnions != null) childLamianOnions.SetActive(true);
            onOnionsAdded?.Invoke();
            Debug.Log("[RamenAssemblyReceiver] Green onions placed in bowl!");

            if (YataiSessionDirector.Instance != null && (YataiSessionDirector.Instance.CurrentStep == CookingStep.Onions_Grab || YataiSessionDirector.Instance.CurrentStep == CookingStep.Onions_Release))
            {
                YataiSessionDirector.Instance.SetStep(CookingStep.Nori_Hold);
            }

            CheckBowlCompletion();
        }

        public void AddNori()
        {
            if (HasNori) return;

            if (childLamianNori != null) childLamianNori.SetActive(true);
            onNoriAdded?.Invoke();
            Debug.Log("[RamenAssemblyReceiver] Golden Nori placed on bowl rim!");

            if (YataiSessionDirector.Instance != null && YataiSessionDirector.Instance.CurrentStep == CookingStep.Nori_Hold)
            {
                YataiSessionDirector.Instance.SetStep(CookingStep.Broth_Ladle);
            }

            CheckBowlCompletion();
        }

        public void AddBroth()
        {
            if (HasBroth) return;

            if (childLamianBroth != null) childLamianBroth.SetActive(true);
            onBrothAdded?.Invoke();
            Debug.Log("[RamenAssemblyReceiver] Broth poured into bowl!");

            if (YataiSessionDirector.Instance != null &&
                (YataiSessionDirector.Instance.CurrentStep == CookingStep.Broth_Ladle ||
                 YataiSessionDirector.Instance.CurrentStep == CookingStep.Broth_Pour))
            {
                YataiSessionDirector.Instance.SetStep(CookingStep.ReadyToServe);
            }

            CheckBowlCompletion();
        }

        private void TriggerHotkeyReleaseFallback()
        {
            if (!HasNoodles) AddNoodles();
            else if (!HasMeat) AddMeat();
            else if (!HasEgg) AddEgg();
            else if (!HasOnions) AddOnions();
            else if (!HasNori) AddNori();
            else if (!HasBroth) AddBroth();
        }

        private void CheckBowlCompletion()
        {
            if (IsBowlComplete)
            {
                SetBowlGrabbable(true);
                onBowlCompleted?.Invoke();
                Debug.Log("[RamenAssemblyReceiver] Ramen bowl fully assembled! Ready to serve customer (Key S or grab & push forward).");
            }
        }

        public void ResetBowlForNextCustomer()
        {
            SetBowlGrabbable(false);
            InitializeChildMeshes(false);
        }

        /// <summary>
        /// Toggles grabbability of the lamian_bowl. Kept locked/kinematic until all ingredients & broth are complete.
        /// </summary>
        public void SetBowlGrabbable(bool grabbable)
        {
            if (bowlGrabbable != null) bowlGrabbable.enabled = grabbable;
            if (bowlHandGrab != null) bowlHandGrab.enabled = grabbable;
            if (bowlRigidbody != null)
            {
                // The bowl's only collider is the assembly trigger, so a dynamic gravity body falls straight
                // through the counter (it vanished after the first completed bowl). Keep it kinematic and
                // gravity-free in every state; Meta Grabbable moves kinematic bodies by transform.
                if (!bowlRigidbody.isKinematic)
                {
                    bowlRigidbody.linearVelocity = Vector3.zero;
                    bowlRigidbody.angularVelocity = Vector3.zero;
                }
                bowlRigidbody.useGravity = false;
                bowlRigidbody.isKinematic = true;
            }
        }

        private void InitializeChildMeshes(bool active)
        {
            if (childLamianNoodles != null) childLamianNoodles.SetActive(active);
            if (childLamianMeat != null) childLamianMeat.SetActive(active);
            if (childLamianEgg != null) childLamianEgg.SetActive(active);
            if (childLamianOnions != null) childLamianOnions.SetActive(active);
            if (childLamianNori != null) childLamianNori.SetActive(active);
            if (childLamianBroth != null) childLamianBroth.SetActive(active);
        }

        public void SetChildMeshes(GameObject noodles, GameObject meat, GameObject egg, GameObject onions, GameObject broth, GameObject nori = null)
        {
            childLamianNoodles = noodles;
            childLamianMeat = meat;
            childLamianEgg = egg;
            childLamianOnions = onions;
            childLamianBroth = broth;
            childLamianNori = nori;
        }

        private void FindChildMeshesIfNull()
        {
            if (childLamianNoodles == null)
            {
                Transform t = transform.Find("lamian_noodle") ?? transform.Find("lamian_noodles");
                if (t != null) childLamianNoodles = t.gameObject;
            }
            if (childLamianMeat == null)
            {
                Transform t = transform.Find("lamian_meat");
                if (t != null) childLamianMeat = t.gameObject;
            }
            if (childLamianEgg == null)
            {
                Transform t = transform.Find("lamian_egg");
                if (t != null) childLamianEgg = t.gameObject;
            }
            if (childLamianOnions == null)
            {
                Transform t = transform.Find("lamian_onion") ?? transform.Find("lamian_onions");
                if (t != null) childLamianOnions = t.gameObject;
            }
            if (childLamianBroth == null)
            {
                Transform t = transform.Find("lamian_broth");
                if (t != null) childLamianBroth = t.gameObject;
            }
        }
    }
}
