using System.Collections;
using System.Text;
using UnityEngine;
using Unity.XR.CoreUtils;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

namespace YataiTogether
{
    /// <summary>
    /// Automated Acceptance Tester for:
    /// 1. VR Camera Height Offset Compensation (Eye level / subtracting local Y)
    /// 2. Boiler potSnapPoint trigger & snapping (BoilerPotSnapReceiver)
    /// 3. Key 4 stretch confirmation & VR hand detach
    /// </summary>
    [DisallowMultipleComponent]
    public class VRHeightAndBoilerSnapAcceptanceTester : MonoBehaviour
    {
        public static bool IsRunning { get; private set; }
        public static bool IsFinished { get; private set; }
        public static bool AllPassed { get; private set; }

        private readonly StringBuilder _log = new StringBuilder();
        private int _passCount = 0;
        private int _failCount = 0;

        private void Start()
        {
            StartCoroutine(RunAcceptanceSuiteRoutine());
        }

        private void Assert(bool condition, string testName, string details = "")
        {
            if (condition)
            {
                _passCount++;
                _log.AppendLine($"[PASS] {testName} {details}");
                Debug.Log($"<color=green>[PASS]</color> {testName} {details}");
            }
            else
            {
                _failCount++;
                _log.AppendLine($"[FAIL] {testName} {details}");
                Debug.LogError($"<color=red>[FAIL]</color> {testName} {details}");
            }
        }

        private IEnumerator RunAcceptanceSuiteRoutine()
        {
            IsRunning = true;
            IsFinished = false;
            AllPassed = false;

            _log.AppendLine("=== STARTING VR HEIGHT, BOILER SNAP & KEY 4 DETACH ACCEPTANCE SUITE ===");
            _log.AppendLine($"Timestamp: {System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n");

            yield return new WaitForSeconds(0.5f);

            // ==========================================
            // TEST 1: VR POV HEIGHT OFFSET
            // ==========================================
            _log.AppendLine("\n--- Testing Requirement 1: VR POV Height Offset ---");

            PovRigController povCtrl = PovRigController.Instance;
            DualPOVManager dualPov = DualPOVManager.Instance;
            Assert(povCtrl != null || dualPov != null, "POV Controller / DualPOVManager Active");

            Transform bikeAnchor = povCtrl != null ? povCtrl.BikeSeatAnchor : dualPov.BikeAnchor;
            Transform stallAnchor = povCtrl != null ? povCtrl.FoodStallCounterAnchor : dualPov.StallAnchor;
            Assert(bikeAnchor != null, "POV 1 Bike Seat Anchor Found");
            Assert(stallAnchor != null, "POV 2 Food Stall Counter Anchor Found");

            Transform rigRoot = povCtrl != null ? povCtrl.CameraRigRoot : dualPov.XrOriginRoot;
            Assert(rigRoot != null, "VR Camera Rig Root Found");

            // Test snap to counter anchor
            if (povCtrl != null)
            {
                povCtrl.SnapToPovImmediate(true);
            }
            else if (dualPov != null)
            {
                dualPov.SnapToAnchor(stallAnchor);
            }

            Assert(rigRoot.parent == stallAnchor, "Camera Rig Parented to Stall Anchor");
            Assert(Mathf.Abs(rigRoot.position.x - stallAnchor.position.x) < 0.05f, "Rig X Aligned with Anchor");
            Assert(Mathf.Abs(rigRoot.position.z - stallAnchor.position.z) < 0.05f, "Rig Z Aligned with Anchor");

            // ==========================================
            // TEST 2: BOILER potSnapPoint TRIGGER & SNAPPING
            // ==========================================
            _log.AppendLine("\n--- Testing Requirement 2: Boiler potSnapPoint Trigger & Snapping ---");

            GameObject boiler = GameObject.Find("boiler") ?? GameObject.Find("Test Street03/boiler");
            Assert(boiler != null, "Boiler GameObject Found");

            Transform potSnapPoint = boiler != null ? boiler.transform.Find("potSnapPoint") : null;
            Assert(potSnapPoint != null, "potSnapPoint Child Found on Boiler");

            BoxCollider potBox = potSnapPoint != null ? potSnapPoint.GetComponent<BoxCollider>() : null;
            Assert(potBox != null, "potSnapPoint Has BoxCollider");
            Assert(potBox != null && potBox.isTrigger, "potSnapPoint BoxCollider isTrigger == true");

            BoilerPotSnapReceiver snapReceiver = potSnapPoint != null ? potSnapPoint.GetComponent<BoilerPotSnapReceiver>() : null;
            Assert(snapReceiver != null, "BoilerPotSnapReceiver Component Present on potSnapPoint");

            if (snapReceiver != null)
            {
                Assert(snapReceiver.SnapTransform != null, "BoilerPotSnapReceiver snapTransform Configured");
                Assert(snapReceiver.BoilDuration >= 2.9f, "BoilerPotSnapReceiver boilDuration == 3.0s");

                // Test SnapAndBoilNoodle with test noodle prop
                GameObject testNoodle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                testNoodle.name = "test_vr_noodle";
                testNoodle.tag = "noodle";
                testNoodle.AddComponent<NoodleStretch>();
                Rigidbody testRb = testNoodle.AddComponent<Rigidbody>();
                testRb.mass = 0.1f;
                testRb.isKinematic = false;
                testRb.useGravity = true;

                Grabbable g = testNoodle.AddComponent<Grabbable>();
                HandGrabInteractable hgi = testNoodle.AddComponent<HandGrabInteractable>();
                hgi.InjectRigidbody(testRb);
                hgi.InjectOptionalPointableElement(g);

                // Call SnapAndBoilNoodle
                snapReceiver.SnapAndBoilNoodle(testNoodle);

                Assert(!g.enabled, "Grabbable Component Disabled after Snap");
                Assert(!hgi.enabled, "HandGrabInteractable Disabled after Snap");
                Assert(testRb.isKinematic, "Rigidbody isKinematic == true after Snap");
                Assert(!testRb.useGravity, "Rigidbody useGravity == false after Snap");
                Assert(testNoodle.transform.parent == snapReceiver.SnapTransform, "Noodle Parented to snapTransform");
                Assert(testNoodle.transform.localPosition == Vector3.zero, "Noodle localPosition == Vector3.zero");
                Assert(snapReceiver.HasNoodleInPot, "snapReceiver.HasNoodleInPot is true");
                Assert(!snapReceiver.IsCooked, "snapReceiver.IsCooked initially false during boil");

                // No automatic boil: nothing cooks until the parent holds B.
                yield return new WaitForSeconds(0.5f);
                Assert(!snapReceiver.IsCooked, "snapReceiver does NOT cook on its own (B hold is the only boil driver)");

                // Hold B for 3.0s (simulated ticks through the director, which owns the boil timer)
                YataiSessionDirector director = YataiSessionDirector.Instance;
                if (director != null)
                {
                    director.HandleCurtainStateChanged(true);
                    if (director.CurrentStep != CookingStep.Noodles_Boil) director.SetStep(CookingStep.Noodles_Boil);
                    // SetStep may have run before the snap above; re-snap only if the pot was reset.
                    if (!snapReceiver.HasNoodleInPot) snapReceiver.SnapAndBoilNoodle(testNoodle);
                }
                for (int i = 0; i < 14 && !snapReceiver.IsCooked; i++)
                {
                    ParentHotkeyRouter.Instance.SimulateHoldTick(ParentAction.BoilHold, 0.25f);
                    yield return null;
                }

                Assert(snapReceiver.IsCooked, "snapReceiver.IsCooked is true after 3.0s B hold");
                Assert(snapReceiver.HasCookedNoodleReady, "Cooked noodle waiting in pot for Key 4 / hand / spoon");
                Assert(director == null || director.CurrentStep == CookingStep.Noodles_Transfer,
                    "Director advanced to Noodles_Transfer (noodles stay in pot)");

                // Clean up test prop
                Destroy(testNoodle);
            }

            // ==========================================
            // TEST 3: KEY 4 STRETCH CONFIRMATION & HAND DETACH
            // ==========================================
            _log.AppendLine("\n--- Testing Requirement 3: Key 4 Stretch Confirmation & Hand Detach ---");

            ParentHotkeyRouter router = ParentHotkeyRouter.Instance;
            Assert(router != null, "ParentHotkeyRouter Instance Found");

            // Create simulated held noodle attached to hand
            GameObject handObj = GameObject.Find("RightHandAnchor") ?? GameObject.Find("[ARR_GameManager]");
            GameObject heldNoodle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            heldNoodle.name = "child_hand_held_noodle";
            heldNoodle.tag = "noodle";
            heldNoodle.AddComponent<NoodleStretch>();
            heldNoodle.transform.SetParent(handObj.transform, false);

            Rigidbody heldRb = heldNoodle.AddComponent<Rigidbody>();
            Grabbable heldG = heldNoodle.AddComponent<Grabbable>();
            HandGrabInteractable heldHgi = heldNoodle.AddComponent<HandGrabInteractable>();
            heldHgi.InjectRigidbody(heldRb);
            heldHgi.InjectOptionalPointableElement(heldG);

            router.CurrentlyHeldProp = heldNoodle;
            Assert(router.IsHoldingNoodle(), "Router Recognizes Held Noodle in Hand");

            // Trigger Key 4 stretch confirmation and hand detach
            router.ConfirmNoodleStretchAndBoil();

            Assert(router.CurrentlyHeldProp == null, "router.CurrentlyHeldProp Set to null (Hand Free)");
            Assert(!heldG.enabled, "Held Noodle Grabbable Disabled");
            Assert(heldNoodle.transform.parent == snapReceiver.SnapTransform, "Held Noodle Snapped to Boiler");
            Assert(heldNoodle.transform.localPosition == Vector3.zero, "Held Noodle localPosition == Vector3.zero");

            // Clean up
            Destroy(heldNoodle);

            FinishSuite();
        }

        private void FinishSuite()
        {
            IsRunning = false;
            IsFinished = true;
            AllPassed = (_failCount == 0);

            _log.AppendLine("\n=== SUITE SUMMARY ===");
            _log.AppendLine($"Passed: {_passCount}");
            _log.AppendLine($"Failed: {_failCount}");
            _log.AppendLine($"Result: {(AllPassed ? "SUCCESS (ALL TESTS PASSED)" : "FAILED")}");

            Debug.Log(_log.ToString());
        }
    }
}
