using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// ROUND 4 (2026-11-09, life): NPCs and animals MUST visibly move.
/// The user's v1.0.5 phone video (30.8 s) showed the horse in the
/// same spot at 10 s / 12 s / 22 s and Meera + cow stationary in every
/// frame — yet no test in the repo had ever asserted that a position
/// CHANGES. The routines read correct in static review (no gate, no
/// blocker, open ground), so this test is the probe that discriminates:
/// if it FAILS in CI, the freeze is in the shipped code and
/// reproducible; if it PASSES in CI, the routines move in the editor
/// and the phone freeze is build/device-only. Path length (not raw
/// displacement) is the metric: a wandering animal that loops back
/// near its start still accumulates path, while a frozen one stays
/// at exactly 0. Chicken_1 is deliberately excluded — its spawn sits
/// inside a collider (known pre-existing placement bug), not a
/// routine bug.
/// </summary>
public class MovementTest
{
    // Subjects: two animals (roam) + two NPCs (waypoints). All walk
    // 0.5-0.8 m/s with 1-6 s pauses; in 16 s each covers several
    // metres, so 1.0 m of path is a very safe bar for "it moves".
    private static readonly string[] kSubjects =
        { "Horse", "Cow", "Meera", "Villager_Man" };

    [UnityTest]
    public IEnumerator NpcAndAnimalsMove()
    {
        if (!Application.isPlaying)
        {
            yield break;
        }

        yield return SceneManager.LoadSceneAsync("Main");

        // Same wait as ScreenshotTest: real .glb models load async,
        // the village must be built before positions mean anything.
        yield return new WaitForSeconds(1f);
        float modelWait = 0f;
        while (GameObject.Find("ModelsLoaded") == null && modelWait < 150f)
        {
            yield return new WaitForSeconds(0.5f);
            modelWait += 0.5f;
        }
        Debug.Log("[MovementTest] Waited " + modelWait + "s for models; loaded="
            + (GameObject.Find("ModelsLoaded") != null));
        yield return new WaitForSeconds(1f);

        // Find subjects and record start positions (XZ only — the walk
        // bob moves Y by 0.02 m and must not count as movement).
        Transform[] found = new Transform[kSubjects.Length];
        Vector3[] last = new Vector3[kSubjects.Length];
        float[] pathLen = new float[kSubjects.Length];
        int foundCount = 0;
        for (int i = 0; i < kSubjects.Length; i++)
        {
            GameObject g = GameObject.Find(kSubjects[i]);
            if (g == null)
            {
                Debug.LogWarning("[MovementTest] Subject missing: " + kSubjects[i]);
                continue;
            }
            found[i] = g.transform;
            last[i] = Flat(g.transform.position);
            foundCount++;
            Debug.Log("[MovementTest] " + kSubjects[i] + " starts at " + last[i].ToString("F2"));
        }
        Assert.GreaterOrEqual(foundCount, 3,
            "Fewer than 3 movement subjects found (" + foundCount + ") — village did not build");

        // Sample every 1 s for 16 s and accumulate path length.
        for (int s = 0; s < 16; s++)
        {
            yield return new WaitForSeconds(1f);
            for (int i = 0; i < kSubjects.Length; i++)
            {
                if (found[i] == null) continue;
                Vector3 now = Flat(found[i].position);
                pathLen[i] += Vector3.Distance(now, last[i]);
                last[i] = now;
            }
        }

        int moved = 0;
        for (int i = 0; i < kSubjects.Length; i++)
        {
            if (found[i] == null) continue;
            Debug.Log("[MovementTest] " + kSubjects[i] + " path length in 16 s: "
                + pathLen[i].ToString("F2") + " m (now at " + last[i].ToString("F2") + ")");
            if (pathLen[i] > 1.0f) moved++;
        }
        Assert.AreEqual(foundCount, moved,
            "Only " + moved + "/" + foundCount + " subjects moved > 1 m in 16 s — "
            + "the routines are frozen (see [MovementTest] positions above). "
            + "Horse/Cow/Meera/Villager_Man must all visibly roam.");
        yield return null;
    }

    private static Vector3 Flat(Vector3 v) { return new Vector3(v.x, 0f, v.z); }
}
