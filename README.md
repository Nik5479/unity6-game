# unity6-game

Unity 6 game project — **Unity 6000.3.28f1**, Android target, full-screen landscape.

> Game choice is NOT locked yet (realistic horror / ranch / Mahabharat Story RPG
> were discussed). This repo is a clean **base project + CI** with a simple
> playable starter scene, so the first green build can be proven before a
> full game is built on top of it.

## What is in here

- `Assets/Scenes/Main.unity` — starter scene: Main Camera, Directional Light,
  Ground (50 x 50), Player cube with `PlayerController`.
- `Assets/Scripts/PlayerController.cs` — WASD / arrow keys on keyboard,
  touch-drag on phone. Space = jump. Movement is clamped to the ground.
- `Assets/Tests/ScreenshotTest.cs` — PlayMode smoke test: loads Main, checks
  Player + Main Camera exist, captures `TestScreenshots/main-scene.png`.
- `ProjectSettings/` — Android target, min SDK 24, landscape
  (`defaultOrientation: LandscapeLeft`), package `com.nik.unity6game`,
  Built-in Render Pipeline (no URP yet — switch later if needed).
- `.github/workflows/build-android.yml` — GitHub Actions CI.

## Open locally (Unity Hub)

1. Install Unity **6000.3.28f1** (Unity Hub > Installs > Add, or `unityhub://6000.3.28f1/...`).
2. Add Android Build Support (Android SDK & NDK Tools + OpenJDK).
3. Clone this repo, open the folder in Unity Hub, press Open.
4. Open `Assets/Scenes/Main.unity`, press Play. WASD moves the cube.
5. First open will regenerate `Library/` and fill in any missing default
   ProjectSettings fields — that is expected for a skeleton project.

## CI — how it works

GitHub Actions (`.github/workflows/build-android.yml`, Unity `6000.3.28f1`):

1. **test** job — `game-ci/unity-test-runner@v4`, PlayMode tests.
   Uploads test results + `TestScreenshots/` as artifacts.
2. **build** job — `game-ci/unity-builder@v4`, `targetPlatform: Android`,
   `buildName: unity6-game`, uploads the APK as artifact `unity6-game-apk`.

Unity does NOT run on this machine — the build runs on GitHub's runner
with a ready Unity image, exactly as described in the project notes.

## Secrets — REQUIRED before the first build can go green

CI has never built yet because these repo secrets are missing. The repo
owner must add them in GitHub: repo > **Settings > Secrets and variables >
Actions > New repository secret**. Never paste them in chat.

| Secret | What it is | Where it comes from |
|---|---|---|
| `UNITY_LICENSE` | Contents of the Unity `.ulf` license file | Unity Hub > Preferences > Licenses, or `Unity_lic.ulf` from activation. For Unity Personal, activate once and export the `.ulf`. |
| `UNITY_EMAIL` | Unity ID email used for the license | The Unity account email |
| `UNITY_PASSWORD` | Unity ID password | The Unity account password — store only as a GitHub secret, never in chat / code / files |

Without `UNITY_LICENSE` the `unity-builder` step fails at activation.
A release keystore for a signed release APK is a separate follow-up
(the CI build currently produces a debug/development APK artifact).

## Next steps

1. Add the three secrets above.
2. Push a commit (or re-run the workflow) and watch Actions go green.
3. Pick the game (horror / ranch / Mahabharat Chapter 1) and build it on
   top of `Main.unity` / `PlayerController.cs`.
