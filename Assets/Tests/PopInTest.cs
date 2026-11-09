using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// ROUND 4 (2026-11-09, pop-in): the village must not appear/disappear.
/// The user saw objects popping in and out during play. Two causes were
/// found: (1) the jungle-ring tree clones each had a single LODGroup at
/// 0.025 that CULLED the whole tree when it covered less than 2.5% of
/// the screen — ring trees winked in/out as the camera panned; (2) the
/// build watchdog could call FinishBuild() (opening the loading gate)
/// while BuildVillageAsync() was still placing objects — a pop-in tail.
/// The loading gate covers the build phase; this test covers AFTER the
/// build: zero LODGroups under the village root, a full (not cut-short)
/// build, and an object count that does not change over 5 s of play.
/// NOTE: the test assembly cannot reference game classes (CS0246), so
/// everything game-side is reached by reflection / GameObject.Find.
/// </summary>
public class PopInTest
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

    [UnityTest]
    public IEnumerator VillageStaysFullyVisibleAfterBuild()
    {
        if (!Application.isPlaying)
        {
            yield break;
        }

        yield return SceneManager.LoadSceneAsync("Main");

        // Same wait as MovementTest/ScreenshotTest: real .glb models load
        // async, the village must be built before counting means anything.
        yield return new WaitForSeconds(1f);
        float modelWait = 0f;
        while (GameObject.Find("ModelsLoaded") == null && modelWait < 150f)
        {
            yield return new WaitForSeconds(0.5f);
            modelWait += 0.5f;
        }
        Debug.Log("[PopInTest] Waited " + modelWait + "s for models; loaded="
            + (GameObject.Find("ModelsLoaded") != null));
        // Let FinishBuild() run (it is called right after ModelsLoaded
        // is signalled in the build's finally block).
        yield return new WaitForSeconds(1f);

        GameObject village = GameObject.Find("Village");
        Assert.IsNotNull(village, "Village root not found after build");

        // (1) No LODGroup anywhere under the village root — a single-LOD
        // setup's only next step is "culled", which is the pop-in bug.
        LODGroup[] lods = village.GetComponentsInChildren<LODGroup>(true);
        Debug.Log("[PopInTest] LODGroups under Village: " + lods.Length);
        Assert.AreEqual(0, lods.Length,
            "LODGroup(s) still present under the village root — ring trees can be culled mid-play");

        // (2) The build must be FULL, not cut short by the deadline —
        // a cut-short build is a partial village, not a complete one.
        System.Type builder = FindType("VillageBuilder");
        Assert.IsNotNull(builder, "VillageBuilder type not found");
        string last = (string)builder.GetField("LastBootStatus",
            BindingFlags.Public | BindingFlags.Static).GetValue(null);
        Debug.Log("[PopInTest] LastBootStatus: \"" + last + "\"");
        Assert.IsTrue(last.Contains("objects built"),
            "LastBootStatus is not the final message: \"" + last + "\"");
        Assert.IsFalse(last.Contains("timed out"),
            "Build was cut short by the deadline — partial village shipped as done: \"" + last + "\"");

        // (3) Object count must not change over 5 s of play — nothing
        // may be placed, unloaded, or culled after the build.
        int before = CountObjects(village);
        Debug.Log("[PopInTest] Objects at FinishBuild: " + before);
        yield return new WaitForSeconds(5f);
        int after = CountObjects(village);
        Debug.Log("[PopInTest] Objects after +5s play: " + after);
        Assert.AreEqual(before, after,
            "Village object count changed after the build (" + before + " -> " + after
            + ") — objects are still appearing/disappearing");
        yield return null;
    }

    private static int CountObjects(GameObject village)
    {
        return village.GetComponentsInChildren<Transform>(true).Length - 1;
    }
}
