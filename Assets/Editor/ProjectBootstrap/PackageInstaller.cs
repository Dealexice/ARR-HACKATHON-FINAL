using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace ProjectBootstrap
{
    // Installs the Meta Quest 3 Bare-Hand Tracking, Interaction SDK, Spatial Audio,
    // Haptics, OpenXR, XR Hands, XRI, and Procedural Visual/Mesh packages via UPM Client API.
    public static class PackageInstaller
    {
        const string SessionKey = "ProjectBootstrap_PackageInstaller_Running";

        static readonly string[] PackagesToAdd =
        {
            "com.unity.xr.management",
            "com.unity.xr.openxr",
            "com.unity.xr.hands",
            "com.unity.xr.interaction.toolkit",
            "com.meta.xr.sdk.core",
            "com.meta.xr.sdk.interaction",
            "com.meta.xr.sdk.interaction.ovr",
            "com.meta.xr.mrutilitykit",
            "com.meta.xr.sdk.audio",
            "com.meta.xr.sdk.haptics",
            "com.unity.splines",
            "com.unity.probuilder",
            "com.unity.mathematics",
            "com.unity.timeline"
        };

        static readonly string[] PackagesToRemove = { };

        const double TimeoutSeconds = 600;

        static AddAndRemoveRequest _request;
        static double _deadline;

        [InitializeOnLoadMethod]
        static void OnDomainReloadAfterInstall()
        {
            if (!Application.isBatchMode || !SessionState.GetBool(SessionKey, false))
                return;

            string manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
            if (File.Exists(manifestPath))
            {
                string manifest = File.ReadAllText(manifestPath);
                if (PackagesToAdd.All(pkg => manifest.Contains($"\"{pkg.Split('@')[0]}\"")))
                {
                    SessionState.EraseBool(SessionKey);
                    Debug.Log("[PackageInstaller] All packages confirmed in manifest.json after domain reload. Exiting 0.");
                    EditorApplication.delayCall += () => EditorApplication.Exit(0);
                }
            }
        }

        // Invoke with: -executeMethod ProjectBootstrap.PackageInstaller.Install  (NO -quit)
        public static void Install()
        {
            if (PackagesToAdd.Length == 0 && PackagesToRemove.Length == 0)
            {
                Debug.Log("[PackageInstaller] Nothing to do.");
                EditorApplication.Exit(0);
                return;
            }

            SessionState.SetBool(SessionKey, true);
            Debug.Log($"[PackageInstaller] Adding: {string.Join(", ", PackagesToAdd)}");
            _request = Client.AddAndRemove(packagesToAdd: PackagesToAdd, packagesToRemove: PackagesToRemove);
            _deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (_request == null) return;

            if (!_request.IsCompleted)
            {
                if (EditorApplication.timeSinceStartup > _deadline)
                {
                    EditorApplication.update -= Poll;
                    SessionState.EraseBool(SessionKey);
                    Debug.LogError("[PackageInstaller] Timed out waiting for UPM.");
                    EditorApplication.Exit(2);
                }
                return;
            }

            EditorApplication.update -= Poll;
            SessionState.EraseBool(SessionKey);

            if (_request.Status == StatusCode.Success)
            {
                var names = _request.Result.Select(p => $"{p.name}@{p.version}");
                Debug.Log($"[PackageInstaller] Resolved: {string.Join(", ", names)}");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[PackageInstaller] Failed: {_request.Error?.message}");
                EditorApplication.Exit(1);
            }
        }
    }
}
