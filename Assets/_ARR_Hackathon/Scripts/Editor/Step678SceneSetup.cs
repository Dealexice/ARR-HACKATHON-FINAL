#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using YataiTogether;

namespace YataiTogether.Editor
{
    public static class Step678SceneSetup
    {
        private const string MainScenePath = "Assets/_ARR_Hackathon/Scenes/YataiTogether_Main.unity";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Yatai Together/Setup Step 6, 7 & 8 Cooking Assembly")]
        public static void Setup()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != MainScenePath)
            {
                EditorSceneManager.OpenScene(MainScenePath);
            }

            Debug.Log("[Step678SceneSetup] Starting setup for Steps 6, 7 & 8...");

            SetupTableCollider();
            SetupLamianBowl();
            SetupBoiler();
            SetupSpoon();
            SetupPork();
            SetupEgg();
            SetupGameManager();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            // Sync to SampleScene
            FileUtil.ReplaceFile(MainScenePath, SampleScenePath);
            AssetDatabase.Refresh();

            Debug.Log("[Step678SceneSetup] Setup complete and synced to SampleScene!");
        }

        private static void SetupTableCollider()
        {
            GameObject tableColGo = GameObject.Find("counter_table_collider");
            if (tableColGo == null)
            {
                tableColGo = new GameObject("counter_table_collider");
            }
            tableColGo.transform.position = new Vector3(7.05f, 1.20f, 0.60f);
            BoxCollider box = tableColGo.GetComponent<BoxCollider>();
            if (box == null) box = tableColGo.AddComponent<BoxCollider>();
            box.size = new Vector3(1.2f, 0.05f, 2.5f);
        }

        private static void SetupLamianBowl()
        {
            GameObject bowl = GameObject.Find("lamian_bowl");
            if (bowl == null)
            {
                Debug.LogError("[Step678SceneSetup] lamian_bowl not found!");
                return;
            }

            // Ensure bowl trigger collider
            BoxCollider col = bowl.GetComponent<BoxCollider>();
            if (col == null) col = bowl.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.005f, 0f);
            col.size = new Vector3(0.022f, 0.015f, 0.022f);

            // Reparent child ingredients if they are currently siblings under Test Street
            Transform street = GameObject.Find("Test Street")?.transform;

            Transform noodleT = bowl.transform.Find("lamian_noodle") ?? street?.Find("lamian_noodle");
            if (noodleT != null && noodleT.parent != bowl.transform)
            {
                noodleT.SetParent(bowl.transform, true);
            }

            Transform meatT = bowl.transform.Find("lamian_meat") ?? street?.Find("lamian_meat");
            if (meatT != null && meatT.parent != bowl.transform)
            {
                meatT.SetParent(bowl.transform, true);
            }

            Transform eggT = bowl.transform.Find("lamian_egg") ?? street?.Find("lamian_egg");
            if (eggT != null && eggT.parent != bowl.transform)
            {
                eggT.SetParent(bowl.transform, true);
            }

            Transform onionT = bowl.transform.Find("lamian_onion") ?? street?.Find("lamian_onion");
            if (onionT != null && onionT.parent != bowl.transform)
            {
                onionT.SetParent(bowl.transform, true);
            }

            GameObject noodle = noodleT != null ? noodleT.gameObject : null;
            GameObject meat = meatT != null ? meatT.gameObject : null;
            GameObject egg = eggT != null ? eggT.gameObject : null;
            GameObject onion = onionT != null ? onionT.gameObject : null;

            // Ensure broth child
            Transform brothT = bowl.transform.Find("lamian_broth");
            GameObject brothGo = null;
            if (brothT != null)
            {
                brothGo = brothT.gameObject;
            }
            else
            {
                brothGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                brothGo.name = "lamian_broth";
                brothGo.transform.SetParent(bowl.transform, false);
                brothGo.transform.localPosition = new Vector3(0f, 0.08f, 0f);
                brothGo.transform.localScale = new Vector3(0.46f, 0.008f, 0.46f);

                // Remove collider from visual broth surface
                Collider c = brothGo.GetComponent<Collider>();
                if (c != null) Object.DestroyImmediate(c);

                // Broth material
                Renderer r = brothGo.GetComponent<Renderer>();
                GameObject brothPot = GameObject.Find("broth");
                if (brothPot != null && brothPot.GetComponent<Renderer>() != null)
                {
                    r.sharedMaterial = brothPot.GetComponent<Renderer>().sharedMaterial;
                }
            }

            // Ensure RamenAssemblyReceiver component
            RamenAssemblyReceiver receiver = bowl.GetComponent<RamenAssemblyReceiver>();
            if (receiver == null) receiver = bowl.AddComponent<RamenAssemblyReceiver>();

            receiver.SetChildMeshes(
                noodle,
                meat,
                egg,
                onion,
                brothGo
            );

            // Initialize all child meshes as inactive initially
            if (noodle != null) noodle.SetActive(false);
            if (meat != null) meat.SetActive(false);
            if (egg != null) egg.SetActive(false);
            if (onion != null) onion.SetActive(false);
            if (brothGo != null) brothGo.SetActive(false);

            Debug.Log("[Step678SceneSetup] lamian_bowl configured with RamenAssemblyReceiver and child ingredients.");
        }

        private static void SetupBoiler()
        {
            GameObject boiler = GameObject.Find("boiler");
            if (boiler == null)
            {
                Debug.LogError("[Step678SceneSetup] boiler not found!");
                return;
            }

            BoilerStation station = boiler.GetComponent<BoilerStation>();
            if (station == null) station = boiler.AddComponent<BoilerStation>();

            BoxCollider col = boiler.GetComponent<BoxCollider>();
            if (col == null) col = boiler.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.005f, 0f);
            col.size = new Vector3(0.022f, 0.020f, 0.022f);

            Transform snapPoint = boiler.transform.Find("potSnapPoint");
            if (snapPoint == null)
            {
                GameObject snapGo = new GameObject("potSnapPoint");
                snapGo.transform.SetParent(boiler.transform, false);
                snapGo.transform.localPosition = new Vector3(0f, 0.10f, 0f);
                snapPoint = snapGo.transform;
            }

            BoxCollider snapCol = snapPoint.GetComponent<BoxCollider>();
            if (snapCol == null) snapCol = snapPoint.gameObject.AddComponent<BoxCollider>();
            snapCol.isTrigger = true;
            snapCol.center = Vector3.zero;
            snapCol.size = new Vector3(0.022f, 0.020f, 0.022f);

            BoilerPotSnapReceiver snapReceiver = snapPoint.GetComponent<BoilerPotSnapReceiver>();
            if (snapReceiver == null) snapReceiver = snapPoint.gameObject.AddComponent<BoilerPotSnapReceiver>();
            snapReceiver.Configure(snapPoint, 3.0f, null);

            station.Configure(snapPoint, boiler.GetComponent<Renderer>());

            Debug.Log("[Step678SceneSetup] boiler configured with BoilerStation & trigger collider.");
        }

        private static void SetupSpoon()
        {
            GameObject spoon = GameObject.Find("spoon");
            if (spoon == null)
            {
                Debug.LogError("[Step678SceneSetup] spoon not found!");
                return;
            }

            spoon.name = "spoon";

            Rigidbody rb = spoon.GetComponent<Rigidbody>();
            if (rb == null) rb = spoon.AddComponent<Rigidbody>();
            rb.mass = 0.2f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            MeshCollider mc = spoon.GetComponent<MeshCollider>();
            if (mc == null) mc = spoon.AddComponent<MeshCollider>();
            mc.convex = true;

            Grabbable grabbable = spoon.GetComponent<Grabbable>();
            if (grabbable == null) grabbable = spoon.AddComponent<Grabbable>();

            HandGrabInteractable hgi = spoon.GetComponent<HandGrabInteractable>();
            if (hgi == null)
            {
                hgi = spoon.AddComponent<HandGrabInteractable>();
                hgi.InjectRigidbody(rb);
                hgi.InjectOptionalPointableElement(grabbable);
            }

            SpoonScoopController scoop = spoon.GetComponent<SpoonScoopController>();
            if (scoop == null) scoop = spoon.AddComponent<SpoonScoopController>();

            Debug.Log("[Step678SceneSetup] spoon configured with SpoonScoopController, Rigidbody & HandGrabInteractable.");
        }

        private static void SetupPork()
        {
            GameObject meat = GameObject.Find("meat");
            GameObject meatBowl = GameObject.Find("meat_bowl");

            if (meat == null)
            {
                Debug.LogError("[Step678SceneSetup] meat not found!");
                return;
            }

            meat.name = "meat";

            Rigidbody rb = meat.GetComponent<Rigidbody>();
            if (rb == null) rb = meat.AddComponent<Rigidbody>();
            rb.mass = 0.1f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            MeshCollider mc = meat.GetComponent<MeshCollider>();
            if (mc == null) mc = meat.AddComponent<MeshCollider>();
            mc.convex = true;

            Grabbable grabbable = meat.GetComponent<Grabbable>();
            if (grabbable == null) grabbable = meat.AddComponent<Grabbable>();

            HandGrabInteractable hgi = meat.GetComponent<HandGrabInteractable>();
            if (hgi == null)
            {
                hgi = meat.AddComponent<HandGrabInteractable>();
                hgi.InjectRigidbody(rb);
                hgi.InjectOptionalPointableElement(grabbable);
            }

            GameObject porkStationTarget = meatBowl != null ? meatBowl : meat;
            PorkStationController porkCtrl = porkStationTarget.GetComponent<PorkStationController>();
            if (porkCtrl == null) porkCtrl = porkStationTarget.AddComponent<PorkStationController>();

            porkCtrl.Configure(meat, meatBowl != null ? meatBowl.transform : null, null);

            Debug.Log("[Step678SceneSetup] Chashu pork configured with PorkStationController & HandGrabInteractable.");
        }

        private static void SetupEgg()
        {
            GameObject egg = GameObject.Find("egg");
            GameObject eggBowl = GameObject.Find("egg_bowl");

            if (eggBowl == null)
            {
                Debug.LogError("[Step678SceneSetup] egg_bowl not found!");
                return;
            }

            if (egg == null)
            {
                GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Test Street.fbx");
                Transform fbxEgg = fbx?.transform.Find("egg");
                if (fbxEgg != null)
                {
                    egg = Object.Instantiate(fbxEgg.gameObject);
                    egg.name = "egg";
                    GameObject testStreet = GameObject.Find("Test Street");
                    if (testStreet != null)
                    {
                        egg.transform.SetParent(testStreet.transform, false);
                        egg.transform.localPosition = new Vector3(0.303f, 0.203f, -1.791f);
                        egg.transform.localScale = new Vector3(4.28f, 4.28f, 4.28f);
                    }
                }
            }

            if (egg == null)
            {
                Debug.LogError("[Step678SceneSetup] Could not create or find egg!");
                return;
            }

            egg.name = "egg";

            Rigidbody rb = egg.GetComponent<Rigidbody>();
            if (rb == null) rb = egg.AddComponent<Rigidbody>();
            rb.mass = 0.1f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.isKinematic = true;

            SphereCollider sc = egg.GetComponent<SphereCollider>();
            if (sc == null) sc = egg.AddComponent<SphereCollider>();
            sc.radius = 0.010f;

            Grabbable grabbable = egg.GetComponent<Grabbable>();
            if (grabbable == null) grabbable = egg.AddComponent<Grabbable>();

            HandGrabInteractable hgi = egg.GetComponent<HandGrabInteractable>();
            if (hgi == null)
            {
                hgi = egg.AddComponent<HandGrabInteractable>();
                hgi.InjectRigidbody(rb);
                hgi.InjectOptionalPointableElement(grabbable);
            }

            EggStationController eggCtrl = eggBowl.GetComponent<EggStationController>();
            if (eggCtrl == null) eggCtrl = eggBowl.AddComponent<EggStationController>();

            Transform chute = eggBowl.transform.Find("chute_drop_point");
            if (chute == null)
            {
                GameObject chuteGo = new GameObject("chute_drop_point");
                chuteGo.transform.SetParent(eggBowl.transform, false);
                chuteGo.transform.localPosition = new Vector3(0f, 0.22f, 0f);
                chute = chuteGo.transform;
            }

            eggCtrl.Configure(egg, chute, null);

            Debug.Log("[Step678SceneSetup] Ajitama egg configured with EggStationController & HandGrabInteractable.");
        }

        private static void SetupGameManager()
        {
            GameObject mgr = GameObject.Find("[ARR_GameManager]");
            if (mgr != null)
            {
                ParentGuidanceHUDOverlay hud = mgr.GetComponent<ParentGuidanceHUDOverlay>();
                if (hud == null) hud = mgr.AddComponent<ParentGuidanceHUDOverlay>();
            }
            Debug.Log("[Step678SceneSetup] ParentGuidanceHUDOverlay attached to [ARR_GameManager].");
        }
    }
}
#endif
