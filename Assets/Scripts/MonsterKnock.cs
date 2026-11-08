using UnityEngine;

/// <summary>
/// Night monster sequence (Phase 1, GDD Chapter 2: first night).
/// At nightfall the monster walks out of the jungle to Nilesh's door,
/// knocks three times and calls in a mimicked familiar voice
/// (on-screen subtitles — no voice audio files in Phase 1).
/// - Door LOCKED: monster gives up, walks away, dawn = survived.
/// - Door open / unlocked: monster forces it open and enters = game over.
/// - Player outside the house while the monster is out: it chases.
/// OVERNIGHT R3c — LAIR MOVED: the monster used to appear at (-2,-19),
/// the jungle edge south of the Temple. That spot is INSIDE the Temple's
/// FullBounds collider, and the south is sealed by overlapping building
/// footprints (School / House_3 / Temple / House_6) — the R2+R3b CI tests
/// proved NO walkable path exists from (-2,-19) to the door even on a
/// 0.5 m A* grid, so the monster could only ever reach the door by
/// clipping through walls. The lair is now (-3.5,-10.5), the open temple
/// approach north of the seal: still south, still dark, and the walk to
/// the door runs down the open lane where the player can see it coming.
/// (The seal itself is a layout defect for a future pass — see
/// VillagePathfinder.LogMap — not something a spawn point should hide.)
/// </summary>
public class MonsterKnock : MonoBehaviour
{
    // The monster's lair (spawn + retreat point). See the class comment.
    public static readonly Vector3 kLair = new Vector3(-3.5f, 0f, -10.5f);
    public Transform monster;
    public DoorController door;
    public Transform doorFront;
    public Transform player;
    public CameraFollow camFollow;
    public Vector3 playerHouseCenter = new Vector3(-9f, 0f, 6f);

    public bool MonsterOutside { get; private set; }

    private int stage; // 0 idle, 1 wait, 2 walk to door, 3 knocking, 4 resolve, 5 leave
    private float t;
    private int knocksDone;
    private float nextKnockAt;
    private bool chaseMode;

    // OVERNIGHT R2: A* path following (VillagePathfinder) + senses.
    // The old WalkToward was straight-line MoveTowards — the monster
    // walked through walls, houses and trees (audit 4.6b / 6.5).
    private System.Collections.Generic.List<Vector3> path =
        new System.Collections.Generic.List<Vector3>();
    private int pathIdx;
    private Vector3 pathTarget;
    private float repathT;
    private float loseSightT;

    private static readonly string[] kKnockLines = new string[]
    {
        "Awaaz: Nilesh... beta... darwaza kholo... main hoon...",
        "Awaaz: Nilesh, bahar aao... tumhari maa bula rahi hai...",
        "Awaaz: KHOLO! Main andar aa raha hoon..."
    };

    private bool PlayerOutsideHouse()
    {
        return player != null && !GameManager.IsInsidePlayerHouse(player.position);
    }

    public void BeginNight()
    {
        if (monster == null || door == null) return;
        stage = 1;
        t = 0f;
        knocksDone = 0;
        chaseMode = false;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ShowSubtitle("Raat ho gayi... darwaza LOCK karo!", 3.5f);
            GameManager.Instance.SetObjective("Raat: Ghar ke andar raho. Darwaza LOCK rakho!");
        }
    }

    void Update()
    {
        if (stage == 0 || monster == null) return;
        t += Time.deltaTime;

        // Fix-push P2-18a: the monster hunts from night start, not only at
        // knock stage 3 — a player outside the house at night is chased
        // while it is still waiting (stage 1) or walking to the door (2).
        // OVERNIGHT R2: ...but only when it can SENSE the player (see /
        // hear / very close — CanSensePlayer). The old rule chased any
        // outside player instantly, even 25 m away behind a house. An
        // unsensed player is left alone: the monster walks to the door
        // and knocks, as the story demands.
        if ((stage == 1 || stage == 2) && PlayerOutsideHouse() && CanSensePlayer())
        {
            if (stage == 1)
            {
                monster.gameObject.SetActive(true);
                monster.position = kLair;
            }
            chaseMode = true;
            MonsterOutside = true;
            stage = 3;
            t = 0f;
            knocksDone = 0;
            nextKnockAt = 1.2f;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ShowSubtitle("Wo tumhe dekh raha hai... bhaago!", 3f);
            }
        }

        if (stage == 1)
        {
            // Wait in the dark, then appear at the jungle edge.
            if (t > 6f)
            {
                monster.gameObject.SetActive(true);
                monster.position = kLair;
                stage = 2;
                t = 0f;
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.ShowSubtitle("...jungle se kuch aa raha hai...", 3f);
                }
            }
        }
        else if (stage == 2)
        {
            // Walk to the door front.
            Vector3 target = doorFront.position;
            WalkToward(target, 1.6f);
            if (Vector3.Distance(Flat(monster.position), Flat(target)) < 0.4f)
            {
                stage = 3;
                t = 0f;
                knocksDone = 0;
                nextKnockAt = 1.2f;
                MonsterOutside = true;
            }
        }
        else if (stage == 3)
        {
            // If the player left the house, the monster chases instead —
            // OVERNIGHT R2: only when it can sense the player (see/hear).
            // A player standing silently far away in the dark is NOT
            // chased mid-knock; the knocks continue.
            if (PlayerOutsideHouse() && CanSensePlayer())
            {
                chaseMode = true;
            }
            if (chaseMode)
            {
                // Fix-push P2-18a: stop chasing a player who got back
                // inside and locked the door — the monster gives up.
                if (door != null && door.IsLocked && !PlayerOutsideHouse())
                {
                    chaseMode = false;
                    MonsterOutside = false;
                    stage = 5;
                    t = 0f;
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.ShowSubtitle("Awaaz: ...main phir aaunga, Nilesh...", 3f);
                    }
                    return;
                }
                // OVERNIGHT R2: lose the trail — if it can neither see
                // nor hear the player for 4 s and the player is far, it
                // gives up the chase and returns to the door to knock.
                if (CanSensePlayer())
                {
                    loseSightT = 0f;
                }
                else
                {
                    loseSightT += Time.deltaTime;
                    if (loseSightT > 4f && Vector3.Distance(Flat(monster.position), Flat(player.position)) > 15f)
                    {
                        chaseMode = false;
                        stage = 2;
                        t = 0f;
                        loseSightT = 0f;
                        if (GameManager.Instance != null)
                        {
                            GameManager.Instance.ShowSubtitle("...wo tumhe kho baitha. Darwaza band rakho!", 3f);
                        }
                        return;
                    }
                }
                WalkToward(player.position, 3.4f);
                if (Vector3.Distance(Flat(monster.position), Flat(player.position)) < 1.6f)
                {
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.GameOver("Usne tumhe pakad liya...");
                    }
                    stage = 0;
                    MonsterOutside = false;
                }
                return;
            }

            // Knock rounds.
            if (knocksDone < 3 && t >= nextKnockAt)
            {
                Knock(knocksDone);
                knocksDone++;
                nextKnockAt = t + 3.2f;
            }
            else if (knocksDone >= 3 && t >= nextKnockAt + 2.5f)
            {
                stage = 4;
                t = 0f;
            }
        }
        else if (stage == 4)
        {
            // Resolve: locked door = safe; anything else = it gets in.
            if (door.IsLocked)
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.ShowSubtitle("Awaaz: ...main phir aaunga, Nilesh...", 3f);
                }
                stage = 5;
                t = 0f;
                MonsterOutside = false;
            }
            else
            {
                AudioManager.PlayDoorCreak(door.transform.position);
                door.ForceOpen();
                if (camFollow != null)
                {
                    camFollow.AddShake(1.5f);
                }
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.GameOver("Darwaza khula tha... wo andar aa gaya.");
                    // AUDIT FIX 2026-11-08: sting removed here — it now
                    // plays inside GameManager.GameOver() for all deaths.
                }
                stage = 0;
                MonsterOutside = false;
            }
        }
        else if (stage == 5)
        {
            // Walk back into the dark jungle and vanish.
            Vector3 target = kLair;
            WalkToward(target, 1.8f);
            if (Vector3.Distance(Flat(monster.position), Flat(target)) < 0.6f)
            {
                monster.gameObject.SetActive(false);
                stage = 0;
            }
        }
    }

    private void Knock(int index)
    {
        AudioManager.PlayKnock(door != null ? door.transform.position : transform.position);
        AudioManager.PlayWhisperLine(index); // no-op until user records whisper_1/2/3.ogg
        if (door != null)
        {
            door.Shake(0.8f);
        }
        if (camFollow != null)
        {
            camFollow.AddShake(0.7f);
        }
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ShowSubtitle(kKnockLines[index], 3f);
        }
    }

    // OVERNIGHT R2: the monster's senses (audit 6.3 — chase used to
    // trigger on player-outside-at-night alone, with no vision or
    // hearing). Three ways to be noticed:
    //  - PROXIMITY (< 2.5 m): it feels you, even in the dark.
    //  - HEARING: running footsteps (player speed > 3 m/s) within 14 m.
    //  - SIGHT: within 20 m, inside a ~150-degree forward cone, with a
    //    clear line of sight (walls and houses block it — the same
    //    CanMoveTo walkability the pathfinder uses).
    private bool CanSensePlayer()
    {
        if (player == null || monster == null) return false;
        Vector3 toPlayer = Flat(player.position) - Flat(monster.position);
        float dist = toPlayer.magnitude;
        if (dist < 2.5f) return true;
        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null && pc.CurrentSpeed > 3.0f && dist < 14f) return true;
        if (dist < 20f)
        {
            float angle = Vector3.Angle(monster.forward, toPlayer);
            if (angle < 75f && VillagePathfinder.HasLineOfSight(
                    monster.position + Vector3.up * 2f, player.position + Vector3.up * 1.2f, 0.45f))
            {
                return true;
            }
        }
        return false;
    }

    private void WalkToward(Vector3 target, float speed)
    {
        // OVERNIGHT R2: A* path following (VillagePathfinder) instead of
        // straight-line MoveTowards — the old code walked through walls,
        // houses and trees. Replans every 3 s (or when the target moves),
        // so a running player is re-tracked. If no path exists, falls
        // back to the old straight line (never worse than before).
        repathT -= Time.deltaTime;
        if (path == null || Vector3.Distance(Flat(pathTarget), Flat(target)) > 1.5f || repathT <= 0f)
        {
            repathT = 3f;
            pathTarget = target;
            pathIdx = 0;
            if (path == null) path = new System.Collections.Generic.List<Vector3>();
            if (!VillagePathfinder.FindPath(monster.position, target, 0.45f, path))
            {
                path = null;
                Debug.LogWarning("[Monster] No path to " + target + " — straight-line fallback");
            }
        }
        Vector3 dest = target;
        if (path != null && path.Count > 0)
        {
            while (pathIdx < path.Count - 1 && Vector3.Distance(Flat(monster.position), Flat(path[pathIdx])) < 0.35f)
            {
                pathIdx++;
            }
            dest = path[Mathf.Min(pathIdx, path.Count - 1)];
        }
        Vector3 from = Flat(monster.position);
        Vector3 to = Flat(dest);
        Vector3 next = Vector3.MoveTowards(from, to, speed * Time.deltaTime);
        monster.position = new Vector3(next.x, monster.position.y, next.z);
        Vector3 dir = to - from;
        if (dir.sqrMagnitude > 0.001f)
        {
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            monster.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
        // Slight bob while walking.
        float bob = Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 0.08f;
        monster.position = new Vector3(monster.position.x, bob, monster.position.z);
    }

    private static Vector3 Flat(Vector3 v)
    {
        return new Vector3(v.x, 0f, v.z);
    }
}
