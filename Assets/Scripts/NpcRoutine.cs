using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// OVERNIGHT R2 (2026-11-09): day/night routine for villagers.
/// Day: wander between waypoints near home (slow walk, pauses, idle
/// breathing). Night: walk home and stay there until dawn. Movement uses
/// VillagePathfinder for trips (never through walls) with a direct-steer
/// fallback for short hops; facing follows movement, and an idle
/// villager turns to face a nearby player. Includes the PilotNpcIdle
/// breathing so rigged-but-clipless NPCs (Pujari: rigged, 0 clips) still
/// look alive instead of frozen. NOTE: villagers walk without a walk
/// clip (their .glb files carry only an idle clip) — the slow pace +
/// small bob reads as a shuffle; a real walk-cycle remap for NPCs is
/// out of scope for tonight.
/// </summary>
public class NpcRoutine : MonoBehaviour
{
    public Vector3 home;
    public Vector3[] waypoints; // day wander spots (world XZ, y ignored)
    public float walkSpeed = 0.8f;
    public bool idleOnly = false; // elder (Mukhiya): never walks, just breathes + faces the player
    public bool stayHomeAtNight = true;

    private int wpIndex;
    private float pauseT = 2f;
    private bool walking;
    private Vector3 baseScale;
    private readonly List<Vector3> path = new List<Vector3>();
    private int pathIdx;
    private Vector3 pathTarget;
    private float repathT;
    private DayNightCycle cycle;
    private Transform player;
    private float stuckT;
    private Vector3 lastPos;

    void Start()
    {
        baseScale = transform.localScale;
        if (home == Vector3.zero) home = transform.position;
        cycle = Object.FindFirstObjectByType<DayNightCycle>();
        GameObject p = GameObject.Find("Player");
        if (p != null) player = p.transform;
        lastPos = transform.position;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        bool night = stayHomeAtNight && cycle != null && cycle.IsNight;

        // Breathing (always — even while standing at home).
        float breathe = 1f + 0.008f * Mathf.Sin(Time.time * 1.57f);
        transform.localScale = new Vector3(baseScale.x, baseScale.y * breathe, baseScale.z);

        if (idleOnly)
        {
            FacePlayerIfNear(dt);
            return;
        }

        if (night)
        {
            // Go home and stay there.
            if (Vector3.Distance(Flat(transform.position), Flat(home)) > 0.6f)
            {
                WalkTo(home, dt);
            }
            else
            {
                walking = false;
                FacePlayerIfNear(dt);
            }
            return;
        }

        if (walking)
        {
            WalkTo(pathTarget, dt);
            if (Vector3.Distance(Flat(transform.position), Flat(pathTarget)) < 0.5f)
            {
                walking = false;
                pauseT = Random.Range(2f, 5f);
            }
            return;
        }

        pauseT -= dt;
        FacePlayerIfNear(dt);
        if (pauseT > 0f || waypoints == null || waypoints.Length == 0) return;
        wpIndex = (wpIndex + 1) % waypoints.Length;
        pathTarget = waypoints[wpIndex];
        path.Clear();
        pathIdx = 0;
        repathT = 0f;
        walking = true;
    }

    private void WalkTo(Vector3 target, float dt)
    {
        repathT -= dt;
        if (path.Count == 0 || Vector3.Distance(Flat(pathTarget), Flat(target)) > 1.5f || repathT <= 0f)
        {
            repathT = 3f;
            pathTarget = target;
            pathIdx = 0;
            if (!VillagePathfinder.FindPath(transform.position, target, 0.4f, path))
            {
                // No path (should not happen for open wander spots) —
                // direct steer with collision slide instead.
                path.Clear();
                DirectSteer(target, dt);
                return;
            }
        }
        Vector3 dest = path[Mathf.Min(pathIdx, path.Count - 1)];
        while (pathIdx < path.Count - 1 && Vector3.Distance(Flat(transform.position), Flat(path[pathIdx])) < 0.35f)
        {
            pathIdx++;
            dest = path[Mathf.Min(pathIdx, path.Count - 1)];
        }
        StepToward(dest, dt);
    }

    private void DirectSteer(Vector3 target, float dt)
    {
        Vector3 step = (Flat(target) - Flat(transform.position)).normalized * walkSpeed * dt;
        Vector3 p = transform.position;
        TryMove(ref p, step);
        transform.position = p;
        FaceDir(Flat(target) - Flat(transform.position), dt);
        Bob();
    }

    private void StepToward(Vector3 dest, float dt)
    {
        Vector3 from = Flat(transform.position);
        Vector3 to = Flat(dest);
        Vector3 dir = to - from;
        if (dir.sqrMagnitude < 0.0004f) return;
        Vector3 step = dir.normalized * walkSpeed * dt;
        Vector3 p = transform.position;
        TryMove(ref p, step);
        // Stuck (wall in the way and the path says otherwise)? Give up
        // this trip and pick a new waypoint next pause.
        if (Vector3.Distance(Flat(p), Flat(lastPos)) < 0.001f && step.sqrMagnitude > 0.0001f)
        {
            stuckT += dt;
            if (stuckT > 1.5f)
            {
                walking = false;
                pauseT = Random.Range(2f, 4f);
                stuckT = 0f;
                return;
            }
        }
        else stuckT = 0f;
        lastPos = Flat(p);
        transform.position = p;
        FaceDir(dir, dt);
        Bob();
    }

    private void Bob()
    {
        Vector3 p = transform.position;
        p.y = Mathf.Abs(Mathf.Sin(Time.time * 5f)) * 0.02f;
        transform.position = p;
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

    private void FaceDir(Vector3 dir, float dt)
    {
        if (dir.sqrMagnitude < 0.001f) return;
        float targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        float curYaw = transform.eulerAngles.y;
        transform.rotation = Quaternion.Euler(0f, Mathf.MoveTowardsAngle(curYaw, targetYaw, 360f * dt), 0f);
    }

    private void FacePlayerIfNear(float dt)
    {
        if (player == null) return;
        Vector3 toPlayer = Flat(player.position) - Flat(transform.position);
        if (toPlayer.sqrMagnitude < 3.5f * 3.5f) FaceDir(toPlayer, dt);
    }

    private static Vector3 Flat(Vector3 v) { return new Vector3(v.x, 0f, v.z); }
}
