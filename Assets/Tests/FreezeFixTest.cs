using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Freeze fix (2026-11-08): the village build is ~50 sequential glTF
/// loads with no timeouts — on a 4 GB phone it never finished (5+ min
/// stuck at "Village: building... Field OK", no day-night, no movement,
/// because SetupSystems — which creates the DayNightCycle + GameManager
/// — only ran at the END of the build). The fix adds a per-load timeout
/// (30 s) and a hard build deadline (90 s) after which the game starts
/// with whatever loaded. This test proves the game starts: (1) the full
/// build completes and the game-start systems exist, and (2) when the
/// build deadline is forced short, the build is cut short cleanly and
/// the game STILL starts with a partial village.
/// NOTE: the test assembly cannot reference game classes (CS0246), so
/// everything game-side is reached by reflection, like AuditLogicTest.
/// No Camera.Render here — wide renders OOM-kill this machine; this test
/// proves start-up + movement by log/flag data only.
/// </summary>
public class FreezeFixTest
{
    private static System.Type FindType(string name)
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(name);
            if (t != null) return t;
        }
        return null;
    }

    private static Component GetComp(GameObject go, string typeName)
    {
        return go.GetComponent(typeName);
    }

    // Finds a component by type name (reflection — the test assembly
    // cannot reference game classes). Note: the VillageBuilder script
    // lives on the scene GameObject named "GameManager", NOT on an
    // object called "VillageBuilder", so GameObject.Find would fail.
    private static Component FindComponent(string typeName)
    {
        System.Type t = FindType(typeName);
        if (t == null) return null;
        Object[] found = Object.FindObjectsOfType(t);
        if (found == null || found.Length == 0) return null;
        return found[0] as Component;
    }

    // Waits for the "ModelsLoaded" signal (the build's finally block).
    private static IEnumerator WaitForModelsLoaded(float maxSeconds)
    {
        float w = 0f;
        while (GameObject.Find("ModelsLoaded") == null && w < maxSeconds)
        {
            yield return new WaitForSeconds(0.5f);
            w += 0.5f;
        }
        Debug.Log("[FreezeTest] Waited " + w + "s for models; loaded="
            + (GameObject.Find("ModelsLoaded") != null));
        yield return new WaitForSeconds(1.5f); // let SetupSystems + camera settle
    }

    // Game-start systems must exist: DayNightCycle + GameManager are
    // created by SetupSystems, which only runs when the build finishes.
    private static void AssertGameStarted()
    {
        Component vbComp = FindComponent("VillageBuilder");
        Assert.IsNotNull(vbComp, "VillageBuilder not found");
        Component cycle = GetComp(vbComp.gameObject, "DayNightCycle");
        Assert.IsNotNull(cycle, "DayNightCycle missing — SetupSystems never ran, game never started");
        Component gm = FindComponent("GameManager");
        Assert.IsNotNull(gm, "GameManager missing — SetupSystems never ran, game never started");
    }

    // The day cycle must actually advance (its private clock t grows).
    private static IEnumerator AssertDayCycleAdvances()
    {
        Component cycle = FindComponent("DayNightCycle");
        Assert.IsNotNull(cycle, "DayNightCycle missing");
        FieldInfo tField = cycle.GetType().GetField("t", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(tField, "DayNightCycle.t field not found");
        float t1 = (float)tField.GetValue(cycle);
        yield return new WaitForSeconds(2f);
        float t2 = (float)tField.GetValue(cycle);
        Debug.Log("[FreezeTest] DayNightCycle t: " + t1 + " -> " + t2);
        Assert.Greater(t2, t1, "Day-night cycle is not advancing (t stuck at " + t1 + ")");
    }

    // The player must move with joystick input (moveInput), from the
    // open chowk, driving along the camera forward for 2 s.
    private static IEnumerator AssertPlayerMoves()
    {
        GameObject player = GameObject.Find("Player");
        Assert.IsNotNull(player, "Player not found");
        Component pc = GetComp(player, "PlayerController");
        Assert.IsNotNull(pc, "PlayerController not found");
        FieldInfo moveInputField = pc.GetType().GetField("moveInput");
        Assert.IsNotNull(moveInputField, "moveInput field not found");
        Camera cam = Camera.main;
        float yaw = cam != null ? cam.transform.eulerAngles.y * Mathf.Deg2Rad : 0f;
        Vector3 fwd = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
        float h = Vector3.Dot(fwd, right);
        float v = Vector3.Dot(fwd, fwd);
        player.transform.position = new Vector3(0f, 0.5f, 0f);
        yield return null;
        Vector3 startPos = player.transform.position;
        float driveT = 0f;
        while (driveT < 2f)
        {
            moveInputField.SetValue(pc, new Vector2(h, v));
            yield return null;
            driveT += Time.deltaTime;
        }
        moveInputField.SetValue(pc, Vector2.zero);
        Vector3 endPos = player.transform.position;
        float moved = Vector3.Distance(startPos, endPos);
        Debug.Log("[FreezeTest] Player drive: start=" + startPos + " end=" + endPos + " moved=" + moved + "m");
        Assert.Greater(moved, 2f, "Player did not move with joystick input (moved " + moved + "m)");
    }

    // Phase 1 (2026-11-08): the final "N objects built" banner now
    // CLEARS from the screen 5 s after the build finishes (it is a debug
    // banner — it must not sit on the game in release). The message
    // itself is kept in VillageBuilder.LastBootStatus, so this polls up
    // to 10 s for the on-screen text to go empty, then verifies the
    // message via that field instead of the screen.
    private static System.Collections.IEnumerator AssertBootStatusCleared(bool expectTimedOut)
    {
        GameObject statusGO = GameObject.Find("BootStatus");
        Assert.IsNotNull(statusGO, "BootStatus text object not found");
        Component textComp = GetComp(statusGO, "Text");
        Assert.IsNotNull(textComp, "BootStatus Text component not found");
        PropertyInfo textProp = textComp.GetType().GetProperty("text");
        string text = "";
        float w = 0f;
        while (w < 10f)
        {
            text = (string)textProp.GetValue(textComp, null);
            if (string.IsNullOrEmpty(text)) break;
            yield return new WaitForSeconds(0.5f);
            w += 0.5f;
        }
        Debug.Log("[FreezeTest] Boot status after " + w + "s: \"" + text + "\"");
        Assert.IsTrue(string.IsNullOrEmpty(text),
            "Boot status banner did not clear after the build: \"" + text + "\"");
        // The message itself must still be recorded in LastBootStatus.
        System.Type builder = FindType("VillageBuilder");
        Assert.IsNotNull(builder, "VillageBuilder type not found");
        string last = (string)builder.GetField("LastBootStatus",
            BindingFlags.Public | BindingFlags.Static).GetValue(null);
        Debug.Log("[FreezeTest] LastBootStatus: \"" + last + "\"");
        Assert.IsTrue(last.Contains("objects built"),
            "LastBootStatus is not the final message: \"" + last + "\"");
        if (expectTimedOut)
        {
            Assert.IsTrue(last.Contains("timed out"),
                "Cut-short build did not report the timeout: \"" + last + "\"");
        }
    }

    // Logs the slowest model loads from GltfModelLoader.LoadTimings.
    private static void LogSlowestLoads(int n)
    {
        System.Type loader = FindType("GltfModelLoader");
        if (loader == null) { Debug.LogWarning("[FreezeTest] GltfModelLoader type not found"); return; }
        var timings = loader.GetField("LoadTimings", BindingFlags.Public | BindingFlags.Static)
            .GetValue(null) as List<string>;
        if (timings == null || timings.Count == 0)
        {
            Debug.Log("[FreezeTest] No load timings recorded");
            return;
        }
        var sorted = new List<string>(timings);
        sorted.Sort((a, b) => ParseSeconds(b).CompareTo(ParseSeconds(a)));
        int take = System.Math.Min(n, sorted.Count);
        Debug.Log("[FreezeTest] Slowest " + take + " of " + timings.Count + " loads: "
            + string.Join(", ", sorted.GetRange(0, take).ToArray()));
    }

    private static float ParseSeconds(string entry)
    {
        int c = entry.LastIndexOf(':');
        float v;
        if (c >= 0 && float.TryParse(entry.Substring(c + 1).Trim().TrimEnd('s'), out v)) return v;
        return 0f;
    }

    [UnityTest]
    public IEnumerator BuildCompletesAndGameStarts()
    {
        if (!Application.isPlaying) yield break;
        yield return SceneManager.LoadSceneAsync("Main");
        float w = 0f;
        while (GameObject.Find("ModelsLoaded") == null && w < 150f)
        {
            yield return new WaitForSeconds(0.5f);
            w += 0.5f;
        }
        Debug.Log("[FreezeTest] Full build waited " + w + "s; loaded="
            + (GameObject.Find("ModelsLoaded") != null));
        Assert.IsNotNull(GameObject.Find("ModelsLoaded"), "ModelsLoaded signal never appeared — build hung");
        yield return new WaitForSeconds(1.5f);
        AssertGameStarted();
        yield return AssertDayCycleAdvances();
        yield return AssertPlayerMoves();
        yield return AssertBootStatusCleared(expectTimedOut: false);
        LogSlowestLoads(5);
    }

    [UnityTest]
    public IEnumerator ShortDeadlineStillStartsGame()
    {
        if (!Application.isPlaying) yield break;
        yield return SceneManager.LoadSceneAsync("Main");
        // Force the build deadline to 2 s from now — the remaining models
        // must be skipped and the game must still start.
        System.Type loader = FindType("GltfModelLoader");
        Assert.IsNotNull(loader, "GltfModelLoader type not found");
        loader.GetField("BuildDeadline", BindingFlags.Public | BindingFlags.Static)
            .SetValue(null, Time.realtimeSinceStartup + 2f);
        float w = 0f;
        while (GameObject.Find("ModelsLoaded") == null && w < 60f)
        {
            yield return new WaitForSeconds(0.5f);
            w += 0.5f;
        }
        Debug.Log("[FreezeTest] Short-deadline build waited " + w + "s; loaded="
            + (GameObject.Find("ModelsLoaded") != null));
        Assert.IsNotNull(GameObject.Find("ModelsLoaded"), "ModelsLoaded never appeared after forced deadline");
        Assert.Less(w, 30f, "Build did not stop promptly after the deadline (waited " + w + "s)");
        yield return new WaitForSeconds(1.5f);
        AssertGameStarted();
        yield return AssertDayCycleAdvances();
        yield return AssertPlayerMoves();
        yield return AssertBootStatusCleared(expectTimedOut: true);
    }
}
