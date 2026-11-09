using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// ROUND 5 (final chance, 2026-11-09): the user's v1.0.6 phone videos
/// prove the camera is broken on the real device — a small downward drag
/// flips the camera fully down, and the camera passes THROUGH walls and
/// objects (the stilt house goes black-white sketch when the camera clips
/// inside it). The emulator never caught it: the old occluder snapshot
/// was collected once, before House_6 finished loading, so the stilt
/// house was never in the collision test at all.
/// This test proves, on the real build:
///   (a) with the player standing west of the stilt house (House_6) and
///       the camera dragged around to face it (wanted position INSIDE
///       the house), the live camera position NEVER ends up inside the
///       house's rendered bounds — the collision pull keeps it out;
///   (b) a full-screen downward drag cannot flip the camera down: the
///       target pitch clamps to minPitch and the live pitch stays inside
///       [minPitch, maxPitch] over 60 frames, same for a full upward drag.
/// The test assembly cannot reference game classes (CS0246), so the
/// CameraFollow component is driven by name + reflection, the same way
/// PlayerSpeedTest drives PlayerController.
/// </summary>
public class CameraCollisionTest
{
    [UnityTest]
    public IEnumerator CameraNeverEntersStiltHouseAndPitchStaysClamped()
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
        Debug.Log("[CameraCollisionTest] Waited " + modelWait + "s for models; loaded="
            + (GameObject.Find("ModelsLoaded") != null));
        yield return new WaitForSeconds(1.5f);

        var player = GameObject.Find("Player");
        Assert.IsNotNull(player, "Player not found in Main scene");
        var cam = Camera.main;
        Assert.IsNotNull(cam, "Main Camera not found in Main scene");
        var house = GameObject.Find("House_6");
        Assert.IsNotNull(house, "House_6 (stilt house) not found in Main scene");
        Component follow = cam.GetComponent("CameraFollow") as Component;
        Assert.IsNotNull(follow, "CameraFollow not found on Main Camera");

        System.Type followType = follow.GetType();
        MethodInfo dragMethod = followType.GetMethod("ApplyDragDelta");
        Assert.IsNotNull(dragMethod, "CameraFollow.ApplyDragDelta missing — the drag path cannot be driven");
        FieldInfo pitchField = followType.GetField("pitch");
        Assert.IsNotNull(pitchField, "CameraFollow.pitch field missing");
        FieldInfo targetPitchField = followType.GetField("targetPitch",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(targetPitchField, "CameraFollow.targetPitch field missing");
        FieldInfo minPitchField = followType.GetField("minPitch");
        FieldInfo maxPitchField = followType.GetField("maxPitch");
        Assert.IsNotNull(minPitchField, "CameraFollow.minPitch field missing");
        Assert.IsNotNull(maxPitchField, "CameraFollow.maxPitch field missing");
        float minPitch = (float)minPitchField.GetValue(follow);
        float maxPitch = (float)maxPitchField.GetValue(follow);

        // House_6 rendered bounds (world space). The stilt house is
        // 9 m wide, 5.5 m tall at (13, -14); the camera must never be
        // inside this volume.
        Bounds houseBounds = GetWorldBounds(house);
        Debug.Log("[CameraCollisionTest] House_6 bounds: " + houseBounds);

        // Stand the player just west of the house, facing it. The wanted
        // camera position for yaw -90 sits ~3.4 m EAST of the player —
        // inside the house — so the collision pull must engage.
        Vector3 spawnY = player.transform.position;
        player.transform.position = new Vector3(
            houseBounds.min.x - 1.5f, spawnY.y, houseBounds.center.z);
        yield return null;
        yield return null;

        // Drag the camera around: +750 px at 0.12 deg/px = -90 deg yaw.
        dragMethod.Invoke(follow, new object[] { new Vector2(750f, 0f) });

        Vector3 headPos = player.transform.position + Vector3.up * 1.4f;
        float minCamDist = float.MaxValue;
        int framesInside = 0;
        for (int i = 0; i < 90; i++)
        {
            yield return null;
            Vector3 camPos = cam.transform.position;
            float d = Vector3.Distance(headPos, camPos);
            if (d < minCamDist) minCamDist = d;
            if (i >= 5 && houseBounds.Contains(camPos))
            {
                framesInside++;
                Debug.LogWarning("[CameraCollisionTest] Frame " + i
                    + ": camera INSIDE House_6 bounds at " + camPos);
            }
        }
        Debug.Log("[CameraCollisionTest] Camera min distance from head: "
            + minCamDist.ToString("F2") + " m (full distance 3.4 m; a pull must bring it well under 3.4)");
        Assert.AreEqual(0, framesInside,
            "Camera spent " + framesInside + " frames INSIDE the stilt house bounds — collision did not keep it out");
        Assert.Less(minCamDist, 2.0f,
            "Camera never pulled in (min distance " + minCamDist.ToString("F2")
            + " m) — the collision test did not engage for House_6");

        // Proof shot: the pulled-in camera at the stilt house wall.
        string shot = RenderScreenshot(cam, "camera-collision-stilt.png", 1280, 720);
        Assert.IsNotNull(shot, "Collision proof screenshot failed to render");

        // Pitch clamp, downward: a full-screen downward drag (5000 px)
        // must clamp the TARGET pitch to minPitch, and the live pitch
        // must stay inside [minPitch, maxPitch] for 60 frames.
        dragMethod.Invoke(follow, new object[] { new Vector2(0f, -5000f) });
        float targetPitch = (float)targetPitchField.GetValue(follow);
        Debug.Log("[CameraCollisionTest] Target pitch after full downward drag: "
            + targetPitch.ToString("F2") + " (min " + minPitch.ToString("F2") + ")");
        Assert.AreEqual(minPitch, targetPitch, 0.01f,
            "Full downward drag left targetPitch at " + targetPitch.ToString("F2")
            + " — the clamp must hold it at minPitch " + minPitch.ToString("F2"));
        for (int i = 0; i < 60; i++)
        {
            yield return null;
            float p = (float)pitchField.GetValue(follow);
            Assert.GreaterOrEqual(p, minPitch - 0.01f,
                "Live pitch dipped to " + p.ToString("F2") + " (below minPitch " + minPitch.ToString("F2") + ")");
            Assert.LessOrEqual(p, maxPitch + 0.01f,
                "Live pitch rose to " + p.ToString("F2") + " (above maxPitch " + maxPitch.ToString("F2") + ")");
        }

        // Pitch clamp, upward: same for a full-screen upward drag.
        dragMethod.Invoke(follow, new object[] { new Vector2(0f, 5000f) });
        targetPitch = (float)targetPitchField.GetValue(follow);
        Debug.Log("[CameraCollisionTest] Target pitch after full upward drag: "
            + targetPitch.ToString("F2") + " (max " + maxPitch.ToString("F2") + ")");
        Assert.AreEqual(maxPitch, targetPitch, 0.01f,
            "Full upward drag left targetPitch at " + targetPitch.ToString("F2")
            + " — the clamp must hold it at maxPitch " + maxPitch.ToString("F2"));
        for (int i = 0; i < 60; i++)
        {
            yield return null;
            float p = (float)pitchField.GetValue(follow);
            Assert.GreaterOrEqual(p, minPitch - 0.01f,
                "Live pitch dipped to " + p.ToString("F2") + " (below minPitch " + minPitch.ToString("F2") + ")");
            Assert.LessOrEqual(p, maxPitch + 0.01f,
                "Live pitch rose to " + p.ToString("F2") + " (above maxPitch " + maxPitch.ToString("F2") + ")");
        }
        Debug.Log("[CameraCollisionTest] PASS — camera stayed out of House_6 and pitch stayed in ["
            + minPitch.ToString("F2") + ", " + maxPitch.ToString("F2") + "]");
        yield return null;
    }

    private static Bounds GetWorldBounds(GameObject root)
    {
        Renderer[] rr = root.GetComponentsInChildren<Renderer>();
        Assert.Greater(rr.Length, 0, "House_6 has no renderers — nothing to collide with");
        Bounds b = rr[0].bounds;
        foreach (Renderer r in rr) b.Encapsulate(r.bounds);
        return b;
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
            Debug.Log("[CameraCollisionTest] Screenshot saved: " + path);
            return path;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Screenshot render failed: " + e.Message);
            return null;
        }
    }
}
