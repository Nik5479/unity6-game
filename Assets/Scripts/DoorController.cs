using System.Collections;
using UnityEngine;

/// <summary>
/// Working door for Nilesh's house (Phase 1).
/// Hinge pivot rotates the panel open/closed; lock (barricade) blocks
/// opening. The monster can force an unlocked door. While closed the
/// doorway is registered as a collision block; while open it is not.
/// </summary>
public class DoorController : MonoBehaviour
{
    public Transform hinge;
    public float closedYaw = 90f;
    public float openYaw = -10f;
    public Vector3 closedBlockCenter;
    public Vector3 closedBlockSize;

    public bool IsOpen { get; private set; }
    public bool IsLocked { get; private set; }

    // ROUND 4 DOOR: the dark doorway quad (VillageBuilder assigns it) —
    // shown only while the door is open, so an open door reveals
    // darkness instead of wall texture. Single-sided (faces east), so
    // it never blocks the view out from inside.
    public GameObject doorwayDark;

    // ROUND 4 DOOR: the world-space centre of the doorway (the closed
    // collision block's centre: hinge + half the door width, at door
    // mid-height). Interaction ranges measure to THIS point, XZ-only —
    // the hinge sits at the door's edge, so measuring to it shrank the
    // usable range and made the check depend on which edge the player
    // stood near.
    public Vector3 DoorCenter
    {
        get { return closedBlockCenter; }
    }

    private float targetYaw;
    private float currentYaw;
    private bool blockRegistered;

    void Awake()
    {
        if (hinge == null)
        {
            hinge = transform;
        }
        currentYaw = closedYaw;
        targetYaw = closedYaw;
        hinge.localRotation = Quaternion.Euler(0f, currentYaw, 0f);
    }

    void Update()
    {
        if (Mathf.Abs(currentYaw - targetYaw) > 0.01f)
        {
            currentYaw = Mathf.MoveTowards(currentYaw, targetYaw, 170f * Time.deltaTime);
            hinge.localRotation = Quaternion.Euler(0f, currentYaw, 0f);
        }
    }

    public void ToggleDoor()
    {
        if (IsLocked)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ShowSubtitle("Darwaza band hai — pehle LOCK kholo.", 2f);
            }
            AudioManager.PlayLockClunk();
            return;
        }
        SetOpen(!IsOpen);
    }

    public void SetOpen(bool open)
    {
        IsOpen = open;
        targetYaw = open ? openYaw : closedYaw;
        if (open) AudioManager.PlayDoorOpen(transform.position);
        else AudioManager.PlayDoorClose(transform.position);
        // ROUND 4 DOOR: the dark doorway quad shows only while open.
        if (doorwayDark != null) doorwayDark.SetActive(open);
        if (open)
        {
            UnregisterBlock();
        }
        else
        {
            RegisterBlock();
        }
    }

    public void SetLocked(bool locked)
    {
        IsLocked = locked;
        AudioManager.PlayLockClunk();
        if (locked && IsOpen)
        {
            SetOpen(false);
        }
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ShowSubtitle(locked ? "Darwaza LOCK kar diya. Ab koi andar nahi aa sakta." : "Darwaza UNLOCK kar diya.", 2f);
        }
    }

    // Monster forces an unlocked door open.
    public void ForceOpen()
    {
        IsLocked = false;
        SetOpen(true);
    }

    public void Shake(float duration)
    {
        StartCoroutine(ShakeRoutine(duration));
    }

    private IEnumerator ShakeRoutine(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float wobble = Mathf.Sin(elapsed * 42f) * 4f;
            hinge.localRotation = Quaternion.Euler(0f, currentYaw + wobble, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        hinge.localRotation = Quaternion.Euler(0f, currentYaw, 0f);
    }

    public void RegisterBlock()
    {
        if (blockRegistered) return;
        VillageColliders.Register(new Bounds(closedBlockCenter, closedBlockSize));
        blockRegistered = true;
    }

    public void UnregisterBlock()
    {
        if (!blockRegistered) return;
        VillageColliders.Unregister(new Bounds(closedBlockCenter, closedBlockSize));
        blockRegistered = false;
    }
}
