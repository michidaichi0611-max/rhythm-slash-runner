using System;
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmSlashRunner.EditorTools
{
    /// <summary>
    /// Menu + batch-mode builds. Example:
    /// Unity.exe -batchmode -quit -projectPath unity_project -executeMethod RhythmSlashRunner.EditorTools.BuildTools.BuildWindows
    /// Store builds (AAB) read the upload key from environment variables and never from the repository:
    /// RSR_KEYSTORE (path), RSR_KEYSTORE_PASS, RSR_KEY_ALIAS, RSR_KEY_PASS.
    /// iPhone (round 32): BuildIOS writes an Xcode project to Builds/iOS (Windows can export it; signing and installing need
    /// a Mac with Xcode). Optional RSR_APPLE_TEAM (Apple Developer Team ID) turns on automatic signing for that team.
    /// </summary>
    public static class BuildTools
    {
        const string ScenePath = "Assets/RhythmRunner/Scenes/Main.unity";
        const string IconDir = "Assets/RhythmRunner/Icons/";
        const string Version = "1.0";
        static string BuildNumberFile { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "../build_number.txt")); } }

        [MenuItem("Rhythm Slash Runner/Ensure Main Scene")]
        public static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                var camera = UnityEngine.Object.FindAnyObjectByType<Camera>();
                if (camera != null) { camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.02f, .04f, .09f); }
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        /// <summary>Monotonic build number for Android versionCode; bumped once per store build.</summary>
        static int NextBuildNumber(bool bump)
        {
            int n = 1;
            if (File.Exists(BuildNumberFile)) int.TryParse(File.ReadAllText(BuildNumberFile).Trim(), out n);
            if (bump) { n++; File.WriteAllText(BuildNumberFile, n.ToString()); }
            return Math.Max(1, n);
        }

        static void ApplyPlayerSettings(bool bumpVersion)
        {
            int build = NextBuildNumber(bumpVersion);
            PlayerSettings.companyName = "HamsterRhythmLab";
            PlayerSettings.productName = "Rhythm Slash Runner";
            PlayerSettings.bundleVersion = Version + "." + build;
            PlayerSettings.Android.bundleVersionCode = build;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.hamsterrhythmlab.rhythmslashrunner");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36; // explicit for reproducible builds (release P2)
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            // round 34 (app size): smaller native code; Medium stripping keeps what JsonUtility, Unity IAP and Play Games reflect on
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.Android, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.iOS, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Medium);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Medium);
            // iPhone (round 32): portrait, full screen, no status bar; the home-indicator swipe needs a second swipe so a
            // tap near the bottom edge never leaves the game mid-run
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.hamsterrhythmlab.rhythmslashrunner");
            PlayerSettings.iOS.buildNumber = build.ToString();
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.iOS.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.All;
            PlayerSettings.iOS.hideHomeButton = false;
            PlayerSettings.iOS.appInBackgroundBehavior = iOSAppInBackgroundBehavior.Suspend;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            string team = Environment.GetEnvironmentVariable("RSR_APPLE_TEAM");
            PlayerSettings.iOS.appleEnableAutomaticSigning = !string.IsNullOrEmpty(team);
            if (!string.IsNullOrEmpty(team)) PlayerSettings.iOS.appleDeveloperTeamID = team;
            ApplyIcons();
            ApplyLaunchScreen();
        }

        /// <summary>
        /// Round 32: no Unity logo; the platform launch screen shows the launch art (iPhone) / the app icon (Android 12+),
        /// then the game's own splash (the same art, the logo and the running hamster) takes over.
        /// </summary>
        static void ApplyLaunchScreen()
        {
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            Texture2D launch = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + "launch_bg.png");
            if (launch == null) return;
            PlayerSettings.iOS.SetiPhoneLaunchScreenType(iOSLaunchScreenType.ImageAndBackgroundRelative);
            PlayerSettings.iOS.SetLaunchScreenImage(launch, iOSLaunchScreenImageType.iPhonePortraitImage);
            PlayerSettings.iOS.SetLaunchScreenImage(launch, iOSLaunchScreenImageType.iPadImage);
        }

        static void ApplyIcons()
        {
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + "icon_app.png");
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            Texture2D fg = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + "icon_adaptive_foreground.png");
            Texture2D bg = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + "icon_adaptive_background.png");
            if (fg == null || bg == null) return;
            PlatformIcon[] adaptive = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive);
            foreach (PlatformIcon slot in adaptive) slot.SetTextures(bg, fg);
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive, adaptive);
            PlatformIcon[] round = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Round);
            foreach (PlatformIcon slot in round) slot.SetTexture(icon);
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Round, round);
            PlatformIcon[] legacy = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Legacy);
            foreach (PlatformIcon slot in legacy) slot.SetTexture(icon);
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Legacy, legacy);
        }

        /// <summary>iPhone: an Xcode project (open Builds/iOS/Unity-iPhone.xcodeproj on a Mac, pick a team, run on the phone).</summary>
        [MenuItem("Rhythm Slash Runner/Build iOS (Xcode project)")]
        public static void BuildIOS() { Build(BuildTarget.iOS, "../Builds/iOS", false); }

        [MenuItem("Rhythm Slash Runner/Build Windows")]
        public static void BuildWindows() { Build(BuildTarget.StandaloneWindows64, "../Builds/Windows/RhythmSlashRunner.exe", false); }

        /// <summary>Side-loadable test APK (debug-signed unless the keystore variables are set).</summary>
        [MenuItem("Rhythm Slash Runner/Build Android APK")]
        public static void BuildAndroid()
        {
            EditorUserBuildSettings.buildAppBundle = false;
            ApplySigning(false);
            Build(BuildTarget.Android, "../Builds/Android/RhythmSlashRunner.apk", false);
        }

        /// <summary>Google Play upload: Android App Bundle, release-signed. Refuses to run without a keystore.</summary>
        [MenuItem("Rhythm Slash Runner/Build Android AAB (store)")]
        public static void BuildAndroidAab()
        {
            if (!ApplySigning(true)) return;
            EditorUserBuildSettings.buildAppBundle = true;
            Build(BuildTarget.Android, "../Builds/Android/RhythmSlashRunner.aab", true);
            EditorUserBuildSettings.buildAppBundle = false;
        }

        static bool ApplySigning(bool required)
        {
            string path = Environment.GetEnvironmentVariable("RSR_KEYSTORE");
            bool available = !string.IsNullOrEmpty(path) && File.Exists(path);
            PlayerSettings.Android.useCustomKeystore = available;
            if (available)
            {
                PlayerSettings.Android.keystoreName = path;
                PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("RSR_KEYSTORE_PASS");
                PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("RSR_KEY_ALIAS");
                PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("RSR_KEY_PASS");
                return true;
            }
            if (required)
            {
                Debug.LogError("Store build needs an upload key: set RSR_KEYSTORE, RSR_KEYSTORE_PASS, RSR_KEY_ALIAS, RSR_KEY_PASS.");
                if (Application.isBatchMode) EditorApplication.Exit(2);
            }
            return false;
        }

        static void Build(BuildTarget target, string output, bool bumpVersion)
        {
            EnsureScene();
            ApplyPlayerSettings(bumpVersion);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = target,
                options = BuildOptions.None,
            });
            Debug.Log("Build " + target + ": " + report.summary.result + " size=" + report.summary.totalSize + " errors=" + report.summary.totalErrors + " version=" + PlayerSettings.bundleVersion + " → " + Path.GetFullPath(output));
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
