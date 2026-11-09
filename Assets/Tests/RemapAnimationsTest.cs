using NUnit.Framework;
using UnityEngine;

/// <summary>
/// OVERNIGHT R3c: presence check for the remapped Nilesh clips.
/// The clips in Assets/Resources/Animations/ are COMMITTED artifacts
/// (made once with the RemapMixamoClips editor tool). They must NOT be
/// regenerated in CI: the R3b run proved the CI remap produces T-pose
/// clips (Nilesh stood arms-spread in every R3b screenshot and emulator
/// shot — the remapped curves do not drive his bones), while the
/// committed clips animate correctly (R3 emulator: normal run cycle).
/// The remap tool itself is kept for manual use (menu item) — but the
/// CI must never overwrite these files. If the remap is ever fixed and
/// re-run by hand, re-commit the regenerated .anim files.
/// CrouchWalk / Jump have no committed clip yet (the remap that was
/// supposed to make them is the one that broke Idle/Walk/Run), so
/// NileshAnimator falls back to Walk for crouch and to no jump clip —
/// that is the honest state, not a failure of this test.
/// </summary>
public class RemapAnimationsTest
{
    [Test]
    public void CommittedNileshClipsExist()
    {
        string[] required = { "NileshIdle", "NileshWalk", "NileshRun", "NileshSprint" };
        foreach (string n in required)
        {
            AnimationClip clip = Resources.Load<AnimationClip>("Animations/" + n);
            Assert.IsNotNull(clip, "Committed clip missing: Resources/Animations/" + n + ".anim");
            Assert.Greater(clip.length, 0.1f, n + " has no length — corrupt clip");
            Debug.Log("[ClipCheck] " + n + ": " + clip.length.ToString("F2") + "s");
        }
        // Optional (the animator falls back gracefully): log, don't fail.
        foreach (string n in new string[] { "NileshCrouchWalk", "NileshJump" })
        {
            AnimationClip clip = Resources.Load<AnimationClip>("Animations/" + n);
            Debug.Log("[ClipCheck] " + n + ": " + (clip != null ? clip.length.ToString("F2") + "s" : "MISSING (animator falls back)"));
        }
    }

    // ROUND 4 WALK: the committed clips must carry NO forward root
    // motion. The old files ramped Hips z 1.81 m (walk) / 3.68 m (run)
    // per loop while PlayerController ALSO moved the root — feet slid
    // and the whole body snapped back every cycle. In-place clips have
    // near-constant Hips x/z (only the vertical bob remains, which is
    // the y curve and is intentionally kept).
    [Test]
    public void ClipsAreInPlace()
    {
        const string hipsPath = "Sketchfab_model/803b21a9e5ca42e2adaea3abf262f423.fbx/Object_2/RootNode/AvatarRoot/Object_5/_rootJoint/Hips_01";
        foreach (string n in new string[] { "NileshIdle", "NileshWalk", "NileshRun", "NileshSprint" })
        {
            AnimationClip clip = Resources.Load<AnimationClip>("Animations/" + n);
            Assert.IsNotNull(clip, "Committed clip missing: " + n);
            foreach (string prop in new string[] { "m_LocalPosition.x", "m_LocalPosition.z" })
            {
                AnimationCurve curve = clip.GetCurve(hipsPath, typeof(Transform), prop);
                Assert.IsNotNull(curve, n + ": no Hips " + prop + " curve — rig path changed?");
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (Keyframe k in curve.keys)
                {
                    lo = Mathf.Min(lo, k.value);
                    hi = Mathf.Max(hi, k.value);
                }
                Debug.Log("[ClipCheck] " + n + " Hips " + prop + " range: " + lo.ToString("F4") + ".." + hi.ToString("F4"));
                Assert.Less(hi - lo, 0.01f, n + ": Hips " + prop + " still carries root motion (range " + (hi - lo).ToString("F3") + " m) — clip is not in-place");
            }
        }
    }
}
