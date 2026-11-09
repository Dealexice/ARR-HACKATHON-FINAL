#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Text;

namespace YataiTogether.Editor
{
    public static class TestStreetInspector
    {
        [MenuItem("Yatai Together/Inspect Street Differences")]
        public static void InspectDifferences()
        {
            GameObject fbx2 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_ARR_Hackathon/Prefabs/Test street02.fbx");
            GameObject prefab1 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_ARR_Hackathon/Prefabs/Test Street.prefab");

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== INSPECTING TEST STREET 02 (FBX) ===");
            if (fbx2 != null)
            {
                foreach (Transform child in fbx2.transform)
                {
                    MeshFilter mf = child.GetComponent<MeshFilter>();
                    Renderer rend = child.GetComponent<Renderer>();
                    int vertCount = (mf != null && mf.sharedMesh != null) ? mf.sharedMesh.vertexCount : 0;
                    Vector3 meshBounds = (mf != null && mf.sharedMesh != null) ? mf.sharedMesh.bounds.size : Vector3.zero;
                    Vector3 worldBounds = (rend != null) ? rend.bounds.size : Vector3.zero;
                    sb.AppendLine($"FBX2: '{child.name}' pos={child.localPosition} scale={child.localScale} meshBounds={meshBounds} worldBounds={worldBounds}");
                }
            }
            else
            {
                sb.AppendLine("FBX2 NOT FOUND!");
            }

            sb.AppendLine("\n=== INSPECTING CURRENT PREFAB 1 (Test Street.prefab) ===");
            if (prefab1 != null)
            {
                foreach (Transform child in prefab1.transform)
                {
                    MeshFilter mf = child.GetComponent<MeshFilter>();
                    int vertCount = (mf != null && mf.sharedMesh != null) ? mf.sharedMesh.vertexCount : 0;
                    sb.AppendLine($"PREFAB1: '{child.name}' pos={child.localPosition} rot={child.localEulerAngles} scale={child.localScale} verts={vertCount}");
                }
            }
            else
            {
                sb.AppendLine("PREFAB1 NOT FOUND!");
            }

            sb.AppendLine("\n=== INSPECTING PREFAB 03 (Test Street03.prefab) ===");
            GameObject prefab03 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_ARR_Hackathon/Prefabs/Test Street03.prefab");
            if (prefab03 != null)
            {
                foreach (Transform child in prefab03.transform)
                {
                    MeshFilter mf = child.GetComponent<MeshFilter>();
                    int vertCount = (mf != null && mf.sharedMesh != null) ? mf.sharedMesh.vertexCount : 0;
                    sb.AppendLine($"PREFAB03: '{child.name}' pos={child.localPosition} scale={child.localScale} verts={vertCount} components={string.Join(", ", System.Array.ConvertAll(child.GetComponents<Component>(), c => c.GetType().Name))}");
                    if (child.name == "lamian_bowl")
                    {
                        foreach (Transform bowlChild in child)
                        {
                            sb.AppendLine($"   -> BowlChild: '{bowlChild.name}' active={bowlChild.gameObject.activeSelf} pos={bowlChild.localPosition}");
                        }
                    }
                }
            }
            else
            {
                sb.AppendLine("PREFAB03 NOT FOUND!");
            }

            sb.AppendLine("\n=== INSPECTING SCENE 'Test Street03' ===");
            GameObject sceneStreet = GameObject.Find("Test Street03") ?? GameObject.Find("Test Street");
            if (sceneStreet != null)
            {
                sb.AppendLine($"Scene '{sceneStreet.name}' Root: pos={sceneStreet.transform.position} rot={sceneStreet.transform.eulerAngles} scale={sceneStreet.transform.localScale}");
                foreach (Transform child in sceneStreet.transform)
                {
                    sb.AppendLine($"Scene: '{child.name}' localPos={child.localPosition} worldPos={child.position} localScale={child.localScale}");
                }
            }
            else
            {
                sb.AppendLine("Scene Street root not found!");
            }

            Debug.Log(sb.ToString());
        }
    }
}
#endif
