# Migration plan — feature-namespace skeleton

## Why nothing is moved in this change

Every existing script in `Assets/Scripts/` has a `.meta` file whose GUID
is referenced by scenes and prefabs. Moving or renaming a script without
its `.meta` breaks those references, and CI has failed on invalid GUIDs
before. So this change only *adds* the new `ForgottenVillage/` skeleton
alongside the flat scripts; it moves, renames, or edits nothing existing.

## Gradual move order (one script at a time)

1. Pick one script from the table below — lowest-risk first
   (leaf scripts with few references, e.g. `SafeAreaFitter.cs`).
2. Move the `.cs` **and** its `.meta` together, in the Unity editor
   (drag the file) or with `git mv` of both files. Never move one alone.
3. Add the target namespace (`ForgottenVillage.<Feature>`) to the moved
   file and fix any `using` / reference errors it causes.
4. Re-open the scene(s) in the Unity editor and verify all references
   resolve (no "Missing Script" components).
5. Run CI (build + tests). Only move the next script after CI is green.
6. Never move two scripts in one commit — a broken GUID is much easier
   to bisect one script at a time.

## Namespace adoption rule

- New code uses `ForgottenVillage.<Feature>` (e.g.
  `ForgottenVillage.Player`, `ForgottenVillage.AI`).
- Old code keeps the global namespace until the file is moved. Do not
  add namespaces to unmoved files — that alone can break scene
  references, since Unity resolves scripts by type name.
- Global-namespace code can still reference namespaced code (and vice
  versa via `using ForgottenVillage.<Feature>;`); the two coexist
  during the migration.

## Mapping: existing script → target folder

| Script | Target folder |
|---|---|
| `PlayerController.cs` | `Player/` |
| `NileshAnimator.cs` | `Player/` |
| `CharacterWalk.cs` | `Player/` |
| `CameraFollow.cs` | `Player/` |
| `TorchController.cs` | `Player/` |
| `MonsterKnock.cs` | `AI/` |
| `NpcRoutine.cs` | `AI/` |
| `PilotNpcIdle.cs` | `AI/` |
| `AnimalRoam.cs` | `AI/` |
| `VillagePathfinder.cs` | `AI/` |
| `VillageBuilder.cs` | `Village/` |
| `GltfModelLoader.cs` | `Village/` |
| `Pickup.cs` | `Village/` |
| `DoorController.cs` | `Home/` |
| `DayNightCycle.cs` | `Weather/` |
| `MobileControls.cs` | `UI/` |
| `VirtualJoystick.cs` | `UI/` |
| `ControlIcons.cs` | `UI/` |
| `SafeAreaFitter.cs` | `UI/` |
| `AudioManager.cs` | `Audio/` |
| `AUDIO_WIRING.md` | `Audio/` |
| `GameManager.cs` | *stays at root — no Core folder in this skeleton; do not force it into a feature folder* |
| *(nothing yet)* | `Minimap/` — new code only |
| *(nothing yet)* | `Save/` — new code only |

Note: five scripts have no `.meta` file in the repo listing (checked
2026-11-27): `AnimalRoam.cs`, `NpcRoutine.cs`, `PilotNpcIdle.cs`,
`SafeAreaFitter.cs`, `VillagePathfinder.cs`. Confirm on disk before
moving them — if Unity never generated one, let Unity generate it on
import instead of hand-writing a GUID.
