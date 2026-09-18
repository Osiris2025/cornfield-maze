#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CornMaze.EditorTools
{
    public static class CornMazeSetup
    {
        const string ScenePath = "Assets/Scenes/CornMaze.unity";
        const string MacBuildPath = "Builds/Corn Field Maze.app";
        const string IosBuildPath = "Builds/iOS";

        [MenuItem("Corn Maze/Apply Fullscreen Player Settings")]
        public static void ApplyFullscreenPlayerSettings()
        {
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.allowFullscreenSwitch = true;
            AssetDatabase.SaveAssets();
            Debug.Log("Corn Field Maze: Player Settings fullscreen mode is Fullscreen Window. " +
                      "Build a standalone .app (Corn Maze > Build Standalone macOS App, or File > Build Settings) to play outside the Unity editor.");
        }

        [MenuItem("Corn Maze/Open Build Settings…")]
        public static void OpenBuildSettings()
        {
            ApplyFullscreenPlayerSettings();
            EnsureSceneInBuild();
            BuildPlayerWindow.ShowBuildPlayerWindow();
        }

        /// <summary>
        /// Where the playable .app is written. Defaults to the project's Builds/ folder, but
        /// CORN_BUILD_OUT overrides it — necessary here because the project sits on exFAT, which
        /// cannot hard-link, and Unity's macOS build links resources into the bundle.
        /// </summary>
        static string ResolveMacBuildPath()
        {
            var over = System.Environment.GetEnvironmentVariable("CORN_BUILD_OUT");
            if (!string.IsNullOrEmpty(over))
            {
                return Path.Combine(over, "Corn Field Maze.app");
            }
            return MacBuildPath;
        }

        [MenuItem("Corn Maze/Build Standalone macOS App")]
        public static void BuildStandaloneMac()
        {
            ApplyFullscreenPlayerSettings();
            EnsureSceneInBuild();
            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog(
                    "Corn Field Maze",
                    "The CornMaze scene is missing. Open the project so setup can create Assets/Scenes/CornMaze, then try again.",
                    "OK");
                return;
            }

            // The build output must live on a filesystem that supports HARD LINKS. Unity's
            // macOS build links resources into the .app bundle rather than copying them, and
            // this project lives on an exFAT volume where `ln` returns 'Operation not
            // supported' — which surfaces in the build log as
            //   Copying .../unity_builtin_extra failed: Operation not permitted
            // after first creating the file at zero bytes, and then as 'Tundra build failed'.
            // Point CORN_BUILD_OUT at an APFS path to get a playable app.
            var outPath = ResolveMacBuildPath();
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outPath,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log("Built standalone player: " + Path.GetFullPath(outPath) +
                          " — double-click the .app to play outside Unity. F11 or Cmd+F toggles fullscreen.");
                EditorUtility.RevealInFinder(outPath);
            }
            else
            {
                Debug.LogError("Standalone macOS build failed: " + report.summary.result);
            }
        }

        [MenuItem("Corn Maze/Apply iOS Player Settings")]
        public static void ApplyIosPlayerSettings()
        {
            PlayerSettings.companyName = "arl480";
            PlayerSettings.productName = "Corn Field Maze";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.arl480.cornfieldmaze");
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(NamedBuildTarget.iOS, 1);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Low);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.allowFullscreenSwitch = true;
            // Playback category: game audio continues with the iPhone mute switch on.
            PlayerSettings.muteOtherAudioSources = true;
            // This project has no [BurstCompile] usage; Burst is only transitive via Collections.
            // Disable Burst AOT for iOS so Xcode does not link lib_burst_generated.a objects
            // missing LC_BUILD_VERSION (Xcode 16+/26 warning/error: "No platform load command").
            EnsureIosBurstAotDisabled();
            AssetDatabase.SaveAssets();
            Debug.Log("Corn Field Maze: iOS player settings applied (com.arl480.cornfieldmaze, ARM64, IL2CPP, iOS 15+, landscape, muteOtherAudioSources, Burst AOT off).");
        }

        [MenuItem("Corn Maze/Build iOS Xcode Project")]
        public static void BuildIosPlayer()
        {
            ApplyIosPlayerSettings();
            EnsureSceneInBuild();
            if (!File.Exists(ScenePath))
            {
                var message = "The CornMaze scene is missing. Open the project so setup can create Assets/Scenes/CornMaze, then try again.";
                Debug.LogError(message);
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("Corn Field Maze", message, "OK");
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
                return;
            }

            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
            {
                var message = "iOS Build Support is not installed for this Unity editor. In Unity Hub: Installs → 6000.3.23f1 → Add modules → iOS Build Support.";
                Debug.LogError(message);
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
                return;
            }

            EditorUserBuildSettings.SwitchActiveBuildTarget(NamedBuildTarget.iOS, BuildTarget.iOS);
            Directory.CreateDirectory(IosBuildPath);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = IosBuildPath,
                target = BuildTarget.iOS,
                options = BuildOptions.Development
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                var full = Path.GetFullPath(IosBuildPath);
                Debug.Log("Built iOS Xcode project: " + full +
                          " — open Unity-iPhone.xcodeproj in Xcode, pick your iPhone, set Signing Team, then Run.");
                if (!Application.isBatchMode)
                    EditorUtility.RevealInFinder(IosBuildPath);
            }
            else
            {
                Debug.LogError("iOS build failed: " + report.summary.result);
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
            }
        }

        public static void Run()
        {
            PlayerSettings.companyName = "arl480";
            PlayerSettings.productName = "Corn Field Maze";
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Low);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (cam.gameObject.name.Contains("Main Camera") || cam.CompareTag("MainCamera"))
                    Object.DestroyImmediate(cam.gameObject);
            }

            var game = new GameObject("Game");
            game.AddComponent<GameBootstrap>();

            var light = Object.FindFirstObjectByType<Light>();
            if (light != null)
            {
                light.color = new Color(1f, 0.86f, 0.55f);
                light.intensity = 1.35f;
                light.transform.rotation = Quaternion.Euler(38f, 155f, 0f);
            }

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            EditorSceneManager.playModeStartScene = sceneAsset;
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            EditorSceneManager.OpenScene(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Corn Field Maze scene is ready. Press Play.");
        }

        static void EnsureIosBurstAotDisabled()
        {
            const string path = "ProjectSettings/BurstAotSettings_iOS.json";
            var json =
                "{\n" +
                "  \"MonoBehaviour\": {\n" +
                "    \"Version\": 5,\n" +
                "    \"EnableBurstCompilation\": false,\n" +
                "    \"EnableOptimisations\": true,\n" +
                "    \"EnableSafetyChecks\": false,\n" +
                "    \"EnableDebugInAllBuilds\": false,\n" +
                "    \"DebugDataKind\": 1,\n" +
                "    \"EnableArmv9SecurityFeatures\": false,\n" +
                "    \"CpuMinTargetX32\": 0,\n" +
                "    \"CpuMaxTargetX32\": 0,\n" +
                "    \"CpuMinTargetX64\": 0,\n" +
                "    \"CpuMaxTargetX64\": 0,\n" +
                "    \"OptimizeFor\": 0,\n" +
                "    \"FloatMode\": 0\n" +
                "  }\n" +
                "}\n";
            File.WriteAllText(path, json);
        }

        static void EnsureSceneInBuild()
        {
            if (!File.Exists(ScenePath)) return;
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
        }
    }
}
#endif
