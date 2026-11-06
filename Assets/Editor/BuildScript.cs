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
