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
        ShowSubtitle(itemName + " mil gaya! (" + foodCollected + "/1)", 2.5f);
        SetObjective("Khana mil gaya! Raat hone se pehle ghar ke andar jao.");
    }

    // ACTION button / E key: nearest interactable within range wins.
    public void TryInteract()
    {
        if (isOver || player == null) return;

        Vector3 p = player.transform.position;

        if (foodPickup != null && !foodPickup.Collected &&
            Vector3.Distance(p, foodPickup.transform.position) < 2.8f)
        {
            foodPickup.Collect();
            return;
        }

        if (playerDoor != null &&
            Vector3.Distance(p, playerDoor.transform.position) < 3.0f)
        {
            playerDoor.ToggleDoor();
            return;
        }

        ShowSubtitle("Paas me kuch nahi hai — kuaan ya darwaze ke paas jao.", 2f);
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
