using UnityEngine;

/// <summary>
/// Night monster sequence (Phase 1, GDD Chapter 2: first night).
/// At nightfall the monster walks out of the jungle to Nilesh's door,
/// knocks three times and calls in a mimicked familiar voice
/// (on-screen subtitles — no voice audio files in Phase 1).
/// - Door LOCKED: monster gives up, walks away, dawn = survived.
/// - Door open / unlocked: monster forces it open and enters = game over.
/// - Player outside the house while the monster is out: it chases.
/// </summary>
public class MonsterKnock : MonoBehaviour
{
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

    private static readonly string[] kKnockLines = new string[]
    {
        "Awaaz: Nilesh... beta... darwaza kholo... main hoon...",
        "Awaaz: Nilesh, bahar aao... tumhari maa bula rahi hai...",
        "Awaaz: KHOLO! Main andar aa raha hoon..."
    };

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

        if (stage == 1)
        {
            // Wait in the dark, then appear at the jungle edge.
            if (t > 6f)
            {
                monster.gameObject.SetActive(true);
                monster.position = new Vector3(-2f, 0f, -19f);
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
            // If the player left the house, the monster chases instead.
            if (player != null && Vector3.Distance(Flat(player.position), Flat(playerHouseCenter)) > 7f)
            {
                chaseMode = true;
            }
            if (chaseMode)
            {
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
                door.ForceOpen();
                if (camFollow != null)
                {
                    camFollow.AddShake(1.5f);
                }
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.GameOver("Darwaza khula tha... wo andar aa gaya.");
                }
                stage = 0;
                MonsterOutside = false;
            }
        }
        else if (stage == 5)
        {
            // Walk back into the dark jungle and vanish.
            Vector3 target = new Vector3(-2f, 0f, -21f);
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

    private void WalkToward(Vector3 target, float speed)
    {
        Vector3 from = Flat(monster.position);
        Vector3 to = Flat(target);
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
