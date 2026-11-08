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
        // Stable debug keystore (project-root debug.keystore): Unity does not
        // persist keystore passwords in ProjectSettings.asset, so set the
        // keystore + passwords in-process here before every headless build.
        // Passwords are the standard Android debug defaults ("android").
        PlayerSettings.Android.keystoreName = "/home/hatch/workspace/unity6-game-setup/debug.keystore";
        PlayerSettings.Android.keystorePass = "android";
        PlayerSettings.Android.keyaliasName = "androiddebugkey";
        PlayerSettings.Android.keyaliasPass = "android";
        PlayerSettings.Android.useCustomKeystore = true;
        Debug.Log("[Build] Stable keystore wired in-process (alias androiddebugkey).");

        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = "build/Android/unity6-game.apk",
            target = BuildTarget.Android,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log("Build result: " + report.summary.result);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new System.Exception("Android build failed: " + report.summary.result);
        }
    }
}
