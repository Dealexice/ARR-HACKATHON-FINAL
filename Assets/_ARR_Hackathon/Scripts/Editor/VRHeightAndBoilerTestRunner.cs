#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YataiTogether;

namespace YataiTogether.Editor
{
    public static class VRHeightAndBoilerTestRunner
    {
        private const string MainScenePath = "Assets/_ARR_Hackathon/Scenes/YataiTogether_Main.unity";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Yatai Together/Attach VR Height and Boiler Tester")]
        public static void AttachTester()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != MainScenePath)
            {
                EditorSceneManager.OpenScene(MainScenePath);
            }

            RemoveAllTestersInternal();

            GameObject testerGo = new GameObject("__VRHeightBoilerTester");
            testerGo.AddComponent<VRHeightAndBoilerSnapAcceptanceTester>();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            FileUtil.ReplaceFile(MainScenePath, SampleScenePath);
            AssetDatabase.Refresh();

            Debug.Log("[VRHeightAndBoilerTestRunner] Attached VRHeightAndBoilerSnapAcceptanceTester and synced scenes.");
        }

        [MenuItem("Yatai Together/Remove VR Height and Boiler Tester")]
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

            Debug.Log("[VRHeightAndBoilerTestRunner] Removed all testers and synced scenes.");
        }

        private static void RemoveAllTestersInternal()
        {
            GameObject[] allGos = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in allGos)
            {
                if (go != null && (go.name == "__VRHeightBoilerTester" || go.name == "__Step12Tester" || go.name == "__Step11Tester"))
                {
                    Object.DestroyImmediate(go);
                }
            }

            var testers = Object.FindObjectsByType<VRHeightAndBoilerSnapAcceptanceTester>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in testers)
            {
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }
        }
    }
}
#endif
