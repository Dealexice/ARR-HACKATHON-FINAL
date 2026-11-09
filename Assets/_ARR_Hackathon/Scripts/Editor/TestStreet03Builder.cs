#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Grab;
using YataiTogether;

namespace YataiTogether.Editor
{
    public static class TestStreet03Builder
    {
        private const string Fbx2Path = "Assets/_ARR_Hackathon/Prefabs/Test street02.fbx";
        private const string Prefab03Path = "Assets/_ARR_Hackathon/Prefabs/Test Street03.prefab";
        private const string MainScenePath = "Assets/_ARR_Hackathon/Scenes/YataiTogether_Main.unity";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Yatai Together/Build Test Street 03 and Update Scene")]
        public static void BuildAndSetupScene()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != MainScenePath)
            {
                EditorSceneManager.OpenScene(MainScenePath);
            }

            Debug.Log("[TestStreet03Builder] Starting Test Street 03 build...");

            // 1. Remove old "Test Street" from the scene if present
            GameObject oldStreet = GameObject.Find("Test Street");
            Vector3 streetPos = new Vector3(7.02f, 1.04f, 1.71f);
            Quaternion streetRot = Quaternion.Euler(0f, 4.33f, 0f);
            Vector3 streetScale = Vector3.one;

            if (oldStreet != null)
            {
                streetPos = oldStreet.transform.position;
                streetRot = oldStreet.transform.rotation;
                streetScale = oldStreet.transform.localScale;
                Object.DestroyImmediate(oldStreet);
            }

            // Also remove any existing Test Street03 in scene before recreating
            GameObject oldStreet03 = GameObject.Find("Test Street03");
            if (oldStreet03 != null)
            {
                Object.DestroyImmediate(oldStreet03);
            }

            // 2. Load and instantiate FBX 2
            GameObject fbx2Asset = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx2Path);
            if (fbx2Asset == null)
            {
                Debug.LogError($"[TestStreet03Builder] FBX not found at {Fbx2Path}!");
                return;
            }

            GameObject rootGo = Object.Instantiate(fbx2Asset);
            rootGo.name = "Test Street03";
            rootGo.transform.position = streetPos;
            rootGo.transform.rotation = streetRot;
            rootGo.transform.localScale = streetScale;

            // 3. Replace static 'Cube.001' with animated Panda (with animation joints & Animator) from Panda_Prefab_Animation
            // ponytail: Replace static FBX2 mesh with the jointed animated Panda prefab directly.
            Transform staticPandaT = rootGo.transform.Find("Cube.001") ?? rootGo.transform.Find("panda");
            if (staticPandaT != null)
            {
                Object.DestroyImmediate(staticPandaT.gameObject);
                Debug.Log("[TestStreet03Builder] Destroyed static Cube.001 / panda mesh.");
            }

            const string PandaPrefabPath = "Assets/_ARR_Hackathon/Prefabs/Panda_Prefab_Animation/Panda.prefab";
            GameObject pandaAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PandaPrefabPath);
            if (pandaAsset == null)
            {
                pandaAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_ARR_Hackathon/Prefabs/Panda_Prefab_Animation/Panda.fbx");
            }

            if (pandaAsset != null)
            {
                GameObject pandaGo = Object.Instantiate(pandaAsset, rootGo.transform);
                pandaGo.name = "panda";

                // Remove inactive placeholder bowl in prefab if present
                Transform extraBowl = pandaGo.transform.Find("lamain");
                if (extraBowl != null)
                {
                    Object.DestroyImmediate(extraBowl.gameObject);
                }

                // Fitted local transform: faces ramen counter (-X in world, 270 deg local), exact bounds match original stall position
                pandaGo.transform.localPosition = new Vector3(0.88f, -0.48f, -1.05f);
                pandaGo.transform.localEulerAngles = new Vector3(0f, 270f, 0f);
                pandaGo.transform.localScale = new Vector3(0.2815f, 0.2815f, 0.2815f);

                // Ensure Animator with Panda.controller
                Animator anim = pandaGo.GetComponent<Animator>();
                if (anim == null) anim = pandaGo.AddComponent<Animator>();
                var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_ARR_Hackathon/Prefabs/Panda_Prefab_Animation/Panda.controller");
                if (ctrl != null) anim.runtimeAnimatorController = ctrl;

                Debug.Log("[TestStreet03Builder] Instantiated animated Panda with joints and Animator in Test Street03.");
            }
            else
            {
                Debug.LogError("[TestStreet03Builder] Animated Panda asset not found at " + PandaPrefabPath);
            }

            Transform gibbonT = rootGo.transform.Find("gibbon");
            if (gibbonT != null)
            {
                Object.DestroyImmediate(gibbonT.gameObject);
                Debug.Log("[TestStreet03Builder] Removed 'gibbon'.");
            }

            // 4. Swap Pots: clean swap where old broth (pot_01) becomes "boiler", and old boiler (pot_02) becomes "broth"
            Transform pot02 = rootGo.transform.Find("pot_02");
            Transform pot01 = rootGo.transform.Find("pot_01");
            Transform noodleTemplateT = rootGo.transform.Find("noodle");

            if (pot01 != null)
            {
                pot01.name = "boiler";
                try { pot01.tag = "boiler"; } catch { }
                SetupBoiler(pot01.gameObject, noodleTemplateT != null ? noodleTemplateT.gameObject : null);
            }
            if (pot02 != null)
            {
                pot02.name = "broth";
                try { pot02.tag = "broth"; } catch { }
                SetupBrothPot(pot02.gameObject);
            }

            // ponytail: food cart remains at its original FBX scale (100, 100, 100)
            Transform foodCartT = rootGo.transform.Find("food cart");
            if (foodCartT != null)
            {
                foodCartT.localScale = new Vector3(100f, 100f, 100f);
                Debug.Log($"[TestStreet03Builder] Restored 'food cart' to original scale {foodCartT.localScale}");
            }

            // 5. Setup lamian_bowl and its child ingredients from FBX2
            Transform bowlT = rootGo.transform.Find("lamian_bowl");
            if (bowlT != null)
            {
                SetupLamianBowl(rootGo.transform, bowlT.gameObject);
            }

            // 6. Setup egg & egg_bowl with chute_drop_point and infinite spawner
            Transform eggT = rootGo.transform.Find("egg");
            Transform eggBowlT = rootGo.transform.Find("egg_bowl");
            if (eggBowlT != null && eggT != null)
            {
                SetupEggStation(eggBowlT.gameObject, eggT.gameObject);
            }

            // 7. Setup meat & meat_bowl with infinite spawner
            Transform meatT = rootGo.transform.Find("meat");
            Transform meatBowlT = rootGo.transform.Find("meat_bowl");
            if (meatBowlT != null && meatT != null)
            {
                SetupMeatStation(meatBowlT.gameObject, meatT.gameObject);
            }

            // 8. Setup onion & onion_bowl with infinite spawner
            Transform onionT = rootGo.transform.Find("onion");
            Transform onionBowlT = rootGo.transform.Find("onion_bwol") ?? rootGo.transform.Find("onion_bowl");
            if (onionBowlT != null && onionT != null)
            {
                onionBowlT.name = "onion_bowl";
                SetupOnionStation(onionBowlT.gameObject, onionT.gameObject);
            }

            // 9. Setup noodle & noodle_bowl with infinite spawner
            Transform noodleT = rootGo.transform.Find("noodle");
            Transform noodleBowlT = rootGo.transform.Find("noodle_bwol") ?? rootGo.transform.Find("noodle_bowl");
            if (noodleBowlT != null && noodleT != null)
            {
                noodleBowlT.name = "noodle_bowl";
                SetupNoodleStation(noodleBowlT.gameObject, noodleT.gameObject);
            }

            // 10. Setup spoon (Step 6 SpoonScoopController + Step 11 SpoonBrothScooper)
            Transform spoonT = rootGo.transform.Find("spoon");
            if (spoonT != null)
            {
                RamenAssemblyReceiver receiver = bowlT != null ? bowlT.GetComponent<RamenAssemblyReceiver>() : null;
                // pot02 is the NEW broth pot
                SetupSpoon(spoonT.gameObject, pot02 != null ? pot02.gameObject : null, receiver);
            }

            // 10b. Create and export standalone prefabs for spoon, meat, egg, onion, noodle
            CreateOrUpdateStandalonePrefabs(rootGo.transform);

            // 11. Setup Table collider
            SetupTableCollider();

            // 12. Setup Golden Nori Controller & wire Test Street03 references on [ARR_GameManager]
            SetupGoldenNori(rootGo.transform);

            // 13. Setup Customer Serving Station (Step 12)
            SetupCustomerServingStation(rootGo.transform);

            // 14. Setup Ambient Audio (BGM & Street Noise)
            SetupAmbientAudio();

            // 15. Save Prefab to Assets/_ARR_Hackathon/Prefabs/Test Street03.prefab
            PrefabUtility.SaveAsPrefabAssetAndConnect(rootGo, Prefab03Path, InteractionMode.AutomatedAction);
            Debug.Log($"[TestStreet03Builder] Saved {Prefab03Path} successfully!");

            // 14. Save Scene and Sync to SampleScene
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            FileUtil.ReplaceFile(MainScenePath, SampleScenePath);
            AssetDatabase.Refresh();

            Debug.Log("[TestStreet03Builder] Completed building Test Street03 and synced scenes!");
        }

        private static void SetupTableCollider()
        {
            GameObject tableColGo = GameObject.Find("counter_table_collider");
            if (tableColGo == null)
            {
                tableColGo = new GameObject("counter_table_collider");
            }
            tableColGo.transform.position = new Vector3(7.05f, 1.05f, 0.60f);
            BoxCollider box = tableColGo.GetComponent<BoxCollider>();
            if (box == null) box = tableColGo.AddComponent<BoxCollider>();
            box.size = new Vector3(1.6f, 0.05f, 2.5f);
        }

        private static void SetupBoiler(GameObject boiler, GameObject noodleTemplate = null)
        {
            boiler.tag = "boiler";
            BoxCollider col = boiler.GetComponent<BoxCollider>();
            if (col == null) col = boiler.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.001f, 0f);
            col.size = new Vector3(0.0055f, 0.004f, 0.0055f);

            Transform snapPoint = boiler.transform.Find("potSnapPoint");
            if (snapPoint == null)
            {
                GameObject snapGo = new GameObject("potSnapPoint");
                snapGo.transform.SetParent(boiler.transform, false);
                snapGo.transform.localPosition = new Vector3(0f, 0.0015f, 0f);
                snapPoint = snapGo.transform;
            }

            // BoxCollider (isTrigger = true) on child potSnapPoint
            BoxCollider snapCol = snapPoint.GetComponent<BoxCollider>();
            if (snapCol == null) snapCol = snapPoint.gameObject.AddComponent<BoxCollider>();
            snapCol.isTrigger = true;
            snapCol.center = Vector3.zero;
            snapCol.size = new Vector3(0.0055f, 0.004f, 0.0055f);

            // BoilerPotSnapReceiver on potSnapPoint
            BoilerPotSnapReceiver snapReceiver = snapPoint.GetComponent<BoilerPotSnapReceiver>();
            if (snapReceiver == null) snapReceiver = snapPoint.gameObject.AddComponent<BoilerPotSnapReceiver>();

            Transform cookedVis = snapPoint.Find("cookedNoodleVisual");
            if (cookedVis != null && cookedVis.GetComponent<MeshFilter>()?.sharedMesh?.name.Contains("Cylinder") == true)
            {
                Object.DestroyImmediate(cookedVis.gameObject);
                cookedVis = null;
            }

            if (cookedVis == null)
            {
                GameObject visObj;
                if (noodleTemplate != null)
                {
                    visObj = Object.Instantiate(noodleTemplate, snapPoint);
                    visObj.name = "cookedNoodleVisual";
                    visObj.transform.localPosition = Vector3.zero;
                    visObj.transform.localRotation = Quaternion.identity;
                    visObj.transform.localScale = noodleTemplate.transform.localScale;

                    Collider vc = visObj.GetComponent<Collider>();
                    if (vc != null) Object.DestroyImmediate(vc);
                    Rigidbody rb = visObj.GetComponent<Rigidbody>();
                    if (rb != null) Object.DestroyImmediate(rb);

                    var grabbables = visObj.GetComponentsInChildren<Grabbable>(true);
                    for (int i = 0; i < grabbables.Length; i++) Object.DestroyImmediate(grabbables[i]);
                    var handGrabs = visObj.GetComponentsInChildren<HandGrabInteractable>(true);
                    for (int i = 0; i < handGrabs.Length; i++) Object.DestroyImmediate(handGrabs[i]);
                }
                else
                {
                    visObj = new GameObject("cookedNoodleVisual");
                    visObj.transform.SetParent(snapPoint, false);
                    visObj.transform.localPosition = Vector3.zero;
                }
                visObj.SetActive(false);
                cookedVis = visObj.transform;
            }
            snapReceiver.Configure(snapPoint, 3.0f, cookedVis.gameObject);

            BoilerStation station = boiler.GetComponent<BoilerStation>();
            if (station == null) station = boiler.AddComponent<BoilerStation>();
            station.Configure(snapPoint, boiler.GetComponent<Renderer>());
        }

        private static void SetupLamianBowl(Transform streetRoot, GameObject bowl)
        {
            BoxCollider col = bowl.GetComponent<BoxCollider>();
            if (col == null) col = bowl.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.0006f, 0f);
            col.size = new Vector3(0.0045f, 0.002f, 0.0045f);

            // Reparent child ingredients under lamian_bowl
            Transform noodleT = bowl.transform.Find("lamian_noodle") ?? streetRoot.Find("lamian_noodle");
            if (noodleT != null && noodleT.parent != bowl.transform)
            {
                noodleT.SetParent(bowl.transform, true);
            }

            Transform meatT = bowl.transform.Find("lamian_meat") ?? streetRoot.Find("lamian_meat");
            if (meatT != null && meatT.parent != bowl.transform)
            {
                meatT.SetParent(bowl.transform, true);
            }

            Transform eggT = bowl.transform.Find("lamian_egg") ?? streetRoot.Find("lamian_egg");
            if (eggT != null && eggT.parent != bowl.transform)
            {
                eggT.SetParent(bowl.transform, true);
            }

            Transform onionT = bowl.transform.Find("lamian_onion") ?? streetRoot.Find("lamian_onion");
            if (onionT != null && onionT.parent != bowl.transform)
            {
                onionT.SetParent(bowl.transform, true);
            }

            Transform brothT = bowl.transform.Find("lamian_broth") ?? bowl.transform.Find("lamian_soup") ?? streetRoot.Find("lamian_soup");
            if (brothT != null)
            {
                brothT.name = "lamian_broth";
                if (brothT.parent != bowl.transform)
                {
                    brothT.SetParent(bowl.transform, true);
                }
            }

            // Create crispy golden nori sheet on bowl rim if not present
            Transform noriT = bowl.transform.Find("lamian_nori");
            if (noriT == null)
            {
                GameObject newNoriGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                newNoriGo.name = "lamian_nori";
                newNoriGo.transform.SetParent(bowl.transform, false);
                newNoriGo.transform.localPosition = new Vector3(0.0016f, 0.0010f, 0.0002f);
                newNoriGo.transform.localEulerAngles = new Vector3(30f, 45f, 15f);
                newNoriGo.transform.localScale = new Vector3(0.0014f, 0.00008f, 0.0012f);

                Collider noriCol = newNoriGo.GetComponent<Collider>();
                if (noriCol != null) Object.DestroyImmediate(noriCol);

                Renderer noriRend = newNoriGo.GetComponent<Renderer>();
                if (noriRend != null)
                {
                    // Deep nori seaweed green, saved as asset so the prefab keeps its material
                    noriRend.sharedMaterial = GetOrCreateMaterialAsset("CrispyNori_Mat", new Color(0.10f, 0.15f, 0.08f));
                }
                noriT = newNoriGo.transform;
            }

            // Setup Rigidbody, Grabbable and HandGrabInteractable for completed bowl serving
            Rigidbody bowlRb = bowl.GetComponent<Rigidbody>();
            if (bowlRb == null) bowlRb = bowl.AddComponent<Rigidbody>();
            bowlRb.mass = 0.3f;
            bowlRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            bowlRb.isKinematic = true;

            Grabbable bowlGrab = bowl.GetComponent<Grabbable>();
            if (bowlGrab == null) bowlGrab = bowl.AddComponent<Grabbable>();
            bowlGrab.enabled = false;

            HandGrabInteractable bowlHandGrab = bowl.GetComponent<HandGrabInteractable>();
            if (bowlHandGrab == null)
            {
                bowlHandGrab = bowl.AddComponent<HandGrabInteractable>();
                bowlHandGrab.InjectRigidbody(bowlRb);
                bowlHandGrab.InjectOptionalPointableElement(bowlGrab);
            }
            bowlHandGrab.enabled = false;

            RamenAssemblyReceiver receiver = bowl.GetComponent<RamenAssemblyReceiver>();
            if (receiver == null) receiver = bowl.AddComponent<RamenAssemblyReceiver>();

            GameObject noodleGo = noodleT != null ? noodleT.gameObject : null;
            GameObject meatGo = meatT != null ? meatT.gameObject : null;
            GameObject eggGo = eggT != null ? eggT.gameObject : null;
            GameObject onionGo = onionT != null ? onionT.gameObject : null;
            GameObject brothGo = brothT != null ? brothT.gameObject : null;
            GameObject noriGo = noriT != null ? noriT.gameObject : null;

            receiver.SetChildMeshes(
                noodleGo,
                meatGo,
                eggGo,
                onionGo,
                brothGo,
                noriGo
            );

            // Initially deactivate all 6 ingredients
            if (noodleGo != null) noodleGo.SetActive(false);
            if (meatGo != null) meatGo.SetActive(false);
            if (eggGo != null) eggGo.SetActive(false);
            if (onionGo != null) onionGo.SetActive(false);
            if (brothGo != null) brothGo.SetActive(false);
            if (noriGo != null) noriGo.SetActive(false);
        }

        private static void SetupEggStation(GameObject eggBowl, GameObject egg)
        {
            BoxCollider bowlCol = eggBowl.GetComponent<BoxCollider>();
            if (bowlCol == null) bowlCol = eggBowl.AddComponent<BoxCollider>();
            bowlCol.isTrigger = true;
            bowlCol.size = new Vector3(0.003f, 0.002f, 0.003f);

            Transform chute = eggBowl.transform.Find("chute_drop_point");
            if (chute == null)
            {
                GameObject chuteGo = new GameObject("chute_drop_point");
                chuteGo.transform.SetParent(eggBowl.transform, false);
                chuteGo.transform.localPosition = new Vector3(0f, 0.003f, 0f);
                chute = chuteGo.transform;
            }

            EggStationController eggCtrl = eggBowl.GetComponent<EggStationController>();
            if (eggCtrl == null) eggCtrl = eggBowl.AddComponent<EggStationController>();
            eggCtrl.Configure(egg, chute, null);

            // Infinite spawner on egg_bowl
            IngredientSpawnerBowl spawner = eggBowl.GetComponent<IngredientSpawnerBowl>();
            if (spawner == null) spawner = eggBowl.AddComponent<IngredientSpawnerBowl>();
            spawner.Configure(egg, egg, "egg", ParentAction.DropEggIntoPalm);

            // Egg physics (kept stationary in bowl)
            Rigidbody rb = egg.GetComponent<Rigidbody>();
            if (rb == null) rb = egg.AddComponent<Rigidbody>();
            rb.mass = 0.1f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.isKinematic = true;

            SphereCollider sc = egg.GetComponent<SphereCollider>();
            if (sc == null) sc = egg.AddComponent<SphereCollider>();
            sc.radius = 0.00045f;

            Grabbable grabbable = egg.GetComponent<Grabbable>();
            if (grabbable == null) grabbable = egg.AddComponent<Grabbable>();
            grabbable.InjectOptionalRigidbody(rb);
            grabbable.enabled = false;

            GrabInteractable gi = egg.GetComponent<GrabInteractable>();
            if (gi == null)
            {
                gi = egg.AddComponent<GrabInteractable>();
                gi.InjectRigidbody(rb);
                gi.InjectOptionalPointableElement(grabbable);
                gi.UseClosestPointAsGrabSource = true;
            }
            gi.enabled = false;

            HandGrabInteractable hgi = egg.GetComponent<HandGrabInteractable>();
            if (hgi == null)
            {
                hgi = egg.AddComponent<HandGrabInteractable>();
                hgi.InjectRigidbody(rb);
                hgi.InjectOptionalPointableElement(grabbable);
            }
            hgi.InjectSupportedGrabTypes(GrabTypeFlags.All);
            hgi.enabled = false;
            try { egg.tag = "egg"; } catch { }
        }

        private static void SetupMeatStation(GameObject meatBowl, GameObject meat)
        {
            BoxCollider bowlCol = meatBowl.GetComponent<BoxCollider>();
            if (bowlCol == null) bowlCol = meatBowl.AddComponent<BoxCollider>();
            bowlCol.isTrigger = true;
            bowlCol.size = new Vector3(0.003f, 0.002f, 0.003f);

            PorkStationController porkCtrl = meatBowl.GetComponent<PorkStationController>();
            if (porkCtrl == null) porkCtrl = meatBowl.AddComponent<PorkStationController>();
            porkCtrl.Configure(meat, meatBowl.transform, null);

            // Infinite spawner on meat_bowl
            IngredientSpawnerBowl spawner = meatBowl.GetComponent<IngredientSpawnerBowl>();
            if (spawner == null) spawner = meatBowl.AddComponent<IngredientSpawnerBowl>();
            spawner.Configure(meat, meat, "meat", ParentAction.GrabPork);

            Rigidbody rb = meat.GetComponent<Rigidbody>();
            if (rb == null) rb = meat.AddComponent<Rigidbody>();
            rb.mass = 0.1f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.isKinematic = true;

            MeshCollider mc = meat.GetComponent<MeshCollider>();
            if (mc == null) mc = meat.AddComponent<MeshCollider>();
            mc.convex = true;

            Grabbable grabbable = meat.GetComponent<Grabbable>();
            if (grabbable == null) grabbable = meat.AddComponent<Grabbable>();
            grabbable.InjectOptionalRigidbody(rb);
            grabbable.enabled = false;

            GrabInteractable gi = meat.GetComponent<GrabInteractable>();
            if (gi == null)
            {
                gi = meat.AddComponent<GrabInteractable>();
                gi.InjectRigidbody(rb);
                gi.InjectOptionalPointableElement(grabbable);
                gi.UseClosestPointAsGrabSource = true;
            }
            gi.enabled = false;

            HandGrabInteractable hgi = meat.GetComponent<HandGrabInteractable>();
            if (hgi == null)
            {
                hgi = meat.AddComponent<HandGrabInteractable>();
                hgi.InjectRigidbody(rb);
                hgi.InjectOptionalPointableElement(grabbable);
            }
            hgi.InjectSupportedGrabTypes(GrabTypeFlags.All);
            hgi.enabled = false;
            try { meat.tag = "meat"; } catch { }
        }

        private static void SetupOnionStation(GameObject onionBowl, GameObject onion)
        {
            BoxCollider bowlCol = onionBowl.GetComponent<BoxCollider>();
            if (bowlCol == null) bowlCol = onionBowl.AddComponent<BoxCollider>();
            bowlCol.isTrigger = true;
            bowlCol.size = new Vector3(0.003f, 0.002f, 0.003f);

            OnionStationController onionCtrl = onionBowl.GetComponent<OnionStationController>();
            if (onionCtrl == null) onionCtrl = onionBowl.AddComponent<OnionStationController>();
            onionCtrl.Configure(onion, onionBowl.transform, null);

            // Infinite spawner on onion_bowl
            IngredientSpawnerBowl spawner = onionBowl.GetComponent<IngredientSpawnerBowl>();
            if (spawner == null) spawner = onionBowl.AddComponent<IngredientSpawnerBowl>();
            spawner.Configure(onion, onion, "onion", ParentAction.GrabOnions);

            Rigidbody rb = onion.GetComponent<Rigidbody>();
            if (rb == null) rb = onion.AddComponent<Rigidbody>();
            rb.mass = 0.1f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.isKinematic = true;

            MeshCollider mc = onion.GetComponent<MeshCollider>();
            if (mc == null) mc = onion.AddComponent<MeshCollider>();
            mc.convex = true;

            Grabbable grabbable = onion.GetComponent<Grabbable>();
            if (grabbable == null) grabbable = onion.AddComponent<Grabbable>();
            grabbable.InjectOptionalRigidbody(rb);
            grabbable.enabled = false;

            GrabInteractable gi = onion.GetComponent<GrabInteractable>();
            if (gi == null)
            {
                gi = onion.AddComponent<GrabInteractable>();
                gi.InjectRigidbody(rb);
                gi.InjectOptionalPointableElement(grabbable);
                gi.UseClosestPointAsGrabSource = true;
            }
            gi.enabled = false;

            HandGrabInteractable hgi = onion.GetComponent<HandGrabInteractable>();
            if (hgi == null)
            {
                hgi = onion.AddComponent<HandGrabInteractable>();
                hgi.InjectRigidbody(rb);
                hgi.InjectOptionalPointableElement(grabbable);
            }
            hgi.InjectSupportedGrabTypes(GrabTypeFlags.All);
            hgi.enabled = false;
            try { onion.tag = "onion"; } catch { }
        }

        private static void SetupNoodleStation(GameObject noodleBowl, GameObject noodle)
        {
            BoxCollider bowlCol = noodleBowl.GetComponent<BoxCollider>();
            if (bowlCol == null) bowlCol = noodleBowl.AddComponent<BoxCollider>();
            bowlCol.isTrigger = true;
            bowlCol.size = new Vector3(0.003f, 0.002f, 0.003f);

            // Infinite spawner on noodle_bowl
            IngredientSpawnerBowl spawner = noodleBowl.GetComponent<IngredientSpawnerBowl>();
            if (spawner == null) spawner = noodleBowl.AddComponent<IngredientSpawnerBowl>();
            spawner.Configure(noodle, noodle, "noodle", ParentAction.GrabNoodles);

            Rigidbody rb = noodle.GetComponent<Rigidbody>();
            if (rb == null) rb = noodle.AddComponent<Rigidbody>();
            rb.mass = 0.1f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.isKinematic = true;

            MeshCollider mc = noodle.GetComponent<MeshCollider>();
            if (mc == null) mc = noodle.AddComponent<MeshCollider>();
            mc.convex = true;

            Grabbable grabbable = noodle.GetComponent<Grabbable>();
            if (grabbable == null) grabbable = noodle.AddComponent<Grabbable>();
            grabbable.InjectOptionalRigidbody(rb);
            grabbable.enabled = false;

            GrabInteractable gi = noodle.GetComponent<GrabInteractable>();
            if (gi == null)
            {
                gi = noodle.AddComponent<GrabInteractable>();
                gi.InjectRigidbody(rb);
                gi.InjectOptionalPointableElement(grabbable);
                gi.UseClosestPointAsGrabSource = true;
            }
            gi.enabled = false;

            HandGrabInteractable hgi = noodle.GetComponent<HandGrabInteractable>();
            if (hgi == null)
            {
                hgi = noodle.AddComponent<HandGrabInteractable>();
                hgi.InjectRigidbody(rb);
                hgi.InjectOptionalPointableElement(grabbable);
            }
            hgi.InjectSupportedGrabTypes(GrabTypeFlags.All);
            hgi.enabled = false;

            try { noodle.tag = "noodle"; } catch { }
            if (noodle.GetComponent<NoodleStretch>() == null)
            {
                noodle.AddComponent<NoodleStretch>();
            }

            // StretchableNoodle lives on [ARR_GameManager] (it hides this raw 'noodle' on Key 4;
            // living on 'noodle' itself would deactivate its own listener).
            StretchableNoodle stretch = noodle.GetComponent<StretchableNoodle>();
            if (stretch != null) Object.DestroyImmediate(stretch);
        }

        private static Material GetOrCreateMaterialAsset(string name, Color color)
        {
            const string dir = "Assets/_ARR_Hackathon/Materials";
            if (!AssetDatabase.IsValidFolder(dir))
            {
                AssetDatabase.CreateFolder("Assets/_ARR_Hackathon", "Materials");
            }
            string path = $"{dir}/{name}.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void SetupBrothPot(GameObject brothPot)
        {
            BoxCollider col = brothPot.GetComponent<BoxCollider>();
            if (col == null) col = brothPot.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(-0.00043f, 0.00030f, 0.00050f);
            col.size = new Vector3(0.0056f, 0.0046f, 0.0035f);
        }

        private static void SetupSpoon(GameObject spoon, GameObject brothPot, RamenAssemblyReceiver bowlReceiver)
        {
            Rigidbody rb = spoon.GetComponent<Rigidbody>();
            if (rb == null) rb = spoon.AddComponent<Rigidbody>();
            rb.mass = 0.2f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.isKinematic = true;

            MeshCollider mc = spoon.GetComponent<MeshCollider>();
            if (mc == null) mc = spoon.AddComponent<MeshCollider>();
            mc.convex = true;

            // CRITICAL: Dedicated solid BoxCollider on the body/handle of the spoon
            // Spans the handle (22cm length, 6cm width, 6cm height in world space) for effortless VR grip
            Transform bodyColT = spoon.transform.Find("spoon_body_collider");
            if (bodyColT == null)
            {
                GameObject bodyColGo = new GameObject("spoon_body_collider");
                bodyColGo.transform.SetParent(spoon.transform, false);
                bodyColGo.transform.localPosition = new Vector3(-0.0009f, -0.0002f, 0.00005f);
                bodyColGo.transform.localRotation = Quaternion.identity;
                bodyColGo.transform.localScale = Vector3.one;
                BoxCollider bc = bodyColGo.AddComponent<BoxCollider>();
                bc.isTrigger = false;
                bc.size = new Vector3(0.0022f, 0.0006f, 0.0006f);
            }
            else
            {
                BoxCollider bc = bodyColT.GetComponent<BoxCollider>();
                if (bc == null) bc = bodyColT.gameObject.AddComponent<BoxCollider>();
                bc.isTrigger = false;
                bc.size = new Vector3(0.0022f, 0.0006f, 0.0006f);
                bodyColT.localPosition = new Vector3(-0.0009f, -0.0002f, 0.00005f);
            }

            Grabbable grabbable = spoon.GetComponent<Grabbable>();
            if (grabbable == null) grabbable = spoon.AddComponent<Grabbable>();
            grabbable.InjectOptionalRigidbody(rb);
            grabbable.enabled = true;

            GrabInteractable gi = spoon.GetComponent<GrabInteractable>();
            if (gi == null)
            {
                gi = spoon.AddComponent<GrabInteractable>();
                gi.InjectRigidbody(rb);
                gi.InjectOptionalPointableElement(grabbable);
                gi.UseClosestPointAsGrabSource = true;
            }
            gi.enabled = true;

            HandGrabInteractable hgi = spoon.GetComponent<HandGrabInteractable>();
            if (hgi == null)
            {
                hgi = spoon.AddComponent<HandGrabInteractable>();
                hgi.InjectRigidbody(rb);
                hgi.InjectOptionalPointableElement(grabbable);
            }
            hgi.InjectSupportedGrabTypes(GrabTypeFlags.All);
            hgi.enabled = true;

            Transform attach = spoon.transform.Find("spoon_attach_point");
            if (attach == null)
            {
                GameObject attachGo = new GameObject("spoon_attach_point");
                attachGo.transform.SetParent(spoon.transform, false);
                attachGo.transform.localPosition = new Vector3(0.00095f, 0.00009f, 0.00005f);
                attach = attachGo.transform;
            }

            Transform scoopTrig = spoon.transform.Find("spoon_scoop_trigger");
            if (scoopTrig == null)
            {
                GameObject scoopGo = new GameObject("spoon_scoop_trigger");
                scoopGo.transform.SetParent(spoon.transform, false);
                scoopGo.transform.localPosition = new Vector3(0.00095f, 0.00009f, 0.00005f);
                SphereCollider sc = scoopGo.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.radius = 0.00045f;
                scoopTrig = scoopGo.transform;
            }
            else
            {
                SphereCollider sc = scoopTrig.GetComponent<SphereCollider>();
                if (sc != null)
                {
                    sc.isTrigger = true;
                    sc.radius = 0.00045f;
                }
            }

            // Create child "spoon_broth_fill" mesh on the spoon bowl tip (initially inactive)
            Transform brothFillT = spoon.transform.Find("spoon_broth_fill");
            if (brothFillT == null)
            {
                GameObject fillGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                fillGo.name = "spoon_broth_fill";
                fillGo.transform.SetParent(spoon.transform, false);
                fillGo.transform.localPosition = new Vector3(0.00095f, 0.00009f, 0.00008f);
                fillGo.transform.localRotation = Quaternion.identity;
                fillGo.transform.localScale = new Vector3(0.00075f, 0.00075f, 0.00012f);

                Collider fillCol = fillGo.GetComponent<Collider>();
                if (fillCol != null) Object.DestroyImmediate(fillCol);

                Renderer fillRend = fillGo.GetComponent<Renderer>();
                if (fillRend != null)
                {
                    fillRend.sharedMaterial = GetOrCreateMaterialAsset("SpoonBrothFill_Mat", new Color(0.92f, 0.68f, 0.22f, 1f));
                }

                fillGo.SetActive(false);
                brothFillT = fillGo.transform;
            }
            else
            {
                brothFillT.gameObject.SetActive(false);
            }

            SpoonScoopController scoopCtrl = spoon.GetComponent<SpoonScoopController>();
            if (scoopCtrl == null) spoon.AddComponent<SpoonScoopController>();

            SpoonBrothScooper brothScooper = spoon.GetComponent<SpoonBrothScooper>();
            if (brothScooper == null) brothScooper = spoon.AddComponent<SpoonBrothScooper>();

            GameObject bowlBrothChild = bowlReceiver != null ? bowlReceiver.ChildBroth : null;
            brothScooper.Configure(
                brothFillT != null ? brothFillT.gameObject : null,
                brothPot,
                bowlReceiver,
                bowlBrothChild
            );
        }

        private static void CreateOrUpdateStandalonePrefabs(Transform streetRoot)
        {
            const string prefabFolder = "Assets/_ARR_Hackathon/Prefabs";
            if (!AssetDatabase.IsValidFolder(prefabFolder))
            {
                AssetDatabase.CreateFolder("Assets/_ARR_Hackathon", "Prefabs");
            }

            // 1. spoon.prefab
            Transform spoonT = streetRoot.Find("spoon");
            if (spoonT != null)
            {
                SaveCleanStandalonePrefab(spoonT.gameObject, $"{prefabFolder}/spoon.prefab", isSpoon: true);
            }

            // 2. meat.prefab
            Transform meatT = streetRoot.Find("meat");
            if (meatT != null)
            {
                GameObject meatPrefab = SaveCleanStandalonePrefab(meatT.gameObject, $"{prefabFolder}/meat.prefab", isSpoon: false);
                Transform meatBowlT = streetRoot.Find("meat_bowl");
                if (meatBowlT != null)
                {
                    IngredientSpawnerBowl spawner = meatBowlT.GetComponent<IngredientSpawnerBowl>();
                    if (spawner != null && meatPrefab != null)
                    {
                        spawner.Configure(meatPrefab, meatT.gameObject, "meat", ParentAction.GrabPork);
                    }
                }
            }

            // 3. egg.prefab
            Transform eggT = streetRoot.Find("egg");
            if (eggT != null)
            {
                GameObject eggPrefab = SaveCleanStandalonePrefab(eggT.gameObject, $"{prefabFolder}/egg.prefab", isSpoon: false);
                Transform eggBowlT = streetRoot.Find("egg_bowl");
                if (eggBowlT != null)
                {
                    IngredientSpawnerBowl spawner = eggBowlT.GetComponent<IngredientSpawnerBowl>();
                    if (spawner != null && eggPrefab != null)
                    {
                        spawner.Configure(eggPrefab, eggT.gameObject, "egg", ParentAction.DropEggIntoPalm);
                    }
                }
            }

            // 4. onion.prefab
            Transform onionT = streetRoot.Find("onion");
            if (onionT != null)
            {
                GameObject onionPrefab = SaveCleanStandalonePrefab(onionT.gameObject, $"{prefabFolder}/onion.prefab", isSpoon: false);
                Transform onionBowlT = streetRoot.Find("onion_bowl");
                if (onionBowlT != null)
                {
                    IngredientSpawnerBowl spawner = onionBowlT.GetComponent<IngredientSpawnerBowl>();
                    if (spawner != null && onionPrefab != null)
                    {
                        spawner.Configure(onionPrefab, onionT.gameObject, "onion", ParentAction.GrabOnions);
                    }
                }
            }

            // 5. noodle.prefab
            Transform noodleT = streetRoot.Find("noodle");
            if (noodleT != null)
            {
                GameObject noodlePrefab = SaveCleanStandalonePrefab(noodleT.gameObject, $"{prefabFolder}/noodle.prefab", isSpoon: false);
                Transform noodleBowlT = streetRoot.Find("noodle_bowl");
                if (noodleBowlT != null)
                {
                    IngredientSpawnerBowl spawner = noodleBowlT.GetComponent<IngredientSpawnerBowl>();
                    if (spawner != null && noodlePrefab != null)
                    {
                        spawner.Configure(noodlePrefab, noodleT.gameObject, "noodle", ParentAction.GrabNoodles);
                    }
                }
            }
        }

        private static GameObject SaveCleanStandalonePrefab(GameObject sourceGo, string assetPath, bool isSpoon)
        {
            GameObject clone = Object.Instantiate(sourceGo);
            clone.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            clone.transform.position = Vector3.zero;
            clone.transform.rotation = Quaternion.identity;

            Rigidbody rb = clone.GetComponent<Rigidbody>();
            if (rb == null) rb = clone.AddComponent<Rigidbody>();
            rb.mass = isSpoon ? 0.2f : 0.12f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.isKinematic = true;

            Grabbable grabbable = clone.GetComponent<Grabbable>();
            if (grabbable == null) grabbable = clone.AddComponent<Grabbable>();
            grabbable.InjectOptionalRigidbody(rb);
            grabbable.enabled = true;

            GrabInteractable gi = clone.GetComponent<GrabInteractable>();
            if (gi == null) gi = clone.AddComponent<GrabInteractable>();
            gi.InjectRigidbody(rb);
            gi.InjectOptionalPointableElement(grabbable);
            gi.UseClosestPointAsGrabSource = true;
            gi.enabled = true;

            HandGrabInteractable hgi = clone.GetComponent<HandGrabInteractable>();
            if (hgi == null) hgi = clone.AddComponent<HandGrabInteractable>();
            hgi.InjectRigidbody(rb);
            hgi.InjectOptionalPointableElement(grabbable);
            hgi.InjectSupportedGrabTypes(GrabTypeFlags.All);
            hgi.enabled = true;

            if (isSpoon)
            {
                Transform bodyColT = clone.transform.Find("spoon_body_collider");
                if (bodyColT == null)
                {
                    GameObject bodyColGo = new GameObject("spoon_body_collider");
                    bodyColGo.transform.SetParent(clone.transform, false);
                    bodyColGo.transform.localPosition = new Vector3(-0.0009f, -0.0002f, 0.00005f);
                    BoxCollider bc = bodyColGo.AddComponent<BoxCollider>();
                    bc.isTrigger = false;
                    bc.size = new Vector3(0.0022f, 0.0006f, 0.0006f);
                }

                Transform scoopTrig = clone.transform.Find("spoon_scoop_trigger");
                if (scoopTrig != null)
                {
                    SphereCollider sc = scoopTrig.GetComponent<SphereCollider>();
                    if (sc != null) sc.radius = 0.00045f;
                }
            }

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(clone, assetPath);
            Object.DestroyImmediate(clone);
            Debug.Log($"[TestStreet03Builder] Saved clean standalone prefab: {assetPath}");
            return savedPrefab;
        }

        private static void SetupGoldenNori(Transform street03Root)
        {
            GameObject mgr = GameObject.Find("[ARR_GameManager]");
            if (mgr != null)
            {
                // Single StretchableNoodle instance lives on [ARR_GameManager]
                if (mgr.GetComponent<StretchableNoodle>() == null)
                {
                    mgr.AddComponent<StretchableNoodle>();
                }

                // Wire BikeRideController.scrollingStreetTransform to Test Street03/street via serialized field
                // (ConfigureReferences() builds runtime visuals and must not run in edit mode)
                BikeRideController bike = mgr.GetComponent<BikeRideController>();
                if (bike != null && street03Root != null)
                {
                    Transform streetT = street03Root.Find("street");
                    if (streetT != null)
                    {
                        SerializedObject so = new SerializedObject(bike);
                        SerializedProperty prop = so.FindProperty("scrollingStreetTransform");
                        if (prop != null)
                        {
                            prop.objectReferenceValue = streetT;
                            so.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }
                }

                GoldenNoriController nori = mgr.GetComponent<GoldenNoriController>();
                if (nori == null) nori = mgr.AddComponent<GoldenNoriController>();

                ParentGuidanceHUDOverlay hud = mgr.GetComponent<ParentGuidanceHUDOverlay>();
                if (hud == null) mgr.AddComponent<ParentGuidanceHUDOverlay>();
            }
        }

        private static void SetupCustomerServingStation(Transform street03Root)
        {
            Transform bowlT = street03Root.Find("lamian_bowl");
            Transform pandaT = street03Root.Find("panda") ?? street03Root.Find("Panda");

            if (pandaT != null)
            {
                BoxCollider pandaBox = pandaT.GetComponent<BoxCollider>();
                if (pandaBox == null) pandaBox = pandaT.gameObject.AddComponent<BoxCollider>();
                pandaBox.isTrigger = true;
                pandaBox.size = new Vector3(1.2f, 1.4f, 1.2f);
                pandaBox.center = new Vector3(0f, 0.7f, 0f);
            }

            // 1. Create or resolve customer_serve_point
            Transform servePointT = street03Root.Find("customer_serve_point");
            if (servePointT == null)
            {
                GameObject servePointGo = new GameObject("customer_serve_point");
                servePointGo.transform.SetParent(street03Root, false);
                servePointGo.transform.position = new Vector3(7.35f, 1.11f, 0.52f);
                servePointT = servePointGo.transform;
            }

            BoxCollider serveBox = servePointT.GetComponent<BoxCollider>();
            if (serveBox == null) serveBox = servePointT.gameObject.AddComponent<BoxCollider>();
            serveBox.isTrigger = true;
            serveBox.size = new Vector3(0.50f, 0.35f, 0.50f);

            // 2. Attach CustomerServingStation to customer_serve_point
            CustomerServingStation station = servePointT.GetComponent<CustomerServingStation>();
            if (station == null) station = servePointT.gameObject.AddComponent<CustomerServingStation>();

            // 3. Create or resolve ConfettiFX
            Transform confettiT = street03Root.Find("ConfettiFX");
            ParticleSystem confettiPs = null;
            if (confettiT != null)
            {
                confettiPs = confettiT.GetComponent<ParticleSystem>();
            }

            // 4. Create or resolve FireworkFX
            Transform fireworkT = street03Root.Find("FireworkFX");
            ParticleSystem fireworkPs = null;
            if (fireworkT != null)
            {
                fireworkPs = fireworkT.GetComponent<ParticleSystem>();
            }

            Animator pandaAnim = pandaT != null ? (pandaT.GetComponent<Animator>() ?? pandaT.GetComponentInChildren<Animator>()) : null;
            station.Configure(
                bowlT != null ? bowlT.gameObject : null,
                servePointT,
                pandaAnim,
                confettiPs,
                fireworkPs
            );
        }

        // ponytail: Adds [ARR_AmbientAudio] with looping bgm and street noise at 50% volume.
        [MenuItem("Yatai Together/Setup Ambient Audio")]
        public static void SetupAmbientAudio()
        {
            const string bgmPath = "Assets/_ARR_Hackathon/Audio/bgm.mp3";
            const string streetNoisePath = "Assets/_ARR_Hackathon/Audio/street_noise.mp3";

            var bgmClip = AssetDatabase.LoadAssetAtPath<AudioClip>(bgmPath);
            var streetClip = AssetDatabase.LoadAssetAtPath<AudioClip>(streetNoisePath);

            GameObject audioGo = GameObject.Find("[ARR_AmbientAudio]");
            if (audioGo == null)
            {
                audioGo = new GameObject("[ARR_AmbientAudio]");
            }

            var controller = audioGo.GetComponent<AmbientAudioController>();
            if (controller == null) controller = audioGo.AddComponent<AmbientAudioController>();

            // Setup BGM
            Transform bgmT = audioGo.transform.Find("BGM");
            if (bgmT == null)
            {
                GameObject bgmGo = new GameObject("BGM");
                bgmGo.transform.SetParent(audioGo.transform, false);
                bgmT = bgmGo.transform;
            }
            var bgmSource = bgmT.GetComponent<AudioSource>();
            if (bgmSource == null) bgmSource = bgmT.gameObject.AddComponent<AudioSource>();
            bgmSource.clip = bgmClip;
            bgmSource.loop = true;
            bgmSource.volume = 0.5f;
            bgmSource.playOnAwake = true;
            bgmSource.spatialBlend = 0f;

            // Setup StreetNoise
            Transform streetT = audioGo.transform.Find("StreetNoise");
            if (streetT == null)
            {
                GameObject streetGo = new GameObject("StreetNoise");
                streetGo.transform.SetParent(audioGo.transform, false);
                streetT = streetGo.transform;
            }
            var streetSource = streetT.GetComponent<AudioSource>();
            if (streetSource == null) streetSource = streetT.gameObject.AddComponent<AudioSource>();
            streetSource.clip = streetClip;
            streetSource.loop = true;
            streetSource.volume = 0.5f;
            streetSource.playOnAwake = true;
            streetSource.spatialBlend = 0f;

            var so = new SerializedObject(controller);
            so.FindProperty("bgmSource").objectReferenceValue = bgmSource;
            so.FindProperty("streetNoiseSource").objectReferenceValue = streetSource;
            so.FindProperty("volume").floatValue = 0.5f;
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(audioGo.scene);
            Debug.Log("[TestStreet03Builder] Ambient audio setup completed with bgm.mp3 and street_noise.mp3 on loop at 50% volume.");
        }
    }
}
#endif
