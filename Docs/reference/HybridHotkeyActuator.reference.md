# Reference: `HybridHotkeyActuator.cs` (user-supplied, verbatim)

> **Do not copy into `Assets/` as-is.** This is the design reference for the hotkey model. The production code splits it into the components listed in [`../implementation_roadmap.md`](../implementation_roadmap.md) §2 and applies the fixes listed there. It is kept as `.md` so Unity never compiles it.

Intended use (original): attach to an empty GameObject `ARR_GameManager`. Uses `UnityEngine.InputSystem` (`Keyboard.current`).

```csharp
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

public class HybridHotkeyActuator : MonoBehaviour
{
    [Header("1. Dual Camera POV Anchors (Spacebar & Key V)")]
    [SerializeField] private Transform vrCameraRigRoot;
    [SerializeField] private Transform pov1BikeAnchor;
    [SerializeField] private Transform pov2StallAnchor;
    [SerializeField] private Transform bikeCartWorldModel;
    [SerializeField] private float metersPerPedalClick = 10f;
    [SerializeField] private float targetRideDistance = 100f;

    [Header("2. Child Virtual Hand & Bowl Targets")]
    [SerializeField] private Transform childVirtualHandAttachPoint;
    [SerializeField] private Transform magicEggChuteSpawnPoint;
    [SerializeField] private Transform zoneCBowlDropTarget;

    [Header("3. Ingredient & Tool Prefabs (Keys 1, 2, 3, 4, 5)")]
    [SerializeField] private GameObject chashuPorkPrefab;     // Key 1
    [SerializeField] private GameObject ajitamaEggPrefab;     // Key 2
    [SerializeField] private GameObject greenOnionsPrefab;    // Key 3
    [SerializeField] private GameObject rawNoodlesPrefab;     // Key 4
    [SerializeField] private GameObject brothLadleObject;     // Key 5

    [Header("4. Hold Timers (Keys W, E, F)")]
    [SerializeField] private float bandPullHoldDuration = 3.0f;   // Key W
    [SerializeField] private float boilerStretchDuration = 3.0f;  // Key E
    [SerializeField] private float handFlattenDuration = 5.0f;    // Key F

    [Header("5. Visual & Audio Events (Hook up in Inspector)")]
    public UnityEvent<float> onBikeDistanceUpdated;           // Passes current meters (0-100)
    public UnityEvent<bool> onStallCurtainToggled;            // Key Q (True = Open, False = Closed)
    public UnityEvent<float> onBandPullProgress;              // Hold Key W (0.0 to 1.0)
    public UnityEvent onBandPullCompleted;
    public UnityEvent<float> onBoilerTimerProgress;           // Hold Key E (0.0 to 1.0)
    public UnityEvent onBoilerTimerFlashedGreen;
    public UnityEvent<float> onHandFlattenProgress;           // Hold Key F (0.0 to 1.0)
    public UnityEvent onGoldenNoriPressed;
    public UnityEvent onBrothPoured;                          // Key D
    public UnityEvent<int> onCustomerServedWithFireworks;     // Key S (Passes Customer 1-5)

    // Internal Runtime State
    private float _currentRideMeters = 0f;
    private bool _isAtStallPOV = false;
    private bool _isCurtainOpen = false;
    private GameObject _currentlyHeldProp = null;
    private float _bandHoldTimer = 0f;
    private float _boilerHoldTimer = 0f;
    private float _flattenHoldTimer = 0f;
    private int _customersServedCount = 0;
    private const int MaxCustomers = 5;

    private void Start()
    {
        if (vrCameraRigRoot == null || pov1BikeAnchor == null || pov2StallAnchor == null)
        {
            Debug.LogError($"[{nameof(HybridHotkeyActuator)}] Missing Camera Rig or POV Anchors on {gameObject.name}", this);
            return;
        }

        // Start game on the Bike (POV 1)
        SnapCameraToPOV(false);
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        HandleBikeAndPOVHotkeys();
        HandleCurtainAndStretchHolds();
        HandleIngredientSpawnAndDropHotkeys();
        HandleLadleAndServingHotkeys();
    }

    private void HandleBikeAndPOVHotkeys()
    {
        // SPACEBAR: +10m Bike Pedaling Active Stretch
        if (Keyboard.current.spaceKey.wasPressedThisFrame && !_isAtStallPOV)
        {
            _currentRideMeters += metersPerPedalClick;
            if (bikeCartWorldModel != null)
            {
                bikeCartWorldModel.position += bikeCartWorldModel.forward * (metersPerPedalClick * 0.2f);
            }

            onBikeDistanceUpdated?.Invoke(_currentRideMeters);
            Debug.Log($"[Bike Stretch] Distance: {_currentRideMeters}m / {targetRideDistance}m");

            if (_currentRideMeters >= targetRideDistance)
            {
                SnapCameraToPOV(true); // Arrived at village stall!
            }
        }

        // KEY V: Manual POV Toggle Failsafe (Bike <-> Food Stall)
        if (Keyboard.current.vKey.wasPressedThisFrame)
        {
            SnapCameraToPOV(!_isAtStallPOV);
        }
    }

    private void HandleCurtainAndStretchHolds()
    {
        // KEY Q: Overhead Reach -> Open / Close Stall Curtain
        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            _isCurtainOpen = !_isCurtainOpen;
            onStallCurtainToggled?.Invoke(_isCurtainOpen);
            Debug.Log($"[Overhead Reach - Key Q] Stall Curtain Open: {_isCurtainOpen}");
        }

        // HOLD KEY W: Two-Hand Band Pull (Wrists Apart)
        if (Keyboard.current.wKey.isPressed)
        {
            _bandHoldTimer += Time.deltaTime;
            onBandPullProgress?.Invoke(Mathf.Clamp01(_bandHoldTimer / bandPullHoldDuration));

            if (_bandHoldTimer >= bandPullHoldDuration)
            {
                onBandPullCompleted?.Invoke();
                _bandHoldTimer = 0f;
                Debug.Log("[Two-Hand Band Pull - Key W] Dough stretched into noodles!");
            }
        }

        // HOLD KEY E: Straighten Elbow Stretch Over Boiler Pot (3s Green Timer)
        if (Keyboard.current.eKey.isPressed)
        {
            _boilerHoldTimer += Time.deltaTime;
            onBoilerTimerProgress?.Invoke(Mathf.Clamp01(_boilerHoldTimer / boilerStretchDuration));

            if (_boilerHoldTimer >= boilerStretchDuration)
            {
                onBoilerTimerFlashedGreen?.Invoke();
                _boilerHoldTimer = 0f;
                if (_currentlyHeldProp != null) Destroy(_currentlyHeldProp);
                Debug.Log("[Elbow Extension - Key E] 3s Boiler Timer GREEN! Noodles transferred to bowl.");
            }
        }

        // HOLD KEY F: Parent Flattens Hand & Holds (5s Golden Nori Press)
        if (Keyboard.current.fKey.isPressed)
        {
            _flattenHoldTimer += Time.deltaTime;
            onHandFlattenProgress?.Invoke(Mathf.Clamp01(_flattenHoldTimer / handFlattenDuration));

            if (_flattenHoldTimer >= handFlattenDuration)
            {
                onGoldenNoriPressed?.Invoke();
                _flattenHoldTimer = 0f;
                Debug.Log("[Hand Flatten - Key F] 5s Hold Complete! Golden Nori added to bowl.");
            }
        }
    }

    private void HandleIngredientSpawnAndDropHotkeys()
    {
        // KEY 1: Pinch Chashu Pork
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            AttachPropToChildHand(chashuPorkPrefab, "Chashu Pork");

        // KEY 2: Forearm Palm-Up -> Magic Ajitama Egg Dispenser Drop
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            TriggerMagicEggDispenser();

        // KEY 3: Pinch Green Onions
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
            AttachPropToChildHand(greenOnionsPrefab, "Green Onions");

        // KEY 4: Pinch Uncooked Noodles
        if (Keyboard.current.digit4Key.wasPressedThisFrame)
            AttachPropToChildHand(rawNoodlesPrefab, "Raw Noodles");

        // KEY A or ENTER: Reach & Let Go (Release Held Ingredient into Bowl with Goofy Physics)
        if (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)
        {
            ReleaseHeldPropIntoBowl();
        }

        // BACKSPACE: Failsafe Clear Held Prop
        if (Keyboard.current.backspaceKey.wasPressedThisFrame && _currentlyHeldProp != null)
        {
            Destroy(_currentlyHeldProp);
            _currentlyHeldProp = null;
        }
    }

    private void HandleLadleAndServingHotkeys()
    {
        // KEY 5: Grasp Broth Ladle
        if (Keyboard.current.digit5Key.wasPressedThisFrame && brothLadleObject != null)
        {
            brothLadleObject.transform.SetParent(childVirtualHandAttachPoint);
            brothLadleObject.transform.localPosition = Vector3.zero;
            brothLadleObject.transform.localRotation = Quaternion.identity;
            Debug.Log("[Grasp - Key 5] Broth Ladle attached to child's virtual hand.");
        }

        // KEY D: Wrist Rotation -> Pour Broth into Bowl
        if (Keyboard.current.dKey.wasPressedThisFrame)
        {
            if (brothLadleObject != null)
            {
                brothLadleObject.transform.localRotation = Quaternion.Euler(0f, 0f, 85f);
            }
            onBrothPoured?.Invoke();
            Debug.Log("[Wrist Pour - Key D] Broth poured into Ramen Bowl!");
        }

        // KEY S: Serve Ramen Bowl to Customer -> Fireworks + "Yippee!"
        if (Keyboard.current.sKey.wasPressedThisFrame)
        {
            _customersServedCount = Mathf.Clamp(_customersServedCount + 1, 1, MaxCustomers);
            onCustomerServedWithFireworks?.Invoke(_customersServedCount);
            Debug.Log($"[Serve Customer - Key S] Customer {_customersServedCount}/{MaxCustomers} served! YIPPEE + FIREWORKS!");
        }
    }

    private void AttachPropToChildHand(GameObject prefab, string itemName)
    {
        if (prefab == null || childVirtualHandAttachPoint == null) return;
        if (_currentlyHeldProp != null) Destroy(_currentlyHeldProp);

        _currentlyHeldProp = Instantiate(prefab, childVirtualHandAttachPoint.position, childVirtualHandAttachPoint.rotation, childVirtualHandAttachPoint);
        if (_currentlyHeldProp.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.isKinematic = true; // Held steady until Key A / Enter is pressed
        }
        Debug.Log($"[Hybrid Spawn] Attached {itemName} to child's VR hand.");
    }

    private void TriggerMagicEggDispenser()
    {
        if (ajitamaEggPrefab == null || childVirtualHandAttachPoint == null) return;
        if (_currentlyHeldProp != null) Destroy(_currentlyHeldProp);

        Vector3 spawnPos = magicEggChuteSpawnPoint != null ? magicEggChuteSpawnPoint.position : childVirtualHandAttachPoint.position + Vector3.up * 0.15f;
        _currentlyHeldProp = Instantiate(ajitamaEggPrefab, spawnPos, Quaternion.identity, childVirtualHandAttachPoint);
        _currentlyHeldProp.transform.localPosition = Vector3.zero;

        if (_currentlyHeldProp.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.isKinematic = true;
        }
        Debug.Log("[Palm-Up Supination - Key 2] Magic Egg dispensed into child's palm!");
    }

    private void ReleaseHeldPropIntoBowl()
    {
        if (_currentlyHeldProp == null) return;

        _currentlyHeldProp.transform.SetParent(null);
        if (zoneCBowlDropTarget != null)
        {
            // Gently magnetize drop position above Zone C bowl so the child never "misses" or fails
            _currentlyHeldProp.transform.position = zoneCBowlDropTarget.position + new Vector3(Random.Range(-0.03f, 0.03f), 0.12f, Random.Range(-0.03f, 0.03f));
        }

        if (_currentlyHeldProp.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.isKinematic = false;
            rb.mass = 0.08f; // Goofy low-mass toy physics
        }

        _currentlyHeldProp = null;
        Debug.Log("[Reach & Let Go - Key A/Enter] Ingredient dropped into Ramen Bowl!");
    }

    private void SnapCameraToPOV(bool toStall)
    {
        _isAtStallPOV = toStall;
        Transform targetAnchor = toStall ? pov2StallAnchor : pov1BikeAnchor;
        vrCameraRigRoot.SetPositionAndRotation(targetAnchor.position, targetAnchor.rotation);
        Debug.Log($"[POV Switched] Active POV: {(toStall ? "POV 2 - Food Stall Counter" : "POV 1 - Bike Seat")}");
    }
}
```
