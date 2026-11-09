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
///  Walk speed: measured m/s vs the 2.5 m/s jog constant (full joystick
///  deflection = jog; Phase 1, 2026-11-08 — was 5 m/s).
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

        // ---- B1 (ROUND 4 DOOR rewrite): the PLAYER flow, not the
        // mechanism. The old B1 called SetOpen(true) directly (bypassing
        // TryInteract, the ACTION button, the range + lock checks and the
        // input gate), teleported, and never asserted anything — it passed
        // while the phone flow failed. Now: the door must BLOCK while
        // closed, the panel must be true size, the prompt must fire in
        // range, TryInteract must open it, the gap must become walkable,
        // and driving through must end INSIDE the house (hard assert).
        // All points derive from the real door centre / DoorFront.
        Transform doorFront = GameObject.Find("DoorFront") != null
            ? GameObject.Find("DoorFront").transform : null;
        Assert.IsNotNull(doorFront, "DoorFront missing — B1 cannot run");
        Vector3 doorFrontPos = doorFront.position;
        Debug.Log("[Audit] DoorFront at " + doorFrontPos);
        GameObject doorObj = GameObject.Find("Door_Player");
        Assert.IsNotNull(doorObj, "Door_Player missing — B1 cannot run");
        Component dc = GetComp(doorObj, "DoorController");
        Assert.IsNotNull(dc, "DoorController missing — B1 cannot run");
        PropertyInfo doorCenterProp = dc.GetType().GetProperty("DoorCenter");
        Assert.IsNotNull(doorCenterProp, "DoorController.DoorCenter missing");
        Vector3 doorCenter = (Vector3)doorCenterProp.GetValue(dc, null);
        doorCenter.y = 0.5f;
        Debug.Log("[Audit] B1 door centre: " + doorCenter);
        // (a) Panel must be TRUE size (1.5 x 2.1 m real path, 1.2 m
        // primitive fallback) — the old bug was a 1.46x slab (~2.2 wide,
        // ~3.1 tall). The panel is hinge-local (doorW, doorH, 0.09) at
        // closed yaw 90 deg, so its world bounds read ~(0.09, 2.1, 1.5).
        GameObject panel = GameObject.Find("DoorPanel");
        Assert.IsNotNull(panel, "DoorPanel missing — B1 cannot run");
        Renderer panelR = panel.GetComponent<Renderer>();
        Assert.IsNotNull(panelR, "DoorPanel has no Renderer");
        Vector3 panelSize = panelR.bounds.size;
        Debug.Log("[Audit] B1 panel world size: " + panelSize);
        Assert.LessOrEqual(panelSize.y, 2.3f, "Door panel too TALL (" + panelSize.y + " m) — hut-scale slab bug is back");
        Assert.GreaterOrEqual(Mathf.Max(panelSize.x, panelSize.z), 1.0f, "Door panel too NARROW (" + panelSize + ") — not a real door");
        Assert.LessOrEqual(Mathf.Max(panelSize.x, panelSize.z), 1.7f, "Door panel too WIDE (" + panelSize + ") — hut-scale slab bug is back");
        // (b) Closed door must BLOCK the doorway.
        Assert.IsFalse(CanMoveTo(doorCenter, 0.45f), "Doorway walkable while the door is CLOSED — closed block missing");
        // (c) Teleport to 1.0 m east of the door centre (inside the 1.5 m
        // door-priority radius, so no NPC can steal the press) — the
        // prompt must be active there.
        player.transform.position = new Vector3(doorCenter.x + 1.0f, 0.5f, doorCenter.z);
        yield return null;
        yield return null; // let MobileControls.Update run the prompt check
        GameObject gmGO = GameObject.Find("GameManager");
        Assert.IsNotNull(gmGO, "GameManager object missing for door prompt check");
        Component uiComp = GetComp(gmGO, "MobileControls");
        Assert.IsNotNull(uiComp, "MobileControls missing for door prompt check");
        PropertyInfo promptProp = uiComp.GetType().GetProperty("DoorPromptActive");
        Assert.IsNotNull(promptProp, "MobileControls.DoorPromptActive missing");
        Assert.IsTrue((bool)promptProp.GetValue(uiComp, null),
            "Door prompt NOT active 1 m from the closed door — the player gets no hint");
        // (d) Open via the REAL path — GameManager.TryInteract, the same
        // call the ACTION button makes. Not SetOpen.
        Component gmComp = GetComp(gmGO, "GameManager");
        Assert.IsNotNull(gmComp, "GameManager missing for TryInteract");
        MethodInfo tryInteract = gmComp.GetType().GetMethod("TryInteract", BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(tryInteract, "GameManager.TryInteract missing");
        tryInteract.Invoke(gmComp, null);
        yield return new WaitForSeconds(1.5f); // let the door swing + block unregister
        PropertyInfo isOpenProp = dc.GetType().GetProperty("IsOpen");
        Assert.IsTrue((bool)isOpenProp.GetValue(dc, null),
            "Door did NOT open via TryInteract — the phone flow is broken");
        // (e) The gap must now be walkable.
        Assert.IsTrue(CanMoveTo(doorCenter, 0.45f), "Doorway still BLOCKED after opening — block never unregistered");
        // (f) Drive through the open doorway (direction derived from the
        // door centre, not hardcoded) and HARD-assert the player ends up
        // INSIDE the house.
        yield return null;
        Camera cam = Camera.main;
        float yaw = cam != null ? cam.transform.eulerAngles.y * Mathf.Deg2Rad : 0f;
        Vector3 fwd = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
        Vector3 want = new Vector3(doorCenter.x - player.transform.position.x, 0f, doorCenter.z - player.transform.position.z);
        want.Normalize();
        float h = Vector3.Dot(want, right);
        float v = Vector3.Dot(want, fwd);
        Debug.Log("[Audit] Driving through door: input=(" + h + "," + v + ")");
        Vector3 startPos = player.transform.position;
        float driveT = 0f;
        while (driveT < 3f)
        {
            moveInputField.SetValue(pc, new Vector2(h, v));
            yield return null;
            driveT += Time.deltaTime;
        }
        moveInputField.SetValue(pc, Vector2.zero);
        Vector3 endPos = player.transform.position;
        Debug.Log("[Audit] Door drive: start=" + startPos + " end=" + endPos
            + " dx=" + (endPos.x - startPos.x));
        System.Type gmType = GetGameType("GameManager");
        Assert.IsNotNull(gmType, "GameManager type not found for inside check");
        MethodInfo insideM = gmType.GetMethod("IsInsidePlayerHouse", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(insideM, "GameManager.IsInsidePlayerHouse not found");
        bool insideHouse = (bool)insideM.Invoke(null, new object[] { endPos });
        Debug.Log("[Audit] B1 RESULT: player inside house = " + insideHouse
            + " (endPos=" + endPos + ")");
        Assert.IsTrue(insideHouse,
            "B1 FAILED: player did NOT get inside the house after driving through the open door (endPos=" + endPos + ")");

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
        Debug.Log("[Audit] Walk speed: " + dist + " m in " + wt + " s = " + (dist / wt) + " m/s (jog constant is 2.5 m/s)");

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

        // ---- OVERNIGHT R2: monster pathfinding (audit 4.6b / 6.5) ----
        // A path from the monster's lair to the player house door front
        // must exist AND every waypoint must be walkable (CanMoveTo, same
        // probe height the pathfinder uses) — the old straight-line walk
        // cut straight through walls and houses.
        // OVERNIGHT R3c: the lair moved from (-2,-19) to (-3.5,-10.5)
        // (MonsterKnock.kLair) — (-2,-19) is inside the Temple's
        // FullBounds collider and the south is sealed by overlapping
        // building footprints, so no path existed from there even on a
        // 0.5 m grid (R2+R3b CI proved it). The new lair is the open
        // temple approach north of the seal.
        System.Type pfType = GetGameType("VillagePathfinder");
        if (pfType == null)
        {
            Debug.LogWarning("[Audit] R2: VillagePathfinder type not found");
        }
        else
        {
            MethodInfo findPath = pfType.GetMethod("FindPath", BindingFlags.Public | BindingFlags.Static);
            var outPath = new System.Collections.Generic.List<Vector3>();
            bool ok = findPath != null && (bool)findPath.Invoke(null,
                new object[] { new Vector3(-3.5f, 0f, -10.5f), doorFrontPos, 0.45f, outPath });
            Debug.Log("[Audit] R2 pathfinder: found=" + ok + " waypoints=" + outPath.Count);
            int bad = 0;
            foreach (Vector3 wp in outPath)
            {
                if (!CanMoveTo(new Vector3(wp.x, 1.0f, wp.z), 0.45f)) bad++;
            }
            Debug.Log("[Audit] R2 pathfinder: unwalkable waypoints=" + bad);
            if (!ok)
            {
                // Name the seal: print the blocked-cell map so the next
                // fix knows exactly which footprints close the route.
                MethodInfo logMap = pfType.GetMethod("LogMap", BindingFlags.Public | BindingFlags.Static);
                if (logMap != null)
                    logMap.Invoke(null, new object[] { new Vector3(-3.5f, 0f, -10.5f), doorFrontPos, 0.45f });
            }
            Assert.IsTrue(ok, "No path from monster lair to door front — monster cannot reach the door without clipping");
            Assert.AreEqual(0, bad, "Pathfinder returned unwalkable waypoints (would clip walls)");
        }

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
