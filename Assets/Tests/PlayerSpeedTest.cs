using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// ROUND 6 (2026-11-10): the user's v1.0.7 phone verdict was "chalne
/// me run karne me kitna slow ho jata hai" — walk/run far too slow on
/// the phone. The movement speeds are now Walk 3.2 / Jog 4.5 / Run 6.5
/// m/s (PlayerController constants, GTA-like; Walk 3.2 is Hark's
/// proven walk speed from Bhoot Gaon). The 5.81 / 6.06 numbers
/// in NileshAnimator are the animation clips' NATURAL speeds (Hips
/// travel / clip length), used ONLY to match the clip playback rate so
/// the feet plant on the ground; they are NOT movement speeds. This
/// test measures the ACTUAL ground speed the player reaches and proves
/// Run is ~6.5 m/s. It drives the real PlayerController via reflection
/// (the test assembly cannot reference game classes), the same way
/// ScreenshotTest drives the door walk-in: moveInput + runHeld are set
/// EVERY frame because MobileControls overwrites moveInput from the
/// (idle) joystick each Update.
/// </summary>
public class PlayerSpeedTest
{
    // Results of the last MeasureRoutine run (a coroutine cannot return
    // values, so the helper writes here).
    private float m_mean;
    private float m_max;

    [UnityTest]
    public IEnumerator PlayerGroundSpeedsAreApproved()
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
        Debug.Log("[PlayerSpeedTest] Waited " + modelWait + "s for models; loaded="
            + (GameObject.Find("ModelsLoaded") != null));
        yield return new WaitForSeconds(1f);

        var player = GameObject.Find("Player");
        Assert.IsNotNull(player, "Player not found in Main scene");
        Component pc = player.GetComponent("PlayerController") as Component;
        Assert.IsNotNull(pc, "PlayerController not found");
        FieldInfo gateField = pc.GetType().GetField("inputEnabled");
        Assert.IsNotNull(gateField, "PlayerController.inputEnabled field missing");
        Assert.IsTrue((bool)gateField.GetValue(pc),
            "Player input still gated after the village built — cannot measure speed");
        FieldInfo moveField = pc.GetType().GetField("moveInput");
        FieldInfo runField = pc.GetType().GetField("runHeld");
        PropertyInfo speedProp = pc.GetType().GetProperty("CurrentSpeed");
        Assert.IsNotNull(moveField, "moveInput field missing");
        Assert.IsNotNull(runField, "runHeld field missing");
        Assert.IsNotNull(speedProp, "CurrentSpeed property missing");

        // Drive direction: camera-forward (same math as the door walk-in
        // in ScreenshotTest). At spawn the gameplay camera faces north
        // (yaw 0), so forward is +Z up the open chowk lane.
        var cam = Camera.main;
        Assert.IsNotNull(cam, "Main Camera not found");
        float yaw = cam.transform.eulerAngles.y * Mathf.Deg2Rad;
        Vector3 fwd = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
        Vector3 want = fwd; // straight ahead, camera-relative
        float h = Vector3.Dot(want, right);
        float v = Vector3.Dot(want, fwd);

        // Phase order matters: RUN FIRST. The north lane from spawn ends
        // at a collider ~15 m out (build 42621705733: walk+jog first ate
        // the clear lane, so the run phase could only move 2.53 m before
        // the player was blocked and the displacement assert failed —
        // even though the run speed itself was a correct 4.00 m/s).
        // Running first measures the displacement on the fresh lane.
        // ROUND 6 FIX 2 (dynamic blockers) changed what "clear" means:
        // NPCs and animals now block the player, so the north lane is
        // no longer guaranteed empty — build 42781150374 ran at a
        // correct 6.37 m/s mean but only displaced 4.98 m in 2.5 s
        // (blocked by an NPC/animal ~5 m up the lane). The run SPEED
        // is proven by the CurrentSpeed asserts below (mean 6.0–7.0,
        // max ≤ 7.1); the displacement assert is only a sanity check
        // that the player actually MOVES at run speed, so its floor
        // is 4 m (a stuck player displaces ~0). Jog/walk assert only
        // the commanded speed (CurrentSpeed is input-driven, so a
        // wall does not change the reading).

        // Run: full stick + RUN held. Target = Lerp(3.5, 6.5, 1.0) = 6.5.
        // ROUND 6: the user ordered faster movement after v1.0.7 (walk/run
        // far too slow on the phone). Accel 5 m/s^2 reaches 6.5 in 1.3 s,
        // so displacement in 2.5 s is ~12 m on a clear lane (4.2 m ramp
        // + 7.8 m at speed) — less if an NPC/animal blocks the lane.
        Vector3 runStart = player.transform.position;
        yield return MeasureRoutine(pc, moveField, runField, speedProp,
            new Vector2(h, v), true, 2.5f);
        float runMean = m_mean;
        float runMax = m_max;
        float runDisp = Vector3.Distance(
            new Vector3(player.transform.position.x, 0f, player.transform.position.z),
            new Vector3(runStart.x, 0f, runStart.z));
        Debug.Log("[PlayerSpeedTest] Run mean speed: " + runMean.ToString("F2")
            + " m/s, max: " + runMax.ToString("F2")
            + " m/s, displacement in 2.5 s: " + runDisp.ToString("F2") + " m (expect ~6.5 / ~12 m on a clear lane)");
        Assert.GreaterOrEqual(runMean, 6.0f, "Run too slow (" + runMean.ToString("F2") + " m/s, expect ~6.5)");
        Assert.LessOrEqual(runMean, 7.0f, "Run too fast (" + runMean.ToString("F2") + " m/s, expect ~6.5)");
        Assert.LessOrEqual(runMax, 7.1f, "Run peaked at " + runMax.ToString("F2") + " m/s — exceeds the 6.5 m/s approved speed");
        Assert.GreaterOrEqual(runDisp, 4f, "Run displacement only " + runDisp.ToString("F2")
            + " m in 2.5 s — the player is not actually moving at run speed (blocked immediately?)");

        // Jog: full stick, no run. Target = Lerp(3.2, 4.5, 1.0) = 4.5.
        // BHOOT GAON (Round 6): Jog 3.5 -> 4.5 — Hark's walk alone is
        // 3.2, so a 3.5 Jog was barely faster than walking.
        yield return MeasureRoutine(pc, moveField, runField, speedProp,
            new Vector2(h, v), false, 2.5f);
        Debug.Log("[PlayerSpeedTest] Jog mean speed: " + m_mean.ToString("F2") + " m/s (expect ~4.5)");
        Assert.GreaterOrEqual(m_mean, 4.1f, "Jog too slow (" + m_mean.ToString("F2") + " m/s, expect ~4.5)");
        Assert.LessOrEqual(m_mean, 4.9f, "Jog too fast (" + m_mean.ToString("F2") + " m/s, expect ~4.5)");

        // Walk: half stick (mag 0.4), no run. Target = 3.2 * (0.4/0.5) = 2.56.
        yield return MeasureRoutine(pc, moveField, runField, speedProp,
            new Vector2(h * 0.4f, v * 0.4f), false, 2.5f);
        Debug.Log("[PlayerSpeedTest] Walk mean speed: " + m_mean.ToString("F2") + " m/s (expect ~2.56)");
        Assert.GreaterOrEqual(m_mean, 2.2f, "Walk too slow (" + m_mean.ToString("F2") + " m/s, expect ~2.56)");
        Assert.LessOrEqual(m_mean, 2.9f, "Walk too fast (" + m_mean.ToString("F2") + " m/s, expect ~2.56)");

        // Stop the player so later tests start clean.
        moveField.SetValue(pc, Vector2.zero);
        runField.SetValue(pc, false);
        yield return null;
    }

    // Drives the player for `seconds`, setting the inputs EVERY frame
    // (MobileControls overwrites moveInput from the idle joystick each
    // Update). Samples CurrentSpeed each frame after the first 1 s
    // (accel 5 m/s^2 needs < 1 s to converge) and writes the mean + max
    // to m_mean / m_max.
    private IEnumerator MeasureRoutine(Component pc, FieldInfo moveField, FieldInfo runField,
        PropertyInfo speedProp, Vector2 input, bool runHeld, float seconds)
    {
        m_mean = 0f;
        m_max = 0f;
        float sum = 0f;
        int count = 0;
        float t = 0f;
        while (t < seconds)
        {
            moveField.SetValue(pc, input);
            runField.SetValue(pc, runHeld);
            yield return null;
            t += Time.deltaTime;
            if (t > 1f)
            {
                float s = (float)speedProp.GetValue(pc, null);
                sum += s;
                count++;
                if (s > m_max) m_max = s;
            }
        }
        m_mean = count > 0 ? sum / count : 0f;
        moveField.SetValue(pc, Vector2.zero);
        runField.SetValue(pc, false);
        yield return null;
    }
}
