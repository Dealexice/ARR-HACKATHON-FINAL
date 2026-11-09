using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

namespace YataiTogether
{
    /// <summary>
    /// Automated Play Mode Acceptance Test Suite for Step 11 (Broth Ladle & Pour Assembly)
    /// and Test Street03 script compatibility.
    /// </summary>
    public class Step11AcceptanceTester : MonoBehaviour
    {
        private readonly StringBuilder _log = new StringBuilder();
        private int _passed = 0;
        private int _failed = 0;

        private IEnumerator Start()
        {
            _log.AppendLine("=== STARTING STEP 11 PLAY MODE ACCEPTANCE SUITE ===");
            _log.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n");

            yield return new WaitForSeconds(0.4f);

            YataiSessionDirector director = YataiSessionDirector.Instance;
            ParentHotkeyRouter router = ParentHotkeyRouter.Instance;
            RamenAssemblyReceiver bowl = RamenAssemblyReceiver.Instance;
            SpoonBrothScooper brothScooper = SpoonBrothScooper.Instance;
            SpoonScoopController noodleScooper = FindAnyObjectByType<SpoonScoopController>();

            // =====================================================
            // 1. TEST STREET 03 & ALL SCRIPTS COMPATIBILITY CHECK
            // =====================================================
            _log.AppendLine("--- 1. Verifying Test Street03 & Script Compatibility ---");

            GameObject street03 = GameObject.Find("Test Street03");
            Assert(street03 != null, "Test Street03 Root Present in Scene");

            StretchableNoodle[] stretchInstances = FindObjectsByType<StretchableNoodle>();
            Assert(stretchInstances.Length == 1, $"Single Clean StretchableNoodle Instance in Scene (Found {stretchInstances.Length})");
            Assert(StretchableNoodle.Instance != null, "StretchableNoodle.Instance Initialized on [ARR_GameManager]");

            Assert(BikeRideController.Instance != null, "BikeRideController.Instance Initialized");
            Assert(BoilerStation.Instance != null, "BoilerStation.Instance Initialized on Test Street03/boiler");
            Assert(PorkStationController.Instance != null, "PorkStationController.Instance Initialized on Test Street03/meat_bowl");
            Assert(EggStationController.Instance != null, "EggStationController.Instance Initialized on Test Street03/egg_bowl");
            Assert(OnionStationController.Instance != null, "OnionStationController.Instance Initialized on Test Street03/onion_bowl");
            Assert(GoldenNoriController.Instance != null, "GoldenNoriController.Instance Initialized");
            Assert(bowl != null, "RamenAssemblyReceiver.Instance Initialized on Test Street03/lamian_bowl");
            Assert(noodleScooper != null, "SpoonScoopController Present on Test Street03/spoon");
            Assert(brothScooper != null, "SpoonBrothScooper.Instance Initialized on Test Street03/spoon");

            if (director == null || router == null || bowl == null || brothScooper == null)
            {
                FinishSuite();
                yield break;
            }

            // =====================================================
            // 2. SPOON & BROTH POT COMPONENT REQUIREMENTS CHECK
            // =====================================================
            _log.AppendLine("\n--- 2. Verifying 'spoon', 'broth', and 'lamian_bowl' Components ---");

            GameObject spoonGo = brothScooper.gameObject;
            Assert(spoonGo.name == "spoon", "SpoonBrothScooper Attached to 'spoon' GameObject");
            Assert(spoonGo.GetComponent<Rigidbody>() != null, "'spoon' has Rigidbody");
            Assert(spoonGo.GetComponent<Collider>() != null, "'spoon' has Collider");
            Assert(spoonGo.GetComponent<Grabbable>() != null, "'spoon' has Oculus.Interaction.Grabbable");
            Assert(spoonGo.GetComponent<HandGrabInteractable>() != null, "'spoon' has Oculus.Interaction.HandGrab.HandGrabInteractable");

            Assert(brothScooper.SpoonBrothFill != null, "SpoonBrothScooper has spoonBrothFill Assigned");
            Assert(brothScooper.SpoonBrothFill != null && !brothScooper.SpoonBrothFill.activeSelf, "spoonBrothFill Initially Inactive");
            Assert(!brothScooper.HasBroth, "SpoonBrothScooper._hasBroth Initially False");

            GameObject brothPotGo = brothScooper.BrothPotObject;
            Assert(brothPotGo != null && brothPotGo.name == "broth", "Broth Pot ('broth') Referenced");
            Collider brothPotCollider = brothPotGo != null ? brothPotGo.GetComponent<Collider>() : null;
            Assert(brothPotCollider != null && brothPotCollider.isTrigger, "'broth' Pot has Trigger Collider (isTrigger = true)");

            Assert(bowl.ChildBroth != null && bowl.ChildBroth.name == "lamian_broth", "'lamian_bowl' has Child 'lamian_broth'");
            Assert(bowl.ChildBroth != null && !bowl.ChildBroth.activeSelf, "'lamian_broth' Initially Inactive");

            // =====================================================
            // 3. ADVANCE TO STEP 11 & OUT-OF-ORDER REJECTION
            // =====================================================
            _log.AppendLine("\n--- 3. Advancing to Step 11 & Testing Out-of-Order Guardrails ---");

            director.HandleCurtainStateChanged(true);
            bowl.AddNoodles();
            bowl.AddMeat();
            bowl.AddEgg();
            bowl.AddOnions();
            bowl.AddNori();

            Assert(director.CurrentStep == CookingStep.Broth_Ladle, "Director Reached CookingStep.Broth_Ladle After Toppings & Nori");

            // Out-of-order Key D before grabbing ladle or scooping broth
            InvokeTap(router, ParentAction.PourBroth);
            yield return null;
            Assert(director.CurrentStep == CookingStep.Broth_Ladle, "Out-of-Order Key D Rejected During Broth_Ladle (Before Key 5 / Scoop)");
            Assert(!bowl.HasBroth, "lamian_broth Remains Inactive When Out-of-Order Key D Rejected");

            // Out-of-order Key S before broth added
            InvokeTap(router, ParentAction.ServeCustomer);
            yield return null;
            Assert(director.CurrentStep == CookingStep.Broth_Ladle, "Out-of-Order Key S (Serve) Rejected Before Broth Added");

            // =====================================================
            // 4. COLLIDER-BASED PHYSICAL SCOOP & POUR (REQ 1)
            // =====================================================
            _log.AppendLine("\n--- 4. Testing Collider-Based Broth Scoop & Bowl Pour ---");

            Collider bowlCol = bowl.GetComponent<Collider>();
            MethodInfo onTriggerEnterMethod = typeof(SpoonBrothScooper).GetMethod("OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic);

            // 4a. Touching "lamian_bowl" when _hasBroth == false -> Reject / do nothing
            onTriggerEnterMethod?.Invoke(brothScooper, new object[] { bowlCol });
            yield return null;
            Assert(!brothScooper.HasBroth, "Empty Spoon Touching lamian_bowl: _hasBroth Remains False");
            Assert(!bowl.HasBroth, "Empty Spoon Touching lamian_bowl Rejected: lamian_broth Remains Inactive");
            Assert(director.CurrentStep == CookingStep.Broth_Ladle, "Empty Spoon Touching lamian_bowl: State Remains Broth_Ladle");

            // 4b. Touching "broth" pot -> _hasBroth = true, spoonBrothFill.SetActive(true)
            onTriggerEnterMethod?.Invoke(brothScooper, new object[] { brothPotCollider });
            yield return null;
            Assert(brothScooper.HasBroth, "Spoon Touching 'broth' Pot Sets _hasBroth = true");
            Assert(brothScooper.SpoonBrothFill != null && brothScooper.SpoonBrothFill.activeSelf, "Spoon Touching 'broth' Pot Activates spoonBrothFill");
            Assert(director.CurrentStep == CookingStep.Broth_Pour, "Director Advances to CookingStep.Broth_Pour on Broth Scoop");

            // 4c. Touching "lamian_bowl" when _hasBroth == true -> Pour completes
            onTriggerEnterMethod?.Invoke(brothScooper, new object[] { bowlCol });
            yield return null;
            Assert(bowl.HasBroth && bowl.ChildBroth.activeSelf, "Spoon with Broth Touching lamian_bowl Activates Child 'lamian_broth'");
            Assert(brothScooper.SpoonBrothFill != null && !brothScooper.SpoonBrothFill.activeSelf, "Spoon with Broth Touching lamian_bowl Disables spoonBrothFill");
            Assert(!brothScooper.HasBroth, "Spoon with Broth Touching lamian_bowl Resets _hasBroth = false");
            Assert(director.CurrentStep == CookingStep.ReadyToServe, "Director Advances to CookingStep.ReadyToServe After Collider Pour");
            Assert(bowl.IsBowlComplete, "RamenAssemblyReceiver Reports IsBowlComplete == true");

            // =====================================================
            // 5. PARENT HOTKEY FALLBACK (KEY 5 -> KEY D, REQ 2)
            // =====================================================
            _log.AppendLine("\n--- 5. Testing Parent Hotkey Fallback (Key 5 -> Key D) ---");

            // Reset broth state to test Key 5 -> Key D hotkey sequence
            if (bowl.ChildBroth != null) bowl.ChildBroth.SetActive(false);
            brothScooper.ResetForNextCustomer();
            director.SetStep(CookingStep.Broth_Ladle);
            yield return null;

            Assert(!bowl.HasBroth && director.CurrentStep == CookingStep.Broth_Ladle, "State Reset to Broth_Ladle for Hotkey Fallback Test");

            // Tap Key 5 (GrabLadle)
            InvokeTap(router, ParentAction.GrabLadle);
            yield return null;
            Assert(brothScooper.IsLadleInHand, "Key 5 Snaps 'spoon' to Child Virtual Hand Attach Point (IsLadleInHand == true)");
            Assert(director.CurrentStep == CookingStep.Broth_Pour, "Key 5 Advances Director to CookingStep.Broth_Pour");

            // Tap Key D (PourBroth) -> Tilts spoon 90° and completes pour into lamian_bowl
            InvokeTap(router, ParentAction.PourBroth);
            yield return null;
            Assert(brothScooper.IsPouring, "Key D Initiates 90-Degree Spoon Tilt Pour Animation");
            Assert(bowl.HasBroth && bowl.ChildBroth.activeSelf, "Key D Activates Child 'lamian_broth' Inside lamian_bowl");
            Assert(brothScooper.SpoonBrothFill != null && !brothScooper.SpoonBrothFill.activeSelf, "Key D Disables spoonBrothFill After Pour");
            Assert(!brothScooper.HasBroth, "Key D Resets _hasBroth = false");
            Assert(director.CurrentStep == CookingStep.ReadyToServe, "Key D Advances Director to CookingStep.ReadyToServe");

            // Wait for 90-degree tilt coroutine to finish and return spoon to rest pose
            yield return new WaitForSeconds(0.55f);
            Assert(!brothScooper.IsPouring, "Spoon 90-Degree Tilt Animation Completed Cleanly and Returned to Rest Pose");
            Assert(director.NextExpectedPrompt.Contains("[S]"), "HUD NextExpectedPrompt Displays [S] Serve Prompt");

            FinishSuite();
        }

        private static void InvokeTap(ParentHotkeyRouter router, ParentAction action)
        {
            if (router != null) router.SimulateTap(action);
        }

        private void Assert(bool condition, string testName)
        {
            if (condition)
            {
                _passed++;
                _log.AppendLine($"[PASS] {testName}");
            }
            else
            {
                _failed++;
                _log.AppendLine($"[FAIL] {testName}");
                Debug.LogError($"[Step11AcceptanceTester] FAIL: {testName}");
            }
        }

        private void FinishSuite()
        {
            _log.AppendLine($"\n=== SUITE COMPLETE: {_passed} PASSED, {_failed} FAILED ===");
            string outputPath = Path.Combine(Application.dataPath, "../Temp/step11_acceptance_results.txt");
            File.WriteAllText(outputPath, _log.ToString());
            Debug.Log(_log.ToString());
        }
    }
}
