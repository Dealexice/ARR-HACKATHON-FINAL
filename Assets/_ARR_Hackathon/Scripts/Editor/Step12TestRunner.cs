#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YataiTogether;

namespace YataiTogether.Editor
{
    public static class Step12TestRunner
    {
        private const string MainScenePath = "Assets/_ARR_Hackathon/Scenes/YataiTogether_Main.unity";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Yatai Together/Attach Step 12 Acceptance Tester")]
        public static void AttachTester()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != MainScenePath)
            {
                EditorSceneManager.OpenScene(MainScenePath);
            }

            // Remove all existing tester instances to ensure strictly ONE runner
            RemoveAllTestersInternal();

            GameObject testerGo = new GameObject("__Step12Tester");
            testerGo.AddComponent<Step12AcceptanceTester>();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            FileUtil.ReplaceFile(MainScenePath, SampleScenePath);
            AssetDatabase.Refresh();

            Debug.Log("[Step12TestRunner] Single Step12AcceptanceTester attached cleanly to scene and synced to SampleScene.");
        }

        [MenuItem("Yatai Together/Remove Step 12 Acceptance Tester")]
        public static void RemoveTester()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != MainScenePath)
            {
                EditorSceneManager.OpenScene(MainScenePath);
            }

            RemoveAllTestersInternal();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            FileUtil.ReplaceFile(MainScenePath, SampleScenePath);
            AssetDatabase.Refresh();

            Debug.Log("[Step12TestRunner] All testers removed cleanly.");
        }

        private static void RemoveAllTestersInternal()
        {
            // Remove any __Step12Tester GameObjects
            GameObject[] allGos = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in allGos)
            {
                if (go != null && (go.name == "__Step12Tester" || go.name == "__Step11Tester"))
                {
                    Object.DestroyImmediate(go);
                }
            }

            Step12AcceptanceTester[] testers12 = Object.FindObjectsByType<Step12AcceptanceTester>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in testers12)
            {
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }

            Step11AcceptanceTester[] testers11 = Object.FindObjectsByType<Step11AcceptanceTester>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in testers11)
            {
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }

            Step910AcceptanceTester[] testers910 = Object.FindObjectsByType<Step910AcceptanceTester>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in testers910)
            {
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }

            Step678AcceptanceTester[] testers678 = Object.FindObjectsByType<Step678AcceptanceTester>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in testers678)
            {
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }
        }
    }
}
#endif
