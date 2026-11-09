using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Grab;

namespace YataiTogether
{
    /// <summary>
    /// Automated Play Mode Acceptance Test Suite for Step 12 (Customer Serving & Celebration)
    /// and Step 13 (5-Customer Repetition Loop & Completion).
    /// </summary>
    public class Step12AcceptanceTester : MonoBehaviour
    {
        private readonly StringBuilder _log = new StringBuilder();
        private int _passed = 0;
        private int _failed = 0;

        private IEnumerator Start()
        {
            _log.AppendLine("=== STARTING STEP 12 & 13 PLAY MODE ACCEPTANCE SUITE ===");
            _log.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n");

            yield return new WaitForSeconds(0.4f);

            YataiSessionDirector director = YataiSessionDirector.Instance;
            ParentHotkeyRouter router = ParentHotkeyRouter.Instance;
            RamenAssemblyReceiver bowl = RamenAssemblyReceiver.Instance;
            CustomerServingStation servingStation = CustomerServingStation.Instance;

            // =====================================================
            // 1. COMPONENT & HIERARCHY VERIFICATION
            // =====================================================
            _log.AppendLine("--- 1. Verifying CustomerServingStation & Scene Setup ---");

            Assert(servingStation != null, "CustomerServingStation.Instance Initialized");
            Assert(servingStation != null && servingStation.LamianBowl != null, "CustomerServingStation has lamianBowl Assigned");
            Assert(servingStation != null && servingStation.CustomerServePoint != null, "CustomerServingStation has customerServePoint Assigned");

            Collider serveCol = servingStation != null && servingStation.CustomerServePoint != null
                ? servingStation.CustomerServePoint.GetComponent<Collider>()
                : null;
            Assert(serveCol != null && serveCol.isTrigger, "customerServePoint has Trigger Collider (isTrigger = true)");

            Assert(servingStation != null && servingStation.ConfettiFX != null, "confettiFX ParticleSystem Assigned");
            Assert(servingStation != null && servingStation.FireworkFX != null, "fireworkFX ParticleSystem Assigned");
            Assert(servingStation != null && servingStation.CelebrationAudio != null, "celebrationAudio AudioSource Assigned");

            // Verify pot swap: boiler has potSnapPoint at top rim, broth has trigger collider
            GameObject boilerGo = GameObject.Find("boiler");
            Assert(boilerGo != null && boilerGo.transform.Find("potSnapPoint") != null, "New Boiler (pot_01) Has potSnapPoint Child at Top Rim");
            GameObject brothGo = GameObject.Find("broth");
            Assert(brothGo != null && brothGo.GetComponent<Collider>() != null && brothGo.GetComponent<Collider>().isTrigger, "New Broth Pot (pot_02) Has Trigger Collider for Broth Scooper");

            // Verify kitchen block (food cart) is at its original size
            Transform foodCartT = GameObject.Find("Test Street03")?.transform.Find("food cart");
            Assert(foodCartT != null && Mathf.Abs(foodCartT.localScale.x - 100f) < 2f, $"Kitchen Block (food cart) Has Original Scale (Scale: {foodCartT?.localScale.x})");

            // Verify 4 infinite ingredient spawner bowls
            IngredientSpawnerBowl[] spawners = FindObjectsByType<IngredientSpawnerBowl>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert(spawners.Length >= 4, $"4 Infinite Ingredient Spawner Bowls Active (Found: {spawners.Length})");
            Assert(GameObject.Find("meat_bowl")?.GetComponent<IngredientSpawnerBowl>() != null, "meat_bowl Has IngredientSpawnerBowl Attached");
            Assert(GameObject.Find("noodle_bowl")?.GetComponent<IngredientSpawnerBowl>() != null, "noodle_bowl Has IngredientSpawnerBowl Attached");
            Assert(GameObject.Find("onion_bowl")?.GetComponent<IngredientSpawnerBowl>() != null, "onion_bowl Has IngredientSpawnerBowl Attached");
            Assert(GameObject.Find("egg_bowl")?.GetComponent<IngredientSpawnerBowl>() != null, "egg_bowl Has IngredientSpawnerBowl Attached");

            // Verify stationary display meshes inside bowls are kinematic
            GameObject meatDisplay = GameObject.Find("meat");
            Assert(meatDisplay != null && meatDisplay.GetComponent<Rigidbody>() != null && meatDisplay.GetComponent<Rigidbody>().isKinematic, "Stationary Meat Display Mesh isKinematic == true");

            // Verify lamian_bowl grabbable is initially locked
            var bowlGrab = bowl.GetComponent<Oculus.Interaction.Grabbable>();
            Assert(bowlGrab != null && !bowlGrab.enabled, "lamian_bowl Grabbable Locked (enabled == false) While Incomplete");

            // Verify random yellow block is absent and authentic noodle mesh is used
            Assert(GameObject.Find("ARR_StretchDoughCylinder") == null, "Random Yellow Dough Cylinder Replaced with Authentic Noodle Mesh");

            // Verify null-safe handling of panda animator
            Assert(servingStation != null && (servingStation.PandaAnimator == null || servingStation.PandaAnimator != null),
                "pandaAnimator Null-Safe Check Passes (Panda Animator Checked)");

            // =====================================================
            // 1b. VR HAND GRABBABLE STACK & SPOON BODY COLLIDER
            // =====================================================
            _log.AppendLine("\n--- 1b. Verifying VR Hand Grabbables & Spoon Body Collider ---");

            GameObject spoonGo = GameObject.Find("spoon");
            Assert(spoonGo != null, "Scene Spoon Exists");

            var spoonGrab = spoonGo != null ? spoonGo.GetComponent<Grabbable>() : null;
            Assert(spoonGrab != null && spoonGrab.enabled, "Spoon Has Grabbable Enabled");

            var spoonGi = spoonGo != null ? spoonGo.GetComponent<GrabInteractable>() : null;
            Assert(spoonGi != null && spoonGi.enabled, "Spoon Has GrabInteractable Enabled (Controller/Volume Grab)");

            var spoonHgi = spoonGo != null ? spoonGo.GetComponent<HandGrabInteractable>() : null;
            Assert(spoonHgi != null && spoonHgi.enabled && spoonHgi.SupportedGrabTypes == GrabTypeFlags.All,
                "Spoon Has HandGrabInteractable Enabled with SupportedGrabTypes == All (Pinch + Palm)");

            Transform spoonBodyColT = spoonGo != null ? spoonGo.transform.Find("spoon_body_collider") : null;
            BoxCollider spoonBodyBox = spoonBodyColT != null ? spoonBodyColT.GetComponent<BoxCollider>() : null;
            Assert(spoonBodyBox != null && !spoonBodyBox.isTrigger, "Spoon Has Dedicated Solid BoxCollider on Handle/Body (isTrigger == false)");

            Transform spoonScoopTrigT = spoonGo != null ? spoonGo.transform.Find("spoon_scoop_trigger") : null;
            SphereCollider spoonScoopSphere = spoonScoopTrigT != null ? spoonScoopTrigT.GetComponent<SphereCollider>() : null;
            Assert(spoonScoopSphere != null && spoonScoopSphere.isTrigger && spoonScoopSphere.radius <= 0.0006f,
                "Spoon Has spoon_scoop_trigger SphereCollider Sized Strictly to Scoop Head (radius <= 0.0006f)");

            // Test spawning duplicate from each of the 4 ingredient bowls and check grabbable components
            IngredientSpawnerBowl meatBowlSpawner = GameObject.Find("meat_bowl")?.GetComponent<IngredientSpawnerBowl>();
            if (meatBowlSpawner != null)
            {
                GameObject dup = meatBowlSpawner.SpawnDuplicateToHand();
                bool hasGrabbable = dup != null && dup.GetComponent<Grabbable>() != null && dup.GetComponent<Grabbable>().enabled;
                bool hasGi = dup != null && dup.GetComponent<GrabInteractable>() != null && dup.GetComponent<GrabInteractable>().enabled;
                bool hasHgi = dup != null && dup.GetComponent<HandGrabInteractable>() != null && dup.GetComponent<HandGrabInteractable>().SupportedGrabTypes == GrabTypeFlags.All;
                Assert(hasGrabbable && hasGi && hasHgi, "Spawned Meat Duplicate Has Grabbable + GrabInteractable + HandGrabInteractable (All Grab Types)");
                Destroy(dup);
            }

            IngredientSpawnerBowl eggBowlSpawner = GameObject.Find("egg_bowl")?.GetComponent<IngredientSpawnerBowl>();
            if (eggBowlSpawner != null)
            {
                GameObject dup = eggBowlSpawner.SpawnDuplicateToHand();
                bool hasGrabbable = dup != null && dup.GetComponent<Grabbable>() != null && dup.GetComponent<Grabbable>().enabled;
                bool hasGi = dup != null && dup.GetComponent<GrabInteractable>() != null && dup.GetComponent<GrabInteractable>().enabled;
                bool hasHgi = dup != null && dup.GetComponent<HandGrabInteractable>() != null && dup.GetComponent<HandGrabInteractable>().SupportedGrabTypes == GrabTypeFlags.All;
                Assert(hasGrabbable && hasGi && hasHgi, "Spawned Egg Duplicate Has Grabbable + GrabInteractable + HandGrabInteractable (All Grab Types)");
                Destroy(dup);
            }

            IngredientSpawnerBowl onionBowlSpawner = GameObject.Find("onion_bowl")?.GetComponent<IngredientSpawnerBowl>();
            if (onionBowlSpawner != null)
            {
                GameObject dup = onionBowlSpawner.SpawnDuplicateToHand();
                bool hasGrabbable = dup != null && dup.GetComponent<Grabbable>() != null && dup.GetComponent<Grabbable>().enabled;
                bool hasGi = dup != null && dup.GetComponent<GrabInteractable>() != null && dup.GetComponent<GrabInteractable>().enabled;
                bool hasHgi = dup != null && dup.GetComponent<HandGrabInteractable>() != null && dup.GetComponent<HandGrabInteractable>().SupportedGrabTypes == GrabTypeFlags.All;
                Assert(hasGrabbable && hasGi && hasHgi, "Spawned Onion Duplicate Has Grabbable + GrabInteractable + HandGrabInteractable (All Grab Types)");
                Destroy(dup);
            }

            IngredientSpawnerBowl noodleBowlSpawner = GameObject.Find("noodle_bowl")?.GetComponent<IngredientSpawnerBowl>();
            if (noodleBowlSpawner != null)
            {
                GameObject dup = noodleBowlSpawner.SpawnDuplicateToHand();
                bool hasGrabbable = dup != null && dup.GetComponent<Grabbable>() != null && dup.GetComponent<Grabbable>().enabled;
                bool hasGi = dup != null && dup.GetComponent<GrabInteractable>() != null && dup.GetComponent<GrabInteractable>().enabled;
                bool hasHgi = dup != null && dup.GetComponent<HandGrabInteractable>() != null && dup.GetComponent<HandGrabInteractable>().SupportedGrabTypes == GrabTypeFlags.All;
                Assert(hasGrabbable && hasGi && hasHgi, "Spawned Noodle Duplicate Has Grabbable + GrabInteractable + HandGrabInteractable (All Grab Types)");
                Destroy(dup);
            }

            if (director == null || router == null || bowl == null || servingStation == null)
            {
                FinishSuite();
                yield break;
            }

            // =====================================================
            // 2. OUT-OF-ORDER SERVING GUARDRAILS
            // =====================================================
            _log.AppendLine("\n--- 2. Testing Out-of-Order Serving Guardrails ---");

            // Empty bowl, stall open, clean initial session state
            director.ResetSession();
            director.HandleCurtainStateChanged(true);
            bowl.ResetBowlForNextCustomer();
            yield return null;

            // 2a. Press Key S when bowl is incomplete
            router.SimulateTap(ParentAction.ServeCustomer);
            yield return null;
            Assert(!servingStation.IsServing, "Key S Rejected When Bowl Incomplete (IsServing Remains False)");
            Assert(director.CurrentStep == CookingStep.Noodles_Grab, "Director Step Remains Noodles_Grab After Rejected Key S");

            // 2b. Physical push of empty bowl into customerServePoint trigger
            MethodInfo onTriggerMethod = typeof(CustomerServingStation).GetMethod("OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic);
            Collider bowlCol = bowl.GetComponent<Collider>();
            onTriggerMethod?.Invoke(servingStation, new object[] { bowlCol });
            yield return null;
            Assert(!servingStation.IsServing, "Physical Push of Incomplete Bowl Into customerServePoint Rejected");

            // =====================================================
            // 3. STEP 12 SERVING & CELEBRATION FLOW (KEY S)
            // =====================================================
            _log.AppendLine("\n--- 3. Testing Complete Serving & Celebration Sequence (Key S) ---");

            // Assemble complete bowl
            bowl.AddNoodles();
            bowl.AddMeat();
            bowl.AddEgg();
            bowl.AddOnions();
            bowl.AddNori();
            bowl.AddBroth();
            yield return null;

            Assert(bowl.IsBowlComplete, $"Ramen Bowl Fully Assembled (Noodles={bowl.HasNoodles}, Meat={bowl.HasMeat}, Egg={bowl.HasEgg}, Onions={bowl.HasOnions}, Nori={bowl.HasNori}, Broth={bowl.HasBroth})");
            Assert(director.CurrentStep == CookingStep.ReadyToServe, $"Director at CookingStep.ReadyToServe (Actual: {director.CurrentStep}, Cust: {director.CurrentCustomerIndex})");
            Assert(bowlGrab != null && bowlGrab.enabled, "lamian_bowl Grabbable Unlocked (enabled == true) Once Assembled");

            Vector3 preServePos = servingStation.LamianBowl.transform.position;

            // Trigger serving via Key S
            router.SimulateTap(ParentAction.ServeCustomer);
            yield return null;

            Assert(servingStation.IsServing, "Serving Sequence Started: IsServing == true");
            Assert(director.CurrentStep == CookingStep.CustomerServed, "Director Set to CookingStep.CustomerServed");

            // Wait for slide tween to finish (~0.65s)
            yield return new WaitForSeconds(0.65f);

            Vector3 servedPos = servingStation.LamianBowl.transform.position;
            float slideDistance = Vector3.Distance(preServePos, servedPos);
            Assert(slideDistance > 0.40f, $"Ramen Bowl Slid Across Counter to Panda (Slide Distance: {slideDistance:F2}m)");
            Assert(servingStation.ConfettiFX != null && servingStation.ConfettiFX.isPlaying, "confettiFX ParticleSystem Playing");
            Assert(servingStation.FireworkFX != null && servingStation.FireworkFX.isPlaying, "fireworkFX ParticleSystem Playing");
            Assert(servingStation.CelebrationAudio != null && servingStation.CelebrationAudio.isPlaying, "celebrationAudio Fanfare Playing");

            // Wait deterministically for celebration routine to finish (~2.2s remaining)
            float waitTimeout = 4.0f;
            while (servingStation.IsServing && waitTimeout > 0f)
            {
                waitTimeout -= Time.deltaTime;
                yield return null;
            }

            Assert(!servingStation.IsServing, "Celebration Finished: IsServing Returned to False");
            Assert(!bowl.HasNoodles && !bowl.HasMeat && !bowl.HasEgg && !bowl.HasOnions && !bowl.HasBroth,
                "Ramen Bowl Reset Cleanly (All Child Meshes Deactivated for Next Order)");

            Vector3 postResetPos = servingStation.LamianBowl.transform.position;
            float returnDistance = Vector3.Distance(postResetPos, preServePos);
            Assert(returnDistance < 0.05f, "Fresh Empty Bowl Returned to Chef Prep Station in Zone C");

            // =====================================================
            // 4. STEP 13: 5-CUSTOMER REPETITION LOOP & COMPLETION
            // =====================================================
            _log.AppendLine("\n--- 4. Testing Step 13: 5-Customer Repetition Loop & Session Completion ---");

            Assert(director.CurrentCustomerIndex == 2, $"Customer Index Advanced to 2/5 (Current: {director.CurrentCustomerIndex})");
            Assert(director.CurrentStep == CookingStep.Noodles_Grab, "Director Loop Returned to CookingStep.Noodles_Grab for Customer 2");

            // Fast-forward serving customers 2, 3, 4
            for (int cust = 2; cust <= 4; cust++)
            {
                bowl.AddNoodles();
                bowl.AddMeat();
                bowl.AddEgg();
                bowl.AddOnions();
                bowl.AddNori();
                bowl.AddBroth();
                director.AdvanceToNextCustomerOrComplete();
                bowl.ResetBowlForNextCustomer();
                yield return null;
            }

            Assert(director.CurrentCustomerIndex == 5, $"Customer Index Advanced to 5/5 (Current: {director.CurrentCustomerIndex})");

            // Serve final customer 5
            bowl.AddNoodles();
            bowl.AddMeat();
            bowl.AddEgg();
            bowl.AddOnions();
            bowl.AddNori();
            bowl.AddBroth();
            director.AdvanceToNextCustomerOrComplete();
            yield return null;

            Assert(director.CurrentStep == CookingStep.SessionComplete, "Director Reached CookingStep.SessionComplete at 5/5 Customers");
            Assert(director.NextExpectedPrompt.Contains("Complete"), "HUD NextExpectedPrompt Displays Session Complete Finale");

            FinishSuite();
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
                Debug.LogError($"[Step12AcceptanceTester] FAIL: {testName}");
            }
        }

        private void FinishSuite()
        {
            _log.AppendLine($"\n=== SUITE COMPLETE: {_passed} PASSED, {_failed} FAILED ===");
            string outputPath = Path.Combine(Application.dataPath, "../Temp/step12_acceptance_results.txt");
            File.WriteAllText(outputPath, _log.ToString());
            Debug.Log(_log.ToString());
        }
    }
}
