using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Explicit Android build used by CI (game-ci unity-builder, buildMethod).
/// Builds the Main scene directly by path, so the build does not depend on
/// EditorBuildSettings scene resolution.
/// </summary>
public static class BuildScript
{
    public static void BuildAndroid()
    {
        // Stable debug keystore (local machine only): Unity does not
        // persist keystore passwords in ProjectSettings.asset, so set the
        // keystore + passwords in-process here before every headless build.
        // Passwords are the standard Android debug defaults ("android").
        // The keystore file is deliberately NOT in the repo, so on CI
        // (GitHub runner) it does not exist: there we turn the custom
        // keystore off and let Unity sign with its auto-generated debug key.
        // (ProjectSettings has androidUseCustomKeystore: 1, which without a
        // keystore + passwords fails with "Can not sign the application".)
        const string stableKeystore = "/home/hatch/workspace/unity6-game-setup/debug.keystore";
        if (System.IO.File.Exists(stableKeystore))
        {
            PlayerSettings.Android.keystoreName = stableKeystore;
            PlayerSettings.Android.keystorePass = "android";
            PlayerSettings.Android.keyaliasName = "androiddebugkey";
            PlayerSettings.Android.keyaliasPass = "android";
            PlayerSettings.Android.useCustomKeystore = true;
            Debug.Log("[Build] Stable keystore wired in-process (alias androiddebugkey).");
        }
        else
        {
            PlayerSettings.Android.useCustomKeystore = false;
            Debug.Log("[Build] Stable keystore not found (CI) - signing with Unity's auto debug key.");
        }

        // CI passes -devBuild for a Development build (more logging in logcat).
        bool dev = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-devBuild") >= 0;

        // CI passes -addX86_64 so the APK also carries x86_64 native libs.
        // The GitHub emulator is x86_64; running the arm64-only libunity.so
        // through its ARM translator (ndk_translation) dies with SIGILL on an
        // unsupported NEON instruction (FCVTZS) a few seconds after launch.
        // Local / release builds stay arm64-only.
        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-addX86_64") >= 0)
        {
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.X86_64;
            Debug.Log("[Build] Target architectures: ARM64 + X86_64 (CI emulator).");
        }

        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = "build/Android/unity6-game.apk",
            target = BuildTarget.Android,
            options = dev ? BuildOptions.Development : BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log("Build result: " + report.summary.result + (dev ? " (development)" : ""));
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new System.Exception("Android build failed: " + report.summary.result);
        }
    }
}
