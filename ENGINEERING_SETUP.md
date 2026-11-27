# Engineering Setup — Sheru Bhai Foundation (Adopted, Not Copied)

## What the Sheru recipe is

Sheru Bhai Life Simulator Game v9 (com.Skorax.SheruBhaiGame, by Skorax) was decompiled from its real 240 MB release XAPK (APKPure) with REA + jadx on 2026-11-27. The resulting build-spec guide records how that game was engineered in Unity 6 — engine version, Player / build settings, URP rendering, package list, feature-namespace code structure, two-network ads, analytics-only backend, and Play Asset Delivery split.

It is an engineering reference only: adopt the setup, build original content on it. Do not reuse Sheru Bhai's assets, models, sounds, code, package name, AdMob ID, or Firebase project.

Full findings: `Nik5479/reverse-engineering-anything`, `targets/sheru-bhai-life-simulator/notes.md`.

## What was APPLIED in this change

### Player Settings

| Setting | Before | After |
|---|---|---|
| Scripting backend | — | IL2CPP |
| Target architectures | — | ARM64 only |
| Managed code stripping | — | On (engine code stripping on) |
| Multithreaded rendering | — | On |
| Orientation | — | Landscape, fullscreen |
| minSdk | 25 | 32 (Android 12+) |
| targetSdk | Auto | 36 |

### Packages

Already present in the project:

- AI Navigation 2.0.16
- Timeline
- Terrain module

Newly added free packages (versions per `Packages/manifest.json`):

- Input System
- TextMeshPro
- Burst
- Mathematics

Post Processing was NOT added as a package: the legacy `com.unity.postprocessing`
stack is for the Built-in Render Pipeline and would be dead weight in this URP
project. The recipe's "Post Processing" maps to URP's built-in Volume
post-processing (part of `com.unity.render-pipelines.universal`): enable it on
the URP Renderer asset and add a global Volume in the scene — a content step,
not a package step.

### Code structure

- New skeleton: `Assets/Scripts/ForgottenVillage/`, with namespaces `ForgottenVillage.*`.
- A gradual `MIGRATION.md` plan covers moving existing code into that structure over time.
- No existing scripts were moved in this change.

## What was DELIBERATELY NOT copied

- No Sheru assets, models, sounds, or code.
- No Sheru package name (`com.Skorax.SheruBhaiGame`).
- No Sheru AdMob ID.
- No Sheru Firebase project.
- No Sheru brand, characters, or story.
- Unity version stays **6000.3.28f1** — the same 6000.3.x LTS line as Sheru's 6000.3.23f1, newer. It was NOT downgraded.
- **Behavior Designer + CarKit NOT added.** They are paid Asset Store items; adding them needs a purchase decision.
- **Vehicles module** is already in the manifest, but there is no driving gameplay in this game, so it is unused for now.
- **AR module skipped** — it was present but unused in Sheru Bhai.

The game stays **THE FORGOTTEN VILLAGE** — Bhairavpur village, hero Nilesh, horror loop: night knock in a mimicked voice, survive by keeping the door LOCKED.

## FUTURE steps (LATER — needs Nik's own accounts)

None of the following are implemented in this change. Each needs Nik's own accounts; never create accounts on his behalf, and never reuse Sheru IDs.

- **LATER:** AdMob + UMP consent, with Nik's own AdMob App ID.
- **LATER:** Meta Audience Network, with Nik's own Meta app.
- **LATER:** ANR-guard idea — a watchdog ticking every ~100 ms that disables ads (writes a flag file) if an ad freezes the game, instead of letting Android kill the app for ANR.
- **LATER:** Firebase Analytics, in Nik's own new Firebase project, plus Unity Analytics.
- **LATER:** Play Asset Delivery (AAB + install-time asset packs) — small base APK plus asset packs, not one giant APK.
- **LATER:** Google Play license verification at app start.
- **LATER (optional):** C# obfuscation.

## Validation note

There is no local Unity editor on this VM. Changes were validated via GitHub Actions CI only. No APK release came from this change.
