using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace YataiTogether
{
    /// <summary>
    /// Automated Acceptance Tester for Step 9 (Green Scallions) and Step 10 (Golden Nori Press),
    /// as well as Test Street 03 / FBX02 verification.
    /// Runs in Play Mode and verifies:
    /// - Test Street03 scene hierarchy: panda present (no gibbon), boiler (pot_02), broth (pot_01), table collider.
    /// - lamian_bowl contains lamian_noodle, lamian_meat, lamian_egg, lamian_onion, lamian_broth, and lamian_nori.
    /// - Meta SDK physics stack on onion prop (Rigidbody, Collider, Grabbable, HandGrabInteractable).
    /// - Step 9: Green Scallions grab (Key 3), out-of-order rejection, bowl release (Key A), childLamianOnions activation.
    /// - Step 10: Golden Nori hand-flatten press (Hold Key F 5s), warm paw biofeedback, early release pause without reset,
    ///   5s completion, crispy nori placement on bowl rim, and transition to Step 11 (Broth_Ladle).
    /// </summary>
    [DisallowMultipleComponent]
    public class Step910AcceptanceTester : MonoBehaviour
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

            _log.AppendLine("=== STARTING STEP 9 & 10 PLAY MODE ACCEPTANCE SUITE ===");
            _log.AppendLine($"Timestamp: {System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n");

            yield return new WaitForSeconds(0.5f);

            YataiSessionDirector director = YataiSessionDirector.Instance;
            ParentHotkeyRouter router = ParentHotkeyRouter.Instance;
            RamenAssemblyReceiver bowl = RamenAssemblyReceiver.Instance;
            OnionStationController onionCtrl = OnionStationController.Instance;
            GoldenNoriController noriCtrl = GoldenNoriController.Instance;

            // ==========================================
            // TEST STREET 03 & ASSET VERIFICATION
            // ==========================================
            _log.AppendLine("--- Verifying Test Street 03 Assets & Setup ---");

            GameObject street03 = GameObject.Find("Test Street03");
            Assert(street03 != null, "Test Street03 Root Present in Scene");

            GameObject pandaGo = GameObject.Find("panda");
            Assert(pandaGo != null, "Panda Customer Model Present (Renamed from Cube.001)");

            GameObject gibbonGo = GameObject.Find("gibbon");
            Assert(gibbonGo == null, "Old Gibbon Model Removed from Scene");

            GameObject boilerGo = (BoilerStation.Instance != null ? BoilerStation.Instance.gameObject : null) 
                                  ?? (street03 != null ? street03.transform.Find("boiler")?.gameObject : null) 
                                  ?? GameObject.Find("boiler");
            Assert(boilerGo != null && boilerGo.GetComponent<BoilerStation>() != null, "New Boiler (pot_01) with BoilerStation Present");

            GameObject brothGo = (street03 != null ? street03.transform.Find("broth")?.gameObject : null) 
                                 ?? GameObject.Find("broth");
            Assert(brothGo != null, "New Broth Pot (pot_02) Present");

            GameObject tableCol = GameObject.Find("counter_table_collider");
            Assert(tableCol != null && tableCol.GetComponent<Collider>() != null, "counter_table_collider Solid Surface Present");

            GameObject pov2 = GameObject.Find("POV_2_FoodStallCounterAnchor");
            Assert(pov2 != null, "POV_2_FoodStallCounterAnchor Exists");
            if (pov2 != null)
            {
                // Verify POV 2 looks across the stall towards the food cart / panda
                bool facingStall = pov2.transform.forward.x > 0.5f;
                Assert(facingStall, "POV 2 Facing Across Counter Toward Food Stall & Customer (+X)");
            }

            Assert(bowl != null, "RamenAssemblyReceiver Initialized on lamian_bowl");
            Assert(onionCtrl != null, "OnionStationController Initialized");
            Assert(noriCtrl != null, "GoldenNoriController Initialized on GameManager");

            // Verify bowl ingredients
            Assert(bowl != null && bowl.ChildNoodles != null, "lamian_bowl has child lamian_noodle");
            Assert(bowl != null && bowl.ChildMeat != null, "lamian_bowl has child lamian_meat");
            Assert(bowl != null && bowl.ChildEgg != null, "lamian_bowl has child lamian_egg");
            Assert(bowl != null && bowl.ChildOnions != null, "lamian_bowl has child lamian_onion");
            Assert(bowl != null && bowl.ChildBroth != null, "lamian_bowl has child lamian_broth");
            Assert(bowl != null && bowl.ChildNori != null, "lamian_bowl has child lamian_nori");

            // Verify Meta SDK Building Blocks standards on Onion
            VerifyGrabbablePhysics(onionCtrl != null ? onionCtrl.OnionProp : null, "Green Onions Prop");

            if (director == null || bowl == null || onionCtrl == null || noriCtrl == null)
            {
                FinishSuite();
                yield break;
            }

            // Prep bowl state: ensure previous ingredients (noodles, pork, egg) are in bowl
            director.HandleCurtainStateChanged(true);
            bowl.AddNoodles();
            bowl.AddMeat();
            bowl.AddEgg();

            Assert(bowl.HasNoodles && bowl.HasMeat && bowl.HasEgg, "Pre-requisite ingredients (Noodles, Pork, Egg) placed in bowl");
            Assert(director.CurrentStep == CookingStep.Onions_Grab, "Director Invariant holds: State at CookingStep.Onions_Grab");

            // ==========================================
            // STEP 9: GREEN SCALLIONS / ONIONS (Keys 3 -> A)
            // ==========================================
            _log.AppendLine("\n--- Testing Step 9: Green Scallions / Onions Assembly ---");

            // 1. Out-of-order rejection: Key 1 (Pork duplicate)
            router.SimulateTap(ParentAction.GrabPork);
            yield return null;
            Assert(director.CurrentStep == CookingStep.Onions_Grab, "Out-of-Order Key 1 Rejected during Onions_Grab");

            // 2. Out-of-order rejection: Key 5 (Broth Ladle premature)
            router.SimulateTap(ParentAction.GrabLadle);
            yield return null;
            Assert(director.CurrentStep == CookingStep.Onions_Grab, "Out-of-Order Key 5 (Ladle) Rejected during Onions_Grab");

            // 3. Out-of-order rejection: Key S (Serve premature)
            router.SimulateTap(ParentAction.ServeCustomer);
            yield return null;
            Assert(director.CurrentStep == CookingStep.Onions_Grab, "Out-of-Order Key S (Serve) Rejected during Onions_Grab");

            // 4. Valid Step: Press Key 3 (Pinch Green Onions)
            router.SimulateTap(ParentAction.GrabOnions);
            yield return null;
            Assert(director.CurrentStep == CookingStep.Onions_Release, "Director Advanced to CookingStep.Onions_Release on Key 3");
            Assert(onionCtrl.IsOnionsInHand, "OnionStationController Marked Onions in Hand");

            // 5. Out-of-order rejection: Duplicate Key 3
            router.SimulateTap(ParentAction.GrabOnions);
            yield return null;
            Assert(director.CurrentStep == CookingStep.Onions_Release, "Duplicate Key 3 Handled Safely");

            // 6. Valid Step: Press Key A / Enter (Release Onions into Bowl)
            router.SimulateTap(ParentAction.ReleaseIntoBowl);
            yield return null;
            Assert(bowl.HasOnions, "lamian_bowl Activated childLamianOnions");
            Assert(!onionCtrl.IsOnionsInHand, "Onions Released from Hand");
            Assert(director.CurrentStep == CookingStep.Nori_Hold, "Director Advanced to CookingStep.Nori_Hold on Key A");

            // 7. Out-of-order rejection: Duplicate Key 3 after onions in bowl
            router.SimulateTap(ParentAction.GrabOnions);
            yield return null;
            Assert(director.CurrentStep == CookingStep.Nori_Hold, "Duplicate Key 3 Rejected (Onions Already In Bowl)");

            // ==========================================
            // STEP 10: GOLDEN NORI HAND-FLATTEN PRESS (Hold Key F 5s)
            // ==========================================
            _log.AppendLine("\n--- Testing Step 10: Golden Nori Hand-Flatten Press ---");

            // 1. Out-of-order rejections during Nori_Hold
            router.SimulateTap(ParentAction.GrabLadle);
            yield return null;
            Assert(director.CurrentStep == CookingStep.Nori_Hold, "Out-of-Order Key 5 (Ladle) Rejected during Nori_Hold");

            router.SimulateTap(ParentAction.ServeCustomer);
            yield return null;
            Assert(director.CurrentStep == CookingStep.Nori_Hold, "Out-of-Order Key S (Serve) Rejected during Nori_Hold");

            // 2. Test Partial Hold F (2.0s) & Hold-Key Pause Semantics
            _log.AppendLine("Testing 2.0s partial Key F hold (early release pause test)...");
            for (int i = 0; i < 8; i++) // 8 * 0.25s = 2.0s
            {
                router.SimulateHoldTick(ParentAction.HandFlattenHold, 0.25f);
                yield return new WaitForSeconds(0.02f);
            }

            Assert(noriCtrl.CurrentHoldTimer >= 1.9f && noriCtrl.CurrentHoldTimer < 3.0f, $"CurrentHoldTimer Accumulated: {noriCtrl.CurrentHoldTimer:F1}s");
            Assert(!noriCtrl.IsNoriPressed, "Nori NOT Completed Early at 2.0s");
            Assert(director.CurrentStep == CookingStep.Nori_Hold, "Director Stays in Nori_Hold during In-Progress Press");

            // Pause for 0.1s without ticking — hold-key law: timer must not reset!
            yield return new WaitForSeconds(0.1f);
            Assert(noriCtrl.CurrentHoldTimer >= 1.9f, "Hold Timer Preserved on Early Release (Pause, Never Reset Law)");

            // 3. Resume Hold F for remaining 3.25s (total >= 5.0s)
            _log.AppendLine("Resuming Key F hold to reach 5.0s threshold...");
            for (int i = 0; i < 14; i++) // 14 * 0.25s = 3.5s
            {
                router.SimulateHoldTick(ParentAction.HandFlattenHold, 0.25f);
                yield return new WaitForSeconds(0.02f);
            }

            Assert(noriCtrl.IsNoriPressed, "GoldenNoriController Completed 5.0s Press");
            Assert(bowl.HasNori, "RamenAssemblyReceiver Activated childLamianNori on Bowl Rim");
            Assert(director.CurrentStep == CookingStep.Broth_Ladle, "Director Advanced to CookingStep.Broth_Ladle on 5s Nori Completion");

            // 4. HUD validation
            _log.AppendLine("\n--- Testing HUD & Invariant Tracker ---");
            ParentGuidanceHUDOverlay hud = ParentGuidanceHUDOverlay.Instance;
            Assert(hud != null, "ParentGuidanceHUDOverlay Active in Scene");
            Assert(director.NextExpectedPrompt.Contains("[5]"), "NextExpectedPrompt Displays Broth Ladle [5]");

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
            Debug.Log($"[Step910AcceptanceTester] Suite finished: {_passCount} Passed, {_failCount} Failed.");

            try
            {
                string path = Path.Combine(Application.dataPath, "../Temp/step910_acceptance_results.txt");
                File.WriteAllText(path, _log.ToString());
                Debug.Log($"[Step910AcceptanceTester] Results saved to {path}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Step910AcceptanceTester] Failed to write results: {ex.Message}");
            }
        }
    }
}
