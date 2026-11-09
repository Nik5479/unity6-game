using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// ROUND 5 (final chance, 2026-11-09): the user's v1.0.6 phone video
/// proves the Round 4 loading gate does not hold on the real device —
/// after GAME OVER -> DOBARA KHELO the scene reloads and he watches the
/// village assemble in front of him: "Gaon ban raha hai..." over an empty
/// mud plain at 1 s, the fallback blocky Nilesh, houses popping in at
/// 2-5 s. The Round 4 gate (input disabled + HUD hidden until
/// FinishBuild) existed, but the loading screen itself was a 0.72-alpha
/// dim over the LIVE camera — the build was visible through it.
/// This test proves the gate on the real build:
///   (a) input stays DISABLED for the whole build — PlayerController.
///       inputEnabled is false at every poll until "ModelsLoaded" exists;
///   (b) the HUD stays hidden during the build (MobileCanvas inactive)
///       and the loading dim is OPAQUE (alpha 1) while the build runs;
///   (c) after the gate opens, input is enabled, the HUD is shown, the
///       loading dim is gone, and the village object count is STABLE for
///       5 s — no objects may appear after the gate opens.
/// The test assembly cannot reference game classes (CS0246), so the
/// PlayerController is driven by name + reflection, the same way
/// PlayerSpeedTest drives it.
/// </summary>
public class LoadingGateTest
{
    [UnityTest]
    public IEnumerator InputStaysDisabledUntilBuildCompletes()
    {
        if (!Application.isPlaying)
        {
            yield break;
        }

        yield return SceneManager.LoadSceneAsync("Main");

        var player = GameObject.Find("Player");
        Assert.IsNotNull(player, "Player not found in Main scene");
        Component pc = player.GetComponent("PlayerController") as Component;
        Assert.IsNotNull(pc, "PlayerController not found");
        FieldInfo gateField = pc.GetType().GetField("inputEnabled");
        Assert.IsNotNull(gateField, "PlayerController.inputEnabled field missing");

        // (a) Poll the gate from the first frame: it must be false until
        // the build signals completion. No 1 s wait first — the build is
        // the thing under test, and a fast build must not skip the check.
        bool sawBuilding = false;
        float wait = 0f;
        while (GameObject.Find("ModelsLoaded") == null && wait < 150f)
        {
            Assert.IsFalse((bool)gateField.GetValue(pc),
                "Input gate OPENED before the build finished (ModelsLoaded not yet present) — the loading gate does not hold");
            sawBuilding = true;
            // (b) HUD hidden + loading dim present and opaque, while the
            // build runs. GameObject.Find only finds ACTIVE objects, so
            // a null MobileCanvas proves the HUD is hidden.
            Assert.IsNull(GameObject.Find("MobileCanvas"),
                "MobileCanvas (HUD) is active DURING the build — the player sees dead controls");
            GameObject dim = GameObject.Find("LoadingDim");
            Assert.IsNotNull(dim, "LoadingDim missing DURING the build — no loading screen at all");
            CheckDimOpaque(dim);
            yield return new WaitForSeconds(0.25f);
            wait += 0.25f;
        }
        Assert.IsTrue(sawBuilding,
            "The build finished before the first poll — the during-build gate was never observed");
        Debug.Log("[LoadingGateTest] Build took ~" + wait.ToString("F1")
            + "s; input stayed disabled throughout");

        // (c) After the gate: input enabled, HUD shown, dim gone.
        Assert.IsTrue((bool)gateField.GetValue(pc),
            "Input gate still CLOSED after the build finished — the game never starts");
        Assert.IsNotNull(GameObject.Find("MobileCanvas"),
            "MobileCanvas (HUD) still hidden after the build finished");
        Assert.IsNull(GameObject.Find("LoadingDim"),
            "LoadingDim still active after the build finished — the loading screen never cleared");

        // Object count stable for 5 s after the gate opens.
        var village = GameObject.Find("Village");
        Assert.IsNotNull(village, "Village root not found in Main scene");
        int countAtGate = village.GetComponentsInChildren<Transform>(true).Length - 1;
        Debug.Log("[LoadingGateTest] Village objects at gate: " + countAtGate);
        Assert.Greater(countAtGate, 50, "Village has too few objects at the gate (" + countAtGate + ")");
        yield return new WaitForSeconds(5f);
        int countLater = village.GetComponentsInChildren<Transform>(true).Length - 1;
        Debug.Log("[LoadingGateTest] Village objects 5 s after gate: " + countLater);
        Assert.AreEqual(countAtGate, countLater,
            "Village object count changed after the gate opened (" + countAtGate + " -> " + countLater
            + ") — objects appeared during play");
        Debug.Log("[LoadingGateTest] PASS — gate held during the build, count stable after it");
        yield return null;
    }

    // The loading dim must be fully opaque: a translucent dim lets the
    // player watch the village assemble (the v1.0.6 defect). Read through
    // reflection so this test needs no compile-time UI reference.
    private static void CheckDimOpaque(GameObject dim)
    {
        Component img = dim.GetComponent("UnityEngine.UI.Image") as Component;
        if (img == null) img = dim.GetComponent("Image") as Component;
        if (img == null)
        {
            Debug.LogWarning("[LoadingGateTest] LoadingDim has no Image component — opacity check skipped");
            return;
        }
        PropertyInfo colorProp = img.GetType().GetProperty("color");
        if (colorProp == null)
        {
            Debug.LogWarning("[LoadingGateTest] Image.color property missing — opacity check skipped");
            return;
        }
        Color c = (Color)colorProp.GetValue(img, null);
        Assert.GreaterOrEqual(c.a, 0.99f,
            "Loading dim is translucent (alpha " + c.a.ToString("F2")
            + ") — the player can see the village assembling through it");
    }
}
