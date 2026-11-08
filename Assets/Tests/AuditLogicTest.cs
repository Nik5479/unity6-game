using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// FULL GAME AUDIT — logic checks (no camera rendering, log-only so it
/// cannot OOM). Verifies the audit's highest-risk static claims at runtime:
///  B1: can the player walk through the player-house doorway (lintel)?
///  Walk speed: measured m/s vs the 5 m/s constant (reported ~6.2 m/s).
///  Overlaps O1/O2/O8: wolf vs chicken, ring tree vs gate, shamshan cluster.
///  Moon: material shader + _Fog property (dark-disc root cause).
/// Uses reflection only (test assembly cannot reference game classes).
/// RETAINED after the audit for the re-test: the Unity license was lost
/// when the VM restarted mid-audit (2026-11-07 23:23 UTC), so the B1
/// re-test after the fix could not run. Re-run with
/// -testFilter AuditLogicTest after the license is re-activated.
/// </summary>
public class AuditLogicTest
{
    private static System.Type GetGameType(string name)
    {
        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type t = a.GetType(name);
            if (t != null) return t;
        }
        return null;
    }

    private static bool CanMoveTo(Vector3 pos, float radius)
    {
        // VillageColliders is a TOP-LEVEL static class (VillageBuilder.cs:10),
        // not nested inside VillageBuilder.
        System.Type t = GetGameType("VillageColliders");
        if (t == null) { Debug.LogWarning("[Audit] Colliders type not found"); return true; }
        MethodInfo m = t.GetMethod("CanMoveTo", BindingFlags.Public | BindingFlags.Static);
        if (m == null) { Debug.LogWarning("[Audit] CanMoveTo not found"); return true; }
        return (bool)m.Invoke(null, new object[] { pos, radius });
    }

    private static Component GetComp(GameObject go, string typeName)
    {
        return go.GetComponent(typeName);
    }

    [UnityTest]
    public IEnumerator AuditLogic()
    {
        if (!Application.isPlaying) yield break;
        yield return SceneManager.LoadSceneAsync("Main");
        yield return new WaitForSeconds(1f);
        float w = 0f;
        while (GameObject.Find("ModelsLoaded") == null && w < 150f)
        {
            yield return new WaitForSeconds(0.5f);
            w += 0.5f;
        }
        Debug.Log("[Audit] Waited " + w + "s for models; loaded=" + (GameObject.Find("ModelsLoaded") != null));
        yield return new WaitForSeconds(1.5f);

        GameObject player = GameObject.Find("Player");
        Assert.IsNotNull(player, "Player not found");
        Component pc = GetComp(player, "PlayerController");
        Assert.IsNotNull(pc, "PlayerController not found");
        FieldInfo moveInputField = pc.GetType().GetField("moveInput");
        Assert.IsNotNull(moveInputField, "moveInput field not found");

        // ---- B1: doorway probe (CanMoveTo along the door path) ----
        Transform doorFront = GameObject.Find("DoorFront") != null
            ? GameObject.Find("DoorFront").transform : null;
        Vector3 doorFrontPos = doorFront != null ? doorFront.position : new Vector3(-3.75f, 0.5f, 6f);
        Debug.Log("[Audit] DoorFront at " + doorFrontPos);
        // Points from outside the door (east) through the doorway to inside.
        Vector3[] path = {
            new Vector3(-3.75f, 0.5f, 6f),
            new Vector3(-4.5f, 0.5f, 6f),
            new Vector3(-5.0f, 0.5f, 6f),
            new Vector3(-5.5f, 0.5f, 6f),
            new Vector3(-5.75f, 0.5f, 6f),
            new Vector3(-6.5f, 0.5f, 6f),
            new Vector3(-7.5f, 0.5f, 6f),
            new Vector3(-9f, 0.5f, 6f),
        };
        foreach (Vector3 p in path)
            Debug.Log("[Audit] CanMoveTo(" + p + ", 0.45) = " + CanMoveTo(p, 0.45f));

        // ---- B1 gameplay: open the door, then drive the player through it ----
        GameObject doorObj = GameObject.Find("Door_Player");
        if (doorObj != null)
        {
            Component dc = GetComp(doorObj, "DoorController");
            if (dc != null)
            {
                MethodInfo setOpen = dc.GetType().GetMethod("SetOpen", BindingFlags.Public | BindingFlags.Instance);
                if (setOpen != null) setOpen.Invoke(dc, new object[] { true });
                Debug.Log("[Audit] Door opened via SetOpen(true)");
            }
        }
        yield return new WaitForSeconds(1.5f); // let the door swing + block unregister
        player.transform.position = new Vector3(doorFrontPos.x, 0.5f, doorFrontPos.z);
        yield return null;
        // Drive west (-x) toward/through the door: compute input from camera yaw.
        Camera cam = Camera.main;
        float yaw = cam != null ? cam.transform.eulerAngles.y * Mathf.Deg2Rad : 0f;
        Vector3 fwd = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
        Vector3 want = new Vector3(-1f, 0f, 0f);
        float h = Vector3.Dot(want, right);
        float v = Vector3.Dot(want, fwd);
        Debug.Log("[Audit] Driving west: camYaw=" + (yaw * Mathf.Rad2Deg) + " input=(" + h + "," + v + ")");
        Vector3 startPos = player.transform.position;
        float driveT = 0f;
        while (driveT < 5f)
        {
            moveInputField.SetValue(pc, new Vector2(h, v));
            yield return null;
            driveT += Time.deltaTime;
        }
        moveInputField.SetValue(pc, Vector2.zero);
        Vector3 endPos = player.transform.position;
        Debug.Log("[Audit] Door drive: start=" + startPos + " end=" + endPos
            + " dx=" + (endPos.x - startPos.x));
        // House interior is x < -5.75 (east face) and |z-6|<=3, |x+9|<=3.5.
        bool insideHouse = endPos.x < -6.0f && Mathf.Abs(endPos.z - 6f) <= 3f;
        Debug.Log("[Audit] B1 RESULT: player inside house = " + insideHouse
            + " (endPos=" + endPos + ")");
        // NOTE: no hard assert here — the audit must continue to the other
        // checks even if B1 fails; the result is recorded in the report.

        // ---- Walk speed: open ground, full forward input, 2 s ----
        player.transform.position = new Vector3(0f, 0.5f, 0f);
        yield return null;
        yaw = cam != null ? cam.transform.eulerAngles.y * Mathf.Deg2Rad : 0f;
        fwd = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        right = new Vector3(fwd.z, 0f, -fwd.x);
        want = fwd; // drive along camera forward
        h = Vector3.Dot(want, right);
        v = Vector3.Dot(want, fwd);
        Vector3 wStart = player.transform.position;
        float wt = 0f;
        while (wt < 2f)
        {
            moveInputField.SetValue(pc, new Vector2(h, v));
            yield return null;
            wt += Time.deltaTime;
        }
        moveInputField.SetValue(pc, Vector2.zero);
        Vector3 wEnd = player.transform.position;
        float dist = Vector3.Distance(new Vector3(wStart.x, 0, wStart.z), new Vector3(wEnd.x, 0, wEnd.z));
        Debug.Log("[Audit] Walk speed: " + dist + " m in " + wt + " s = " + (dist / wt) + " m/s (constant is 5 m/s)");

        // ---- Overlaps via renderer bounds ----
        CheckOverlap("Wolf", "Chicken_1", "O1 wolf/chicken");
        CheckOverlap("Gate", null, "O2 gate (bounds only)");
        LogBoundsAt("Wolf", "O1");
        LogBoundsAt("Chicken_1", "O1");
        // Ring tree at (0,0,21): find any tree-like object near it.
        GameObject[] all = GameObject.FindObjectsOfType<GameObject>();
        foreach (GameObject go in all)
        {
            if (go.transform.parent == null) continue;
            Vector3 p = go.transform.position;
            if (Mathf.Abs(p.x) < 1f && Mathf.Abs(p.z - 21f) < 1f && go.name.Contains("Tree"))
                Debug.Log("[Audit] O2 ring tree near gate: " + go.name + " at " + p);
        }
        // Shamshan cluster distances.
        LogDist("PyreWood", "GraveStones", "O8");
        LogDist("PyreWood", "ShamshanFire", "O8");
        LogDist("GraveStones", "ShamshanFire", "O8");
        LogDist("Wolf", "Chicken_1", "O1");
        LogDist("Wolf", "Matka", "O17");

        // ---- Moon diagnostics ----
        GameObject moon = GameObject.Find("Moon");
        if (moon == null)
        {
            foreach (GameObject go in all)
                if (go.name.ToLower().Contains("moon")) { moon = go; break; }
        }
        if (moon != null)
        {
            Renderer mr = moon.GetComponentInChildren<Renderer>();
            if (mr != null)
            {
                Material mm = mr.sharedMaterial;
                Debug.Log("[Audit] Moon: obj=" + moon.name + " pos=" + moon.transform.position
                    + " shader=" + (mm != null ? mm.shader.name : "NULL")
                    + " has _Fog=" + (mm != null ? mm.HasProperty("_Fog") : false)
                    + " emission=" + (mm != null && mm.HasProperty("_EmissionColor") ? mm.GetColor("_EmissionColor").ToString() : "n/a"));
            }
            else Debug.Log("[Audit] Moon object has no renderer: " + moon.name);
        }
        else Debug.Log("[Audit] Moon object NOT FOUND by name");

        // ---- Counts ----
        GameObject village = GameObject.Find("Village");
        int vc = village != null ? village.GetComponentsInChildren<Transform>(true).Length - 1 : -1;
        Debug.Log("[Audit] Village objects: " + vc);
        yield return null;
    }

    private void CheckOverlap(string aName, string bName, string tag)
    {
        GameObject a = GameObject.Find(aName);
        GameObject b = bName != null ? GameObject.Find(bName) : null;
        if (a == null) { Debug.Log("[Audit] " + tag + ": " + aName + " NOT FOUND"); return; }
        Renderer ra = a.GetComponentInChildren<Renderer>();
        if (ra == null) { Debug.Log("[Audit] " + tag + ": " + aName + " has no renderer"); return; }
        Debug.Log("[Audit] " + tag + ": " + aName + " bounds=" + ra.bounds);
        if (b != null)
        {
            Renderer rb = b.GetComponentInChildren<Renderer>();
            if (rb == null) { Debug.Log("[Audit] " + tag + ": " + bName + " has no renderer"); return; }
            Debug.Log("[Audit] " + tag + ": " + bName + " bounds=" + rb.bounds
                + " INTERSECTS=" + ra.bounds.Intersects(rb.bounds));
        }
    }

    private void LogBoundsAt(string name, string tag)
    {
        GameObject go = GameObject.Find(name);
        if (go == null) { Debug.Log("[Audit] " + tag + ": " + name + " NOT FOUND"); return; }
        Renderer r = go.GetComponentInChildren<Renderer>();
        if (r != null) Debug.Log("[Audit] " + tag + ": " + name + " center=" + r.bounds.center + " size=" + r.bounds.size);
    }

    private void LogDist(string aName, string bName, string tag)
    {
        GameObject a = GameObject.Find(aName);
        GameObject b = GameObject.Find(bName);
        if (a == null || b == null)
        {
            Debug.Log("[Audit] " + tag + ": " + aName + "/" + bName + " found=" + (a != null) + "/" + (b != null));
            return;
        }
        Debug.Log("[Audit] " + tag + ": dist(" + aName + "," + bName + ") = "
            + Vector3.Distance(a.transform.position, b.transform.position)
            + " a=" + a.transform.position + " b=" + b.transform.position);
    }
}
