using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Phase 1 game state for THE FORGOTTEN VILLAGE.
/// Singleton, wired by VillageBuilder. Handles objectives, the food
/// pickup, door interaction (ACTION near the door / pickup), the LOCK
/// button, game-over and survived-the-night states.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public MobileControls ui;
    public DoorController playerDoor;
    public PlayerController player;
    public DayNightCycle dayNight;
    public MonsterKnock monster;
    public TorchController torch;
    public Pickup foodPickup;
    public Pickup waterPickup;
    public Pickup medicinePickup;
    public Pickup batteryPickup;
    public int totalPickups = 1;
    // Fix-push P2-15: NPC spots (set by VillageBuilder in SetupSystems).
    public Vector3 npcMukhiyaPos;
    public Vector3 npcMeeraPos;
    public Vector3 npcPujariPos;

    // Fix-push P1-9: the player's house (hut at (-9, 6) in VillageBuilder,
    // normalised to ~6.5 m wide). Dawn only counts as survived if the
    // player is inside these bounds.
    // ROUND 4 DOOR: the half extents come from the ACTUAL hut footprint
    // (VillageBuilder sets them in SetupSystems from the measured world
    // bounds, ~±4.65/±4.15). The old hardcoded ±3.5/±3.0 was smaller than
    // the hut — a player standing inside near the walls counted as
    // outside at dawn. Default = the old values until SetupSystems runs.
    public static readonly Vector3 PlayerHouseCenter = new Vector3(-9f, 0f, 6f);
    public static Vector3 PlayerHouseHalfExtents = new Vector3(3.5f, 0f, 3f);
    public const string MukhiyaWarning = "Raat me koi bhi darwaza khatkhataye to kholna mat, awaaz chahe kisi apne ki hi kyun na ho!";

    public static bool IsInsidePlayerHouse(Vector3 p)
    {
        return Mathf.Abs(p.x - PlayerHouseCenter.x) <= PlayerHouseHalfExtents.x
            && Mathf.Abs(p.z - PlayerHouseCenter.z) <= PlayerHouseHalfExtents.z;
    }

    private int foodCollected;
    private bool isOver;

    public bool IsOver
    {
        get { return isOver; }
    }

    void Awake()
    {
        Instance = this;
    }

    public void ShowSubtitle(string text, float duration)
    {
        if (ui != null)
        {
            ui.ShowSubtitle(text, duration);
        }
    }

    public void SetObjective(string text)
    {
        if (ui != null)
        {
            ui.SetObjective(text);
        }
    }

    public void OnTorchToggled(bool on)
    {
        if (ui != null)
        {
            ui.SetTorchState(on);
        }
    }

    public void OnPickupCollected(string itemName)
    {
        foodCollected++;
        AudioManager.PlayPickup();
        ShowSubtitle(itemName + " mil gaya! (" + foodCollected + "/" + totalPickups + ")", 2.5f);
        if (itemName == "Khana")
        {
            SetObjective("Khana mil gaya! Raat hone se pehle ghar ke andar jao.");
        }
        else
        {
            SetObjective("Saman mil gaya! Raat hone se pehle ghar ke andar jao.");
        }
    }

    // ACTION button / E key: nearest interactable within range wins.
    // ROUND 4 DOOR: the door wins when the player is right at it
    // (< 1.5 m XZ) — before pickups/NPCs, so pressing ACTION to close
    // the door from inside no longer collects the Dawai crate instead,
    // and a nearby NPC can no longer steal the press at the door itself.
    // From further out the door still works, at 4.0 m XZ to the door
    // centre (was: 3.0 m 3D distance to the hinge — the hinge sits at
    // the door's edge and the Y difference shrank the usable range).
    public void TryInteract()
    {
        if (isOver || player == null) return;

        Vector3 p = player.transform.position;

        if (playerDoor != null && DistXZ(p, playerDoor.DoorCenter) < 1.5f)
        {
            playerDoor.ToggleDoor();
            return;
        }

        // Fix-push P2-18b: any pickup works the same way as the food crate.
        if (TryCollectPickup(foodPickup)) return;
        if (TryCollectPickup(waterPickup)) return;
        if (TryCollectPickup(medicinePickup)) return;
        if (TryCollectPickup(batteryPickup)) return;

        // Fix-push P2-15: one line each from the NPCs (existing subtitles).
        if (npcMukhiyaPos.sqrMagnitude > 0.01f &&
            Vector3.Distance(p, npcMukhiyaPos) < 3f)
        {
            ShowSubtitle(MukhiyaWarning, 4f);
            return;
        }
        if (npcMeeraPos.sqrMagnitude > 0.01f &&
            Vector3.Distance(p, npcMeeraPos) < 3f)
        {
            ShowSubtitle("Meera: Mandir ke peeche se raat ko awaazein aati hain... pujari bhi wahan jaane se darta hai.", 4f);
            return;
        }
        if (npcPujariPos.sqrMagnitude > 0.01f &&
            Vector3.Distance(p, npcPujariPos) < 3f)
        {
            ShowSubtitle("Pujari: Mandir ka shivling gayab hai... jab se wo gaya, gaon par chhaya hai.", 4f);
            return;
        }

        if (playerDoor != null &&
            DistXZ(p, playerDoor.DoorCenter) < 4.0f)
        {
            playerDoor.ToggleDoor();
            return;
        }

        ShowSubtitle("Paas me kuch nahi hai — kuaan ya darwaze ke paas jao.", 2f);
    }

    // XZ-only distance (ignore height — the door centre sits at y=1.05
    // and the player at y=0.5, so a 3D check shrank the range).
    private static float DistXZ(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private bool TryCollectPickup(Pickup p)
    {
        if (p != null && !p.Collected &&
            Vector3.Distance(player.transform.position, p.transform.position) < 2.8f)
        {
            p.Collect();
            return true;
        }
        return false;
    }

    // LOCK button: locks/unlocks Nilesh's door when the player is near it.
    public void ToggleLock()
    {
        if (isOver || player == null || playerDoor == null) return;

        if (Vector3.Distance(player.transform.position, playerDoor.transform.position) > 3.5f)
        {
            ShowSubtitle("LOCK karne ke liye darwaze ke paas jao.", 2f);
            return;
        }
        playerDoor.SetLocked(!playerDoor.IsLocked);
        if (ui != null)
        {
            ui.SetLockState(playerDoor.IsLocked);
        }
    }

    public void GameOver(string reason)
    {
        if (isOver) return;
        isOver = true;
        // AUDIT FIX 2026-11-08: the sting (sting + roar + stop ambience)
        // belongs here so ALL THREE death paths play it — door forced open,
        // chase catch, and dawn-outside. Previously it was called only in
        // MonsterKnock stage 4, so 2 of 3 deaths were silent.
        AudioManager.PlayGameOverSting();
        if (player != null)
        {
            player.enabled = false;
        }
        if (ui != null)
        {
            ui.ShowCenter("GAME OVER\n" + reason, true);
            ui.ShowRestartButton(true);
        }
    }

    public void Survived()
    {
        if (isOver) return;
        // Fix-push P1-9: surviving the night only counts INSIDE the house.
        // Caught outside at dawn = the night takes you.
        if (player == null || !IsInsidePlayerHouse(player.transform.position))
        {
            GameOver("Subah hui, par tum ghar ke bahar the... wo tumhe le gaya.");
            return;
        }
        isOver = true;
        if (ui != null)
        {
            ui.ShowCenter("SUBAH HO GAYI\nTum raat bhar bach gaye!", false);
            ui.ShowRestartButton(true);
        }
        SetObjective("Phase 1 poora! Subah ho gayi — tum bach gaye.");
    }

    public void Restart()
    {
        SceneManager.LoadScene("Main");
    }
}
