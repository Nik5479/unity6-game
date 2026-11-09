using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// ROUND 4 follow-up (2026-11-09): the user ordered "fast speed na ho"
/// (no fast speed). The movement speeds are Walk 1.4 / Jog 2.5 / Run 4.0
/// m/s (PlayerController constants) — the 5.81 / 6.06 numbers in
/// NileshAnimator are the animation clips' NATURAL speeds (Hips travel /
/// clip length), used ONLY to match the clip playback rate so the feet
/// plant on the ground; they are NOT movement speeds and must never be
/// "fixed" down to 4.0 (that would make the feet slide backwards at
/// ~1.8 m/s). This test measures the ACTUAL ground speed the player
/// reaches and proves Run is ~4.0 m/s, not 5.81. It drives the real
/// PlayerController via reflection (the test assembly cannot reference
/// game classes), the same way ScreenshotTest drives the door walk-in:
/// moveInput + runHeld are set EVERY frame because MobileControls
/// overwrites moveInput from the (idle) joystick each Update.
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
        // Running first measures the displacement on the fresh, clear
        // lane from spawn (~8.4 m in 2.5 s). Jog/walk follow and assert
        // only the commanded speed (CurrentSpeed is input-driven, so a
        // wall does not change the reading).

        // Run: full stick + RUN held. Target = Lerp(2.5, 4.0, 1.0) = 4.0.
        // The user's bar is "fast speed na ho": Run must be ~4.0, never 5.81.
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
            + " m/s, displacement in 2.5 s: " + runDisp.ToString("F2") + " m (expect ~4.0 / ~8.4 m)");
        Assert.GreaterOrEqual(runMean, 3.5f, "Run too slow (" + runMean.ToString("F2") + " m/s, expect ~4.0)");
        Assert.LessOrEqual(runMean, 4.5f, "Run too fast (" + runMean.ToString("F2") + " m/s — user ordered no fast speed, expect ~4.0)");
        Assert.LessOrEqual(runMax, 4.6f, "Run peaked at " + runMax.ToString("F2") + " m/s — exceeds the 4.0 m/s approved speed");
        Assert.GreaterOrEqual(runDisp, 4f, "Run displacement only " + runDisp.ToString("F2")
            + " m in 2.5 s — the player is not actually moving at run speed (blocked?)");

        // Jog: full stick, no run. Target = Lerp(1.4, 2.5, 1.0) = 2.5.
        yield return MeasureRoutine(pc, moveField, runField, speedProp,
            new Vector2(h, v), false, 2.5f);
        Debug.Log("[PlayerSpeedTest] Jog mean speed: " + m_mean.ToString("F2") + " m/s (expect ~2.5)");
        Assert.GreaterOrEqual(m_mean, 2.1f, "Jog too slow (" + m_mean.ToString("F2") + " m/s, expect ~2.5)");
        Assert.LessOrEqual(m_mean, 2.9f, "Jog too fast (" + m_mean.ToString("F2") + " m/s, expect ~2.5)");

        // Walk: half stick (mag 0.4), no run. Target = 1.4 * (0.4/0.5) = 1.12.
        yield return MeasureRoutine(pc, moveField, runField, speedProp,
            new Vector2(h * 0.4f, v * 0.4f), false, 2.5f);
        Debug.Log("[PlayerSpeedTest] Walk mean speed: " + m_mean.ToString("F2") + " m/s (expect ~1.12)");
        Assert.GreaterOrEqual(m_mean, 0.8f, "Walk too slow (" + m_mean.ToString("F2") + " m/s, expect ~1.12)");
        Assert.LessOrEqual(m_mean, 1.5f, "Walk too fast (" + m_mean.ToString("F2") + " m/s, expect ~1.12)");

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
