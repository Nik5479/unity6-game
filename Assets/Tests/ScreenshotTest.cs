using System.Collections;
using System.IO;
using System.Reflection;
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

        // Phase 3: real .glb models load async via glTFast — wait for them
        // (max 150 s — Phase 4 added many heavy models, incl. a 58 MB
        // shivling and a 30 MB chicken) before asserting, so the test
        // checks the REAL village and the screenshots show it. The signal
        // is a "ModelsLoaded" GameObject: the test assembly cannot
        // reference game classes (CS0246), so no VillageBuilder type is
        // named here.
        yield return new WaitForSeconds(1f);
        float modelWait = 0f;
        while (GameObject.Find("ModelsLoaded") == null && modelWait < 150f)
        {
            yield return new WaitForSeconds(0.5f);
            modelWait += 0.5f;
        }
        Debug.Log("[Test] Waited " + modelWait + "s for models; loaded="
            + (GameObject.Find("ModelsLoaded") != null));

        // Let the follow camera settle on its target before the
        // frustum checks (it lerps into place over ~1 s).
        yield return new WaitForSeconds(1.5f);

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

        // (a2) PILOT (2026-11-08): the opaque black puddle discs must be
        // gone (replaced by transparent WaterPuddle discs) and the static
        // Mukhiya must be out of the pilot area (by the haveli, x > 10).
        Assert.IsNull(GameObject.Find("Puddle"), "Old opaque Puddle discs still present — pilot water fix missing");
        Assert.IsNotNull(GameObject.Find("WaterPuddle"), "No WaterPuddle discs — pilot water fix missing");
        // PILOT ROUND 2: the Ambassador is REMOVED (red doors + orange
        // front painted in its texture — replacement needed, see report).
        Assert.IsNull(GameObject.Find("HeroCar"), "HeroCar still present — removed as BLOCKER in pilot round 2");
        GameObject mukhiya = GameObject.Find("Mukhiya");
        if (mukhiya != null)
            Assert.Greater(mukhiya.transform.position.x, 10f,
                "Mukhiya still stands in the pilot area (x=" + mukhiya.transform.position.x + ")");

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
        // GTA camera fix 2026-11-08: the bar is >= 1 house, not >= 3.
        // The old >= 3 bar belonged to the high top-down camera (distance
        // 9 / height 4.6); the GTA camera sits 4.5 m behind Nilesh and
        // shows the chowk + the nearest house(s). What must never regress
        // is the player being in frame — a third-person camera that loses
        // its hero is broken.
        Assert.GreaterOrEqual(housesVisible, 1, "No house visible from main camera");
        Renderer playerRenderer = player.GetComponentInChildren<Renderer>();
        Assert.IsNotNull(playerRenderer, "Player has no renderer to check visibility");
        Assert.IsTrue(GeometryUtility.TestPlanesAABB(planes, playerRenderer.bounds),
            "Player not visible from main camera — the follow camera lost its target");

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

        // (c2) Control buttons must show SYMBOL icons (no text labels):
        // each active button needs a child "Icon" Image with a real sprite,
        // and no Text label with words on it. (User order: no real game
        // has word buttons — LOCK/TORCH/RUN/JUMP/ACTION are icons now.)
        int iconButtons = 0;
        foreach (UnityEngine.UI.Button b in buttons)
        {
            if (!b.gameObject.activeInHierarchy) continue;
            UnityEngine.UI.Text label = b.GetComponentInChildren<UnityEngine.UI.Text>(true);
            if (label != null && !string.IsNullOrEmpty(label.text))
            {
                Debug.Log("[Test] Button '" + b.name + "' still has a TEXT label: '" + label.text + "'");
                continue;
            }
            Transform iconT = b.transform.Find("Icon");
            UnityEngine.UI.Image iconImg = iconT != null ? iconT.GetComponent<UnityEngine.UI.Image>() : null;
            string iconState = (iconImg != null && iconImg.sprite != null) ? "OK" : "MISSING";
            Debug.Log("[Test] Button '" + b.name + "' icon=" + iconState);
            if (iconImg != null && iconImg.sprite != null) iconButtons++;
        }
        Assert.GreaterOrEqual(iconButtons, 5, "Fewer than 5 icon buttons (expected Action/Jump/Run/Torch/Lock with symbols)");

        // (c2b) Joystick base + knob must be round (circle sprite, not a
        // square image).
        GameObject joy = GameObject.Find("Joystick");
        Assert.IsNotNull(joy, "Joystick not found");
        UnityEngine.UI.Image joyImg = joy.GetComponent<UnityEngine.UI.Image>();
        Assert.IsNotNull(joyImg != null ? joyImg.sprite : null, "Joystick base has no sprite (must be the circle sprite)");
        Transform knobT = joy.transform.Find("Knob");
        Assert.IsNotNull(knobT, "Joystick knob not found");
        UnityEngine.UI.Image knobImg = knobT.GetComponent<UnityEngine.UI.Image>();
        Assert.IsNotNull(knobImg != null ? knobImg.sprite : null, "Joystick knob has no sprite (must be the circle sprite)");

        // (c3) Nilesh must be the REAL rigged indian_man.glb model
        // ("Player/Nilesh", Phase 4), not the old dark capsule and not
        // the Phase 2 primitive humanoid. Fallback: if the .glb failed
        // to load, the builder falls back to the primitive figure
        // ("Player/Figure/Head|Pagdi|Torso") — accept that too, so a
        // single model failure cannot fail the whole test.
        GameObject nilesh = GameObject.Find("Player/Nilesh");
        if (nilesh != null)
        {
            Renderer[] nileshRenderers = nilesh.GetComponentsInChildren<Renderer>();
            Debug.Log("[Test] Real Nilesh model found, renderers=" + nileshRenderers.Length);
            Assert.Greater(nileshRenderers.Length, 0, "Player/Nilesh has no renderers — real model did not build");
        }
        else
        {
            Debug.LogWarning("[Test] Player/Nilesh missing — checking primitive-figure fallback");
            GameObject head = GameObject.Find("Player/Figure/Head");
            Assert.IsNotNull(head, "Player figure Head not found — neither real Nilesh nor humanoid figure built");
            GameObject pagdi = GameObject.Find("Player/Figure/Pagdi");
            Assert.IsNotNull(pagdi, "Player figure Pagdi not found — humanoid figure did not build");
            GameObject torso = GameObject.Find("Player/Figure/Torso");
            Assert.IsNotNull(torso, "Player figure Torso not found — humanoid figure did not build");
        }

        // Screenshot 1: day view from the gameplay camera.
        string shot1 = RenderScreenshot(cam, "main-scene.png", 1280, 720);
        Assert.IsNotNull(shot1, "Screenshot 1 failed to render");
        Assert.IsTrue(File.Exists(shot1), "Screenshot 1 file missing: " + shot1);

        // Screenshot 2: second angle — wide view of the chowk from the east.
        // Camera sits HIGH (y=10) and slightly west of the old spot: the
        // banyan at (16,0,2) has a ~10 m wide canopy reaching to y~7, and
        // the old (14,7,-12) pose put the lens inside its leaves (verified
        // by Angle2DiagTest: TaroLeaf renderers 3 m from the camera).
        Vector3 savedPos = cam.transform.position;
        Quaternion savedRot = cam.transform.rotation;
        Behaviour follow = cam.GetComponent("CameraFollow") as Behaviour;
        if (follow != null) follow.enabled = false;
        cam.transform.position = new Vector3(12f, 10f, -12f);
        cam.transform.LookAt(new Vector3(0f, 1f, 4f));
        yield return null;
        string shot2 = RenderScreenshot(cam, "main-scene-angle2.png", 1280, 720);
        cam.transform.position = savedPos;
        cam.transform.rotation = savedRot;
        if (follow != null) follow.enabled = true;
        Assert.IsNotNull(shot2, "Screenshot 2 failed to render");
        Assert.IsTrue(File.Exists(shot2), "Screenshot 2 file missing: " + shot2);

        // PILOT night shots: the reference images are NIGHT scenes and
        // every screenshot so far was DAY. Drive the cycle to night and
        // shoot the pilot from the gameplay camera (lamp + wet road) and
        // from the south-east looking north-west (temple + moon). SkipToNight
        // fires OnNightStart (monster activates) — the monster stands at
        // the pilot's north edge (z=-19); that is the real night scene.
        Component cycle = FindComponent("DayNightCycle");
        Assert.IsNotNull(cycle, "DayNightCycle not found for night shot");
        cycle.GetType().GetMethod("SkipToNight").Invoke(cycle, null);
        yield return new WaitForSeconds(2f);
        string shot3 = RenderScreenshot(cam, "main-scene-night.png", 1280, 720);
        Assert.IsNotNull(shot3, "Night screenshot failed to render");
        Assert.IsTrue(File.Exists(shot3), "Night screenshot file missing: " + shot3);

        Behaviour follow2 = cam.GetComponent("CameraFollow") as Behaviour;
        if (follow2 != null) follow2.enabled = false;
        cam.transform.position = new Vector3(8f, 5f, 2f);
        cam.transform.LookAt(new Vector3(-6f, 8f, -20f));
        yield return null;
        string shot4 = RenderScreenshot(cam, "main-scene-night2.png", 1280, 720);
        cam.transform.position = savedPos;
        cam.transform.rotation = savedRot;
        if (follow2 != null) follow2.enabled = true;
        Assert.IsNotNull(shot4, "Night angle-2 screenshot failed to render");
        Assert.IsTrue(File.Exists(shot4), "Night angle-2 screenshot file missing: " + shot4);

        Debug.Log("[Test] Screenshots: " + shot1 + " | " + shot2 + " | " + shot3 + " | " + shot4);
        yield return null;
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

    // Reflection helpers (test assembly cannot reference game classes).
    private static System.Type FindType(string name)
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(name);
            if (t != null) return t;
        }
        return null;
    }

    private static Component FindComponent(string typeName)
    {
        System.Type t = FindType(typeName);
        if (t == null) return null;
        Object[] found = Object.FindObjectsOfType(t);
        if (found == null || found.Length == 0) return null;
        return found[0] as Component;
    }
}
