using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// PlayMode smoke test: Main scene loads, the Phase 1 village is actually
/// built AND visible from the gameplay camera, and the mobile UI exists.
/// Screenshots are rendered synchronously (Camera.Render to a
/// RenderTexture) and written to TestScreenshots/ so the CI workflow can
/// upload them as an artifact. This test exists because run #14 shipped a
/// blank frame on the device while the editor test passed: the village
/// must be proven visible from the camera, not just present in the scene.
/// NOTE: yield statements must NOT sit inside a try block with a catch
/// clause (C# error CS1626), so captures are wrapped in try/catch and the
/// yields stay outside it.
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
        yield return new WaitForSeconds(2f);

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

        // (a) The village must actually be built (not an empty root).
        int childCount = village.GetComponentsInChildren<Transform>(true).Length - 1;
        Debug.Log("[Test] Village objects: " + childCount);
        Assert.Greater(childCount, 50, "Village has too few objects (" + childCount + ") — builder did not run");

        // (b) Houses must be visible from the gameplay camera.
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
        string[] houseNames = { "House_Player", "House_2", "House_3", "House_4", "House_5", "House_6" };
        int housesVisible = 0;
        foreach (string hn in houseNames)
        {
            GameObject h = GameObject.Find(hn);
            if (h == null) continue;
            Renderer[] rs = h.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in rs)
            {
                if (GeometryUtility.TestPlanesAABB(planes, r.bounds)) { housesVisible++; break; }
            }
        }
        Debug.Log("[Test] Houses visible from camera: " + housesVisible);
        Assert.GreaterOrEqual(housesVisible, 3, "Fewer than 3 houses visible from main camera");

        // (c) The mobile UI must exist with visible buttons.
        GameObject canvas = GameObject.Find("MobileCanvas");
        Assert.IsNotNull(canvas, "MobileCanvas not found — MobileControls did not build UI");
        Assert.IsTrue(canvas.activeInHierarchy, "MobileCanvas is not active");
        UnityEngine.UI.Button[] buttons = canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true);
        int visibleButtons = 0;
        foreach (UnityEngine.UI.Button b in buttons)
        {
            if (b.gameObject.activeInHierarchy) visibleButtons++;
        }
        Debug.Log("[Test] Visible UI buttons: " + visibleButtons);
        Assert.GreaterOrEqual(visibleButtons, 3, "Fewer than 3 visible UI buttons");

        // Screenshot 1: day view from the gameplay camera.
        string shot1 = RenderScreenshot(cam, "main-scene.png", 1280, 720);
        Assert.IsNotNull(shot1, "Screenshot 1 failed to render");
        Assert.IsTrue(File.Exists(shot1), "Screenshot 1 file missing: " + shot1);

        // Screenshot 2: second angle — wide view of the chowk from the east.
        Vector3 savedPos = cam.transform.position;
        Quaternion savedRot = cam.transform.rotation;
        CameraFollow follow = cam.GetComponent<CameraFollow>();
        if (follow != null) follow.enabled = false;
        cam.transform.position = new Vector3(14f, 7f, -12f);
        cam.transform.LookAt(new Vector3(0f, 1f, 4f));
        yield return new WaitForEndOfFrame();
        string shot2 = RenderScreenshot(cam, "main-scene-angle2.png", 1280, 720);
        cam.transform.position = savedPos;
        cam.transform.rotation = savedRot;
        if (follow != null) follow.enabled = true;
        Assert.IsNotNull(shot2, "Screenshot 2 failed to render");
        Assert.IsTrue(File.Exists(shot2), "Screenshot 2 file missing: " + shot2);

        Debug.Log("[Test] Screenshots: " + shot1 + " | " + shot2);
        yield return new WaitForEndOfFrame();
    }

    // Synchronous screenshot: renders the camera into a RenderTexture and
    // writes a PNG. Unlike ScreenCapture.CaptureScreenshot this does not
    // depend on the Game View or end-of-frame timing, so it works in the
    // headless CI editor too.
    private string RenderScreenshot(Camera cam, string fileName, int width, int height)
    {
        try
        {
            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            rt.Release();
            Object.Destroy(rt);
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "TestScreenshots");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, fileName);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            Debug.Log("[Test] Screenshot saved: " + path);
            return path;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Screenshot render failed: " + e.Message);
            return null;
        }
    }
}
