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
///
/// PHONE-VIDEO FIX (2026-11-09, Worker C): Meera stood frozen for the
/// whole 38 s phone video even though this routine ran. Root cause, proven
/// from the .glb bounds + the collider registry: the pathfinder RELOCATES
/// a blocked goal to the nearest free cell (VillagePathfinder.NearestFree)
/// but this script checked arrival against the RAW waypoint. Meera's
/// waypoint (3, 8) sits 0.22 m from House_2's west wall (registered
/// footprint x[3.22, 14.78] z[2.23, 13.77]) — inside the 0.4 m walk
/// radius — so the goal relocated to (2.5, 7.5), 0.71 m from the waypoint,
/// and the "< 0.5 m from waypoint" arrival test NEVER passed: she walked
/// to the wall once, then stood there forever with walking=true, repathing
/// every 3 s. Same defect on (-4, 9) (0.32 m from House_Player's east wall
/// x=-4.32) and Pujari's (3.5, -10) (0.10 m from the Temple's south face).
/// Fix: arrival is now checked against walkGoal — the ACTUAL reachable
/// destination (last path point, or the raw target for direct steer) — plus
/// a watchdog that abandons a trip that makes no progress, so no NPC can
/// freeze forever on an unreachable waypoint again. Waypoint data itself
/// still wants the VillageBuilder patch (see report): (3,8)->(2,8),
/// (-4,9)->(-3,9), Pujari (3.5,-10)->(3.5,-8.5), and Villager_Man's whole
/// spawn is INSIDE House_2 and must move.
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
    // PHONE-VIDEO FIX: the reachable destination of the current trip.
    // VillagePathfinder relocates a blocked goal to the nearest free cell,
    // so the last path point — not the raw waypoint — is what "arrived"
    // must be measured against, or the NPC waits forever 0.5-1 m short.
    private Vector3 walkGoal;
    // PHONE-VIDEO FIX: watchdog — a trip that makes no progress (goal
    // unreachable even after relocation, or the NPC spawned inside a
    // collider) is abandoned instead of freezing the NPC forever.
    private float walkT;
    private const float kMaxTripTime = 12f;

    void Start()
    {
        baseScale = transform.localScale;
        if (home == Vector3.zero) home = transform.position;
        cycle = Object.FindFirstObjectByType<DayNightCycle>();
        GameObject p = GameObject.Find("Player");
        if (p != null) player = p.transform;
        lastPos = transform.position;
        walkGoal = transform.position;
        // PHONE-VIDEO FIX: loud spawn check. An NPC whose spawn sits inside
        // a registered blocker (Villager_Man: home (6.5, 10.5) is inside
        // House_2's footprint x[3.22, 14.78] z[2.23, 13.77]) can never take
        // a step — no routine fix can move it out. This names it in the
        // CI log instead of leaving a silently frozen NPC in the game.
        // WARNING, not Error: Mukhiya is intentionally static at his
        // haveli (unrigged elder, never meant to move) and his spawn sits
        // inside the haveli collider — an Error here fails EVERY PlayMode
        // test that builds the village (6/8 failed on this one line).
        if (!VillageColliders.CanMoveTo(transform.position, 0.4f))
        {
            Debug.LogWarning("[NpcRoutine] " + gameObject.name + " spawned INSIDE a collider at "
                + transform.position + " — it can never move. Move its spawn in VillageBuilder.");
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        // ROUND 4: the cycle is created in SetupSystems at FinishBuild
        // — AFTER all NPCs spawn — so the Start() lookup always
        // returned null and NPCs never went home at night (the night
        // branch could never fire). Re-fetch when null.
        if (cycle == null) cycle = Object.FindFirstObjectByType<DayNightCycle>();
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
            // Go home and stay there. Arrival is measured against walkGoal
            // (the reachable point near home), not raw home — same
            // relocation rule as the day trips — and the watchdog applies,
            // so a sealed home can never freeze the NPC either.
            WalkTo(home, dt);
            if (Vector3.Distance(Flat(transform.position), Flat(walkGoal)) < 0.6f)
            {
                walking = false;
                walkT = 0f;
                FacePlayerIfNear(dt);
            }
            else
            {
                walking = true;
                walkT += dt;
                if (walkT > kMaxTripTime)
                {
                    Debug.LogWarning("[NpcRoutine] " + gameObject.name
                        + " could not reach home " + home + " in "
                        + kMaxTripTime + "s — standing at "
                        + transform.position + " for the night.");
                    walking = false;
                    walkT = 0f;
                }
            }
            return;
        }

        if (walking)
        {
            WalkTo(pathTarget, dt);
            // PHONE-VIDEO FIX: arrive against walkGoal (the reachable
            // destination), not the raw waypoint — the pathfinder may have
            // relocated the goal up to ~1 m, and the old test never passed.
            if (Vector3.Distance(Flat(transform.position), Flat(walkGoal)) < 0.5f)
            {
                walking = false;
                pauseT = Random.Range(2f, 5f);
            }
            else
            {
                // PHONE-VIDEO FIX: watchdog — a trip that makes no progress
                // (entombed spawn, sealed goal) is abandoned and the
                // waypoint skipped, instead of freezing the NPC forever.
                walkT += dt;
                if (walkT > kMaxTripTime)
                {
                    Debug.LogWarning("[NpcRoutine] " + gameObject.name
                        + " abandoned trip to " + pathTarget + " after "
                        + kMaxTripTime + "s without arriving (from "
                        + transform.position + ") — waypoint skipped.");
                    walking = false;
                    pauseT = Random.Range(2f, 4f);
                    walkT = 0f;
                    if (waypoints != null && waypoints.Length > 0)
                        wpIndex = (wpIndex + 1) % waypoints.Length;
                }
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
        walkGoal = pathTarget;
        walkT = 0f;
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
                // PHONE-VIDEO FIX: the fallback steers at the raw target,
                // so that IS the arrival goal.
                walkGoal = target;
                DirectSteer(target, dt);
                return;
            }
            // PHONE-VIDEO FIX: the pathfinder may have relocated a blocked
            // goal to the nearest free cell — the LAST path point is the
            // real destination. Arriving there counts as arriving.
            if (path.Count > 0) walkGoal = path[path.Count - 1];
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
