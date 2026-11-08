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
    private string current = "";
    private Vector3 prevPos;
    private float speedSmooth;
    private const float kBlend = 0.25f;

    void Start()
    {
        prevPos = transform.position;
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
        anim.Play("NileshIdle");
        current = "NileshIdle";
    }

    private bool hasWalk, hasRun;

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
        Vector3 p = transform.position;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        float speed = new Vector2(p.x - prevPos.x, p.z - prevPos.z).magnitude / dt;
        prevPos = p;
        speedSmooth = Mathf.Lerp(speedSmooth, speed, Mathf.Clamp01(dt * 8f));

        // Phase 1 speeds (PlayerController): walk 1.4, jog 2.5, run 4.0.
        // Jog uses the run clip (there is no separate jog clip).
        string want = "NileshIdle";
        if (speedSmooth > 0.4f) want = speedSmooth > 1.9f ? "NileshRun" : "NileshWalk";
        if (want == "NileshRun" && !hasRun) want = hasWalk ? "NileshWalk" : "NileshIdle";
        if (want == "NileshWalk" && !hasWalk) want = "NileshIdle";
        if (want != current)
        {
            anim.CrossFade(want, kBlend);
            current = want;
            if (want == "NileshIdle" && anim["NileshIdle"] != null)
                anim["NileshIdle"].speed = 1f;
        }
        // Match the clip's playback speed to the ground speed so the feet
        // don't slide: the walk cycle is authored for ~1.4 m/s and the run
        // cycle for ~4.2 m/s. Clamped so a lag spike never fast-forwards
        // the animation. (Idle keeps 1x — it has no ground speed.)
        if (current == "NileshWalk" || current == "NileshRun")
        {
            AnimationState st = anim[current];
            if (st != null)
            {
                float baseSpeed = (current == "NileshRun") ? 4.2f : 1.4f;
                st.speed = Mathf.Clamp(speedSmooth / baseSpeed, 0.5f, 1.5f);
            }
        }
    }
}
