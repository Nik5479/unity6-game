using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// PlayMode smoke test: Main scene loads, Player exists, screenshot is captured.
/// Screenshot is written to TestScreenshots/main-scene.png (project root) so
/// the CI workflow can upload it as an artifact. Best-effort: if capture
/// fails on a headless runner the scene-load assertions still decide pass/fail.
/// NOTE: yield statements must NOT sit inside a try block with a catch clause
/// (C# error CS1626), so the capture is wrapped in try/catch and the yields
/// stay outside it.
/// </summary>
public class ScreenshotTest
{
    [UnityTest]
    public IEnumerator MainSceneLoadsAndScreenshot()
    {
        // testMode: all runs this test in EditMode too, where
        // SceneManager.LoadSceneAsync is not allowed (InvalidOperationException).
        // Skip the body in EditMode; run it fully in PlayMode.
        if (!Application.isPlaying)
        {
            yield break;
        }

        yield return SceneManager.LoadSceneAsync("Main");
        yield return new WaitForSeconds(1f);

        var player = GameObject.Find("Player");
        Assert.IsNotNull(player, "Player not found in Main scene");
        var cam = Camera.main;
        Assert.IsNotNull(cam, "Main Camera not found in Main scene");

        // Phase 1 village: GameManager builds the village at runtime.
        var gm = GameObject.Find("GameManager");
        Assert.IsNotNull(gm, "GameManager not found in Main scene");
        var door = GameObject.Find("Door_Player");
        Assert.IsNotNull(door, "Player house door not found in Main scene");
        var village = GameObject.Find("Village");
        Assert.IsNotNull(village, "Village root not found in Main scene");

        try
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "TestScreenshots");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "main-scene.png");
            ScreenCapture.CaptureScreenshot(path);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Screenshot capture failed (non-fatal): " + e.Message);
        }
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.5f);
    }
}
