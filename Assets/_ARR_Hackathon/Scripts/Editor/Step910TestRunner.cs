#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YataiTogether;

namespace YataiTogether.Editor
{
    public static class Step910TestRunner
    {
        private const string MainScenePath = "Assets/_ARR_Hackathon/Scenes/YataiTogether_Main.unity";

        [MenuItem("Yatai Together/Attach Step 9 & 10 Acceptance Tester")]
        public static void AttachTester()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != MainScenePath)
            {
                EditorSceneManager.OpenScene(MainScenePath);
            }

            // Remove any old Step678AcceptanceTester
            Step678AcceptanceTester old678 = Object.FindFirstObjectByType<Step678AcceptanceTester>();
            if (old678 != null)
            {
                Object.DestroyImmediate(old678);
            }

            // Remove existing Step910AcceptanceTester if any
            Step910AcceptanceTester old910 = Object.FindFirstObjectByType<Step910AcceptanceTester>();
            if (old910 != null)
            {
                Object.DestroyImmediate(old910);
            }

            GameObject mgr = GameObject.Find("[ARR_GameManager]");
            if (mgr == null)
            {
                mgr = new GameObject("[ARR_GameManager]");
            }

            mgr.AddComponent<Step910AcceptanceTester>();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log("[Step910TestRunner] Step910AcceptanceTester attached to [ARR_GameManager] and scene saved.");
        }

        [MenuItem("Yatai Together/Remove Step 9 & 10 Acceptance Tester")]
        public static void RemoveTester()
        {
            Step910AcceptanceTester tester = Object.FindFirstObjectByType<Step910AcceptanceTester>();
            if (tester != null)
            {
                Object.DestroyImmediate(tester);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                Debug.Log("[Step910TestRunner] Step910AcceptanceTester removed.");
            }
        }
    }
}
#endif
