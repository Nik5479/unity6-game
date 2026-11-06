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
/// </summary>
public class ScreenshotTest
{
    [UnityTest]
    public IEnumerator MainSceneLoadsAndScreenshot()
    {
        yield return SceneManager.LoadSceneAsync("Main");
        yield return new WaitForSeconds(1f);

        var player = GameObject.Find("Player");
        Assert.IsNotNull(player, "Player not found in Main scene");
        var cam = Camera.main;
        Assert.IsNotNull(cam, "Main Camera not found in Main scene");

        try
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "TestScreenshots");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "main-scene.png");
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSeconds(0.5f);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Screenshot capture failed (non-fatal): " + e.Message);
        }
    }
}
