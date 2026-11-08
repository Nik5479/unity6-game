# FULL GAME QUALITY AUDIT — THE FORGOTTEN VILLAGE
**Date:** 2026-11-08 (audit run 2026-11-07 23:14 – 2026-11-08 00:05 UTC / 2026-11-08 04:44 – 05:35 IST)
**Auditor:** subagent (parent agent's audit coordinator)
**Project:** ~/workspace/unity6-game-setup — Unity 6000.3.28f1, URP 17.3.0, Android, `com.nik.unity6game`
**State audited:** local working copy after APK v1.0.1 (GitHub commit `52b1dcc2773ae25be542149ab1bd931392db77fc`, release v1.0.1, 482 MB) + the post-push version bump (1.0.1 / code 2, local only)
**Spec:** `FULL_GAME_AUDIT.md` (user order 2026-11-07 ~23:46 IST) — all 15 sections, absolute rules obeyed.

## ⚠️ AUDIT STATUS: PARTIAL — LICENSE LOST MID-AUDIT
The static audit (4 parallel workers) is **complete**. The runtime verification is **partial**: one PlayMode logic run completed and **confirmed the blocking bug B1 at runtime**, the B1 fix was applied, but the re-test could not run — **the VM restarted at 2026-11-07 23:23 UTC** (uptime 1 min) and the Unity license + CLI sign-in session were wiped (known rule: VM restart kills the license). `unity auth status` now says "You are not signed in" and `unity license status` says "none active". Re-activation needs the user's phone tap (sign-in URL) — a user-only step. **No Unity run is possible until the license is re-activated. Nothing in this report that needs a runtime re-test is claimed as verified.**

---

## 15 REQUIRED ITEMS (summary — detail in the sections below)

### 1. Total assets inspected
**577 files under `Assets/`** (incl. 51 model records, 25 scripts, 17 audio clips, 7 materials, 1 scene, 84 PBR texture files) + `ProjectSettings/` + `Packages/manifest.json` + the v1.0.1 APK (`build/Android/unity6-game.apk`). Static inspection by 4 parallel workers; runtime inspection by 1 PlayMode run (partial).

### 2. Total models checked
**51 records: 45 `.glb` + 6 `.gltf` bundles** (2,697,169 tris, 312 meshes, 187.3 MB embedded textures) + 2 FBX (`FallenPine`, `SmallGrave` — present, tris unmeasured). **0 corrupt, 0 unused, 0 missing.** Verdicts: **KEEP 26 / BORDERLINE 24 / REPLACE 1** (dock/pier). Full per-model table in §2.

### 3. Total scripts checked
**25 `.cs` files** (16 runtime + 5 editor + 4 tests incl. the audit's own `AuditLogicTest.cs`), ~6,500 lines of game code. No NavMesh, health, save, pause, inventory, or dialogue code exists anywhere (grep-verified).

### 4. Console errors before and after
- **Before:** 0 LogErrors in every recent verification run (wolfanim, verify-min5, verify-main1, fixpass-moon9, audit-logic run 1).
- **After fixes:** **NOT RE-TESTED** — license lost after the VM restart. The fixes below are applied but uncompiled/unrun in the editor. They must be re-tested after license re-activation (see §13).

### 5. Bugs found
**1 BLOCKING, 8 MAJOR, ~30 MINOR (code) + 6 graphics findings (F1–F6) + 17 overlap candidates (O1–O17) + 12 performance problems + 12 missing features.** Full lists in §5–§12.

### 6. Bugs fixed (in this audit)
| # | Bug | Fix | Verification |
|---|---|---|---|
| B1 | **Player can never enter their own house** (doorway lintel) — game unwinnable | `VillageBuilder.cs` `CanMoveTo`: centre-based interval `[pos.y, pos.y+1.8]` → feet-based `[pos.y-0.5, pos.y+1.3]` | **APPLIED, re-test PENDING** (license). Runtime evidence for the bug itself is in §5. |
| A1 | Game-over sting plays on only 1 of 3 death paths | `PlayGameOverSting()` moved into `GameManager.GameOver()`; removed from `MonsterKnock` stage 4 | **APPLIED, re-test PENDING** |
| A2 | Dawn birds play over the dawn-outside GameOver screen | `OnDawn` delegate: `PlayDawnRelief()` now only when the player is inside the house (the exact survival condition) | **APPLIED, re-test PENDING** |
| A3 | Orphan `whisper_1_old.ogg` (56 KB) shipped in the APK, never referenced | Moved to `fix-backups/audit-20261108/` (recoverable; also in GitHub commit 52b1dcc) | Verified by grep (0 references) |
| A4 | Stale docs claimed whispers were "pending"/"not recorded" | `AUDIO_CREDITS.md` + `AudioManager.cs` TODO corrected (Aria voice, user-selected 2026-11-07; whisper_2/3 approved; whisper_1 verdict pending) | Docs only |

### 7. Bugs remaining
See §5 (code), §2 (graphics), §3 (overlaps), §8 (performance). Every remaining bug lists location, cause, and proposed fix. The most important: **M1 monster can never catch the player (3.4 vs 5/9 m/s), M2 monster walks through walls (no NavMesh/collision), M3 NileshAnimator NRE risk, M4 one exception kills all systems wiring, M5 glTF import leak on reload, M6 camera-disable fallback, M7 sticky joystick, F1 banyan at the black-render threshold, F2 Mahadev/Nandi unlit, moon dark disc (root cause found), 12 missing features.**

### 8. Performance problems found and fixed
**Found 12, fixed 0** (all recorded with proposed fixes — see §8). Worst: ~5.35M scene tris (ring clones), 187 MB uncompressed embedded textures, Android quality defaults to High, 460 MB APK (exceeds Play's 200 MB APK limit), no LOD/occlusion/instancing, `Renderer.material` clones break batching, `s_AliveImports` leak on reload.

### 9. Animation problems found and fixed
**Found 7, fixed 0.** M3 NRE risk (only Idle null-checked), M8 Sprint clip dead (434 KB, never loaded), footstep sounds desync from mocap legs, double-bob (CharacterWalk bob + Hips curves), no crouch/punch/climb/jump animations wired (only 4 Mixamo FBX imported), foot-sliding risk (in-place clips vs 5/9 m/s movement), no turn-in-place/strafe handling. See §5.

### 10. NPC and monster AI problems found and fixed
**Found 8, fixed 0.** M1 (monster slower than walking player), M2 (monster no collision/pathfinding), NPCs are static statues (no movement, no AI, no facing logic), wolf is a static prop (idle_loop only), monster teleports to (-2,0,-19) on spawn (visible pop), 5.7 s dead air after the 3rd knock, no re-trigger after the monster leaves, no NavMesh anywhere. See §5.

### 11. Graphics problems found and fixed
**Found 9, fixed 0 in this audit** (prior passes fixed many — see §2 for what changed vs baseline). Remaining: F1 banyan threshold, F2 unlit statues, F3 textureless tractor/bullcart (code-PBR, unverified visually), dock/pier REPLACE (124k tris, no river), 9 albedo-only files, ground still a flat plane, fences/paths/grass still primitives, moon dark disc (root cause found — §2.4), GroundMud 5×5 tiling (10 m/tile — needs visual check). See §2.

### 12. Tests actually executed and their results
| Test | Result |
|---|---|
| `AuditLogicTest` run 1 (PlayMode, log-only, 640×360 xvfb) | **PARTIAL — B1 CONFIRMED at runtime**, then aborted at the B1 assert (test file since fixed to log-and-continue). 0 LogErrors. Models loaded in 24.5 s. CanMoveTo probe failed on a reflection type-name error (fixed in the test, re-run blocked by license). Walk speed / overlaps / moon diagnostics did NOT run. |
| Prior verification runs (pre-audit, still valid) | `VerifyMinimalTest` 1/1, `VerifyWolfCameraTest` 1/1, `VerifyAnimPathsTest` 1/1, fixpass-moon9 1/1 — all 0 LogErrors. Wolf + Nilesh walk verified in minimal scenes (22:11 UTC screenshots, parent-judged PASS). |
| Static audits (4 workers) | Code (18 scripts), graphics/models (51 records), audio/gameplay/UI (18 ogg + 13 scripts + Main.unity), performance (asset weights + settings + code hotspots). All complete. |

### 13. Tests NOT executed and why
- **B1 re-test after the fix, walk-speed re-measurement, overlap bounds checks, moon diagnostics** — license lost after the VM restart (2026-11-07 23:23 UTC); Unity cannot run until the user re-signs in on his phone. `AuditLogicTest.cs` is **retained** in `Assets/Tests/` for this re-test (`-testFilter AuditLogicTest`).
- **Visual close-ups** (banyan F1, Mahadev/Nandi F2, tractor/bullcart F3, Pujari face F4, GroundMud tiling, crop cards, overlaps O1–O17) — same license block. The parent's earlier judgements on the fix-pass screenshots stand where they exist (monster, crops, Pujari front, chowk, Ambassador — PASS).
- **Wide main-scene renders** — proven OOM-impossible on this machine (8 consecutive exit-137 kills at `cam.Render()`; scene ~5.5 GB RSS under llvmpipe). Only the phone APK can show the full village.
- **Full gameplay route (day → night → dawn), monster-chase catch test, multitouch, save/load, pause** — save/load and pause do not exist; the rest needs a working editor + device.
- **Anything needing a GPU or Android device** — FPS, frame time, draw calls, runtime memory, GC spikes, load time on device, thermal behaviour, moto g85 specifics: **NOT TESTED, never claimed.**
- **Android build validation** — not authorized in this audit (no APK build per the spec).

### 14. List of files changed
**Audit fixes (by me):**
| File | Change |
|---|---|
| `Assets/Scripts/VillageBuilder.cs` | B1 fix (feet-based `CanMoveTo` interval) + OnDawn dawn-relief guard (A2) |
| `Assets/Scripts/GameManager.cs` | `PlayGameOverSting()` moved into `GameOver()` (A1) |
| `Assets/Scripts/MonsterKnock.cs` | Removed redundant `PlayGameOverSting()` call (A1) |
| `Assets/Scripts/AudioManager.cs` | Stale whisper TODO comment corrected (A4) |
| `Assets/StreamingAssets/Audio/AUDIO_CREDITS.md` | Whisper section corrected — files present, Aria voice (A4) |
| `Assets/StreamingAssets/Audio/whisper_1_old.ogg` (+ `.meta`) | Moved out of the build to `fix-backups/audit-20261108/` (A3) |
| `Assets/Tests/AuditLogicTest.cs` | **NEW** — retained for the B1 re-test after license re-activation |

**Editor-touched (not by me, no content change found):** the audit's Unity runs re-saved/touched `Assets/Settings/URP.asset` (verified: byte-identical to the pre-audit backup), `ProjectSettings/GraphicsSettings.asset`, `ProjectSettings/ShaderGraphSettings.asset`, `ProjectSettings/UnityConnectSettings.asset`, `Assets/UniversalRenderPipelineGlobalSettings.asset` (still points at the URP asset — verified). The RG files' mtimes (22:51) predate this audit (earlier verification runs).

### 15. Backup or Git commit reference
- **Pre-audit backup:** `fix-backups/audit-20261108/` — `ProjectSettings.asset.pre-audit` (MD5 `1edb7101a0dcba3516433595a4911503`), `scripts-settings-tests-20261108.tgz` (Scripts + Editor + Tests + Settings + ProjectSettings), `whisper_1_old.ogg` + `.meta`.
- **Git:** the pre-audit state is GitHub commit `52b1dcc2773ae25be542149ab1bd931392db77fc` (300 files, pushed 2026-11-08 ~04:39 IST). **The audit fixes above are LOCAL ONLY and UNPUSHED** (the only other local difference from the repo is the version bump 1.0.1/code 2). Next push must include the audit fixes + the version bump.

---

## §1. COMPLETE PROJECT INSPECTION (spec §1)
- **Scenes:** 1 (`Assets/Scenes/Main.unity`, 712 lines). Roots: Main Camera, Directional Light, Ground plane, Player, GameManager (misleading name — actually runs `VillageBuilder`), PostProcessVolume. No Canvas/EventSystem/AudioListener/Door/Monster/NPCs/pickups in the scene — **everything is built at runtime** by `VillageBuilder`/`MobileControls`/`AudioManager`. The scene alone is an empty shell; a scene opened without the builder running has no UI at all.
- **Missing script references:** NONE in `Main.unity` — all 5 `m_Script` refs resolve to real GUIDs. No `m_Script: {fileID: 0}`.
- **Duplicates:** none in the scene. 67 `InitTestScene<guid>.unity` files (536 KB) under `Assets/` — NOT referenced by `Main.unity`, NOT in the build (only `Main.unity` is in `EditorBuildSettings`). Safe to delete for hygiene; zero build impact.
- **Scripts:** 25 `.cs` (16 runtime + 5 editor + 4 tests). No NavMesh, health, save, pause, inventory, or dialogue code exists (grep-verified).
- **Import settings:** 56 Resources `.meta` files checked — 28 have NO Android platform settings at all; 26 have an Android entry with default format. glTF textures bypass the Unity importer entirely (glTFast decodes to RGBA32 at runtime).
- **Unity 6 migration:** project is on URP 17.3.0; loader shader fixes are in place (metallicFactor, unlit swap, spec-gloss swap). No migration errors in the logs.

## §2. GRAPHICS — REMOVE THE CARTOON LOOK (spec §2)
### Per-model table (51 records; Alb/MR/Nrm/AO = % of materials carrying that map)
| # | File | Tris | Verdict |
|---|---|---|---|
| 1 | Dire_Wolf_Lite.glb | 9,991 | KEEP — full PBR, rigged, 4 anims |
| 2 | abandoned_hospital_building_20_2516.glb | 54,041 | KEEP — 724→21 meshes |
| 3 | ancient_gate_11_4q5m.glb | 4,546 | KEEP — only file 100% on all PBR maps |
| 4 | boer_goat__3d_model_free_7_dr1w.glb | 10,385 | KEEP |
| 5 | chicken_73_eoja.glb | 20,000 | KEEP |
| 6 | chinese_banyan_ficus_microcarpa_9_tpa1.glb | 111,392 | BORDERLINE — **F1, at the black-render threshold** (see below) |
| 7 | classic_tractor__1__11_scro.glb | 81,702 | BORDERLINE — 0 textures; code-PBR, needs visual check |
| 8 | cow_15_u1bj.glb | 4,898 | BORDERLINE — blocky up close |
| 9 | dock_pier_78_n66e.glb | 123,942 | **REPLACE** — no normal/MR/AO, no river in the scene |
| 10 | granite_figure_of_a_nandi_bull_3_3z0y.glb | 149,990 | BORDERLINE — **F2, KHR_materials_unlit** |
| 11 | grave_stone_collection_64_7j05.glb | 1,116 | KEEP |
| 12 | handpump_3d_39_aah5.glb | 30,432 | KEEP |
| 13 | helldemon_reborn_16_sr87.glb | 73,844 | KEEP — full PBR, rigged, 7 anims |
| 14 | hindu_temple_88_62er.glb | 100,502 | BORDERLINE — weak PBR (4/22 mats have normals) |
| 15 | hindustan_ambassador_4_rh04.glb | 12,298 | KEEP — full PBR |
| 16 | historic_miscellaneous_basket…glb | 3,784 | KEEP |
| 17 | historic_miscellaneous_stand…glb | 5,008 | KEEP |
| 18 | horse_6_jrh4.glb | 70,472 | BORDERLINE — no MR |
| 19 | indian_house_old_18_qlsq.glb | 35,565 | BORDERLINE — albedo-only |
| 20 | indian_man_77_hwkm.glb (Nilesh) | 55,660 | BORDERLINE — no MR, 1 textureless mat |
| 21 | indian_man__sunglasses_76_gmed.glb | 54,287 | BORDERLINE — sunglasses at night |
| 22 | indian_man_in_dhoti__1__2_kfur.glb (Pujari) | 21,604 | BORDERLINE — 4/9 mats flat colours; face texture present (needs visual check) |
| 23 | indian_terracotta_matka…glb | 12,160 | KEEP |
| 24 | indian_village_house_famous…glb | 18,910 | BORDERLINE — albedo-only |
| 25 | indian_woman_in_saree_84_8ssr.glb (Meera) | 117,595 | BORDERLINE — heavy, no MR |
| 26 | mahadev_001__1__2_t4b6.glb | **213,132** | BORDERLINE (decimate) — heaviest single object, 1 m statue, 256×512 texture, **F2 unlit** |
| 27 | mango_tree_3_sl11.glb | 130,451 | BORDERLINE — heavy, ball canopy |
| 28 | medieval_wooden_cart_1_jab4.glb | 11,388 | KEEP |
| 29 | meshy_haveli_86_hvl.glb | 130,000 | BORDERLINE — single mesh, no LOD; Meshy free-tier licence flag |
| 30 | meshy_mukhiya_87_mky.glb | 151,064 | BORDERLINE — 151k tris, unrigged static statue; Meshy licence flag |
| 31 | old_and_worn_out_hospital_bed…glb | 526 | KEEP |
| 32 | old_village_house_-_indian_game_asset…glb | 1,119 | BORDERLINE — textured box up close |
| 33 | old_well_with_hanging_bucket…glb | 7,286 | BORDERLINE — no normal map |
| 34 | realistic_hd_chinaberry_tree…glb | 221,798 | KEEP — best tree; count fixed to 1 instance |
| 35 | realistic_palm_tree_free_8_k8rm.glb | 27,152 | BORDERLINE — albedo-only |
| 36 | rusty_and_oil_stained_oil_barrel…glb | 812 | KEEP |
| 37 | sandstone_rocky_ground…glb | 4,373 | KEEP |
| 38 | school-building_74_yizq.glb | 13,566 | BORDERLINE — no MR/AO |
| 39 | small_fire_place_90_23ou.glb | 3,932 | KEEP |
| 40 | thatched_hut_4_p31g.glb (House_Player) | 161,142 | BORDERLINE — heaviest house, weak PBR |
| 41 | thatched_hut_stealth86_50k.glb | 49,991 | BORDERLINE |
| 42 | traditional_round_thatched_hut_50k.glb | 50,000 | KEEP — full PBR |
| 43 | trees_25_wae0.glb | 9,858 | KEEP (spec-gloss, renders fine) |
| 44 | water_buffalo_18_44fy.glb | 19,659 | KEEP |
| 45 | woodpile_game_ready_asset…glb | 3,440 | KEEP |
| 46 | Bhumija/scene.gltf | 150,004 | BORDERLINE — albedo-only scan |
| 47 | Bullcart/scene.gltf | 105,704 | BORDERLINE — 0 textures; code-PBR, needs visual check |
| 48 | SoilGround/xdhhdhl_tier_2.gltf | 2 | KEEP |
| 49 | ForestFloor/sfjmafua_tier_2.gltf | 2 | KEEP |
| 50 | Lantern_01/Lantern_01_1k.gltf | 33,902 | KEEP |
| 51 | brass_diya_lantern/brass_diya_lantern_1k.gltf | 12,742 | KEEP |

**Totals:** 2,697,169 tris (+15% vs the 2026-11-07 baseline — the replacements made the scene heavier, not lighter), 312 meshes, 187.3 MB embedded textures (231 PNG + 84 JPG, 7 images >1024px). Albedo-only files (0% normal AND 0% MR): 9. Files with 0% MR: 16/45. Files with 0% AO: 37/51.
### Graphics findings
- **F1 — Banyan at the exact black-render threshold.** All 7 banyan materials have `specularFactor [0.05, 0.05, 0.05]`; the loader converts spec-gloss → URP Lit only when `specMax < 0.05f`. 0.05 is not < 0.05, so the conversion does NOT fire and the tree keeps `Shader Graphs/glTF-pbrSpecularGlossiness` with near-black specular — likely dark/black in-game. **Proposed fix:** change the threshold to `<= 0.05f` — BUT the banyan's colour comes from `COLOR_0` vertex colours, which a URP-Lit conversion would drop, so the conversion must also handle vertex colours. **Needs a visual check first.**
- **F2 — Mahadev + Nandi are `KHR_materials_unlit`.** The loader swaps them to URP Unlit, which ignores all scene lighting: at night the two statues render at full flat texture brightness against a dark temple — flat/glowy, no day-night response. Mahadev's single 256×512 texture over 213k tris makes it worse. **Proposed fix:** convert to URP Lit (or decimate Mahadev to ~50k and give it a lit material). Needs a visual check.
- **F3 — Tractor + Bullock cart ship with zero textures.** `ApplyTractorPbr` / `ApplyWoodPbr` in code add mud normal maps + per-part metallic/smoothness over flat colours. Whether either reads as real wood/metal rather than flat colour + noise normal is **needs-visual-check**.
- **F4 — Pujari face plausibly fixed** (`Material.007` has a base-colour texture; the 4 textureless mats are dhoti factor / teeth / footwear — correct as flat colours). Parent-judged PASS in `fix-pujari-front.png` (true front view, skin-toned face).
- **Materials:** 5 of 6 `.mat` files fully PBR-wired with keywords. `GroundMud.mat` tiling is 5×5 (was 14×14) = 10 m per tile on the 50×50 m plane — the opposite extreme from the baseline's "minified to flat brown"; needs a visual check. `WoodPlanks.mat` deliberately unwired (albedo-only).
- **Primitives still present:** ground (flat 50×50 plane — the #1 realism gap), dirt paths (flat boxes, now staggered/rotated), grass/ground patches (flat boxes with photo textures), fences (36 boxes, albedo-only wood), chowk lamp (primitives + light), door/handle, house interior (boxes/cylinders — the real matka/diya models are placed *outside*), pickups (crates), moon (sphere + quad). Crop field was rebuilt as 84 crossed-plane wheat plants (parent-judged PASS in `fix-cropfield.png`).

## §3. MODEL COLLISION AND OVERLAPPING (spec §3)
**Previously reported fixes — all verified still in place in code:** Ambassador (1,0,16) clear of east fence; BullockCart (−7.5,0,−4) 0.75 m clear; Goat (13,0,15) clear of tractor; Handpump (4.5,0,9) 1 m clear; Well (−4,0,−3.5) ~0.5 m clear.
### Remaining intersections (from static positions; all NEEDS-VISUAL-CHECK except O1/O2 which are centre-distance-proven)
| # | A | B | Evidence | Severity |
|---|---|---|---|---|
| O1 | Wolf (−6,0,8), 1.8 m | Chicken_1 (−6.5,0,8), 0.6 m | 0.5 m apart; half-extents sum ≈1.2 m — the wolf stands on/through the chicken | HIGH |
| O2 | Ring tree (0,0,21), 5–7 m | Gate (0,0,20.5), 4 m | 0.5 m apart; a tree grows through the gateway arch; its trunk `Block()` sits in the walking line through the gate | HIGH |
| O3 | Ring trees (21,0,9), (21,0,14) | AngkorTemple (21,0,12), 8 m | 3 m / 2 m from centre vs 4 m half-width (orientation-dependent) | MEDIUM |
| O4 | Ring tree (−8,0,21) | Dock (−7,0,19), 6 m | 2.24 m apart vs 3 m half-width | MEDIUM |
| O5 | Tree_Mango (−16,0,−2), 6 m | Clinic (−18,0,−2), 6 m, yaw 90° | 2 m apart; trunk may sit on the clinic's east wall | MEDIUM |
| O6 | DiyaLantern (−10.5,0,4.5) | HospitalBed ≈(−11,0,4.3) + Medicine (−10.5,0,5) | ~1 m tight cluster — diya likely inside the bed footprint | MEDIUM |
| O7 | Tree_Pack (−18,0,−18), 8 m spread | School (−16.5,0,−14), 7 m | Outer trees likely clip the school's SW corner | MEDIUM |
| O8 | PyreWood (18.5,0,−19.5) / GraveStones (19,0,−18) / ShamshanFire (16.8,0,−19.5) | each other | Pyre–Graves 1.58 m apart vs half-sum ≈2.65 m → overlap | MEDIUM |
| O9 | Tree_Banyan (16,0,2), 7 m | Haveli (16,0,−3.5), 8 m | Canopy touches/overlaps the haveli's north face | MEDIUM |
| O10 | Tree_Palm (−4,0,18), 5 m | Dock (−7,0,19), 6 m | Palm trunk sits on the dock's edge | MEDIUM |
| O11 | House_2 (9,0,8) | Tractor (12,0,11) | 4.24 m apart vs half-sum ≈4.25 m — tangent | LOW |
| O12 | Clinic (−18,0,−2) | Ring tree (−21,0,−1) | Grazing | LOW |
| O13 | Nandi (2,0,−12.8) / Pujari (3.6,0,−14.2) | Temple (2,0,−17) | Tight but clear by design | LOW |
| O14 | Shivling (2,0,−16) | Temple shrine interior block | 0.2 m in front of the solid block | LOW |
| O15 | House_Player east wall (≈−5.75) | West fence x=−5.5 | 0.25 m gap, different z from the door — no conflict | LOW |
| O16 | SandstoneGround / ForestFloor tiles | Temple footprint | Intended as ground variation (sunk −0.15 m) | LOW |
| O17 | Matka (−5,0,8.5) | Wolf (−6,0,8) | Grazing (1.12 m vs half-sum ≈1.15 m) | LOW |
**Floating/buried:** none evident from code — `Normalize` grounds every model (`bounds.min.y = 0`); only SandstoneGround is deliberately sunk −0.15 m. **Proposed fixes:** O1 move the chicken ~1.5 m (e.g. (−8,0,9)); O2 skip the ring-tree clone at (0,0,±21) or move the gate; O3–O17 re-space per the table (positions only, no model changes). All need a visual re-check after moving.
**CharacterController:** the player has NO CharacterController — movement is a custom AABB check (`VillageColliders.CanMoveTo`, radius 0.45). No Rigidbody on the player. Monster has no collider checks at all (see M2).

## §4. PLAYER MOVEMENT AND GTA-STYLE CAMERA (spec §4)
- **Walk/run speeds:** constants walk 5 m/s, run 9 m/s (`PlayerController.cs:15-17,53`). The verification coordinator measured **~6.2 m/s for a walk** (7.43 m in 1.2 s) — unexplained by the constants (no diagonal boost: move vector is normalised; joystick clamped to magnitude 1). **Re-measurement is pending (license).**
- **Jump:** no animation, no coyote time; gravity −20, `jumpForce=5` → ~0.63 m apex, 0.5 s air. Ground is hard-coded flat y=0.5.
- **Camera:** third-person follow, distance ~9 m + 4.6 m height (log-verified: min 10.64 / max 11.10 m, max step 0.65 m/frame — smooth, no snapping). **Camera collision raycast has NO layer mask** — it hits every primitive collider (hundreds: ground patches, paths, grass tufts, tables, fence rails); a low rail/table between camera and player can yank the camera. Auto-centre (60°/s after 1.5 s without drag) fights deliberate sideways looking. Camera lerps from the scene pose on spawn (opening swoop). Mouse drag starts even over the joystick area in the editor.
- **Camera requirements status:** follow distance/height ✓ (log data), smooth/no snapping ✓ (log data), wall clipping — NOT TESTED (raycast has no layer mask; no wall-clip evidence in the log), no wall-clip visual proof, touch smoothness — NOT TESTED on device.
- **Strafing handedness:** `right = (fwd.z, 0, -fwd.x)` — not verified statically; forward/back felt correct in prior screenshots, strafe direction unconfirmed. NEEDS-RUNTIME-TEST.

## §5. CODE BUGS — FULL LIST (spec §5, §6, §7, §10, §11)
### 🔴 BLOCKING
**B1. Player can never enter their own house — the game is unwinnable. — FIXED in this audit (re-test pending).**
- **Location:** `VillageBuilder.cs:31-48` (`VillageColliders.CanMoveTo`) + `VillageBuilder.cs:1273` / `:1298` (lintel blocks).
- **Cause:** `CanMoveTo` treats the player as the vertical interval `[pos.y, pos.y+1.8]`. `PlayerController` passes `transform.position` whose y is 0.5 (player centre), so the interval is [0.5, 2.3]. The doorway lintel block is registered with min.y = 2.2. 2.3 > 2.2 → every step through the doorway returns false (full, X-only and Z-only).
- **Runtime proof (AuditLogicTest run 1):** door opened via `SetOpen(true)`, player driven west from DoorFront (−3.75, 0.5, 6) for 5 s at 5 m/s — ended at (−4.99, 0.5, 4.87), having slid south along the wall; stopping x = −4.99 matches the lintel's max.x (−5.45) minus player radius (0.45) = −5.0 exactly.
- **Consequences:** player cannot enter the house; `Survived()` requires `IsInsidePlayerHouse()`, so dawn always = GameOver even with a locked door; the monster-chase escape into the house is impossible; the Medicine pickup at (−10.5, 0.3, 5) is unreachable.
- **Fix applied:** feet-based interval `[pos.y−0.5, pos.y+1.3]` in `CanMoveTo`. **Re-test pending (license) — `AuditLogicTest` retained for it.**
### MAJOR
- **M1. Monster can never catch the player.** `MonsterKnock.cs:135` chase 3.4 m/s vs player walk 5 m/s, run 9 m/s. Catch radius 1.6 m unreachable unless the player stands still. **Fix:** chase speed ≥ 9.5–10 m/s, or scale with player speed + stamina cost for running. (Design-adjacent — needs the user's call on the exact speed; recorded, not changed.)
- **M2. Monster has no collision or pathfinding — walks through walls/houses.** `MonsterKnock.cs:218-236` `WalkToward` is straight-line `MoveTowards`; never calls `CanMoveTo`; zero NavMesh usage in any script. **Fix:** route the monster through `CanMoveTo` (slide like the player) or add NavMesh + NavMeshAgent; at minimum clamp its path to lane waypoints.
- **M3. `NileshAnimator` will NRE if any one clip is missing.** `NileshAnimator.cs:40-50` — only Idle is null-checked; Walk/Run are dereferenced unconditionally (`anim["NileshWalk"].wrapMode`). **Fix:** null-check each clip after `AddClip` and fall back to embedded animation if any is missing.
- **M4. One exception in any async build step kills all systems wiring.** `VillageBuilder.cs:196-224` `BuildVillageAsync` has try/finally but no catch; `SetupSystems()` (CameraFollow, TorchController, DayNightCycle, MonsterKnock, GameManager, MobileControls) runs only if every preceding `yield return Build*Async()` succeeds. A single throw leaves a walkable player, static camera, no HUD, no monster — silently. **Fix:** wrap each async step in try/catch like `RunStep`, and always run `SetupSystems` (or a degraded-systems path) in `finally`.
- **M5. glTF imports leak across restarts.** `GltfModelLoader.cs:37,282` — `s_AliveImports` is never cleared, including on scene reload (`GameManager.Restart()` → `LoadScene("Main")`). Each restart re-loads ~40 models and appends ~40 more imports. **Fix:** add a `DisposeAll()` called on scene unload, after destroying holders.
- **M6. Failed camera check permanently disables the follow camera.** `VillageBuilder.cs:307-353` `VerifyCameraView()` → `CameraFollow.ResetToStarterView()` sets a fixed pose and `enabled = false`. The player can then walk out of frame with no recovery. **Fix:** reset pose but keep the component enabled (or re-enable on movement), and only fail the check after the camera has fully settled.
- **M7. Joystick can stick on touch-cancel.** `VirtualJoystick.cs:19-61` — only `IPointerDown/Drag/Up`; no `IPointerExitHandler`/cancel handling. If a touch is cancelled, `Value` stays and the player keeps walking. **Fix:** implement `IPointerExitHandler` and reset `Value` in `OnDisable`. NEEDS-RUNTIME-TEST on device.
- **M8. Sprint clip exists but is never wired; walk/run speeds don't explain ~6.2 m/s.** `NileshSprint.anim` (434 KB) exists; `NileshAnimator.cs:40-42` only loads Idle/Walk/Run. **Fix:** wire Sprint to a distinct sprint input or delete the asset; re-measure walk speed in play.
### MINOR (highest-value; full list in the workers' reports)
- `GameManager.Survived()` does not disable the player (unlike `GameOver`) — the player keeps walking after the win banner.
- Single-slot subtitle queue (`MobileControls.cs:140-155`) drops lines when >2 arrive quickly.
- West-door yaw latent bug (`DoorController.Awake` runs before `BuildDoor` assigns `closedYaw/openYaw` — a West door would start at +90°).
- `Normalize()` centres/grounds using pre-yaw bounds, then yaw is applied — rotated non-square models shift off their placement point.
- Ambience load race: `LoadAllClips()` loads 17 files sequentially; if night starts before loading finishes, ambience is silently absent for the night.
- `AudioListenerFollow.LateUpdate` calls `Camera.main` every frame (tag search).
- `ApplyUiMaterial` creates ~30 runtime materials, never destroyed — leak per scene restart.
- `BuildUI()` creates an `EventSystem` unconditionally — a second `MobileControls` would duplicate it.
- `MobileControls` centre text and Restart button overlap by ~30 px.
- `DoorController.Shake()` coroutine fights `Update()` if the door is mid-swing.
- `Unregister` relies on exact `Bounds` float equality.
- Footstep timing is procedural while legs are mocap — sounds desync from visible foot strikes.
- Double-bob: `CharacterWalk` bob writes `localPosition.y` on the model holder in addition to the Hips curves in the mocap clips.
- `GltfModelLoader.WaitUntil` has no timeout — a hung glTFast task stalls the build forever.
- Animated models with `childCount != 1` fall through to the `ModelRoot` wrapper branch, which the comment says breaks legacy animation paths.
- `BuildVillageAsync` sets `AllModelsLoaded = true` even on failure — a half-built village reports "loaded".
- Torch is parented to the camera (lights walls behind the player when the camera auto-centres); if `Camera.main` is null in `Awake`, `Toggle()` silently does nothing.
- `DayNightCycle` writes skybox/moon material values every frame even in the held dawn phase; per-frame `shader.name.Contains("Unlit")` string check.
- Monster teleports to (−2,0,−19) on spawn/chase-start (visible pop); 5.7 s dead air after the 3rd knock; no re-trigger after the monster leaves.
- Scene object named `GameManager` actually runs `VillageBuilder` (cosmetic, confusing).
- `BuildScript` hard-codes scene/output paths, no validation, generic exception on failure.
- `RemapMixamoClips.Eval()` returns 0f for a missing curve component (non-unit quaternion → distorted bone); bones with no rotation curve and no rest pose are skipped, silently warping the subtree.
### MISSING FEATURES (spec items that do not exist — not bugs)
Save/load, pause/resume, inventory UI, health/damage system, missions/objectives system (only a single-line objective text), dialogue choices, background music, animal sounds, NPC voices, monster approach/chase sound, torch battery (Battery pickup is a cosmetic counter), death respawn/checkpoint (only full scene reload), crouch/punch/climb/jump animations (only 4 Mixamo FBX imported; runtime wires Idle/Walk/Run only), monster attack animation/damage (only proximity → GameOver), NPC/monster/wolf AI states beyond the scripted knock/chase, multi-night/quest system, settings/minimap/sprint control distinct from RUN.

## §6. NPC AND VILLAGE PEOPLE AI (spec §6)
- NPCs (Mukhiya, Meera, Pujari) are **static statues**: positions fixed, no movement, no AI, no facing logic — only a 3 m subtitle trigger with one fixed line each (`GameManager.cs:116-133`). No NavMeshAgent anywhere. No day/night behaviour change. No dialogue UI.
- Wolf (`Dire_Wolf_Lite`, `idle_loop`) is likewise a static prop at (−6,0,8).
- Monster AI: scripted knock/chase only (see M1/M2). No detection distance tuning beyond the chase trigger; no obstacle avoidance; no attack animation.

## §7. MONSTER AND ANIMAL AI (spec §7)
- Detection: monster begins the night routine 6 s after night start; chase triggers if the player is outside the house during stages 1–3. Chase speed 3.4 m/s (M1 — cannot catch a walking player). Attack = proximity <1.6 m → instant GameOver (no attack animation, no damage model). Monster ignores colliders (M2). Animals (cow, goat, horse, buffalo, chicken, wolf) are static props with no movement or behaviour. **No animal sounds.**

## §8. LIGHTING AND ENVIRONMENT (spec §8)
- Day 60 s / dusk 8 s / night 75 s. Day: sun 1.0 warm, ambient 0.55, fog 0.004. Night: moonlight 0.5 cool, ambient (0.40,0.41,0.48), fog Exp2 0.03. Two-colour warm-lamp vs cool-moon lighting is implemented (6 point lights + 1 directional + torch spot).
- **Moon — dark disc: root cause determined from code.** (1) Fog swallows it: moon at (−28,26,−42), |pos| ≈ 56.8 m; night fog Exp2 0.03 → fog factor ≈ 0.945 — the disc is ~95% fogged toward near-black. (2) The fog-disable is a no-op: `BuildMoon` does `moonMaterial.SetFloat("_Fog", 0f)` + `DisableKeyword("_FOG")`, but the URP Lit shader has **no `_Fog` property**. (3) The Unlit branch is dead code: `DayNightCycle.SetMoon` checks `shader.name.Contains("Unlit")`, but the moon material is URP Lit (`MakeEmissive` → `GetStandardShader()`), so the branch never executes; only `_EmissionColor` (2.0) applies — 95% fogged and clipped by the LDR buffer (HDR off). The crater texture `moon_surface.png` is unused. **Proposed fix:** move the moon much closer, use a skybox-embedded moon, or a genuinely fog-immune path — NOT `SetFloat("_Fog", 0)`.
- Skybox/HDRI: `NightSkybox.mat` (moonless_golf_2k.hdr) wired; exposure animated by day/night. Ambient stays Flat (animated) by design. Reflection probe: one realtime probe, rendered once (128 cubemap) — good. Post: ACES tonemapping, Bloom 0.35, Vignette 0.28; HDR off; no SSAO. Lights: 8 realtime vs URP's 4-per-object cap — URP silently drops to the 4 nearest per object, so light pop-in/out as the player moves is expected.
- **No completely black characters in the latest screenshots** (monster, Ambassador, Pujari verified). Wolf coat reads dark in the minimal-scene shot (single light, no env map — judge on phone).

## §9. AUDIO AND HORROR ATMOSPHERE (spec §9)
- 17 `.ogg` clips (1.0 MB total) + 3 whispers. All referenced files exist; no call site references a missing file. **FIXED in this audit:** game-over sting on all 3 death paths (A1), dawn birds no longer over GameOver (A2). **Remaining:** no background music; no animal sounds; no NPC voices; no monster approach/chase sound (only the game-over roar); no surface-dependent footsteps (same 2 clips on mud/stone/wood); whisper plays at the **camera** position, not the door (`AudioManager.cs:388` + `DefaultPosition()` :197-201) — positional inconsistency vs the knock; 2D one-shots share one source (pitch mutation risk); footsteps depend entirely on `CharacterWalk.figureRoot != null` (silent if null); audio loads async with no load-complete flag — early sounds may be silently dropped. Licence/attribution: Kenney CC0 + Mixkit free-commercial-use, both documented; whispers are Aria AI voice (user-selected), corrected in the credits file.

## §10. GAMEPLAY AND INTERACTIONS (spec §10)
**Playable flow (as coded):** Day 60 s (collect optional pickups) → Dusk 8 s → Night 75 s (monster waits 6 s, walks to the door, knocks 3× with whisper subtitles, resolves) → Dawn (survive only if inside the house). **Verified in code:** door open/close, lock/barricade (auto-closes an open door; toggle blocked while locked), monster forces an unlocked door, torch on/off (no battery), pickups (no repeat collection — `Collected` guard), door shake on knock. **Does not exist:** health/damage, save/load, pause, inventory, missions, dialogue choices, torch battery, respawn/checkpoint. **Bugs:** B1 (fixed, re-test pending) makes the core loop unwinnable until verified; monster ignores collision while chasing (can path through walls to an indoor player); pickups have no effect beyond a counter (battery does not charge the torch; food/water/medicine do not heal — there is no health system); `TryInteract` priority is pickups → NPCs → door (a pickup within 2.8 m always beats the door/NPC); `Survived()` does not disable the player.

## §11. MOBILE UI AND CONTROLS (spec §11)
- **Verified:** 5 buttons round, icon-only, no text (Action 86px, Jump/Run 66px, Torch/Lock 60px); joystick 140px, radius 40, knob 64px; subtitle y=210 (clears joystick top 182 and RUN top 165); no button overlaps at 1280×720; no invisible button blocking input (decorative children `raycastTarget=false`); landscape locked twice (ProjectSettings + runtime); camera drag only on the right half + not on UI.
- **Issues:** no `SafeArea` handling anywhere with `androidRenderOutsideSafeArea: 1` — top-right Torch/Lock (32px from the corner) are at risk under notches; subtitle right edge leaves a **1px gap** to the RUN button at 16:9 — tight on other aspect ratios; no joystick dead zone (effective ~10% from `PlayerController`); no sensitivity curve; single-slot subtitle queue drops lines; RUN button has no click sound and can stick if the finger slides off without PointerExit; multitouch (joystick + RUN + camera drag) NOT TESTED; UI on tall/notched phones NOT TESTED.

## §12. LAG AND PERFORMANCE (spec §12)
### Ranked problems
| Rank | Problem | Location | Severity | Proposed fix |
|---|---|---|---|---|
| 1 | ~5.35M tris in view, no LOD, no occlusion, no instancing | Ring clones (36× mango/banyan/palm = 3.2M tris alone) | Critical | Decimate trees, add LODGroups, replace ring clones with low-poly impostors, GPU-instancing for crops |
| 2 | 187 MB embedded glTF textures, zero GPU compression | All `.glb`; `GltfModelLoader` has no texture handling | Critical | Downscale to ≤1K, convert to ASTC/ETC2 in a preprocess step (glTFast can't do it at runtime) |
| 3 | Android quality defaults to **High** (AA 4x, shadow High) | `QualitySettings.asset` (Android: 2) | High | Default Android to Medium; keep High as an in-game option |
| 4 | 460 MB APK, StreamingAssets stored uncompressed | APK | High | Size cut (#1/#2) + AAB with Play Asset Delivery; exceeds Play's 200 MB APK limit |
| 5 | `Renderer.material` clones per primitive break batching | `VillageBuilder.cs:646,660,674,741,844,2375` | High | Use `sharedMaterial` everywhere |
| 6 | `s_AliveImports` never disposed; no unload cleanup | `GltfModelLoader.cs:37,282,359` | Medium | Add `DisposeAll()` on scene unload |
| 7 | 8 realtime lights vs 4-per-object URP cap | VillageBuilder lights; `URP.asset` | Medium | Bake or cut to ≤4 point lights |
| 8 | Per-frame `Camera.main` in audio follow | `AudioManager.cs:392-397` | Low | Cache camera once |
| 9 | Per-frame `RenderSettings`/skybox/material writes | `DayNightCycle.cs:57,176-243` | Low | Apply only on change; cache the Unlit check |
| 10 | Per-frame linear collider scan (≤3×/frame while moving) | `PlayerController.cs:31` → `VillageBuilder.cs:31-48` | Low–Medium | Spatial hash / broadphase |
| 11 | No frame-rate cap set | No `targetFrameRate` anywhere | Low | Set `Application.targetFrameRate = 30` (or 60) |
| 12 | Post stack: Bloom 0.35 + ACES + opaque-texture copy | `PostProcessProfile.asset`, `URP.asset` | Low | Keep (look justifies it); revisit only if FPS short |
### Other measurements
- APK contents: `assets/Models/` ~374 MB (81%), `assets/bin/Data/` 96 MB, `lib/` 96 MB, audio 1 MB. Every StreamingAssets file is stored uncompressed (`compress=0`).
- Object count: 3,879 village objects. Draw calls: **NOT MEASURED** (no profiler capture) — estimate 500–900; SRP Batcher cannot help most of it (material clones + glTFast Shader Graph materials + dynamic batching OFF).
- `Assets/Resources/` 95.3 MB — `SmallGrave/` 35 MB (incl. a 15.1 MB normal map), `FallenPine/` 20 MB (2K maps), `Textures/` 34 MB. `Assets/Mixamo/` 109 MB (4× ~28 MB FBX) — the FBXs are NOT in the build (only the remapped `.anim` clips are loaded); the APK's `assets/bin/Data` (96 MB) matches `Resources/` (95.3 MB).
- Shadows: main-light shadowmap 2048, 2 cascades, distance 60, soft shadows on. Reflection probe: one-time cost only. No LOD groups, no baked occlusion data, no GPU instancing anywhere.
- **NOT TESTED (needs a real Android device):** FPS / frame time / GPU time, actual draw calls / SetPass / overdraw, runtime memory / VRAM / GC spikes, load time on device, thermal throttling, moto g85 specifics. The 8 headless OOM kills at `cam.Render()` are a **llvmpipe artifact on this VM, not evidence of device OOM** — do not cite them as device memory proof. Target SDK: `ProjectSettings` says Automatic (0), but the v1.0.1 build's aapt2 check confirmed targetSdk 34.

## §13. UNITY 6 AND LINUX CPU-ONLY WORKFLOW (spec §13)
- All inspection was CPU-friendly static analysis + log-only PlayMode tests (no camera rendering where OOM is proven). No heavy AI inference was run locally.
- **License event:** the VM restarted at 2026-11-07 23:23 UTC, wiping the Unity Personal license + CLI sign-in. `unity auth status` → "You are not signed in"; `unity license status` → "none active". **Re-activation needs the user's phone tap** (the CLI sign-in URL). Per the audit brief, Unity work stopped at that point — no further runs were attempted.
- **What this means for the report:** every fix in §6 is applied but **uncompiled and unrun**. The B1 fix changes collision for the whole game; it must be re-tested (doorway walk-through + wall-block sanity) before any APK is built from this working copy. Do NOT build an APK from the current working copy until the re-test passes.

## §14. AUTOMATED TESTING AND COMPLETE QA (spec §14)
- **Existing tests (3):** `ScreenshotTest` (scene load + frustum + UI + Nilesh + 2 wide screenshots — wide renders are OOM-proven, so its screenshot steps cannot pass on this machine), `RenderDiagTest`, `UrpMatDiagTest`.
- **Audit test (1, retained):** `Assets/Tests/AuditLogicTest.cs` — log-only checks for B1, walk speed, overlaps, moon diagnostics. Run 1 confirmed B1 and aborted at the assert; the file was fixed to log-and-continue, but the re-run is blocked by the license.
- **Tests NOT created/run:** full gameplay route (day→night→dawn), monster-chase catch test, multitouch, save/load (feature does not exist), long-duration soak test, Android build validation (not authorized in this audit).
- **Rule compliance:** no test result is faked; every untested item is marked NOT TESTED above; no working feature was removed to hide an error; no realistic asset was replaced with a cartoon asset.

## §15. RE-TEST CHECKLIST (for after license re-activation — in order)
1. `unity auth status` → Signed in as Namsate India; license re-activated.
2. Run `AuditLogicTest` (`-testFilter AuditLogicTest`): B1 doorway walk-through must end INSIDE the house (x < −6.0, |z−6| ≤ 3); walk speed must be re-measured; overlap bounds (O1/O2/O8) and moon diagnostics must be logged.
3. Wall-block sanity: the player must still be blocked by house walls and the closed door (B1 fix must not let the player walk through walls).
4. Visual close-ups (640×360 xvfb, 480×270 captures): banyan (F1), Mahadev+Nandi at night (F2), tractor + bullcart (F3), GroundMud tiling, crop cards, O1 wolf/chicken, O2 gate ring tree. Open each screenshot personally; mtimes must be from the run.
5. Game-over sting on all 3 death paths (A1) — chase catch, door forced open, dawn-outside.
6. Dawn birds only on survival (A2).
7. Then — and only then — consider the next APK build (user authorization required per the audit spec; the morning v1.0.1 was built from the pre-audit code and does NOT contain the B1 fix).

---

## FINAL JUDGEMENT
The game is **substantially improved** since the 2026-11-07 evening state (14 replacement models landed, URP pipeline completed, lighting + crops + Pujari + wolf + Mixamo walk verified), and the v1.0.1 APK is a real, playable build. **But it is not yet a shippable game, and one defect is game-breaking: B1 — the player cannot enter their own house, so the core survive-the-night loop is unwinnable.** The B1 fix is applied but untested. Beyond B1, the biggest risks are performance (5.35M tris, no LOD, High quality default, 460 MB APK) and the monster (too slow to ever catch the player, walks through walls, no NavMesh). The moon dark disc has a determined root cause (fog + no-op `_Fog` + dead Unlit branch) and needs a different approach, not another material tweak. **Do not build a new APK from this working copy until the §15 re-test passes.**
