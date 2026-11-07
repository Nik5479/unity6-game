# THE FORGOTTEN VILLAGE — Asset Audit (2026-11-07)

**Scope:** every 3D model in the village scene, inspected from the actual
project files — no changes made, nothing downloaded, no build, no push.
**Method:** Python glTF parser over all 44 `.glb` + 4 `.gltf` bundles in
`Assets/StreamingAssets/Models/` (mesh/tri counts, rig/animation, embedded
image stats, per-material PBR map coverage: baseColor, metallic-roughness,
normal, occlusion, emissive, alpha mode, KHR extensions); full read of
`Assets/Scripts/VillageBuilder.cs` (1,943 lines) for placements and
primitive-built elements; scene file `Assets/Scenes/Main.unity`; runtime
materials in `Assets/Resources/Materials/` + `Textures/`; the two latest
screenshots (`TestScreenshots/main-scene.png`,
`main-scene-angle2.png`, 20:35 IST); `~/workspace/polyhaven/`.

## ⚠️ CURRENT-STATE ALERT — read this first

**The project on disk is NOT on Built-in RP any more.** The URP migration
that was "cancelled" had already landed before the stop:

- `Packages/manifest.json` contains `"com.unity.render-pipelines.universal": "17.3.0"`.
- `Assets/Settings/URP.asset` + `URP-Renderer.asset` exist; `ProjectSettings/GraphicsSettings.asset`
  `m_CustomRenderPipeline` points at the URP asset (guid `441bec65…` — matches
  `URP.asset.meta`). URP 17.3.0 is the **active** render pipeline.
- `Assets/Editor/UrpSetup.cs` (one-shot migration script) ran: it created the
  URP assets, assigned them, and converted the 6 `.mat` materials to URP Lit.
- `Assets/Tests/UrpMatDiagTest.cs` exists; logs `unity-urp*.log` (14:56–15:08 UTC).

**The latest screenshots (20:35 IST, from the URP run) show models rendering
PURE BLACK:** the player's thatched hut, the palm trees, the horse, and
several ring trees are black silhouettes. Nilesh, Mukhiya, haveli, school and
temple still render textured. This is a **pipeline/material defect, not a
model-quality defect** — most likely the known glTF metallic problem
(`GltfModelLoader.cs` itself warns: under URP with no skybox/reflection
probe, glTF materials whose `metallicFactor` defaulted to 1.0 render black).
**Do not judge the black models' quality from these screenshots, and do not
start replacing models until the pipeline state is decided: finish the URP
migration properly (skybox/reflection probe + material audit under URP) or
revert to Built-in.** The verdicts below are about model quality, measured
from the files — not from the black screenshots.

**VillageBuilder.cs state after the URP coordinator's edit:** braces balanced
(179/179), parens balanced (1119/1119), zero TODO/FIXME markers. The visible
edit is a URP-tolerant shader lookup (`GetStandardShader()` tries
`Universal Render Pipeline/Lit` first, falls back to `Standard`) plus URP
transparency properties in `MakeTransparent()` — coherent and complete, not a
half-written edit. No broken code found in the file.

## Headline numbers

- **48 model records audited:** 44 `.glb` + 4 `.gltf` bundles. **All 44 `.glb`
  are placed in the scene** (none unused). Plus 2 FBX under `Assets/Resources/`
  (FallenPine, SmallGrave).
- **Total geometry: 2,343,735 triangles** across all models. Embedded
  textures: **140.6 MB**.
- **PBR coverage is the core problem:** 13 files have **0% normal maps**,
  18 files have **0% metallic-roughness maps**, 35 files have **0% AO maps**.
  Only 13 of 48 files have any AO map at all. **11 files have ZERO normal
  AND ZERO metallic-roughness maps** (albedo-only): bullock cart, Angkor,
  banyan, dock, horse, thatched-pointed hut, famous house, palms, rusty car,
  shiba, tractor.
- **Runtime materials are albedo-only:** the 6 `.mat` assets
  (GroundMud, WallPlaster, WoodPlanks, RoofTiles, StoneWall, FieldGrass) have
  a base-colour texture and **no bump/normal, no metallic-gloss, no occlusion
  map** (verified in `GroundMud.mat`: `_BumpMap`, `_MetallicGlossMap`,
  `_OcclusionMap` all empty). Every `MakeMat`/`MakeFlat` material built at
  runtime is a flat colour + smoothness value. The Poly Haven textures copied
  into `Assets/Resources/Textures/` are **diffuse-only** (`*_diff_1k.jpg`) —
  the matching normal/roughness/AO maps were never downloaded.
- **Scene:** no skybox (`m_SkyboxMaterial: {fileID: 0}`), camera clear colour
  is a flat solid blue (0.53, 0.81, 0.98), fog exponential-squared 0.004,
  flat grey ambient. No HDRI wired in (the moonless HDRI sits unused on disk).

## Verdict counts (48 model records)

| Verdict | Count |
|---|---|
| KEEP — realistic enough as-is | **15** |
| REPLACE — low-poly / stylised / wrong / broken | **15** |
| BORDERLINE — usable but weak, needs maps/decimation | **18** |

Plus **every primitive-built element is REPLACE** (see §3) and the whole
runtime material system is albedo-only (see §4).

## §1 — Model table

`Nrm/MR/AO` = % of the file's materials carrying a normal map /
metallic-roughness map / occlusion map. `BC` = % with a base-colour texture.

### Houses (6 — user ordered every house different; that part is met)

| Element | File | Tris | Imgs | Nrm/MR/AO | Verdict | Replacement needed |
|---|---|---|---|---|---|---|
| House_Player (Nilesh's hut) | `thatched_hut_4_p31g.glb` | 161,142 | 4 | 33/33/0 | **BORDERLINE** | Good geometry, weak PBR (only 1 of 3 mats has normal). Needs normal+roughness maps on all mats. Renders BLACK in the latest URP screenshot — fix the pipeline/metallic issue first; do not replace on the black render alone. |
| House_2 | `village_house_for_game_20_lafr.glb` | 4,100 | 2 | 0/100/0 | **REPLACE** | 4.1k tris for a whole house = low-poly box with a texture. Realistic Indian village house, plaster/brick, tiled roof, ~30–80k tris, full PBR. |
| House_3 | `village_hut_house_21_tyn9.glb` | 309 | 3 | 100/0/0 | **REPLACE** | 309 triangles = a textured box. Realistic mud/thatch hut, ~20–50k tris, full PBR. |
| House_4 | `hut_with_thatched_pointed_roof_22_96py.glb` | 10,078 | 2 | 0/0/0 | **REPLACE** | No normal, no MR, spec-gloss workflow; thatch renders flat white/grey. Realistic thatched hut with layered thatch, ~20–50k tris, full PBR. |
| House_5 | `old_village_house_-_indian_game_asset_24_bac7.glb` | 1,119 | 3 | 100/100/100 | **BORDERLINE** | Full PBR maps — but only 1,119 tris. The textures carry it; at close range it is a flat box. Keep if nothing better; a higher-poly version of the same style is the ideal replacement. |
| House_6 | `indian_village_house_famous_3d_model_37_4zjm.glb` | 18,910 | 1 | 0/0/0 | **BORDERLINE** | OK geometry, but ONE albedo texture and no normal/roughness/AO — flat lighting response. Needs normal+roughness maps at minimum; prefer a better model. |

### Landmarks

| Element | File | Tris | Imgs | Nrm/MR/AO | Verdict | Replacement needed |
|---|---|---|---|---|---|---|
| Haveli | `meshy_haveli_86_hvl.glb` | 531,462 | 3 | 100/100/0 | **BORDERLINE** | Best PBR in the project (full normal+MR) and it looks right — but **531k tris in ONE mesh = 23% of the entire scene's triangles**. KEEP the look, DECIMATE to ~100–150k tris. Also note: Meshy free tier = personal/evaluation licence only; commercial release needs a paid plan. |
| Temple | `hindu_temple_88_62er.glb` | 100,502 | 23 | 18/36/0 | **BORDERLINE** | Good structure, weak PBR (only 18% of mats have normals). Needs normal/roughness maps on most materials. Temple currently has NO shivling inside (defective file excluded; new Mahadev 5.9 MB + Nandi 13.9 MB sit in `~/workspace/user/files/`, not in the project — Nandi still needs decimation 441k → ~150k). |
| School | `school-building_74_yizq.glb` | 13,566 | 19 | 46/0/0 | **BORDERLINE** | OK geometry; half the mats have normals, none have MR/AO. Add MR/AO maps or replace with a better-detailed school. |
| Clinic | `kralls_eye_clinic_micthell_sd_87_ij5g.glb` | 149,968 | 46 | 12/17/0 | **REPLACE** | **724 meshes / 60 materials** — a fragmented CAD-style export (724 draw calls for one building), weak PBR. Clean realistic clinic/hospital, ~50–100k tris, <20 meshes, full PBR. |
| Angkor (abandoned temple) | `angkor_wat_temple_cambodia_89_pwb5.glb` | 134,993 | 1 | 0/0/0 | **REPLACE** | ONE flat texture across 135k tris, no PBR maps at all; also the wrong style (Cambodian) for an Indian village. Realistic ruined Indian temple/shrine with full PBR — or drop it. |
| Well | `old_well_with_hanging_bucket_2_qm9u.glb` | 7,286 | 3 | 0/100/0 | **BORDERLINE** | Low-ish geometry, no normal map; reads OK at distance. Needs a normal map, or a better-detailed stone well with rope/bucket. |
| Gate | `ancient_gate_11_4q5m.glb` | 4,546 | 3 | 100/100/100 | **KEEP** | The only file with 100% on all three PBR maps. Low tris but it is a gate/arch — acceptable. |

### Trees (the most visible cartoon element — the ring clones 3 models ~36×)

| Element | File | Tris | Imgs | Nrm/MR/AO | Verdict | Replacement needed |
|---|---|---|---|---|---|---|
| Mango | `mango_tree_3_sl11.glb` | 130,451 | 4 | 33/0/0 | **BORDERLINE** | High geometry, weak PBR; canopy reads as a round dense ball. Keep for now; a better mango needs branch structure + leaf cards with alpha. |
| Banyan | `banyan_tree_001_26_fyf3.glb` | 3,236 | 3 | 0/0/0 | **REPLACE** | 3.2k tris for a banyan = very low-poly; 31 materials for 3k tris is wasteful; also ships a 35 m disc + 34–40 m cylinders that the code has to disable. Realistic banyan with aerial roots, branch detail, leaf cards, ~50–150k tris, full PBR. |
| Palms | `palm_trees_27_x7l3.glb` | 2,857 | 7 | 0/0/0 | **REPLACE** | 2.9k tris for a palm cluster; fronds are flat cards with no normal maps; renders BLACK in the latest URP screenshot. Realistic date/coconut palms with detailed fronds, ~20–60k tris, full PBR. |
| Tree pack | `trees_25_wae0.glb` | 9,858 | 12 | 100/0/0 | **BORDERLINE** | Has normals; spec-gloss workflow; 9.8k tris for a 6-tree pack. Keep as background filler; hero trees need better models. |
| Chinaberry ("neem") ×2 | `realistic_hd_chinaberry_tree_1330_83_1wiy.glb` | 221,798 | 25 | 86/0/0 | **KEEP** | The best tree in the project — realistic HD, 86% normal coverage. BUT 222k tris × 2 instances = 444k tris. Keep the model, optimise the count (one instance, or decimate). |
| Fallen pine (FBX) | `Resources/FallenPine/FallenPine.fbx` | — (not measured, FBX) | — | — | **BORDERLINE** | Native FBX prop; visually a fallen trunk. Measure + check PBR before judging; low priority. |

### Animals

| Element | File | Tris | Imgs | Nrm/MR/AO | Verdict | Replacement needed |
|---|---|---|---|---|---|---|
| Cow | `cow_15_u1bj.glb` | 4,898 | 3 | 100/100/0 | **BORDERLINE** | Low-poly (4.9k) but full normal+MR; OK at distance, blocky up close. Realistic Indian cow ~20–40k tris if available. |
| Goat | `goat_17_a8s1.glb` | 1,574 | 3 | 100/100/0 | **REPLACE** | 1.5k tris = a toy goat. Realistic goat ~10–20k tris, full PBR. |
| Buffalo | `water_buffalo_18_44fy.glb` | 19,659 | 3 | 100/100/0 | **KEEP** | Best animal in the project — 19.7k tris, full normal+MR. |
| Horse | `horse_riggedgame_ready_16_2abc.glb` | 2,914 | 1 | 0/0/0 | **REPLACE** | 2.9k tris, single flat texture, no PBR; renders BLACK in the latest URP screenshot. Realistic horse ~30–60k tris, full PBR (rigged is a bonus, not required). |
| Dog (Shiba Inu) | `shiba_19_54zt.glb` | 4,316 | 1 | 0/0/0 | **REPLACE** | Wrong breed for an Indian village AND low-poly with no PBR. Realistic Indian pariah/desi dog ~10–20k tris, full PBR. |
| Chicken | `chicken_73_eoja.glb` | 20,000 | 3 | 100/100/0 | **KEEP** | Decimated 800k → 20k, full normal+MR. Fine. |

### Vehicles & props

| Element | File | Tris | Imgs | Nrm/MR/AO | Verdict | Replacement needed |
|---|---|---|---|---|---|---|
| Tractor | `tractor_12_35hh.glb` | 1,917 | 1 | 0/0/0 | **REPLACE** | 1.9k tris, single flat texture. Realistic Indian tractor (Mahindra/Swaraj style) ~30–60k tris, full PBR. |
| Hero's car | `rusty_old_car_3d_model_vintage_vehicle_low_poly_41_zcgz.glb` | 10,000 | 1 | 0/0/0 | **REPLACE** | Filename says low-poly; no PBR. Story-relevant (hero's broken-down car) — needs a realistic old car (Ambassador-style) with rust PBR, ~40–80k tris. |
| Bullock cart | `an_old_bullock_cart_14_bxqa.glb` | 5,164 | 0 | 0/0/0 | **REPLACE** | 83 meshes with ZERO textures (flat colours only). Realistic wooden bullock cart with wood PBR. |
| Wooden cart | `medieval_wooden_cart_1_jab4.glb` | 11,388 | 3 | 100/100/100 | **KEEP** | Full PBR, decent geometry. Note: 3rd embedded PNG is corrupt/truncated (pre-existing; glTFast tolerates it, texture may render glitched). |
| Handpump | `handpump_3d_39_aah5.glb` | 30,432 | 3 | 100/100/0 | **KEEP** | Good geometry + full normal/MR. |
| Matka (clay pot) | `indian_terracotta_matka_red_clay_water_pot_40_ris7.glb` | 12,160 | 4 | 100/100/100 | **KEEP** | Full PBR. |
| Oil barrel | `rusty_and_oil_stained_oil_barrel_43_s99h.glb` | 812 | 3 | 100/100/100 | **KEEP** | 812 tris is fine for a barrel; full PBR. |
| Hospital bed | `old_and_worn_out_hospital_bed_42_kn6o.glb` | 526 | 9 | 100/100/100 | **BORDERLINE** | Full PBR but 526 tris is very low; it sits inside the house and is barely visible. Keep; low priority. |
| Basket | `historic_miscellaneous_basket_wicker_old_03_xdwjadj_mid_51_y3o8.glb` | 3,784 | 3 | 100/100/100 | **KEEP** | Full PBR. |
| Wooden stand | `historic_miscellaneous_stand_wood_old_05_xeokah0_mid_50_69zs.glb` | 5,008 | 3 | 100/100/100 | **KEEP** | Full PBR. |
| Sandstone ground | `sandstone_rocky_ground_vd3oeic_mid_59_23jb.glb` | 4,373 | 3 | 100/100/100 | **KEEP** | Full PBR rocky patch. |
| Grave stones | `grave_stone_collection_64_7j05.glb` | 1,116 | 3 | 100/100/100 | **BORDERLINE** | Full PBR but only 1,116 tris for a grave set — simple stones. Keep for now; a better-detailed set would help the shamshan. |
| Woodpile (pyre) | `woodpile_game_ready_asset_91_qdgo.glb` | 3,440 | 3 | 100/100/100 | **KEEP** | Full PBR. |
| Fire place | `small_fire_place_90_23ou.glb` | 3,932 | 4 | 100/100/0 | **KEEP** | Fine for a fire pit. |
| Dock/pier | `dock_pier_78_n66e.glb` | 123,942 | 9 | 0/0/0 | **REPLACE** | 124k tris with NO PBR maps on any of 29 materials — and there is no river in the scene ("no river mesh yet — it reads as the pier at the water's edge"), so the pier floats on dirt. Either a proper PBR pier + real water, or drop the dock. |
| Soil ground tile | `SoilGround/xdhhdhl_tier_2.gltf` | 2 | 3 | 100/100/100 | **BORDERLINE** | A single flat quad (2 tris) with a full-PBR texture — a texture tile, not geometry. Fine as a ground patch; it is not a ground replacement. |
| Forest floor tile | `ForestFloor/sfjmafua_tier_2.gltf` | 2 | 3 | 100/100/100 | **BORDERLINE** | Same as soil — a flat textured quad. |
| Diya lantern | `brass_diya_lantern/brass_diya_lantern_1k.gltf` | 12,742 | 4 | 100/100/0 | **KEEP** | Full normal+MR, detailed. |
| Chowk lantern | `Lantern_01/Lantern_01_1k.gltf` | 33,902 | 4 | 100/100/0 | **KEEP** | Full normal+MR, detailed. |
| Small grave (FBX) | `Resources/SmallGrave/SmallGrave.fbx` | — (not measured, FBX) | — | — | **BORDERLINE** | Flat-slab grave, normalised to 2.2 m in code. Measure + check PBR before judging; low priority. |

### Characters

| Element | File | Tris | Rigged | Anim | Nrm/MR | Verdict | Replacement needed |
|---|---|---|---|---|---|---|---|
| Nilesh (hero) | `indian_man_77_hwkm.glb` | 55,660 | yes (1 skin, 104 nodes) | yes (`mixamo.com`, 10.6 s) | 75/0 | **BORDERLINE** | Rigged + animated + decent tris; no MR maps; face/skin is "realistic-ish" scan-style. Keep for now — best human available. A higher-fidelity model would be better, but MetaHuman (Unreal-only) cannot be used in Unity. Needs MR maps + better skin shading. |
| Villager_Man | `indian_man__sunglasses_76_gmed.glb` | 54,287 | yes | yes (`mixamo.com`) | 69/8 | **BORDERLINE** | Same model family as Nilesh; sunglasses on a villager at night in a horror village is a stylistic mismatch. Keep as a villager, or replace with a plain-clothed villager. |
| Meera | `indian_woman_in_saree_84_8ssr.glb` | 117,595 | yes (102 nodes) | yes (`mixamo.com`, 10.0 s) | 73/0 | **BORDERLINE** | High tris, good normals, no MR. Saree model is decent. Keep; add MR maps. |
| Mukhiya | `meshy_mukhiya_87_mky.glb` | 151,064 | **no** (1 node) | none | 100/100 | **BORDERLINE** | Static statue — cannot animate or turn; 151k tris for a static NPC. Keep as a static NPC; a rigged elder would be better. Meshy free-tier licence flag (see Haveli). |
| Pujari | `pujari-mahant-guru_72_b1y1.glb` | 140,945 | **no** (8 nodes) | none | 67/0 | **REPLACE** | 141k tris, no rig — and the code itself notes "his face is flat grey with no facial texture". A priest with an untextured grey face cannot be photorealistic. Realistic rigged priest with a textured face, ~50–100k tris, full PBR. |

### Controls (not 3D assets — confirmed fine, no action)

`MobileControls.cs` + `ControlIcons.cs`: all 5 buttons are round,
semi-transparent, icon-centred (lock, flame, running figure, up-chevron,
hand), **zero text words**; joystick is a round base + round knob. Confirmed
in both screenshots. No replacement needed.

## §2 — REPLACE list (15 models)

1. House_2 (`village_house_for_game`, 4.1k tris) — realistic Indian house.
2. House_3 (`village_hut_house`, 309 tris) — realistic mud/thatch hut.
3. House_4 (`hut_with_thatched_pointed_roof`, 10k tris, no PBR) — realistic thatched hut.
4. Clinic (724 meshes, weak PBR) — clean realistic clinic.
5. Angkor (135k tris, 1 flat texture, wrong style) — realistic ruined Indian shrine, or drop.
6. Banyan (3.2k tris, no PBR) — realistic banyan with aerial roots.
7. Palms (2.9k tris, no PBR) — realistic palms with detailed fronds.
8. Goat (1.5k tris) — realistic goat.
9. Horse (2.9k tris, no PBR, black in URP shot) — realistic horse.
10. Dog / Shiba Inu (4.3k tris, no PBR, wrong breed) — realistic Indian pariah dog.
11. Tractor (1.9k tris, no PBR) — realistic Indian tractor.
12. Hero's car (explicitly low-poly, no PBR) — realistic old Ambassador-style car with rust.
13. Bullock cart (83 meshes, zero textures) — realistic wooden bullock cart.
14. Dock (124k tris, no PBR, no river) — PBR pier + real water, or drop.
15. Pujari (141k tris, no rig, flat grey untextured face) — realistic rigged priest.

## §3 — Primitive-built elements (ALL REPLACE — the user forbids primitive replacements)

These are built in code from `Box()` / `Cylinder()` / `Sphere()` /
`Capsule()` / flat quads. None of them can look photorealistic; each needs a
real model. (Fallbacks marked † only appear if a `.glb` fails to load — but
they are still cartoon when they appear.)

| Element (code location) | What it is now | Realistic replacement needed |
|---|---|---|
| Ground (`Main.unity` "Ground" object, 50×50 m plane + `GroundMud.mat`) | One flat plane, albedo-only mud texture at 14×14 tiling (minified to flat brown) | Real terrain or a sculpted ground mesh with blended PBR soil/grass/dirt (albedo + normal + roughness + AO), undulation, no single flat colour |
| Dirt paths (`BuildGround`, 11 flat boxes) | Flat 2.4×0.04×3.4 m boxes with mud texture | Dirt path meshes / terrain splat / decal paths with tyre/foot wear |
| Chowk disc (`BuildGround`, 9 m cylinder, 0.04 thick) | Flat cylinder with cobble texture | Real circular cobblestone paving mesh, individual stones, PBR + normal/roughness |
| Grass patches (`BuildGround`, 16 + 34 flat boxes) | Flat rectangles lying on the ground with grass/leaf/mud textures — read as flat rectangles in screenshots | 3D grass tufts (modelled blades or crossed alpha cards), leaf-litter meshes, dirt variation meshes |
| Crop field (`BuildField`, 10×6 m box + 36 small dark-green boxes) | A flat green slab with 36 green dice on it | Realistic crop rows (wheat/mustard plants with leaves, soil furrows) |
| Fences (`BuildFenceSegment`, 9 segments × 2 box posts + 2 box rails) | 36 flat wood-coloured boxes | Realistic weathered wooden fence models (varied posts, rails, PBR wood) |
| Chowk lamp (`BuildWellAndLampAsync`) | Cylinder pole + box head + sphere bulb + glow quad | Realistic cast-iron lamp/lantern post model with glass + emissive |
| Door, player house (`BuildDoor`) | Box panel + sphere handle | Realistic wooden plank door (iron hinges, handle, PBR wood) |
| House interior (`BuildHouseInterior`) | Box table + box leg, cylinder clay pot, cylinder diya | Realistic wooden table, clay pot (the matka model exists — use it), brass diya (the diya-lantern `.gltf` exists — use it) |
| Pickups ×4 (`BuildWellAndLampAsync`, `MakePickup`) | Wooden box crates | Realistic items: food sack, water pot/bottle, medicine box, battery/torch |
| Monster (`BuildMonster`) | Capsule body + sphere head + sphere glowing eyes + box arms, flat black — a black capsule-man | **A realistic horror entity (rigged, dark robes/rags, PBR) — the most important character replacement for a horror game** |
| Moon (`BuildMoon`) | 6 m sphere + glow quad | Textured moon in a real skybox/HDRI (low priority — it is in the sky) |
| Fallback house † (`BuildHouse`) | Fully primitive box house (walls, roof panels, windows, frames all boxes) | A simple real house model as fallback — or no fallback (empty spot beats a cartoon house) |
| Fallback player figure † (`BuildPlayerFigure`) | Box torso, sphere head/pagdi/hands, cylinder band, box limbs | Hide the player on load failure (or a plain capsule) — never a cartoon figure |
| Fallback well † (`BuildWellAndLampAsync`) | Cylinder well + box roof + box beams | Same as fallback house — a real well model or nothing |
| Glow quads (lamp/moon) | 2D billboard quads | Keep as light effects (not environment geometry) — acceptable |

## §4 — Materials & lighting (systemic, not per-model)

- **All runtime materials are albedo-only.** No normal, roughness, metallic
  or AO maps anywhere in the runtime material path. This alone caps the
  visuals at "flat game" no matter how good the models are.
- **Poly Haven textures on disk are diffuse-only.** The 10× 1K JPGs are
  `*_diff_1k.jpg`. Poly Haven offers the matching normal/roughness/AO maps
  free (CC0, no login) — they were never downloaded. Downloading them is the
  cheapest realism win in the whole project.
- **No skybox, no HDRI, no reflection probe.** The moonless HDRI
  (`moonless_golf_2k.hdr`, 6.4 MB) sits unused on disk. The scene sky is a
  flat solid blue clear colour; ambient is flat grey. Under URP this also
  causes the black-metallic rendering defect (see the alert at the top).
- **Sun shadows are on** (soft, 0.9 strength) and fog is set — these are the
  only atmosphere systems in place. No post-processing, no bloom, no AO, no
  colour grading, no vignette (Built-in RP has none of this wired in).

## §5 — Poly Haven: what is actually usable

- **Usable now:** 10× 1K diffuse textures (brick, thatch, clay wall, mud,
  bark, cobblestone, withered grass, clay roof tiles, mud leaves, reed roof);
  the moonless HDRI (skybox + environment lighting); the 2 lantern `.gltf`
  bundles (already in the project).
- **Usable after a free download (no login, CC0):** the matching
  normal/roughness/AO maps for those 10 textures — the single cheapest
  upgrade available.
- **NOT available from Poly Haven:** trees, houses, characters, animals,
  vehicles, props. Poly Haven has no such models. Every replacement model
  must come from Sketchfab/Fab (user downloads with his sign-in, as before)
  or another model source.

## §6 — Sourcing note (honest)

- **Poly Haven (free, no login):** textures + HDRI only. Normal/roughness/AO
  maps for the 10 textures can be downloaded free right now.
- **Sketchfab / Fab (user's sign-in, as before):** nearly every REPLACE item
  (houses, trees, animals, vehicles, characters, clinic, dock) must be
  downloaded by the user — the assistant cannot sign in for him. Each file
  must be verified on arrival (glTF v2, <~500k tris for phone use,
  non-commercial licences rejected, no low-poly/stylised files).
- **Meshy (blocked):** haveli + mukhiya came from Meshy free tier
  (personal/evaluation licence only — a paid plan is needed before any
  commercial release) and the free download allowance is exhausted, so no
  new Meshy models can be fetched without a subscription. Do not plan
  replacements around Meshy.
- **Not yet in the project (queued, user already sent):** Mahadev shivling
  (5.9 MB, clean) + Nandi bull (13.9 MB, 441k tris — needs decimation to
  ~150k first). The temple currently has no shivling.

## §7 — Replacement priority (biggest visual win first)

1. **Decide the pipeline first** — finish URP properly (skybox/reflection
   probe + URP material pass) or revert to Built-in. Until then, every
   screenshot is suspect (black models).
2. **Ground + paths + chowk** — the largest surface in every screenshot; a
   real PBR terrain/ground with normal/roughness maps changes the whole look.
3. **Trees** — banyan + palms + mango are the skyline; realistic trees with
   branches/leaves kill the "round green ball" look. (Chinaberry is already
   good — keep it.)
4. **Houses 2, 3, 4** — the three lowest-poly houses (309 / 1,119 / 4,100 /
   10,078 tris) are the most obviously fake buildings.
5. **Monster** — a black capsule-man is the horror game's main threat; a
   realistic entity is essential.
6. **PBR maps everywhere** — download the Poly Haven normal/roughness/AO
   maps (free) and add normal+MR maps to the BORDERLINE files; this lifts
   every KEEP/BORDERLINE model without replacing any of them.
7. **Animals + vehicles** (goat, horse, dog, tractor, car, bullock cart) —
   small, visible, and all low-poly/no-PBR.
8. **Clinic + Angkor + dock** — big, fragmented or wrong-style; replace or
   drop.
9. **Haveli decimation** (531k → ~100–150k tris) and chinaberry count
   (2× 222k tris) — performance, not looks.
10. **Pujari's face** — a grey untextured face on a 141k-tri model.

## §8 — What NOT to do (per the user's order)

- Do not create replacement objects from cubes, spheres or cylinders.
- Do not "increase graphics settings" and claim realism.
- Do not touch gameplay, scripts, animations, controls or scene
  functionality during replacement (the replacement swaps model files and
  the placement calls that reference them; nothing else).
- Do not claim completion until in-game screenshots show a major visual
  improvement.

## Appendix — raw audit data

Full per-file measurements (meshes, tris, images, PBR coverage, rig/anim)
are in the audit JSON produced for this report: `/tmp/audit.json`
(ephemeral — regenerate with `python3 /tmp/glb_audit.py
~/workspace/unity6-game-setup/Assets/StreamingAssets/Models/`).
Key totals: **2,343,735 tris**, **140.6 MB embedded textures**, 13 files with
0% normal maps, 18 with 0% MR maps, 35 with 0% AO maps.
