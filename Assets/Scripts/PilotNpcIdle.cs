using UnityEngine;

/// <summary>
/// PILOT (2026-11-08): subtle idle life for a rigged NPC that has NO
/// embedded animation clip (the Pujari: rigged, 0 clips — the
/// PlayEmbeddedAnimations call is a no-op for him, so he stood frozen).
/// Gentle breathing (vertical scale) + a very slow yaw drift. Deliberately
/// tiny: a priest standing at the temple entrance should look alive, not
/// dance. Does not touch movement, collision or dialogue.
/// </summary>
public class PilotNpcIdle : MonoBehaviour
{
    private float baseYaw;
    private Vector3 baseScale;

    void Start()
    {
        baseYaw = transform.rotation.eulerAngles.y;
        baseScale = transform.localScale;
    }

    void Update()
    {
        float t = Time.time;
        // Breathing: ~1 breath per 4 s, 0.8% height change.
        float breathe = 1f + 0.008f * Mathf.Sin(t * 1.57f);
        transform.localScale = new Vector3(baseScale.x, baseScale.y * breathe, baseScale.z);
        // Slow drift: +/-4 degrees over ~24 s.
        float sway = 4f * Mathf.Sin(t * 0.26f);
        transform.rotation = Quaternion.Euler(0f, baseYaw + sway, 0f);
    }
}
