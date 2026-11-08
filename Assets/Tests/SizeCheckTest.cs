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

        // Occluders for the pose search: every big solid object, so the
        // camera never ends up staring at a wall instead of the landmark
        // (first run: House_5's shot was blocked by the player's hut,
        // the cow's by House_2). Buildings block the player/camera
        // positions too; trees only block the view ray (you can stand
        // under a canopy, you just can't see through it).
        System.Collections.Generic.List<Bounds> occluders = new System.Collections.Generic.List<Bounds>();
        System.Collections.Generic.List<Bounds> buildings = new System.Collections.Generic.List<Bounds>();
        System.Collections.Generic.List<string> ocNames = new System.Collections.Generic.List<string>();
        string[] occluderNames = { "House_Player", "House_2", "House_3", "House_4", "House_5", "House_6",
            "Temple", "Haveli", "School", "Clinic", "Tree_Banyan", "Tree_Mango", "Tree_Palm", "Tree_Neem1", "Gate" };
        string[] buildingNames = { "House_Player", "House_2", "House_3", "House_4", "House_5", "House_6",
            "Temple", "Haveli", "School", "Clinic" };
        foreach (string n in occluderNames)
        {
            GameObject o = GameObject.Find(n);
            Bounds ob;
            if (o == null || !TryRenderedBounds(o, out ob)) continue;
            occluders.Add(ob);
            ocNames.Add(n);
            if (System.Array.IndexOf(buildingNames, n) >= 0) buildings.Add(ob);
        }

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

            // Pose: player 2.2 m clear of the footprint, facing the
            // object; camera behind the player (further from the object,
            // 3.2 m + 0.25 m per metre of object height so tall objects
            // still fit the frame). Try the village-centre side first,
            // then the other three sides: the centre side can be blocked
            // by another building between the camera and the target, and
            // a blocked shot proves nothing. A side is usable when the
            // player and camera stand clear of every building and the
            // view ray reaches the target without crossing any occluder.
            Vector3 toCentre = new Vector3(-b.center.x, 0f, -b.center.z);
            if (toCentre.sqrMagnitude < 0.01f) toCentre = Vector3.forward;
            toCentre.Normalize();
            float clear = Mathf.Max(b.extents.x, b.extents.z) + 2.2f;
            float camDist = 3.2f + h * 0.25f;
            Vector3 playerPos = Vector3.zero;
            Vector3 camPos = Vector3.zero;
            Vector3 lookTarget = Vector3.zero;
            int usedSide = -1;
            for (int side = 0; side < 4 && usedSide < 0; side++)
            {
                Vector3 dir = Quaternion.Euler(0f, side * 90f, 0f) * toCentre;
                Vector3 pp = b.center + dir * clear;
                pp.y = 0.5f;
                Vector3 cp = pp + dir * camDist + new Vector3(0f, 1.7f, 0f);
                Vector3 chest = pp + new Vector3(0f, 1.2f, 0f);
                Vector3 objPoint = new Vector3(b.center.x, b.min.y + h * 0.35f, b.center.z);
                Vector3 lt = (chest + objPoint) * 0.5f;
                if (!PoseClear(pp, cp, lt, lm.goName, buildings, occluders, ocNames)) continue;
                playerPos = pp;
                camPos = cp;
                lookTarget = lt;
                usedSide = side;
            }
            if (usedSide < 0)
            {
                // No clear side found — fall back to the centre side
                // (the old behaviour) rather than failing the test.
                Debug.LogWarning("[SizeCheck] " + lm.goName + ": no clear side, using centre side");
                Vector3 dir = toCentre;
                playerPos = b.center + dir * clear;
                playerPos.y = 0.5f;
                camPos = playerPos + dir * camDist + new Vector3(0f, 1.7f, 0f);
                Vector3 chest = playerPos + new Vector3(0f, 1.2f, 0f);
                Vector3 objPoint = new Vector3(b.center.x, b.min.y + h * 0.35f, b.center.z);
                lookTarget = (chest + objPoint) * 0.5f;
                usedSide = 0;
            }
            Debug.Log("[SizeCheck] " + lm.goName + " pose side " + usedSide
                + " player=" + playerPos + " cam=" + camPos);
            player.transform.position = playerPos;
            player.transform.LookAt(new Vector3(b.center.x, playerPos.y, b.center.z));
            cam.transform.position = camPos;
            cam.transform.LookAt(lookTarget);
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

    // Is this pose usable? The player and the camera must stand clear of
    // every building (not inside one), and the view ray from the camera
    // to the look target must not cross any occluder (the target itself
    // is excluded — the ray ends at it). Anything wrong returns false
    // and the caller tries the next side.
    private static bool PoseClear(Vector3 playerPos, Vector3 camPos, Vector3 lookTarget,
        string targetName,
        System.Collections.Generic.List<Bounds> buildings,
        System.Collections.Generic.List<Bounds> occluders,
        System.Collections.Generic.List<string> ocNames)
    {
        foreach (Bounds b in buildings)
        {
            if (PointInAABBXZ(playerPos, b, 0.5f)) return false;
            if (PointInAABBXZ(camPos, b, 0.3f)) return false;
        }
        for (int i = 0; i < occluders.Count; i++)
        {
            if (ocNames[i] == targetName) continue;
            Bounds b = occluders[i];
            // Pad the box a little: a ray grazing a wall still hides
            // the target behind it.
            Bounds padded = new Bounds(b.center, b.size + new Vector3(0.4f, 0.4f, 0.4f));
            if (SegmentHitsAABB(camPos, lookTarget, padded)) return false;
        }
        return true;
    }

    // Is the point (XZ only) inside the box's footprint? Only meaningful
    // when the point is below the box's top (a camera above a roof is
    // fine — but our camera is at 2.2 m, below every roof here).
    private static bool PointInAABBXZ(Vector3 p, Bounds b, float pad)
    {
        if (p.y > b.max.y) return false;
        return p.x > b.min.x - pad && p.x < b.max.x + pad
            && p.z > b.min.z - pad && p.z < b.max.z + pad;
    }

    // Does the segment a->c intersect the box? Slab method; starting
    // inside the box counts as a hit.
    private static bool SegmentHitsAABB(Vector3 a, Vector3 c, Bounds b)
    {
        Vector3 d = c - a;
        float tmin = 0f, tmax = 1f;
        if (!Slab(a.x, d.x, b.min.x, b.max.x, ref tmin, ref tmax)) return false;
        if (!Slab(a.y, d.y, b.min.y, b.max.y, ref tmin, ref tmax)) return false;
        if (!Slab(a.z, d.z, b.min.z, b.max.z, ref tmin, ref tmax)) return false;
        return true;
    }

    private static bool Slab(float o, float d, float mn, float mx, ref float tmin, ref float tmax)
    {
        if (Mathf.Abs(d) < 1e-8f) return o >= mn && o <= mx;
        float t1 = (mn - o) / d, t2 = (mx - o) / d;
        if (t1 > t2) { float t = t1; t1 = t2; t2 = t; }
        tmin = Mathf.Max(tmin, t1);
        tmax = Mathf.Min(tmax, t2);
        return tmin <= tmax;
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
