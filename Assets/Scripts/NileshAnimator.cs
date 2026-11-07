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
        anim["NileshIdle"].wrapMode = WrapMode.Loop;
        anim["NileshWalk"].wrapMode = WrapMode.Loop;
        anim["NileshRun"].wrapMode = WrapMode.Loop;
        anim.Play("NileshIdle");
        current = "NileshIdle";
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

        // Walk speed is 5 m/s, run is 9 m/s (PlayerController).
        string want = "NileshIdle";
        if (speedSmooth > 0.4f) want = speedSmooth > 7f ? "NileshRun" : "NileshWalk";
        if (want != current)
        {
            anim.CrossFade(want, kBlend);
            current = want;
        }
    }
}
