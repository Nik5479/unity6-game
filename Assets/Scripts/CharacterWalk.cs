using UnityEngine;

/// <summary>
/// Visual-only walk animation for the Nilesh figure (child of the Player
/// root). Bobs the figure and swings arms/legs in step with movement
/// speed. Reads the Player root's position delta — it never touches
/// PlayerController movement or collision.
/// </summary>
public class CharacterWalk : MonoBehaviour
{
    public Transform figureRoot;
    public Transform armL;
    public Transform armR;
    public Transform legL;
    public Transform legR;

    private Vector3 prevPos;
    private float phase;
    private float speedSmooth;
    private int m_lastStepIndex;

    void Start()
    {
        prevPos = transform.position;
    }

    void Update()
    {
        if (figureRoot == null) return;

        Vector3 p = transform.position;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        float speed = new Vector2(p.x - prevPos.x, p.z - prevPos.z).magnitude / dt;
        prevPos = p;

        // Smooth the speed so the bob doesn't jitter.
        speedSmooth = Mathf.Lerp(speedSmooth, speed, Mathf.Clamp01(dt * 8f));
        float k = Mathf.Clamp01(speedSmooth / 4f); // 0 idle, 1 full walk/run

        phase += dt * (4f + speedSmooth * 1.6f) * (k > 0.02f ? 1f : 0f);

        // One footstep per half-cycle (each leg strike).
        int stepIndex = Mathf.FloorToInt(phase / Mathf.PI);
        if (k > 0.02f && stepIndex != m_lastStepIndex)
        {
            m_lastStepIndex = stepIndex;
            AudioManager.PlayFootstep(transform.position);
        }

        // Bob: small vertical bounce while walking, tiny breathing when idle.
        float bob = (k > 0.02f)
            ? Mathf.Abs(Mathf.Sin(phase)) * 0.06f * k
            : Mathf.Sin(Time.time * 1.6f) * 0.008f;
        Vector3 lp = figureRoot.localPosition;
        lp.y = bob;
        figureRoot.localPosition = lp;

        // Arm/leg swing (opposite phase), scaled by speed.
        float swing = Mathf.Sin(phase) * 32f * k;
        if (armL != null) armL.localRotation = Quaternion.Euler(swing, 0f, 0f);
        if (armR != null) armR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
        if (legL != null) legL.localRotation = Quaternion.Euler(-swing, 0f, 0f);
        if (legR != null) legR.localRotation = Quaternion.Euler(swing, 0f, 0f);
    }
}
