# THE FORGOTTEN VILLAGE — Verified Model Download Links (2026-11-07)

**Purpose:** every link below was researched and verified from the model page
before the user downloads anything — so no more wasted downloads of wrong
models. The user downloads each `.glb` himself from his phone (Sketchfab
sign-in, as before). **Nothing was downloaded by the assistant and no
project file was changed.**

**How verification was done:** 4 researchers checked ~40 candidate pages.
Every candidate was opened on its own model page (not trusted from search
snippets). A link is listed as VERIFIED only when the page itself showed:
downloadable ("Download Free"), a game-use licence (CC-BY / CC0 / Sketchfab
Standard), and a triangle count in (or near) the target range and under the
500k hard cap. Rejected candidates are listed per item so nobody re-suggests
them.

**Verification limits (read this — honest gaps):**
- Sketchfab page text usually does **not** list PBR maps by name, file size,
  or the download-format picker. So "PBR confirmed" below means the page
  proves textures exist / lists maps where stated; otherwise it says
  "PBR unconfirmed — check after download". After downloading, open the
  `.glb` and check for normal/roughness maps before using it (the same check
  the asset audit used).
- On Sketchfab, always pick **glTF** (or GLB) in the download dialog — the
  format list is not visible on the crawled pages, so confirm it on the
  download screen.
- The Palm primary link was verified by the researcher via Sketchfab's API
  (licence + tris confirmed); my own live page fetch of it timed out, so it
  is flagged below — tap it and confirm it opens before downloading.
- CC-BY models need the author credited (add to the project README).

**Score: 8 of 16 items have a verified primary link** (Bullock cart, Pujari,
Horse, Tractor, Hero's car, Banyan, Palms, Goat) **+ 1 unsure primary**
(Angkor/Bhumija — judgement call, see item 5). **6 items have NO good link:**
House_2, House_3, House_4, Clinic, Dog, Monster (Dock is a deliberate
DROP — no river in the scene).

---

## 1. House_2 — realistic Indian village house ❌ NO LINK

- **PRIMARY:** none found (verified search)
- **BACKUP:** none found
- **What was checked:** the only realistic Indian house found is 3M tris
  (reject); the only in-range free house is a Slovak/Soviet village house
  (CC-BY, 35.6k tris — European style, wrong region, reject):
  https://sketchfab.com/3d-models/ordinary-slovakslavic-village-house-35bb639c11a4496ea3b3061070101861
  An Indian mud hut (451.5k tris, author says "not good for gaming assets")
  was also rejected. A Fab Nepalese village house is $4.99 paid and ~300k
  faces — reject.
- **What to do:** this slot needs a new source — the user should look for a
  different realistic Indian house himself, or a 30–80k-tri house with full
  PBR from another free source. Do not download the Slovak house.

## 2. House_3 — realistic mud/thatch hut ⚠️ COMPROMISE CANDIDATE ONLY

- **PRIMARY:** none fully verified (no 20–50k-tri, full-PBR mud/thatch hut
  found free)
- **COMPROMISE (one slot only — can fill House_3 OR House_4, never both):**
  https://sketchfab.com/3d-models/thatched-hut-bbd820f6e49041b490eb08ac97abfda8
- **Title:** Thatched Hut — **Author:** stealth86
- **Licence:** CC-BY (confirmed on page — commercial game use allowed,
  credit required)
- **Tris:** 161.1k (vertices 96.9k) — 3× above the 20–50k target, under the
  500k cap. 3,100 downloads.
- **Formats:** "Download Free" confirmed; pick glTF in the download dialog.
- **PBR notes:** NOT confirmed — 4 textures, 3 materials; no
  normal/roughness/metallic/AO map list shown.
- **Fit note:** mud/clay/coconut/palm/thatch tags — the right materials for
  a mud-thatch hut, and the most-downloaded thatch hut found. It needs
  **decimation 161k → ~50k** and a PBR check in Unity before it can be
  approved. Unsure — accept only if you are willing to decimate.
- **BACKUP:** none found.

## 3. House_4 — realistic thatched hut, layered thatch ⚠️ NO FREE PRIMARY

- **PRIMARY:** none fully verified (free)
- **COMPROMISE:** the same stealth86 Thatched Hut as House_3 (link above) —
  it can fill House_3 OR House_4, never both (all houses must be different).
- **SPEC-PERFECT MATCH — PAID, do NOT download as free:**
  https://sketchfab.com/3d-models/wooden-thatch-hut-with-seamless-materials-3f35fdddb3444e58bb4addb61f8b44f0
- **Title:** Wooden Thatch Hut, with seamless materials — **Author:** Danylo
  Tyupa (@danzl0)
- **Tris:** 48.4k (in range) — **PBR: best evidence of any candidate**
  ("All materials have seamless PBR textures… Normal textures in OpenGL
  format", 2K base textures). Sketchfab Store "Buy Royalty Free" listing —
  paid, price not shown in the crawl. Flagged only in case the user wants to
  reconsider a small purchase; it is NOT an approved free download.
- **BACKUP:** none found (free).

## 4. Clinic — clean realistic clinic/hospital ❌ NO LINK

- **PRIMARY:** none found (verified)
- **BACKUP:** none found
- **What was checked:** the only in-range building found is "Low Poly
  Hospital" by mrarfeen (87.4k tris) — explicitly low-poly, licence/PBR/
  mesh-count all unverified — reject. Other hospitals: 149.6k stylised,
  597k, 693k + CC-BY-NC, 2.6M tris — all reject. Fab clinics are interiors
  and $29.99–$84.99 paid — reject.
- **What to do:** no realistic clinic exterior found free. A generic small
  single-storey building re-skinned as a clinic may be the realistic route,
  but no verified one was found to recommend. Do not download the Low Poly
  Hospital.

## 5. Angkor replacement — ruined Indian temple/shrine ⚠️ UNSURE PRIMARY (judgement call)

- **PRIMARY:**
  https://sketchfab.com/3d-models/free-3d-scan-12th-century-bhumija-shikara-da50bd1451e24c5f80c9e48f8bd5ff29
- **Title:** Free - 3D Scan - 12th Century Bhumija Shikara — **Author:**
  gputhige
- **Licence:** CC-BY (commercial game use allowed, credit the author)
- **Tris:** 150,004 (under the 500k cap, but the heaviest pick here — it is
  the one hero ruin, single instance)
- **Formats:** Sketchfab download; pick glTF.
- **PBR notes:** photogrammetry scan — **baked albedo texture only; do NOT
  expect normal/roughness/AO maps.** The albedo itself is richly detailed
  (carved stone, weathered grey), so it will not look flat like Angkor's
  single texture, but it is not a full PBR set.
- **Fit note:** a real 12th-century Hoysala shrine (Bhumija shikhara,
  Chennakeshava Temple, Belur) — genuinely old, weathered grey stone,
  carved doorway and deity reliefs. Unambiguously Indian and old.
  **Caveat: it is intact/weathered, not collapsed/ruined** — it reads as an
  old abandoned shrine at the jungle edge, not a ruin pile.
- **Why unsure:** after a genuine search, NO candidate was ruined + Indian
  + downloadable + game-licenced + full-PBR all at once. This one fixes the
  actual complaint (wrong Cambodian style) and is a real ancient Indian
  shrine; it trades away "collapsed" and full PBR.
- **BACKUP — Old Ruined Temple:**
  https://sketchfab.com/3d-models/old-ruined-temple-bb2b2cc8db1f4fb49917b0eb8cb83977
- **Title:** Old Ruined Temple — **Author:** Muhammet Gündel
- **Licence:** CC-BY | **Tris:** 20,026 | **Formats:** Sketchfab download,
  glTF
- **PBR notes:** modelled in Blender, **painted in Substance Painter** —
  the only temple candidate with a real PBR texturing workflow behind it;
  preview shows mossy grey stone and fallen blocks.
- **Fit note:** a genuinely ruined 3-tier open stone tower with scattered
  fallen stones — the best "abandoned ruin" look and the cheapest tris.
  **Caveat: the author tagged it "inka" — generic-ancient, with no Indian
  markers (no shikhara/dome).** Use it if "ruined" matters more than
  "Indian"; use the Bhumija if "Indian" matters more.

## 6. Banyan tree ✅ VERIFIED PRIMARY

- **PRIMARY:**
  https://sketchfab.com/3d-models/chinese-banyan-ficus-microcarpa-2a0dbcdf8f5d48f5ad79987c7a8170ce
- **Title:** Chinese Banyan (Ficus Microcarpa) — **Author:** Valery.Li
- **Licence:** CC-BY (confirmed on page)
- **Tris:** 111.4k (vertices 71k) — inside the 50–150k target
- **Formats:** Sketchfab download; pick glTF.
- **PBR notes:** detailed bark/leaf textures visible in the preview; author
  notes alpha-channel leaf materials (leaf cards). Normal/roughness maps
  not listed on the page — confirm in the .zip.
- **Fit note:** exactly the brief — a massive banyan with a dense canopy
  and dramatic hanging aerial roots reaching the ground, gnarled trunk.
  Species is *Ficus microcarpa* (not *F. benghalensis*), but the author
  states it is native to India/Sri Lanka, and visually it is a true banyan
  with aerial roots. No 35 m disc, no toy look.
- **BACKUP:** none found (every other banyan failed a hard check: download
  disabled, CC-BY-NC-ND, 2.3M tris paid, or 657k tris).

## 7. Palm trees ✅ VERIFIED PRIMARY

- **PRIMARY:**
  https://sketchfab.com/3d-models/realistic-palm-tree-free-39052ea764c945858449e699318efa53
- **Title:** Realistic Palm Tree Free — **Author:** NextSpring
- **Licence:** CC-BY | **Tris:** 27,152 (vertices 14,686) — inside the
  20–60k target
- **Formats:** Sketchfab download; pick glTF.
- **PBR notes:** tagged `realityscan`, `Game-ready`, `unity`; preview shows
  a single date palm with detailed feathery fronds, diamond-pattern bark
  trunk and dry lower fronds. Normal/roughness maps not listed on the page
  — confirm in the .zip.
- **Fit note:** a realistic date palm, single tree, correct scale for a
  village palm — a straight swap for the current 2.9k flat-card palms.
- **⚠️ Spot-check note:** my own live page fetch of this link timed out;
  the researcher verified it via Sketchfab's API (licence + tris
  confirmed). Tap it and confirm it opens before downloading.
- **BACKUP — Realistic Palm Tree 2 Free (same author, same licence):**
  https://sketchfab.com/3d-models/realistic-palm-tree-2-free-d05c8ffb6ec64a7184c4b673e21f7224
- **Tris:** 60,764 (764 over the 60k top — negligible)
- **Fit note:** a grove of ~25 coconut palms of varied heights/curves in
  one file — very good value if a palm cluster is wanted rather than one
  tree; place the file once and the grove is done.

## 8. Goat ✅ VERIFIED PRIMARY (with one flag)

- **PRIMARY:**
  https://sketchfab.com/3d-models/boer-goat-3d-model-free-d9a3eea388624b6cb6455ddee8866104
- **Title:** Boer Goat 3D Model Free — **Author:** rt699448 (iRahulRajput)
- **Licence:** CC-BY (confirmed on page)
- **Tris:** 10.4k (vertices 5.2k) — inside the 10–20k target
- **Formats:** Sketchfab download; pick glTF.
- **PBR notes:** tagged `photorealistic`, `realistic`; preview shows a
  detailed brown/white Boer goat with fur texture, curved horns, floppy
  ears and correct anatomy. Texture-map list not shown on the page —
  confirm in the .zip.
- **Fit note:** a realistic domestic farm goat, standing pose, correct
  proportions — a direct replacement for the 1.5k toy goat, and a breed
  widely kept in India.
- **⚠️ FLAG (found in my spot-check, not in the researcher's report):**
  the page carries a **"Generated with AI"** tag. The model is a
  downloadable Sketchfab model (not an AI-generation service), but if the
  user wants human-made models only, check the preview carefully and skip
  it if it looks off.
- **BACKUP:** none good found (the only other downloadable CC-BY goat is a
  wild Himalayan mountain goat — wrong animal for a village).

## 9. Horse ✅ VERIFIED PRIMARY

- **PRIMARY:**
  https://sketchfab.com/3d-models/horse-e62e8fae743b40b7b8c382ccca863725
- **Title:** Horse — **Author:** OrelvysRodriguez (@Maxdilan)
- **Licence:** CC-BY (game use allowed, attribution required)
- **Tris:** 70.5k (36.2k verts) — slightly above the 30–60k target, well
  under the 500k cap
- **Formats:** "Download Free" confirmed; pick glTF/GLB in the download
  dialog.
- **PBR notes:** NOT itemised on the page — author says "I modeled and
  textured this horse in Blender for my game", but no normal/roughness/
  metallic/AO list. Textures are included; full PBR maps unconfirmed.
- **Fit note:** human-made (not AI), made specifically for a game,
  realistic farm horse. Not rigged — no rig/animation mentioned.
- **Caveats:** 70.5k is ~10k over target; PBR maps unconfirmed; not rigged.
- **BACKUP — Horse 3D Model (FREE):**
  https://sketchfab.com/3d-models/horse-3d-model-free-8a3c6ad71e0d4b74b282713c7116d12e
- **Author:** Empire (@empire2ofearth) — **Licence:** CC-BY — **Tris:**
  42.3k (in range) — "Download Free" confirmed, 370 downloads. "Made and
  sculpted in Blender… free to use and also can be modified with texture."
  PBR maps likewise not itemised.

## 10. Dog (Indian pariah / desi) ❌ NO LINK

- **PRIMARY:** none. **BACKUP:** none.
- **What was checked:** "indian pariah dog", "desi dog", "street dog",
  "stray dog", "village dog", "mongrel", "indian dog", "realistic dog",
  and Fab dog listings. **No pariah-specific model exists on Sketchfab**
  as far as search can find.
- Realistic free dogs on Sketchfab are almost all WildMesh demos —
  **"personal use only" on Sketchfab, commercial only via paid Fab/
  Patreon** — rejected for a game. Other realistic dogs are Meshy
  AI-generated — rejected. Fab: every realistic dog opened is paid
  (Australian Shepherd $20.99, Yellow Labrador $2.99) — rejected.
- **What to do:** a realistic, free, game-licenced generic/pariah dog in
  10–20k tris with full PBR does not appear to exist on Sketchfab/Fab
  right now. Do NOT send the user any dog link yet. Options: accept a paid
  Fab dog (Labrador $2.99), commission/generate one, or buy the Fab
  version of a WildMesh demo.

## 11. Tractor ✅ VERIFIED PRIMARY

- **PRIMARY:**
  https://sketchfab.com/3d-models/classic-tractor-1f0ca4bb68924c6b84aa86cb91cacc9e
- **Title:** Classic Tractor — **Author:** Adam (@adamo88)
- **Licence:** CC-BY — **Tris:** 40.2k (36.5k verts) — in range
- **Formats:** "Download Free" confirmed; pick glTF/GLB in the download
  dialog.
- **PBR notes:** NOT itemised on the page — "Classic tractor with
  renovated parts (work in progress)". No texture-map list shown.
- **Fit note:** generic old/retro tractor — not Indian-branded, but old
  tractors worldwide share this shape (big rear wheels, exposed engine,
  exhaust stack), so it reads correctly in a village. The best verified
  tractor on Sketchfab. 773 downloads, published 2017.
- **Caveats:** PBR unconfirmed; not a Mahindra/Swaraj by name.
- **BACKUP (weak — use only if you accept the risk):**
  https://sketchfab.com/3d-models/swaraj-855-tractor-2c50f17e2d4d498a9334396197ec358c
- **Title:** swaraj 855 tractor — **Author:** Mrxxx (@sanjaysinghfazilka)
- **Licence:** CC-BY — **Tris:** 111.7k (above the 30–60k target, under
  500k) — "Download Free", 117 downloads, published Sep 2025.
- **⚠️ Tagged "Generated with AI" and "cartoon" on the page** — provenance
  and style risk; it IS a real Swaraj 855 by name, but its look/PBR cannot
  be vouched for. Only a fallback if the Classic Tractor is rejected.

## 12. Hero's car (Ambassador, rust) ✅ VERIFIED PRIMARY (with caveats)

- **PRIMARY:**
  https://sketchfab.com/3d-models/hindustan-ambassador-a84ef7126b2b43adb7b4b28a4d42e5fb
- **Title:** Hindustan Ambassador — **Author:** rSquare
- **Licence:** CC-BY — **Tris:** 12.3k (6.7k verts) — BELOW the 40–80k
  target, but the only verified downloadable Ambassador on Sketchfab
- **Formats:** "Download Free" confirmed; pick glTF/GLB in the download
  dialog.
- **PBR notes:** NOT itemised on the page; **no rust** — it is a clean
  finished old car, not a rusty wreck. Rigged for Unreal/driving.
- **Fit note:** the exact right car — a real Hindustan Ambassador, the
  iconic Indian car. Low tri count means it will need rust/weathering
  added in Unity (decals/texture work) to sell "broken-down". Still far
  better than the current low-poly no-PBR car because the shape and
  proportions are correct. 1,738 downloads, published Dec 2021.
- **Caveats:** 12.3k tris (below range); PBR unconfirmed; no rust out of
  the box.
- **BACKUP — Hindustan Ambassador Car (check BEFORE downloading):**
  https://sketchfab.com/3d-models/hindustan-ambassador-car-dfd2cd5c04c4442d9119a5ab0d71e483
- **Title:** Hindustan Ambassador Car — **Author:** Dharam Patel
  (@dharampatel)
- **Tris:** 111.4k — **FULL PBR explicitly listed on the page: Color,
  Normal, Roughness, Metallic, AO, Alpha** (Maya + Photoshop + Substance
  Painter), tags: india / game-asset / mobilegames / carinterior.
- **⚠️ BUT the page shows NO "Download Free" label and NO licence line**
  (confirmed in my spot-check), so it is NOT confirmed downloadable or
  what licence it carries. Do NOT download this blind — check on the phone
  whether a Download button + CC licence actually appear. If downloadable,
  this is the better car (real PBR, higher detail); if not, stick with
  rSquare.

## 13. Bullock cart ✅ VERIFIED PRIMARY

- **PRIMARY:**
  https://sketchfab.com/3d-models/bullcart-40d5b9123ce34801b337b5e84dd697f6
- **Title:** Bullcart — **Author:** harshitaggarwalh
- **Licence:** CC-BY (allows game use with credit)
- **Tris:** 106.9k (53.1k vertices)
- **Formats:** Sketchfab "Download Free" badge confirmed; pick glTF in the
  download dialog.
- **PBR notes:** page description confirms "rustic wooden textures" and
  structural detail of a traditional Indian cart; explicit
  normal/roughness/metallic map list is NOT exposed in the page text, so
  map-by-map could not be verified. This is a real textured model (not
  flat colours), unlike the current 83-mesh zero-texture one. File size
  not shown.
- **Rigged?** No — not needed for a cart (static prop).
- **Fit note:** explicitly "inspired by rural Indian transportation…
  vintage carts used for farming" — exactly the Bhairavpur setting, and
  106.9k tris is reasonable for a hero prop on phone.
- **BACKUP:** none found (verified — a Myanmar bullock cart was
  unverifiable, a Western farm cart was the wrong region, and a paid
  Indian bullock cart was rejected as paid).

## 14. Dock / pier ⛔ DROP IT — no river in the scene

- **PRIMARY:** none — do not download a pier. **BACKUP:** none.
- **What was checked:** "Rustic Wooden Dock" (no tris/licence shown —
  unverifiable), "DWP Fishing Peer 01" (2.7k tris, medieval, low-poly),
  "Old Wooden Pier" (page says outright "A low poly environmental
  piece"), "Broken Wooden Dock PS1" (822 tris, PS1-style), "Floating pool"
  (132.8k tris but a pool, not a pier), "Dock Pier" collection (no URL/
  tris/licence surfaced).
- **Verdict:** every free pier on Sketchfab is either low-poly/PS1-style,
  unverifiable, or the wrong object — and the scene has no river, so a
  pier would float on dry land. **Drop the dock entirely; spend the
  budget on the monster instead.**

## 15. Pujari (priest) ✅ VERIFIED PRIMARY (with two cautions)

- **PRIMARY:**
  https://sketchfab.com/3d-models/indian-man-in-dhoti-54b9e050d0d6424b848a04bec02a85d0
- **Title:** Indian Man In Dhoti — **Author:** anandmohan662
- **Licence:** CC-BY (allows game use with credit)
- **Tris:** 21.6k (11.2k verts) — below the 50–100k target, but for a
  mobile NPC this is a sensible, light count, not a defect; it is a full
  game character, not a low-poly caricature
- **Formats:** Sketchfab "Download Free" badge confirmed; pick glTF in the
  download dialog.
- **PBR notes:** page text does not list texture maps by name, so PBR
  map-by-map is NOT verifiable from text. It is a clothed, textured man
  model (dhoti/kurta/lungi tags), not a grey untextured statue.
- **Rigged?** **Yes** — page states twice: "Rigged model of indian
  characters… created in compatible for game." No animations listed.
- **Fit note:** Indian man in dhoti/kurta — the right dress for a village
  pujari; re-dress/recolour is far easier than fixing a grey face. 4,963
  downloads, published Feb 2024.
- **⚠️ Two cautions:**
  1. **Face texture NOT verifiable from page text** — look at the model
     preview on the page before downloading: if the face looks flat/grey
     in the viewer, skip it — that is the exact defect being avoided.
  2. **Author discrepancy:** an APK listing credits this same model to
     "rs.sandhu" while Sketchfab lists uploader anandmohan662 — possible
     re-upload. Licence is CC-BY either way, but credit the Sketchfab
     uploader.
- **BACKUP:** none found (verified — other priest candidates were 599.8k
  tris + Western + nude mesh, 384.7k GTA-style rip, 1.1k cartoon, or
  Western anatomy-sculpt).

## 16. Monster (horror entity) ❌ NO VERIFIED PRIMARY — still open

- **PRIMARY:** none — do not force any link below as the monster.
- **BACKUP (weak — does NOT meet the brief):**
  https://sketchfab.com/3d-models/the-dark-wraith-392342e94ee94c1b823a367a8ddcf9ee
- **Title:** The Dark Wraith — **Author:** davidkong — **Licence:** CC-BY
- **Tris:** 150.3k (157.2k verts), 7,370 downloads, 992 likes.
  Dark Souls/LOTR-inspired dark knight. **NOT rigged** (no rig mentioned
  anywhere on the page), it is a medieval **armoured knight, not a
  hooded/robed wraith**, and the 150k tris include a horse and Megascans
  ground — the figure alone would need separating. Use only if you accept
  a static, armoured figure, which is not the knocking/mimicking monster.
  Treat the monster as **still open** rather than downloading this.
- **What was checked and why each failed:**
  - "Abyssal Reaper – Rigged & Animated Demonic Wraith" — ideal
    description (rigged, 11 animations incl. Walk, PBR, dark wraith with
    scythe), but **paid $10.49–$14.99 on Fab**; the Sketchfab listing could
    not be verified (no model URL, tris, or licence obtained).
  - "Azareth – Demon of Forbidden Knowledge, Rigged" — Fab sells it from
    $9.99 with a "Free Version (Idle Animation)" — no Sketchfab model URL,
    tris, or licence obtained. Unverifiable.
  - "Silent Reaper – Wrapped Faceless Wraith" by Pigcraft — perfect horror
    look (faceless wrapped head, burial cloth, PBR), but **AI-assisted 3D
    generation workflow**, Fab lists it at **$9.99 paid**, and the
    Sketchfab page shows no tris, no licence, no rig info. Rejected.
  - "Grim Reaper Rigged with Scythe" by evgeniym — no direct model URL,
    tris, or licence surfaced. Unverifiable lead only.
  - "Terror Engine – Grim Reaper Monster" — rigged with 4 animations, but
    an **extracted/ripped Unity Asset Store asset** (author's own note says
    buy the original); licence not clean. Rejected.
  - "Lowpoly Grim Reaper" by sumi64bit — 1k tris, low-poly, **CC-BY-NC**.
    Rejected twice over.
  - "GRIM REAPER" by NORBERTO-3D — 3,304 downloads, animated, but comments
    report it uses an Alembic cloth simulation that fails to import in
    Blender/Maya — a bad fit for a glTF/Unity pipeline; tris/licence not
    verified. Rejected.
- **Suggested next step for the monster:** the honest gap is a *free,
  rigged, realistic hooded horror figure on Sketchfab* — the good ones
  (Abyssal Reaper, Azareth) are paid on Fab with free versions that are
  idle-only or unverifiable. Options: (a) check whether the Fab "Free
  Version (Idle Animation)" of Azareth/Abyssal Reaper is genuinely free
  and glTF/FBX-downloadable for Unity, (b) put a Mixamo rig onto a static
  verified figure like The Dark Wraith, or (c) keep the current black
  capsule-man as a placeholder until a verified rigged monster is found.
  Do not force a bad link.

---

## Summary table

| # | Item | Primary link | Licence | Tris | Backup | Status |
|---|------|--------------|---------|------|--------|--------|
| 1 | House_2 | — | — | — | — | ❌ NO LINK |
| 2 | House_3 | stealth86 Thatched Hut (compromise — needs decimation 161k→~50k, PBR unconfirmed) | CC-BY | 161.1k | — | ⚠️ COMPROMISE |
| 3 | House_4 | stealth86 Thatched Hut (same file — can fill only ONE of House_3/House_4) / paid danzl0 hut (48.4k, PBR confirmed — PAID, not approved) | CC-BY | 161.1k / 48.4k | — | ⚠️ COMPROMISE / PAID |
| 4 | Clinic | — | — | — | — | ❌ NO LINK |
| 5 | Angkor | Bhumija Shikara (gputhige) — intact shrine, albedo-only scan | CC-BY | 150k | Old Ruined Temple (Gündel, 20k, Substance Painter) | ⚠️ UNSURE |
| 6 | Banyan | Chinese Banyan (Valery.Li) | CC-BY | 111.4k | — | ✅ DOWNLOAD |
| 7 | Palms | Realistic Palm Tree Free (NextSpring) | CC-BY | 27.2k | Palm Tree 2 Free (grove, 60.8k) | ✅ DOWNLOAD |
| 8 | Goat | Boer Goat (rt699448) — ⚠️ "Generated with AI" tag on page | CC-BY | 10.4k | — | ✅ DOWNLOAD (check preview) |
| 9 | Horse | Horse (OrelvysRodriguez) | CC-BY | 70.5k | Horse 3D Model FREE (Empire, 42.3k) | ✅ DOWNLOAD |
| 10 | Dog | — | — | — | — | ❌ NO LINK |
| 11 | Tractor | Classic Tractor (Adam) | CC-BY | 40.2k | Swaraj 855 (111.7k, AI+cartoon tags — risky) | ✅ DOWNLOAD |
| 12 | Hero's car | Hindustan Ambassador (rSquare) | CC-BY | 12.3k | Ambassador (Dharam Patel, 111.4k, full PBR — download/licence UNCONFIRMED) | ✅ DOWNLOAD |
| 13 | Bullock cart | Bullcart (harshitaggarwalh) | CC-BY | 106.9k | — | ✅ DOWNLOAD |
| 14 | Dock | — | — | — | — | ⛔ DROP — no river |
| 15 | Pujari | Indian Man In Dhoti (anandmohan662) — rigged | CC-BY | 21.6k | — | ✅ DOWNLOAD (check face in viewer first) |
| 16 | Monster | — | — | — | Dark Wraith (davidkong, 150.3k, NOT rigged, knight) | ❌ STILL OPEN |

## Download instructions for the user

1. Tap each PRIMARY link, check the preview in the Sketchfab viewer, then
   tap Download and pick **glTF** (or GLB) in the download dialog.
2. After downloading, each file must be verified on arrival (glTF v2,
   triangle count, and PBR maps — normal/roughness — inside the file)
   before it goes into the game. A link being verified here does NOT mean
   the downloaded file is automatically good.
3. Credit every CC-BY author in the project README.
4. Do NOT download: the Slovak house (wrong region), the Low Poly Hospital,
   the paid danzl0 thatch hut (unless the user agrees to buy it), the
   Dharam Patel Ambassador (until its Download button + licence are
   confirmed on the phone), the Dark Wraith as the monster (not rigged,
   not a wraith), or any pier (no river).
5. Items still needing a source: House_2, House_4 (if the stealth86 hut
   goes to House_3), Clinic, Dog, Monster — plus a decision on the Angkor
   primary (Indian-but-intact Bhumija vs ruined-but-generic Old Ruined
   Temple).
