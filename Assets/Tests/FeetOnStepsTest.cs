using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// ROUND 5c (feet clipping, 2026-11-10): the user's screenshot of the
/// Round 5b photo set shows Nilesh's feet SUNK into the temple steps —
/// his shoes are not on the wood, the step surface line passes through
/// his ankles/shins. Root cause: PlayerController grounded to the
/// terrain plane (y=0) and never knew about raised walkable geometry.
/// The camera-collision test teleports him to (6.27, -14), on the
/// plinth's 4th tier (step top ~0.5 m), feet at y=0 — sunk ~0.5 m.
/// This test proves, on the real build:
///   (a) placing the player on the temple steps at several positions
///       (tiers 1-10 + the exact camera-collision spot) leaves his
///       feet ON the step surface: feet Y == GroundHeightAt (the
///       step-top Y) within 2 cm, after the grounding settles;
///   (b) the house floor (0.03 m) and flat ground (0 m) still ground
///       correctly — the fix does not break normal terrain;
///   (c) a close-up screenshot from the same angle as
///       camera-collision-stilt.png, zoomed on the feet, so a human
///       can SEE the shoes on the wood.
/// The test assembly cannot reference game classes (CS0246), so
/// VillageColliders.GroundHeightAt is called by reflection through the
/// PlayerController's assembly, the same way PlayerSpeedTest drives
/// PlayerController.
/// SECONDARY (same test run): a diagnostic that logs every low
/// renderer near (4.5, -8.5) — the two small black objects on the mud
/// near the Pujari in camera-collision-stilt.png / pujari.png — so the
/// CI log identifies what they are (shoes? prop? broken model?).
/// </summary>
public class FeetOnStepsTest
{
    private MethodInfo groundMethod;

    [UnityTest]
    public IEnumerator FeetRestOnTempleSteps()
    {
        if (!Application.isPlaying)
        {
            yield break;
        }

        yield return SceneManager.LoadSceneAsync("Main");

        yield return new WaitForSeconds(1f);
        float modelWait = 0f;
        while (GameObject.Find("ModelsLoaded") == null && modelWait < 150f)
        {
            yield return new WaitForSeconds(0.5f);
            modelWait += 0.5f;
        }
        Debug.Log("[FeetOnStepsTest] Waited " + modelWait + "s for models; loaded="
            + (GameObject.Find("ModelsLoaded") != null));
        yield return new WaitForSeconds(1.5f);

        var player = GameObject.Find("Player");
        Assert.IsNotNull(player, "Player not found in Main scene");
        var cam = Camera.main;
        Assert.IsNotNull(cam, "Main Camera not found in Main scene");
        Component pc = player.GetComponent("PlayerController") as Component;
        Assert.IsNotNull(pc, "PlayerController not found on Player");

        // VillageColliders lives in the game assembly; reach it through
        // the PlayerController's assembly.
        System.Type vcType = pc.GetType().Assembly.GetType("VillageColliders");
        Assert.IsNotNull(vcType, "VillageColliders type not found in game assembly");
        groundMethod = vcType.GetMethod("GroundHeightAt",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(groundMethod, "VillageColliders.GroundHeightAt missing");

        // ---- (c) black-object diagnostic FIRST (before any teleport
        // moves the player into the area and changes what is near it).
        LogLowRenderersNear(new Vector2(4.5f, -8.5f), 2f);

        // ---- (a) temple steps: east of the plinth centre (2,-17) at
        // several Chebyshev distances — each lands on a different tier.
        // d=4.8 tier 1, 4.4 tier 3, 4.0 tier 5, 3.6 tier 7, 3.2 tier 9.
        float[] distances = { 4.8f, 4.4f, 4.0f, 3.6f, 3.2f };
        foreach (float d in distances)
        {
            Vector3 spot = new Vector3(2f + d, 0.5f, -17f);
            yield return PlaceAndCheck(player, spot, true);
        }

        // The EXACT camera-collision spot from the user's screenshot:
        // (House_6 bounds.min.x - 1.5, 0.5, House_6 bounds.center.z).
        var house = GameObject.Find("House_6");
        Assert.IsNotNull(house, "House_6 (stilt house) not found in Main scene");
        Bounds hb = GetWorldBounds(house);
        Vector3 collisionSpot = new Vector3(hb.min.x - 1.5f, 0.5f, hb.center.z);
        yield return PlaceAndCheck(player, collisionSpot, true);

        // ---- (b) house floor + flat ground still ground correctly.
        yield return PlaceAndCheck(player, new Vector3(-9f, 0.5f, 6f), false); // House_Player floor (0.03 m)
        yield return PlaceAndCheck(player, new Vector3(0f, 0.5f, 0f), false);  // flat chowk (0 m)

        // ---- (c) close-up proof shot: the player back on the
        // camera-collision spot (tier 4), camera low and close from
        // the east — the same angle as camera-collision-stilt.png,
        // zoomed on the feet. CameraFollow is disabled while the
        // camera is placed by hand, then restored.
        player.transform.position = collisionSpot;
        yield return null;
        yield return null;
        yield return null;
        float feetY = player.transform.position.y - 0.5f;
        Component follow = cam.GetComponent("CameraFollow") as Component;
        Vector3 savedPos = cam.transform.position;
        Quaternion savedRot = cam.transform.rotation;
        bool followWasEnabled = false;
        if (follow != null)
        {
            followWasEnabled = ((Behaviour)follow).enabled;
            ((Behaviour)follow).enabled = false;
        }
        Vector3 feetPos = new Vector3(player.transform.position.x, feetY, player.transform.position.z);
        cam.transform.position = feetPos + new Vector3(2.6f, 1.1f, 0.9f);
        cam.transform.LookAt(feetPos + Vector3.up * 0.35f);
        yield return null;
        string shot = RenderScreenshot(cam, "feet-on-steps-closeup.png", 1280, 720);
        Assert.IsNotNull(shot, "Feet close-up screenshot failed to render");
        Debug.Log("[FeetOnStepsTest] Feet close-up: player at "
            + player.transform.position.ToString("F2") + ", feet Y "
            + feetY.ToString("F2") + " (step top "
            + GroundAt(player.transform.position.x, player.transform.position.z).ToString("F2") + ")");
        if (follow != null) ((Behaviour)follow).enabled = followWasEnabled;
        cam.transform.position = savedPos;
        cam.transform.rotation = savedRot;

        Debug.Log("[FeetOnStepsTest] PASS — feet rest on the step tops at every tested position");
        yield return null;
    }

    // Teleport the player to a spot, let the grounding settle (3
    // frames), then assert his feet Y equals the sampled ground height
    // (the step-top Y) within 2 cm. expectStep: the spot must be on a
    // raised surface (ground > 0.1 m), not flat terrain.
    private IEnumerator PlaceAndCheck(GameObject player, Vector3 spot, bool expectStep)
    {
        player.transform.position = spot;
        yield return null;
        yield return null;
        yield return null;
        float feetY = player.transform.position.y - 0.5f;
        float ground = GroundAt(spot.x, spot.z);
        Debug.Log("[FeetOnStepsTest] Spot " + spot.ToString("F2")
            + " -> ground " + ground.ToString("F2")
            + " m, feet Y " + feetY.ToString("F2") + " m");
        if (expectStep)
        {
            Assert.Greater(ground, 0.1f,
                "Spot " + spot.ToString("F2") + " should be on a raised step (ground "
                + ground.ToString("F2") + " m) — the walkable top is missing there");
        }
        Assert.AreEqual(ground, feetY, 0.02f,
            "Feet sunk/floating at " + spot.ToString("F2") + ": feet Y "
            + feetY.ToString("F2") + " m but step-top Y is "
            + ground.ToString("F2") + " m");
    }

    private float GroundAt(float x, float z)
    {
        return (float)groundMethod.Invoke(null, new object[] { x, z });
    }

    private static Bounds GetWorldBounds(GameObject root)
    {
        Renderer[] rr = root.GetComponentsInChildren<Renderer>();
        Assert.Greater(rr.Length, 0, root.name + " has no renderers");
        Bounds b = rr[0].bounds;
        foreach (Renderer r in rr) b.Encapsulate(r.bounds);
        return b;
    }

    // Diagnostic for the two small black objects on the mud near the
    // Pujari (visible in camera-collision-stilt.png right-centre and
    // pujari.png centre, ~0.1-0.14 m tall, ~0.03 m wide, ~10 cm apart
    // at ~(4.5, 0, -8.5 to -9.8)). Log every LOW renderer near the
    // point — name, parent, bounds — so the CI log identifies them.
    private void LogLowRenderersNear(Vector2 point, float radius)
    {
        Renderer[] all = Object.FindObjectsOfType<Renderer>();
        int count = 0;
        foreach (Renderer r in all)
        {
            if (r == null || r.gameObject == null) continue;
            Vector3 c = r.bounds.center;
            float dx = c.x - point.x;
            float dz = c.z - point.y;
            if (dx * dx + dz * dz > radius * radius) continue;
            if (r.bounds.max.y > 0.5f) continue; // low props only
            string parent = r.transform.parent != null ? r.transform.parent.name : "(root)";
            Debug.Log("[FeetOnStepsTest] NEAR-OBJECT name=" + r.gameObject.name
                + " parent=" + parent
                + " active=" + r.gameObject.activeInHierarchy
                + " centre=" + c.ToString("F2")
                + " size=" + r.bounds.size.ToString("F2"));
            count++;
        }
        Debug.Log("[FeetOnStepsTest] NEAR-OBJECT total low renderers within "
            + radius.ToString("F1") + " m of " + point.ToString("F2") + ": " + count);
    }

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
            Debug.Log("[FeetOnStepsTest] Screenshot saved: " + path);
            return path;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Screenshot render failed: " + e.Message);
            return null;
        }
    }
}
