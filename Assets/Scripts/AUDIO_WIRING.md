# AUDIO WIRING — patch file for the code-fixes worker

Apply AFTER the code-fixes worker has finished editing the files below
(do NOT edit the same files at the same time as that worker).

## What is already done (do NOT redo)

- `Assets/Scripts/AudioManager.cs` (new file) — self-contained manager.
  It creates itself at runtime (`RuntimeInitializeOnLoadMethod`), creates
  the game's ONLY `AudioListener`, and follows `Camera.main` with it.
  **No scene edit is needed and NO AudioListener should be added to the
  Main Camera** — the runtime listener is the only one.
- `Assets/StreamingAssets/Audio/` — 14 `.ogg` files (0.80 MB total).
  Clips load at runtime from this folder; no Inspector references needed.
  To swap a sound, replace the `.ogg` file — no code change needed.

## Public API (all static, all null-safe — a missing clip is a silent no-op)

| Method | Sound |
|---|---|
| `AudioManager.PlayKnock()` / `PlayKnock(Vector3 pos)` | 1 wood knock (pitch varies per call) |
| `AudioManager.PlayDoorCreak()` / `PlayDoorCreak(Vector3 pos)` | long door creak |
| `AudioManager.PlayDoorOpen()` / `PlayDoorOpen(Vector3 pos)` | door swings open |
| `AudioManager.PlayDoorClose()` / `PlayDoorClose(Vector3 pos)` | door swings shut |
| `AudioManager.PlayLockClunk()` | lock / barricade clunk |
| `AudioManager.PlayFootstep()` / `PlayFootstep(Vector3 pos)` | 1 footstep (alternates 2 clips) |
| `AudioManager.PlayPickup()` | pickup chime |
| `AudioManager.PlayUIClick()` | UI button click |
| `AudioManager.PlayGameOverSting()` | game-over sting + monster roar (also stops night ambience) |
| `AudioManager.PlayDawnRelief()` | dawn birds cue |
| `AudioManager.StartNightAmbience()` | fade in wind + crickets loops |
| `AudioManager.StopNightAmbience()` | fade out wind + crickets loops |
| `AudioManager.PlayWhisperLine(int index)` | whisper line 0–2 — **currently a safe no-op, see §8** |

---

## 1. Monster knock — `Assets/Scripts/MonsterKnock.cs`

**Find** (method `Knock`, ~line 160):

```csharp
    private void Knock(int index)
    {
        if (door != null)
        {
            door.Shake(0.8f);
        }
```

**Insert** as the first line inside `Knock(int index)` (before `door.Shake`):

```csharp
        AudioManager.PlayKnock(door != null ? door.transform.position : transform.position);
        AudioManager.PlayWhisperLine(index); // no-op until user records whisper_1/2/3.ogg (§8)
```

(The subtitle line a few lines below, `GameManager.Instance.ShowSubtitle(kKnockLines[index], 3f);`,
stays as-is — it is the visible text of the whisper until the recording exists.)

**Also, in the same file, stage 4 resolve (~line 118–140):**

Find:

```csharp
                door.ForceOpen();
```

Insert directly BEFORE it:

```csharp
                AudioManager.PlayDoorCreak(door.transform.position);
```

Find (same block):

```csharp
                    GameManager.Instance.GameOver("Darwaza khula tha... wo andar aa gaya.");
```

Insert directly AFTER it:

```csharp
                    AudioManager.PlayGameOverSting();
```

## 2. Night / dawn — `Assets/Scripts/VillageBuilder.cs`

**Find** (~lines 1665–1666):

```csharp
        cycle.OnNightStart = delegate { knock.BeginNight(); };
        cycle.OnDawn = delegate { gm.Survived(); };
```

**Replace with:**

```csharp
        cycle.OnNightStart = delegate { knock.BeginNight(); AudioManager.StartNightAmbience(); };
        cycle.OnDawn = delegate { gm.Survived(); AudioManager.StopNightAmbience(); AudioManager.PlayDawnRelief(); };
```

(Do NOT add a second `PlayDawnRelief()` in `GameManager.Survived()` — `OnDawn`
already covers it.)

## 3. Door open / close / lock — `Assets/Scripts/DoorController.cs`

**Find** (method `SetOpen`, ~line 60):

```csharp
    public void SetOpen(bool open)
    {
        IsOpen = open;
        targetYaw = open ? openYaw : closedYaw;
```

**Insert** after the `targetYaw = ...` line:

```csharp
        if (open) AudioManager.PlayDoorOpen(transform.position);
        else AudioManager.PlayDoorClose(transform.position);
```

**Find** (method `SetLocked`, ~line 75):

```csharp
    public void SetLocked(bool locked)
    {
        IsLocked = locked;
```

**Insert** after the `IsLocked = locked;` line:

```csharp
        AudioManager.PlayLockClunk();
```

**Optional — locked-door rattle:** in `ToggleDoor()` (~line 50), inside the
`if (IsLocked)` block, after the `ShowSubtitle("Darwaza band hai...")` line,
add:

```csharp
            AudioManager.PlayLockClunk();
```

## 4. Pickup — `Assets/Scripts/GameManager.cs`

**Find** (method `OnPickupCollected`, ~line 59):

```csharp
    public void OnPickupCollected(string itemName)
    {
        foodCollected++;
```

**Insert** after the `foodCollected++;` line:

```csharp
        AudioManager.PlayPickup();
```

(No change needed in `Pickup.cs` — it already calls `OnPickupCollected`.)

## 5. UI clicks — `Assets/Scripts/MobileControls.cs`

**Find** the button listeners (~lines 285, 315, 319, 327, 331) and add
`AudioManager.PlayUIClick();` as the first statement inside each delegate:

```csharp
        restartButton.onClick.AddListener(delegate { AudioManager.PlayUIClick(); ... });
        actionBtn.onClick.AddListener(delegate { AudioManager.PlayUIClick(); if (player != null) player.Interact(); });
        jumpBtn.onClick.AddListener(delegate { AudioManager.PlayUIClick(); if (player != null) player.Jump(); });
        torchButton.onClick.AddListener(delegate { AudioManager.PlayUIClick(); if (torch != null) torch.Toggle(); });
        lockButton.onClick.AddListener(delegate { AudioManager.PlayUIClick(); if (gameManager != null) gameManager.ToggleLock(); });
```

(Keep the existing statements — only prepend the `PlayUIClick()` call.)

## 6. Footsteps — `Assets/Scripts/CharacterWalk.cs`

`CharacterWalk` already tracks a walk `phase` that advances once per step
(one full sine cycle = two steps). **Find** in `Update()` (~line 38):

```csharp
        phase += dt * (4f + speedSmooth * 1.6f) * (k > 0.02f ? 1f : 0f);
```

**Insert** directly AFTER it:

```csharp
        // One footstep per half-cycle (each leg strike).
        int stepIndex = Mathf.FloorToInt(phase / Mathf.PI);
        if (k > 0.02f && stepIndex != m_lastStepIndex)
        {
            m_lastStepIndex = stepIndex;
            AudioManager.PlayFootstep(transform.position);
        }
```

**And add the field** next to the other private fields (~line 17, after
`private float speedSmooth;`):

```csharp
    private int m_lastStepIndex;
```

## 7. Game over (backup) — `Assets/Scripts/GameManager.cs`

`PlayGameOverSting()` is already wired in `MonsterKnock.cs` (§1), which is
the only `GameOver()` caller in Phase 1. If a second `GameOver()` caller is
added later, add `AudioManager.PlayGameOverSting();` at the top of
`GameManager.GameOver(string reason)` (~line 107) instead — but do NOT add
it in both places or the sting will play twice.

## 8. PENDING USER STEP — Hindi whisper lines (do NOT fake)

The monster's 3 mimicked-voice lines (the `kKnockLines` in
`MonsterKnock.cs`, ~line 29) must be recorded **by the user on his phone**:
whisper, pitch slightly lowered, one line per file:

- `whisper_1.ogg` → line 0
- `whisper_2.ogg` → line 1
- `whisper_3.ogg` → line 2

Drop the 3 files into `Assets/StreamingAssets/Audio/` — no code change
needed; `AudioManager.PlayWhisperLine(index)` (already wired in §1) will
start playing them automatically. Until then it is a deliberate no-op and
the subtitle text carries the line. **Do not synthesize these lines with
TTS or AI voice — the user must record them himself.**

## 9. Verification checklist (after wiring)

1. Play in Editor: at nightfall, wind + crickets fade in; at dawn they
   fade out and birds play once.
2. Monster knock: 3 knocks, each with a slightly different pitch, coming
   from the door's direction (positional 3D).
3. Door open / close / lock each play their sound; locked-door attempt
   rattles the lock.
4. Walking plays alternating footsteps; ACTION / LOCK / TORCH buttons click;
   pickup chimes.
5. Game over plays the heartbeat sting + roar and the ambience stops.
6. Console shows no `AudioManager` warnings — a warning names the exact
   missing `.ogg` file if one failed to load.
