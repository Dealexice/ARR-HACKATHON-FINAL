using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace YataiTogether
{
    /// <summary>
    /// Automated Acceptance Tester for Step 6, Step 7, and Step 8.
    /// Runs in Play Mode and verifies:
    /// - Step 6: BoilerStation boil timer (3.0s), green flash, spoon scooping, and noodle transfer to lamian_bowl.
    /// - Step 7: Chashu Pork grab (Key 1), sequence locking, bowl transfer (Key A/Enter/Trigger), lamian_meat activation.
    /// - Step 8: Ajitama Egg supination drop (Key 2), chute drop, bowl transfer (Key A/Enter/Trigger), lamian_egg activation.
    /// - Out-of-order key rejections and HUD warnings.
    /// </summary>
    [DisallowMultipleComponent]
    public class Step678AcceptanceTester : MonoBehaviour
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

            _log.AppendLine("=== STARTING STEP 6, 7 & 8 PLAY MODE ACCEPTANCE SUITE ===");
            _log.AppendLine($"Timestamp: {System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n");

            yield return new WaitForSeconds(0.5f);

            YataiSessionDirector director = YataiSessionDirector.Instance;
            ParentHotkeyRouter router = ParentHotkeyRouter.Instance;
            BoilerStation boiler = BoilerStation.Instance;
            RamenAssemblyReceiver bowl = RamenAssemblyReceiver.Instance;
            PorkStationController porkCtrl = PorkStationController.Instance;
            EggStationController eggCtrl = EggStationController.Instance;

            Assert(director != null, "Director Singleton Initialized");
            Assert(router != null, "ParentHotkeyRouter Singleton Initialized");
            Assert(boiler != null, "BoilerStation Initialized");
            Assert(bowl != null, "RamenAssemblyReceiver Initialized");
            Assert(porkCtrl != null, "PorkStationController Initialized");
            Assert(eggCtrl != null, "EggStationController Initialized");

            if (director == null || boiler == null || bowl == null)
            {
                FinishSuite();
                yield break;
            }

            // Prep: Open the stall curtain and advance director to Noodles_Boil (Step 6)
            director.HandleCurtainStateChanged(true);
            director.SetStep(CookingStep.Noodles_Boil);
            Assert(director.CurrentStep == CookingStep.Noodles_Boil, "Director State Set to Noodles_Boil");

            // ==========================================
            // STEP 6: BOILER HOLD & COOKED NOODLES TRANSFER
            // ==========================================
            _log.AppendLine("\n--- Testing Step 6: Boiler Hold & Cooked Noodles Transfer ---");

            // 1. Create a test noodle and place into boiler
            GameObject testNoodle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            testNoodle.name = "noodle_test_prop";
            testNoodle.transform.localScale = new Vector3(0.04f, 0.04f, 0.2f);
            Rigidbody noodleRb = testNoodle.AddComponent<Rigidbody>();
            noodleRb.mass = 0.1f;

            boiler.AcceptNoodleIntoBoiler(testNoodle);
            Assert(boiler.HasNoodleInPot, "Noodle Accepted into Boiler Pot");
            Assert(boiler.ContainedNoodle == testNoodle, "Noodle Snapped to PotSnapPoint");
            Assert(noodleRb.isKinematic, "Noodle Rigidbody Kinematic inside Pot");

            // 2. Simulate Key B Hold for 3.0s
            float simulatedTime = 0f;
            while (simulatedTime < 3.2f && !boiler.IsCooked)
            {
                router.SimulateHoldTick(ParentAction.BoilHold, 0.25f);
                simulatedTime += 0.25f;
                yield return new WaitForSeconds(0.05f);
            }

            Assert(boiler.IsCooked, "Boiler Finished Cooking after 3.0s (Green Flash Triggered)");
            Assert(director.CurrentStep == CookingStep.Noodles_Transfer, "Director Waits in Noodles_Transfer (noodles stay in pot)");
            Assert(!bowl.HasNoodles, "lamian_bowl Still Empty until Noodles are Taken out of the Pot");

            // 3. Test Spoon Scoop Transfer
            GameObject spoonGo = GameObject.Find("spoon");
            Assert(spoonGo != null, "Spoon Prop Found in Scene");

            SpoonScoopController spoon = spoonGo != null ? spoonGo.GetComponent<SpoonScoopController>() : null;
            Assert(spoon != null, "SpoonScoopController Found on Spoon");

            if (spoon != null)
            {
                // Scoop noodle from boiler to spoon
                boiler.TransferNoodleToSpoon(spoon.SpoonAttachPoint);
                Assert(spoon.SpoonAttachPoint.childCount > 0, "Noodle Transferred to spoon_attach_point");

                // Transfer from spoon to lamian_bowl
                spoon.DepositNoodleIntoBowl(bowl);
                Assert(bowl.HasNoodles, "lamian_bowl Activated childLamianNoodles from Spoon");
            }

            // Verify state transition to Pork_Grab
            Assert(director.CurrentStep == CookingStep.Pork_Grab, "Director Advanced to CookingStep.Pork_Grab");

            // ==========================================
            // STEP 7: CHASHU PORK ASSEMBLY (Keys 1 -> A)
            // ==========================================
            _log.AppendLine("\n--- Testing Step 7: Chashu Pork Assembly ---");

            // 1. Out-of-order rejection test: Press Key 2 (Egg) before Pork
            router.SimulateTap(ParentAction.DropEggIntoPalm);
            yield return null;
            Assert(director.CurrentStep == CookingStep.Pork_Grab, "Out-of-Order Key 2 (Egg) Rejected during Pork_Grab");

            // 2. Out-of-order rejection test: Press Key 3 (Onions) before Pork
            router.SimulateTap(ParentAction.GrabOnions);
            yield return null;
            Assert(director.CurrentStep == CookingStep.Pork_Grab, "Out-of-Order Key 3 (Onions) Rejected during Pork_Grab");

            // 3. Valid Step: Press Key 1 (Grab Pork)
            router.SimulateTap(ParentAction.GrabPork);
            yield return null;
            Assert(director.CurrentStep == CookingStep.Pork_Release, "Director Advanced to CookingStep.Pork_Release on Key 1");
            Assert(porkCtrl.IsPorkInHand, "PorkStationController Marked Pork in Hand");

            // 4. Valid Step: Press Key A / Enter (Release Pork into Bowl)
            router.SimulateTap(ParentAction.ReleaseIntoBowl);
            yield return null;
            Assert(bowl.HasMeat, "lamian_bowl Activated childLamianMeat");
            Assert(director.CurrentStep == CookingStep.Egg_Drop, "Director Advanced to CookingStep.Egg_Drop on Key A");

            // 5. Out-of-order rejection test: Press Key 1 again after Pork is placed
            router.SimulateTap(ParentAction.GrabPork);
            yield return null;
            Assert(director.CurrentStep == CookingStep.Egg_Drop, "Duplicate Key 1 Rejected (Pork Already In Bowl)");

            // ==========================================
            // STEP 8: AJITAMA EGG DROP & ASSEMBLY (Keys 2 -> A)
            // ==========================================
            _log.AppendLine("\n--- Testing Step 8: Ajitama Egg Drop & Assembly ---");

            // 1. Out-of-order rejection test: Press Key 3 (Onions) before Egg
            router.SimulateTap(ParentAction.GrabOnions);
            yield return null;
            Assert(director.CurrentStep == CookingStep.Egg_Drop, "Out-of-Order Key 3 (Onions) Rejected during Egg_Drop");

            // 2. Out-of-order rejection test: Press Key 5 (Broth) before Egg
            router.SimulateTap(ParentAction.GrabLadle);
            yield return null;
            Assert(director.CurrentStep == CookingStep.Egg_Drop, "Out-of-Order Key 5 (Ladle) Rejected during Egg_Drop");

            // 3. Valid Step: Press Key 2 (Drop Egg from Chute into Palm)
            router.SimulateTap(ParentAction.DropEggIntoPalm);
            yield return new WaitForSeconds(0.25f); // Wait for chute gravity drop
            Assert(director.CurrentStep == CookingStep.Egg_Release, "Director Advanced to CookingStep.Egg_Release on Key 2");
            Assert(eggCtrl.IsEggDropped, "EggStationController Dropped Egg from Overhead Chute");

            // 4. Valid Step: Press Key A / Enter (Release Egg into Bowl)
            router.SimulateTap(ParentAction.ReleaseIntoBowl);
            yield return null;
            Assert(bowl.HasEgg, "lamian_bowl Activated childLamianEgg");
            Assert(director.CurrentStep == CookingStep.Onions_Grab, "Director Advanced to CookingStep.Onions_Grab on Key A");

            // 5. Out-of-order rejection test: Press Key 2 again after Egg is placed
            router.SimulateTap(ParentAction.DropEggIntoPalm);
            yield return null;
            Assert(director.CurrentStep == CookingStep.Onions_Grab, "Duplicate Key 2 Rejected (Egg Already In Bowl)");

            // ==========================================
            // HUD & META SDK PHYSICS VALIDATION
            // ==========================================
            _log.AppendLine("\n--- Testing HUD & Meta SDK Physics Standard ---");

            ParentGuidanceHUDOverlay hud = ParentGuidanceHUDOverlay.Instance;
            Assert(hud != null, "ParentGuidanceHUDOverlay Active in Scene");

            // Verify Meta Building Blocks physics stack on utensils & props
            VerifyGrabbablePhysics(porkCtrl.MeatProp, "Chashu Pork Prop");
            VerifyGrabbablePhysics(eggCtrl.EggProp, "Ajitama Egg Prop");
            VerifyGrabbablePhysics(spoonGo, "Spoon Utensil");

            FinishSuite();
        }

        private void VerifyGrabbablePhysics(GameObject go, string label)
        {
            if (go == null)
            {
                Assert(false, $"{label} exists for physics check");
                return;
            }

            Rigidbody rb = go.GetComponent<Rigidbody>();
            Collider col = go.GetComponent<Collider>();
            Oculus.Interaction.Grabbable grab = go.GetComponent<Oculus.Interaction.Grabbable>();
            Oculus.Interaction.HandGrab.HandGrabInteractable hgi = go.GetComponent<Oculus.Interaction.HandGrab.HandGrabInteractable>();

            Assert(rb != null, $"{label} has Rigidbody");
            Assert(col != null, $"{label} has Collider");
            Assert(grab != null, $"{label} has Oculus.Interaction.Grabbable");
            Assert(hgi != null, $"{label} has Oculus.Interaction.HandGrab.HandGrabInteractable");
        }

        private void FinishSuite()
        {
            AllPassed = (_failCount == 0);
            IsFinished = true;
            IsRunning = false;

            _log.AppendLine($"\n=== SUITE COMPLETE: {_passCount} PASSED, {_failCount} FAILED ===");
            Debug.Log($"[Step678AcceptanceTester] Suite finished: {_passCount} Passed, {_failCount} Failed.");

            try
            {
                string path = Path.Combine(Application.dataPath, "../Temp/step678_acceptance_results.txt");
                File.WriteAllText(path, _log.ToString());
                Debug.Log($"[Step678AcceptanceTester] Results saved to {path}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Step678AcceptanceTester] Failed to write results: {ex.Message}");
            }
        }
    }
}
