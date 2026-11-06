# unity6-game — THE FORGOTTEN VILLAGE

Unity 6 game project — **Unity 6000.3.28f1**, Android target, full-screen landscape.

> **Game: THE FORGOTTEN VILLAGE** — open-world survival horror in the
> fictional village **Bhairavpur**. Hero: **Nilesh**. Day: explore the
> village lane, collect food near the well. Night: a monster that mimics
> familiar voices knocks on the door and calls your name — survive by
> keeping the door LOCKED, or risk going out. Original story, no copy
> of any existing show/game. Village/hero names are fictional on purpose.

## Phase 1 (built, in `Assets/Scenes/Main.unity`)

- Small village: 6 houses around the main chowk, one small crop field,
  jungle edge (trees), fences, well, chowk lamp, crates/barrels.
- One enterable house (Nilesh's, west side of the chowk) with a working
  door: ACTION opens/closes, LOCK barricades it.
- Day-night cycle (~60 s day, ~8 s dusk, ~75 s night): warm sun → cold
  moonlight, fog thickens at night, chowk lamp flickers, moon fades in.
- Monster night sequence: walks out of the jungle, knocks 3 times with
  mimicked-voice subtitles, door shakes, camera shakes. Door LOCKED =
  it leaves at dawn (survived). Door open/unlocked = it forces its way
  in (game over). Leaving the house at night = it chases you.
- Controls: virtual joystick (move) + RUN / JUMP / ACTION / TORCH /
  LOCK buttons, camera drag on the right half of the screen. WASD +
  Space / E / L / T work on keyboard too.
- One pickup: food crate near the well (ACTION to collect).

## Graphics (Phase 1)

- **Built-in Render Pipeline** (NOT URP yet — see below).
- PBR Standard materials with real **Poly Haven CC0 1k textures**
  (`Assets/Resources/Textures/`): mud ground, clay plaster walls,
  wood planks, clay roof tiles, grass field, old stone well.
- Night look: exponential fog, cold moonlight (directional), flickering
  warm chowk lamp (point light + glow), torch spotlight, pale moon +
  glow. Vignette/glow via runtime-generated radial textures.
- **Character is a TEMPORARY placeholder**: dark-clothed capsule
  figure (plain head, no face/features). No realistic human model is
  available yet — the user has `indian_man.glb` on his phone but has
  NOT sent it. Swap in the real model later; do not claim the
  placeholder is realistic.

## Why Built-in, not URP (Phase 1 decision)

The GDD asks for Unity 6 URP in the final build. URP was investigated
and DELIBERATELY deferred for Phase 1:
- URP for Unity 6000.3 is 17.3.x, but the 17.3.0 package tarball is not
  on the public Unity registry, so its script GUIDs could not be verified.
- The only real editor-generated URP asset files available are URP 10-era
  (their script GUIDs do not match URP 17), and the current Unity Graphics
  repo master has renamed URP classes (different GUIDs again).
- Hand-writing URP asset YAML with unverified script GUIDs risks a
  broken render pipeline (pink/black screen) with no local Unity to check.
- So Phase 1 graphics are maximised on Built-in instead, and the URP
  switch is a separate step to be done where it can be verified
  (in the Unity Editor, or with a verified 17.3 asset template).

## What is in here

- `Assets/Scenes/Main.unity` — Main Camera, Directional Light, Ground,
  Player (PlayerController) + GameManager (VillageBuilder, builds the
  whole village at runtime from primitives + PBR materials).
- `Assets/Scripts/` — PlayerController, VillageBuilder (+ VillageColliders
  AABB registry), DayNightCycle, DoorController, MonsterKnock,
  TorchController, Pickup, MobileControls, VirtualJoystick,
  CameraFollow, GameManager.
- `Assets/Tests/ScreenshotTest.cs` — PlayMode smoke test: loads Main,
  checks Player + Camera + GameManager + Door_Player + Village exist,
  captures `TestScreenshots/main-scene.png`. Skips its body in EditMode
  (testMode: all also runs it there, where LoadSceneAsync is illegal).
- `Assets/Resources/Textures/` — Poly Haven CC0 diffuse maps (1k).
- `ProjectSettings/` — Android target, min SDK 24, landscape
  (`defaultOrientation: LandscapeLeft`), package `com.nik.unity6game`,
  Built-in Render Pipeline, `activeInputHandler: 2` (Both — legacy input
  keeps working alongside the Input System package from RGUnityBots).
- `.github/workflows/build-android.yml` — GitHub Actions CI (user's
  single-job workflow: free disk space first, tests, docker prune, build).
- `Packages/` — Autopilot 1.0.1 tarball (file: path is relative to the
  Packages folder) + Regression Games Unity Bots (git URL).

## CI — how it works

GitHub Actions (`.github/workflows/build-android.yml`, Unity `6000.3.28f1`):

1. **Tests** — `game-ci/unity-test-runner@v4`, `testMode: all`
   (continue-on-error), uploads test results.
2. **Docker prune** — frees disk before the builder (the Unity image is
   re-pulled for the build step).
3. **Build** — `game-ci/unity-builder@v4`, `targetPlatform: Android`,
   scenes from `ProjectSettings/EditorBuildSettings.asset`
   (class `!u!1045`, scene GUID must match `Assets/Scenes/Main.unity.meta`),
   uploads the APK as artifact `APK` (`build/Android`).

Unity does NOT run on this machine — the build runs on GitHub's runner
with a ready Unity image. **Do not spam builds**: repeated failed
activations can trigger a Unity 1-hour lockout. One push = one build.

## Secrets — REQUIRED (already set in the repo)

| Secret | What it is |
|---|---|
| `UNITY_LICENSE` | Contents of the Unity `.ulf` license file |
| `UNITY_EMAIL` | Unity ID email used for the license |
| `UNITY_PASSWORD` | Unity ID password (set by the repo owner in GitHub, never in chat) |

## Open locally (Unity Hub)

1. Install Unity **6000.3.28f1** (Unity Hub > Installs > Add).
2. Add Android Build Support (Android SDK & NDK Tools + OpenJDK).
3. Clone this repo, open the folder in Unity Hub, press Open.
4. Open `Assets/Scenes/Main.unity`, press Play. WASD moves Nilesh,
   drag right side rotates the camera. Wait ~60 s for nightfall.

## Next steps (after Phase 1)

1. URP switch (verified in-editor) + post-processing (bloom/vignette).
2. Realistic Nilesh model (user to send `indian_man.glb`) + Mixamo walk/idle.
3. Chapter 2+: Meera, Mukhiya, school register, temple + tunnel,
   more houses, full village (25–30 houses).
