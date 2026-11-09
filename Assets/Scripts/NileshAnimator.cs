using UnityEngine;

/// <summary>
/// Drives Nilesh's real Mixamo walk/run/idle clips. The clips are the
/// Remy FBX animations remapped onto Nilesh's Mixamo skeleton by the
/// editor tool RemapMixamoClips (Assets/Resources/Animations/
/// Nilesh{Idle,Walk,Run}.anim) and played on Nilesh's legacy Animation
/// component. CrossFades by the player's actual movement speed, so the
/// walk/run is real mocap — not the old procedural arm/leg swing
/// (CharacterWalk only bobs + plays footsteps now).
/// Falls back to the embedded 'mixamo.com' idle clip when the remapped
/// clips are missing, so Nilesh is never frozen.
/// </summary>
public class NileshAnimator : MonoBehaviour
{
    private Animation anim;
    private PlayerController pc;
    private string current = "";
    private Vector3 prevPos;
    private float speedSmooth;
    private const float kBlend = 0.25f;

    void Start()
    {
        prevPos = transform.position;
        pc = GetComponent<PlayerController>();
        GameObject nilesh = GameObject.Find("Player/Nilesh");
        if (nilesh == null)
        {
            Debug.LogWarning("[NileshAnimator] Player/Nilesh not found");
            enabled = false;
            return;
        }
        anim = nilesh.GetComponentInChildren<Animation>();
        if (anim == null)
        {
            Debug.LogWarning("[NileshAnimator] Nilesh has no Animation component");
            enabled = false;
            return;
        }
        AddClip("NileshIdle");
        AddClip("NileshWalk");
        AddClip("NileshRun");
        // OVERNIGHT R3: Sprint was remapped long ago but never loaded
        // (audit 2.2); CrouchWalk + Jump are remapped by the CI remap
        // test (RemapAnimationsTest) from the repo Mixamo FBX.
        AddClip("NileshSprint");
        AddClip("NileshCrouchWalk");
        AddClip("NileshJump");
        // Phone-video fix: resolve the arm bones for the LateUpdate swing
        // dampener (see ArmDampKeep). Null-safe if the rig lacks them.
        ResolveArmBones(nilesh);
        if (anim.GetClip("NileshIdle") == null)
        {
            Debug.LogWarning("[NileshAnimator] Remapped clips missing — using embedded idle clip");
            GltfModelLoader.PlayEmbeddedAnimations(nilesh);
            enabled = false;
            return;
        }
        // Null-guard each clip: a missing Walk/Run clip must not throw in
        // Start and freeze Nilesh (audit M6). Missing ones fall back below.
        SetLoop("NileshIdle");
        hasWalk = SetLoop("NileshWalk");
        hasRun = SetLoop("NileshRun");
        hasSprint = SetLoop("NileshSprint");
        hasCrouch = SetLoop("NileshCrouchWalk");
        // Jump plays ONCE (not looped) and holds its last frame until the
        // landing crossfade takes over.
        hasJump = anim["NileshJump"] != null;
        if (hasJump) anim["NileshJump"].wrapMode = WrapMode.ClampForever;
        anim.Play("NileshIdle");
        current = "NileshIdle";
    }

    private bool hasWalk, hasRun, hasSprint, hasCrouch, hasJump;

    // ARM-DAMPEN (phone-video fix 2026-11-09): the Mixamo run/sprint
    // clips pump the arms far too hard — measured from the .anim curves,
    // NileshRun swings the upper arms 26-28 deg peak-to-peak and the
    // forearms 45-52 deg; NileshSprint swings the right arm 39 deg and
    // both forearms 31-53 deg. The user wants GTA-like movement with NO
    // exaggerated arm/leg swinging. The legacy Animation component writes
    // the bone rotations before LateUpdate, so LateUpdate slerps each arm
    // bone toward its own slow-moving average — the swing is halved
    // around the clip's natural mean posture (bent elbows stay bent, the
    // arms just travel half as far), with no reference pose to capture
    // and no .anim file to regenerate. Idle/crouch/jump are untouched.
    // Bones are resolved by glTF node name (LeftArm_013 etc.); every
    // lookup is null-guarded, so a missing bone is a silent no-op.
    private Transform[] armBones;
    private Quaternion[] armAvg;
    private bool[] armAvgInit;
    private static readonly string[] kArmBoneNames = new string[]
    {
        "LeftShoulder_012", "RightShoulder_038",
        "LeftArm_013", "RightArm_039",
        "LeftForeArm_014", "RightForeArm_040",
    };

    private void ResolveArmBones(GameObject nilesh)
    {
        Transform[] all = nilesh.GetComponentsInChildren<Transform>(true);
        armBones = new Transform[kArmBoneNames.Length];
        armAvg = new Quaternion[kArmBoneNames.Length];
        armAvgInit = new bool[kArmBoneNames.Length];
        for (int i = 0; i < kArmBoneNames.Length; i++)
        {
            foreach (Transform t in all)
            {
                if (t.name == kArmBoneNames[i] || t.name.EndsWith("/" + kArmBoneNames[i]))
                {
                    armBones[i] = t;
                    break;
                }
            }
            if (armBones[i] == null)
                Debug.LogWarning("[NileshAnimator] Arm bone not found: " + kArmBoneNames[i] + " — arm dampen skips it");
        }
    }

    // Damp factor for the current clip: 1 = untouched, 0 = frozen at the
    // running average. Run/sprint keep 45% of the swing (26-39 deg ->
    // ~12-18 deg upper arm, 45-53 deg -> ~20-24 deg forearm); walk keeps
    // 70%; everything else is untouched.
    private float ArmDampKeep()
    {
        if (current == "NileshRun" || current == "NileshSprint") return 0.45f;
        if (current == "NileshWalk") return 0.7f;
        return 1f;
    }

    void LateUpdate()
    {
        if (anim == null || armBones == null) return;
        float keep = ArmDampKeep();
        if (keep >= 1f) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        // The average follows slowly (~0.5 s time constant) so it tracks
        // the clip's mean posture through a crossfade without snapping.
        float follow = 1f - Mathf.Exp(-dt * 2f);
        for (int i = 0; i < armBones.Length; i++)
        {
            Transform b = armBones[i];
            if (b == null) continue;
            Quaternion cur = b.localRotation;
            if (!armAvgInit[i])
            {
                armAvg[i] = cur;
                armAvgInit[i] = true;
                continue;
            }
            armAvg[i] = Quaternion.Slerp(armAvg[i], cur, follow);
            b.localRotation = Quaternion.Slerp(armAvg[i], cur, keep);
        }
    }

    private bool SetLoop(string name)
    {
        AnimationState st = anim[name];
        if (st == null) return false;
        st.wrapMode = WrapMode.Loop;
        return true;
    }

    private void AddClip(string name)
    {
        AnimationClip clip = Resources.Load<AnimationClip>("Animations/" + name);
        if (clip != null)
        {
            anim.AddClip(clip, name);
        }
        else
        {
            Debug.LogWarning("[NileshAnimator] Clip not found in Resources: Animations/" + name);
        }
    }

    void Update()
    {
        if (anim == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        // ROUND 4 WALK: the animator trusts the controller's INTENDED
        // speed (pc.CurrentSpeed — already accel/decel-smoothed), not the
        // position delta. The old delta measure read 0 whenever TryMove
        // was blocked by a wall/door, so Nilesh stood Idle with the
        // joystick held (phone video frame-20s: stick UP, idle at the
        // door). Fallback to the delta only if the controller is missing.
        if (pc != null)
        {
            speedSmooth = pc.CurrentSpeed;
        }
        else
        {
            Vector3 p = transform.position;
            float speed = new Vector2(p.x - prevPos.x, p.z - prevPos.z).magnitude / dt;
            prevPos = p;
            speedSmooth = Mathf.Lerp(speedSmooth, speed, Mathf.Clamp01(dt * 8f));
        }

        // Phase 1 speeds (PlayerController): walk 1.4, jog 2.5, run 4.0.
        // Jog uses the run clip (there is no separate jog clip).
        // OVERNIGHT R3: airborne (player centre y > 0.6 — ground is 0.5)
        // plays the Jump clip once; crouching plays CrouchWalk.
        // ROUND 4 WALK: the Sprint clip is never selected — max script
        // speed 4.0 m/s can never reach its 6.06 m/s natural speed, so
        // full run plays the Run clip at 4.0/5.81 = 0.69x (feet still
        // plant). Walk clip below 2.0 m/s, Run clip at/above, with
        // +/-0.15 m/s hysteresis on the boundary so speed noise near
        // 2.0 does not flip clips every frame.
        bool airborne = transform.position.y > 0.6f;
        if (airborne && hasJump && current != "NileshJump")
        {
            anim.CrossFade("NileshJump", kBlend);
            current = "NileshJump";
        }
        else if (!airborne)
        {
            string want = "NileshIdle";
            if (pc != null && pc.IsCrouching)
            {
                want = hasCrouch ? "NileshCrouchWalk" : (hasWalk ? "NileshWalk" : "NileshIdle");
            }
            else if (speedSmooth > 0.4f)
            {
                bool inRun = (current == "NileshRun" || current == "NileshSprint");
                float runThresh = inRun ? 1.85f : 2.15f;
                if (speedSmooth > runThresh) want = hasRun ? "NileshRun" : "NileshWalk";
                else want = hasWalk ? "NileshWalk" : "NileshIdle";
            }
            if (want != current)
            {
                anim.CrossFade(want, kBlend);
                current = want;
                if (want == "NileshIdle" && anim["NileshIdle"] != null)
                    anim["NileshIdle"].speed = 1f;
            }
        }
        // Match the clip's playback speed to the ground speed so the feet
        // don't slide. ROUND 4 WALK: base speeds are MEASURED from the
        // committed clips (Hips z travel / clip length) — Walk 1.75,
        // Run 5.81, Sprint 6.06 m/s — not the old guesses (1.4/4.2/5.0).
        // With in-place clips, playback = speed / baseSpeed plants the
        // feet exactly. Clamp floor 0.35x (was 0.5x): jog on the Run clip
        // needs 2.5/5.81 = 0.43x, and the old floor made feet slide
        // backwards. (Idle keeps 1x — it has no ground speed.)
        // OVERNIGHT R3: crouch-walk base 0.7 m/s (PlayerController.kCrouchSpeed).
        if (current == "NileshWalk" || current == "NileshRun" || current == "NileshSprint" || current == "NileshCrouchWalk")
        {
            AnimationState st = anim[current];
            if (st != null)
            {
                float baseSpeed = (current == "NileshSprint") ? 6.06f
                    : (current == "NileshCrouchWalk") ? 0.7f
                    : (current == "NileshRun") ? 5.81f : 1.75f;
                st.speed = Mathf.Clamp(speedSmooth / baseSpeed, 0.35f, 1.5f);
            }
        }
    }
}
