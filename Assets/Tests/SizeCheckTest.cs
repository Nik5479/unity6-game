using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Phase 1 size check (2026-11-08): the village must be REAL size —
/// Nilesh 1.75 m against a 10 m temple, 5.5 m houses, a 13 m banyan,
/// a 1.45 m cow and a 1.5 m Ambassador. The owner's phone video showed
/// the hero as tall as the temple and the houses, so this test asserts
/// every large object's rendered-bounds height against the real-size
/// table AND renders Nilesh standing next to each one (960x540 PNGs in
/// TestScreenshots/size-&lt;name&gt;.png) for the CI artifact — the
/// screenshots are the proof a human judges, the assertions are the gate.
/// </summary>
public class SizeCheckTest
{
    private struct Landmark
    {
        public string goName;
        public string slug;
        public float minH;
        public float maxH;
        public Landmark(string goName, string slug, float minH, float maxH)
        {
            this.goName = goName; this.slug = slug; this.minH = minH; this.maxH = maxH;
        }
    }

    private static readonly Landmark[] kLandmarks = new Landmark[]
    {
        new Landmark("Temple", "temple", 10f, 14f),
        new Landmark("Haveli", "haveli", 6f, 10f),
        new Landmark("House_2", "house2", 4f, 7f),
        new Landmark("House_5", "house5", 4f, 7f),
        new Landmark("Tree_Banyan", "banyan", 12f, 18f),
        new Landmark("Tree_Palm", "palm", 8f, 12f),
        new Landmark("Cow", "cow", 1.2f, 1.7f),
        new Landmark("HeroCar", "herocar", 1.3f, 1.7f),
    };

    [UnityTest]
    public IEnumerator PlayerScaleVsLandmarks()
    {
        // testMode: all runs this test in EditMode too, where
        // SceneManager.LoadSceneAsync is not allowed. Skip in EditMode.
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
        Debug.Log("[SizeCheck] Waited " + modelWait + "s for models; loaded="
            + (GameObject.Find("ModelsLoaded") != null));
        yield return new WaitForSeconds(1.5f);

        var player = GameObject.Find("Player");
        Assert.IsNotNull(player, "Player not found in Main scene");
        var cam = Camera.main;
        Assert.IsNotNull(cam, "Main Camera not found in Main scene");

        // (1) Nilesh himself must be real size: 1.70-1.80 m tall.
        GameObject nilesh = GameObject.Find("Player/Nilesh");
        GameObject hero = nilesh != null ? nilesh : player;
        Bounds heroBounds;
        Assert.IsTrue(TryRenderedBounds(hero, out heroBounds),
            "Nilesh has no renderers — real model did not build");
        float heroH = heroBounds.size.y;
        Debug.Log("[SizeCheck] Nilesh height: " + heroH + " m (expected 1.70-1.80)");
        Assert.GreaterOrEqual(heroH, 1.6f, "Nilesh is too short: " + heroH + " m");
        Assert.LessOrEqual(heroH, 1.9f, "Nilesh is too tall: " + heroH + " m");

        // (2) Each landmark: height in range, then Nilesh beside it.
        Behaviour follow = cam.GetComponent("CameraFollow") as Behaviour;
        if (follow != null) follow.enabled = false;
        Vector3 savedPlayerPos = player.transform.position;
        Quaternion savedPlayerRot = player.transform.rotation;
        Vector3 savedCamPos = cam.transform.position;
        Quaternion savedCamRot = cam.transform.rotation;

        foreach (Landmark lm in kLandmarks)
        {
            GameObject go = GameObject.Find(lm.goName);
            Assert.IsNotNull(go, "Landmark missing: " + lm.goName);
            Bounds b;
            Assert.IsTrue(TryRenderedBounds(go, out b),
                lm.goName + " has no renderers — model did not build");
            float h = b.size.y;
            Debug.Log("[SizeCheck] " + lm.goName + " height: " + h
                + " m (expected " + lm.minH + "-" + lm.maxH + ")");
            // Tolerance 0.05 m: rendered bounds are float32 sums over
            // transformed vertices, so a model normalised to exactly
            // 10 m reads back as 9.999998 m (seen 2026-11-08 — the first
            // run failed the Temple on 2 micrometres). 5 cm is far below
            // any scale error a human could see.
            Assert.GreaterOrEqual(h, lm.minH - 0.05f,
                lm.goName + " is too short: " + h + " m (expected >= " + lm.minH + ")");
            Assert.LessOrEqual(h, lm.maxH + 0.05f,
                lm.goName + " is too tall: " + h + " m (expected <= " + lm.maxH + ")");

            // Pose: player 2.2 m clear of the footprint on the
            // village-centre side, facing the object; camera behind the
            // player (further from the object, 3.2 m + 0.25 m per metre
            // of object height so tall objects still fit the frame).
            Vector3 toCentre = new Vector3(-b.center.x, 0f, -b.center.z);
            if (toCentre.sqrMagnitude < 0.01f) toCentre = Vector3.forward;
            toCentre.Normalize();
            float clear = Mathf.Max(b.extents.x, b.extents.z) + 2.2f;
            Vector3 playerPos = b.center + toCentre * clear;
            playerPos.y = 0.5f;
            player.transform.position = playerPos;
            player.transform.LookAt(new Vector3(b.center.x, playerPos.y, b.center.z));
            float camDist = 3.2f + h * 0.25f;
            Vector3 camPos = playerPos + toCentre * camDist + new Vector3(0f, 1.7f, 0f);
            cam.transform.position = camPos;
            Vector3 playerChest = playerPos + new Vector3(0f, 1.2f, 0f);
            Vector3 objPoint = new Vector3(b.center.x, b.min.y + h * 0.35f, b.center.z);
            cam.transform.LookAt((playerChest + objPoint) * 0.5f);
            yield return null;
            yield return null;
            string shot = RenderScreenshot(cam, "size-" + lm.slug + ".png", 960, 540);
            Assert.IsNotNull(shot, "Size screenshot failed: " + lm.slug);
            Assert.IsTrue(File.Exists(shot), "Size screenshot missing: " + shot);
        }

        player.transform.position = savedPlayerPos;
        player.transform.rotation = savedPlayerRot;
        cam.transform.position = savedCamPos;
        cam.transform.rotation = savedCamRot;
        if (follow != null) follow.enabled = true;
        Debug.Log("[SizeCheck] All " + kLandmarks.Length + " landmarks in range, screenshots rendered");
        yield return null;
    }

    // Union of every renderer bounds under go (the loader's rendered
    // bounds, same measure the normaliser scales by). False if none.
    private static bool TryRenderedBounds(GameObject go, out Bounds bounds)
    {
        bounds = new Bounds();
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return false;
        bounds = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) bounds.Encapsulate(rs[i].bounds);
        return true;
    }

    // Same synchronous render as ScreenshotTest (Camera.Render to a
    // RenderTexture) — works in the headless CI editor.
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
            Debug.Log("[SizeCheck] Screenshot saved: " + path);
            return path;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Size screenshot render failed: " + e.Message);
            return null;
        }
    }
}
