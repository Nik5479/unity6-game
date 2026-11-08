using UnityEngine;

/// <summary>
/// OVERNIGHT R2 (2026-11-09): animals roam instead of standing frozen
/// (audit 6.4). Each animal picks a random spot inside a radius of its
/// home, walks there slowly, pauses / grazes, and repeats. Movement
/// steers with VillageColliders.CanMoveTo (axis-separated slide, same as
/// the player) so animals never clip through walls, fences or houses.
/// The wolf switches between its embedded walk_loop / idle_loop clips;
/// the other animals have no animation clips in their .glb files, so
/// they move with a small bob only — a slow graze-walk, not a slide.
/// </summary>
public class AnimalRoam : MonoBehaviour
{
    public Vector3 home;
    public float roamRadius = 4f;
    public float walkSpeed = 0.6f;
    public bool isWolf = false;

    private Vector3 target;
    private bool moving;
    private float pauseT = 2f;
    private float stuckT;
    private Vector3 lastPos;
    private string currentClip = "";

    void Start()
    {
        if (home == Vector3.zero) home = transform.position;
        lastPos = transform.position;
        PickTarget();
        pauseT = Random.Range(1f, 3f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (!moving)
        {
            pauseT -= dt;
            if (pauseT <= 0f)
            {
                PickTarget();
                moving = true;
                SetWolfClip("walk_loop");
            }
            return;
        }

        Vector3 from = Flat(transform.position);
        Vector3 to = Flat(target);
        Vector3 dir = to - from;
        if (dir.sqrMagnitude < 0.09f) // arrived (0.3 m)
        {
            moving = false;
            pauseT = Random.Range(2.5f, 6f);
            SetWolfClip("idle_loop");
            return;
        }
        Vector3 step = dir.normalized * walkSpeed * dt;
        Vector3 p = transform.position;
        TryMove(ref p, step);
        if (Vector3.Distance(Flat(p), Flat(lastPos)) < 0.0005f && step.sqrMagnitude > 0.0001f)
        {
            stuckT += dt;
            if (stuckT > 1.5f) // blocked — give up this target
            {
                moving = false;
                pauseT = Random.Range(2f, 4f);
                stuckT = 0f;
                SetWolfClip("idle_loop");
                return;
            }
        }
        else stuckT = 0f;
        lastPos = Flat(p);
        p.y = Mathf.Abs(Mathf.Sin(Time.time * 5f)) * 0.02f; // walk bob
        transform.position = p;
        if (dir.sqrMagnitude > 0.001f)
        {
            float targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float curYaw = transform.eulerAngles.y;
            transform.rotation = Quaternion.Euler(0f, Mathf.MoveTowardsAngle(curYaw, targetYaw, 360f * dt), 0f);
        }
    }

    private void PickTarget()
    {
        float a = Random.Range(0f, Mathf.PI * 2f);
        float r = Random.Range(0.5f, 1f) * roamRadius;
        target = home + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        target.x = Mathf.Clamp(target.x, -21f, 21f);
        target.z = Mathf.Clamp(target.z, -21f, 21f);
    }

    private void SetWolfClip(string name)
    {
        if (!isWolf || currentClip == name) return;
        currentClip = name;
        GltfModelLoader.PlayEmbeddedAnimation(gameObject, name);
    }

    // Same axis-separated slide as PlayerController.
    private void TryMove(ref Vector3 p, Vector3 step)
    {
        Vector3 full = new Vector3(p.x + step.x, p.y, p.z + step.z);
        if (VillageColliders.CanMoveTo(full, 0.4f)) { p = full; return; }
        Vector3 onlyX = new Vector3(p.x + step.x, p.y, p.z);
        if (VillageColliders.CanMoveTo(onlyX, 0.4f)) { p = onlyX; return; }
        Vector3 onlyZ = new Vector3(p.x, p.y, p.z + step.z);
        if (VillageColliders.CanMoveTo(onlyZ, 0.4f)) { p = onlyZ; }
    }

    private static Vector3 Flat(Vector3 v) { return new Vector3(v.x, 0f, v.z); }
}
