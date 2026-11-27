// VILLAGE EXPANSION — seeded layout data (DESIGN.md §1/§2/§3).
// Pure data, no scene objects: the village is runtime-built, so the
// editor menu (Tools > Validate Village) validates THIS table in edit mode
// (VillageValidator.ValidateLayoutData), and the runtime validator re-uses
// the same rows for its overlap / monster-path / waypoint checks.
//
// MEASURED FOOTPRINTS (2026-11-27, course correction): every footprint
// below is the TRUE GLB footprint, parsed from the real models
// (loader-identical, ByHeight) and re-verified with the SAT checker
// (measurements/recheck.py, measurements/landmark_recheck.py). DESIGN.md's
// Table A assumptions were wrong for 4 of 6 houses — A and C are the SAME
// hut geometry (~8-9 m wide, not 6-7 m): C at H3.5 = 8.18 x 7.26,
// E at H4.0 = 6.57 x 4.22, F at H4.0 = 6.58 x 6.53, G haveli at H8.0 =
// 7.04 x 6.44, temple plinth 9.90 x 9.90, school 13.42 x 4.97,
// clinic 14.02 x 2.75, dock 5.55 x 6.00 (depth +3.00 vs assumed).
// The salvaged house table (5 new houses) and the final landmark positions
// are the jointly SAT-verified values from the checker runs — do NOT
// "restore" DESIGN's assumed numbers.
//
// DO NOT edit a coordinate here without re-running the SAT checker
// (measurements/recheck.py) and the editor menu — a moved house can seal
// the monster corridor or overlap a neighbour by centimetres.

using UnityEngine;

public static class VillageLayoutData
{
    // ---- Model filenames (exact, Assets/StreamingAssets/Models/) ----
    public const string FileHouseE = "old_village_house_-_indian_game_asset_24_bac7.glb";
    public const string FileHouseD = "traditional_round_thatched_hut_50k.glb";
    public const string FileHouseC = "thatched_hut_stealth86_50k.glb";
    public const string FileHandpump = "handpump_3d_39_aah5.glb";
    public const string FileLantern = "Lantern_01.glb";
    public const string FileCow = "cow_15_u1bj.glb";
    public const string FileGoat = "boer_goat__3d_model_free_7_dr1w.glb";
    public const string FileBasket = "historic_miscellaneous_basket_wicker_old_03_xdwjadj_mid_51_y3o8.glb";
    public const string FileMatka = "indian_terracotta_matka_red_clay_water_pot_40_ris7.glb";

    // ---- HOUSE_13 MEASUREMENT GATE (DESIGN.md §1.5 Table B) ----
    // House_13 (16.05, 2.0) fits ONLY if the haveli's true north-south
    // extent is <= 6.0 m. MEASURED 2026-11-27: the haveli GLB at H8.0 is
    // 7.04 x 6.44 (yaw -90), so its world N-S extent is 7.04 m — GREATER
    // than 6.0. House_13 is therefore DROPPED, permanently: at
    // (16.05, 2.0) it penetrates the haveli by 0.13 m, the House_2 gap is
    // 0.02 m, and alternative pocket spots also fail. The code path stays
    // in VillageBuilder (gated by House13Builds below) so the decision is
    // explicit, but the gate constant is a MEASURED value — do NOT flip
    // it, do NOT nudge the house west or south to force it in, and Lane
    // B2 is NOT stamped (it served House_13 only).
    public const float HaveliTrueNSExtentM = 7.04f;
    public static bool House13Builds { get { return HaveliTrueNSExtentM <= 6.0f; } }

    // ---- Seeded house rows (true GLB footprints, 2026-11-27) ----
    public struct LayoutHouse
    {
        public string name;
        public string file;
        public float x, z, yaw, height, w, d;
        public bool isNew;
        public bool conditional;
        public LayoutHouse(string n, string f, float x, float z, float yaw, float h, float w, float d, bool isNew, bool cond)
        {
            name = n; file = f; this.x = x; this.z = z; this.yaw = yaw;
            height = h; this.w = w; this.d = d; this.isNew = isNew; conditional = cond;
        }
    }

    // Existing houses/landmark footprints (TRUE GLB footprints; SAT input).
    // Stored as (centre x, centre z, w, d, yaw) with w/d in MODEL-local axes.
    // Haveli is stored model-local (w 7.04, d 6.44, yaw -90) — its world
    // N-S extent is 7.04 (= w at yaw -90), which IS HaveliTrueNSExtentM.
    public static readonly LayoutHouse[] ExistingHouses = new LayoutHouse[]
    {
        // House_Player is the WALL box (8.61 x 7.58, centre (-9.06, 6.05)),
        // NOT the whole-hut 9.37 x 8.31 box — the wall box is the correct
        // collision representation (the roof overhangs the walls by ~0.4 m).
        // The load position is (-9, 6); the wall-box centre is offset from it.
        new LayoutHouse("House_Player", "thatched_hut_4_p31g.glb", -9.06f, 6.05f, 0f, 4.0f, 8.61f, 7.58f, false, false),
        new LayoutHouse("House_2", "indian_house_old_18_qlsq.glb", 9f, 8f, 15f, 5.5f, 9.43f, 9.43f, false, false),
        new LayoutHouse("House_3", "thatched_hut_stealth86_50k.glb", -9f, -9f, -10f, 3.5f, 8.18f, 7.26f, false, false),
        // House_4 is round: stored as a 5.50 x 5.50 square (conservative —
        // a square over-approximates the r 2.75 m wall circle; the square
        // is the roof's AABB). No separate circle row (removed 2026-11-27).
        new LayoutHouse("House_4", "traditional_round_thatched_hut_50k.glb", 9f, -7f, 0f, 3.5f, 5.50f, 5.50f, false, false),
        new LayoutHouse("House_5", "old_village_house_-_indian_game_asset_24_bac7.glb", -13f, 13f, -20f, 5.5f, 9.04f, 5.80f, false, false),
        new LayoutHouse("House_6", "indian_village_house_famous_3d_model_37_4zjm.glb", 13f, -14f, 10f, 5.5f, 9.05f, 8.99f, false, false),
        // Temple: the SOLID plinth is 9.90 x 9.90 (the 20.43 plate is flat/
        // disabled, so it is NOT the footprint). BellTower block is a
        // separate row below (its base dips below the plinth — the temple
        // floats ~0.13 m in the live game; pre-existing, NOT fixed here).
        new LayoutHouse("Temple", "hindu_temple_88_62er.glb", 2f, -17f, 0f, 10f, 9.90f, 9.90f, false, false),
        // BellTower block (footprint-only row; height unused by validation).
        new LayoutHouse("BellTower", "", 2.15f, -10.55f, 0f, 3.0f, 3.56f, 1.29f, false, false),
        new LayoutHouse("School", "school-building_74_yizq.glb", -16.5f, -14f, 25f, 5f, 13.42f, 4.97f, false, false),
        new LayoutHouse("Haveli", "meshy_haveli_86_hvl.glb", 16f, -3.5f, -90f, 8f, 7.04f, 6.44f, false, false),
        new LayoutHouse("Clinic", "abandoned_hospital_building_20_2516.glb", -13.5f, -2f, 90f, 6f, 14.02f, 2.75f, false, false),
        new LayoutHouse("Bhumija", "Bhumija.glb", 21f, 12f, -90f, 9f, 6.04f, 4.61f, false, false),
        // Dock is 3.00 m deeper than DESIGN assumed (6.00, not 3.00) —
        // this is what killed the old House_11 position.
        new LayoutHouse("Dock", "dock_pier_78_n66e.glb", -7f, 19f, 0f, 6f, 5.55f, 6.00f, false, false),
        new LayoutHouse("Field", "", 10f, 16f, 0f, 0f, 10.0f, 6.0f, false, false),
        new LayoutHouse("Ambassador", "hindustan_ambassador_4_rh04.glb", 2.5f, 15f, 30f, 1.5f, 3.86f, 1.76f, false, false),
        new LayoutHouse("Tractor", "classic_tractor__1__11_scro.glb", 16f, 15f, 30f, 2.5f, 2.12f, 4.46f, false, false),
        new LayoutHouse("FallenPine", "", -18f, 18f, 40f, 0f, 4.0f, 1.0f, false, false),
        new LayoutHouse("SmallGrave", "", 18f, -21.5f, -30f, 0f, 2.2f, 1.1f, false, false),
        new LayoutHouse("BullockCart", "Bullcart.glb", -7.5f, -4f, 0f, 1.6f, 2.98f, 1.22f, false, false),
        // Cart as an OBB row (was a r 1.0 circle; the true footprint is
        // 2.51 x 1.40). Height 1.5 m (ByHeight).
        new LayoutHouse("Cart", "", 5f, 0.5f, 0f, 1.5f, 2.51f, 1.40f, false, false),
        // Fences as thin OBBs (DESIGN §1.1).
        new LayoutHouse("FenceW", "", -5.5f, -5.6f, 0f, 0f, 0.3f, 12.8f, false, false),
        new LayoutHouse("FenceE", "", 5.5f, 14.5f, 0f, 0f, 0.3f, 9.0f, false, false),
    };

    // VERIFIED new houses — SALVAGED TABLE (jointly SAT-verified 2026-11-27,
    // measurements/recheck.py §E3 + landmark_recheck.py). 5 houses total:
    // House_11 is DROPPED (see below) and House_13 is DROPPED (gate 7.04).
    // - House_7: E, UNCHANGED from DESIGN.
    // - House_8: E yaw 90, MOVED south z 2.5 -> 1.5 (min margins: ring 0.64, Clinic 1.27).
    // - House_9: D, MOVED south z 0.1 -> -0.55 (valid window [-0.57,-0.55];
    //   -0.54 FAILS). DAGGER: binding margin only 0.06 m to the player
    //   WALL box; the roof sits under the player's roof overhang (~1.1 m
    //   vertical clearance). The live validator (V4) re-confirms this
    //   against live GetWorldBounds before screenshots count as PASS.
    // - House_10: D, UNCHANGED from DESIGN (School 0.28, House_3 0.96).
    // - House_12: E yaw -90 (MODEL SWAP C->E; yaw -90 puts the model's
    //   actual front face — Unity -Z, the only face with door openings —
    //   EAST onto Lane B; footprint SAT-identical to yaw 90). Min margins:
    //   Cart 0.51, Nandi 0.52, NPCWP (6,-7) 0.79, House_4 1.04.
    // HOUSE_11 DROPPED: the D-at-(-4.5,13.0) swap passes hard constraints,
    // but the Wolf (-6,8) r4 and Chicken (-8.5,9.5) r3 roam discs intrude
    // 1.75 m / 1.54 m into the footprint, and AnimalRoam.PickTarget()
    // picks a random point in the disc with NO validity check — a target
    // inside the house makes the animal press against the wall for up to
    // 1.5 s, give up, pause 2-4 s, and repeat. 11 houses total (6 old +
    // 5 new), not 12.
    public static readonly LayoutHouse[] NewHouses = new LayoutHouse[]
    {
        new LayoutHouse("House_7", FileHouseE, -17.25f, 7.75f, 0f, 4.0f, 6.57f, 4.22f, true, false),
        new LayoutHouse("House_8", FileHouseE, -18.25f, 1.5f, 90f, 4.0f, 6.57f, 4.22f, true, false),
        new LayoutHouse("House_9", FileHouseD, -8.5f, -0.55f, 0f, 3.5f, 5.5f, 5.5f, true, false),
        new LayoutHouse("House_10", FileHouseD, -7.75f, -17.0f, 0f, 3.5f, 5.5f, 5.5f, true, false),
        new LayoutHouse("House_12", FileHouseE, 3.1f, -4.0f, -90f, 4.0f, 6.57f, 4.22f, true, false),
    };

    // CONDITIONAL house — DESIGN Table B. DROPPED: House13Builds is false
    // (haveli 7.04 m > 6.0 m, measured). Row kept so the decision is
    // explicit; the builder gates it behind House13Builds and never builds it.
    public static readonly LayoutHouse ConditionalHouse =
        new LayoutHouse("House_13", FileHouseE, 16.05f, 2.0f, 0f, 4.0f, 6.57f, 4.22f, true, true);

    // ---- Round keep-outs (TRUE values, 2026-11-27) ----
    public struct LayoutCircle
    {
        public string name;
        public float x, z, r;
        public LayoutCircle(string n, float x, float z, float r) { name = n; this.x = x; this.z = z; this.r = r; }
    }

    public static readonly LayoutCircle[] ExistingCircles = new LayoutCircle[]
    {
        // Well r 0.75 (was 1.2 — the loader grounds on the hanging bucket,
        // 50 verts below the rim base; the well floats ~0.35 m in the live
        // game; pre-existing, NOT fixed here).
        new LayoutCircle("Well", -4f, -3.5f, 0.75f),
        new LayoutCircle("Gate", 0f, 20.5f, 1.5f),
        new LayoutCircle("Graves", 19.5f, -18.5f, 2.0f),
        new LayoutCircle("Pyre", 22f, -19.5f, 1.2f),
        new LayoutCircle("ShamshanFire", 15f, -19.5f, 1.2f),
        new LayoutCircle("Nandi", 2f, -8.8f, 1.0f),
        new LayoutCircle("Shivling", 2f, -15f, 0.8f),
        new LayoutCircle("Handpump", 1.5f, 9f, 0.5f),
        new LayoutCircle("Tree_Neem1", 8f, -4f, 0.6f),
        new LayoutCircle("Tree_Mango", -18f, -5f, 0.6f),
        // Banyan root circle (r 0.8, conservative). For House_13 the check
        // uses the trunk BLOCK below instead (DESIGN §1.5c).
        new LayoutCircle("Tree_Banyan", 19f, 5f, 0.8f),
        new LayoutCircle("ChowkLamp", 2.5f, 1.5f, 0.4f),
        new LayoutCircle("ChowkLantern", 3.2f, 2.2f, 0.4f),
        new LayoutCircle("Matka", -4.2f, 9.2f, 0.4f),
        new LayoutCircle("OilBarrel", 7.5f, 0.5f, 0.5f),
    };

    // Banyan trunk Block (0.7 x 0.7 — the loader's Trunk collider). Used
    // for the House_13 banyan clearance (DESIGN §1.5c: gap 0.41 m to the
    // Block, not 0.00 m to the conservative r 0.8 root circle).
    public const float BanyanTrunkX = 19f;
    public const float BanyanTrunkZ = 5f;
    public const float BanyanTrunkHalf = 0.35f;

    // ---- Monster path + test points (DESIGN §1.2/§1.3 — frozen) ----
    // Monster-path endpoint is the TRUE DoorFront (-2.76, 7.60), measured
    // from the live door (doorZ 7.60), NOT DESIGN's (-6, 7.5). Sensitivity
    // re-run: no house verdict changes.
    public static readonly Vector2[] MonsterPath = new Vector2[]
    {
        new Vector2(-3.5f, -10.5f), new Vector2(-3.5f, -12f), new Vector2(0f, 0f),
        new Vector2(0f, 10f), new Vector2(-2.76f, 7.6f),
    };
    public static readonly Vector2[] FreePoints = new Vector2[]
    {
        new Vector2(0f, 0f), new Vector2(0f, 10f), new Vector2(-3.5f, -12f),
    };
    public static readonly Vector2[] BlockedPoints = new Vector2[]
    {
        new Vector2(9f, 8f), new Vector2(2f, -10.5f), new Vector2(0f, 20.5f), new Vector2(-4f, -3.5f),
    };
    public static readonly Vector2 MeeraSpot = new Vector2(-2f, 4f);
    public static readonly Vector2[] NpcWaypoints = new Vector2[]
    {
        new Vector2(3.5f, -8.5f), new Vector2(6f, -7f), new Vector2(4.5f, -8.5f),
        new Vector2(1.5f, 10.5f), new Vector2(0f, 8f), new Vector2(2f, 12.5f), new Vector2(-1f, 12f),
        new Vector2(0.5f, 6.5f), new Vector2(-2f, 4f), new Vector2(2f, 8f), new Vector2(-3f, 9f),
    };
    // Hard roam discs (MovementTest subjects — no overlap allowed).
    public static readonly LayoutCircle[] RoamHard = new LayoutCircle[]
    {
        new LayoutCircle("Cow", 8f, 15.5f, 4.0f),
        new LayoutCircle("Horse", 2f, 5f, 3.0f),
    };
    // Soft roam discs (reported, not failing).
    public static readonly LayoutCircle[] RoamSoft = new LayoutCircle[]
    {
        new LayoutCircle("Goat", 13f, 15f, 3.5f),
        new LayoutCircle("Buffalo", 6.8f, 17f, 4.0f),
        new LayoutCircle("Wolf", -6f, 8f, 4.0f),
        new LayoutCircle("Chicken", -8.5f, 9.5f, 3.0f),
    };

    // Ring-1 trunk centres (CloneRingTree positions — DESIGN §1.2.7).
    public static Vector2[] Ring1Trunks()
    {
        System.Collections.Generic.List<Vector2> list = new System.Collections.Generic.List<Vector2>();
        for (int x = -20; x <= 20; x += 4) { list.Add(new Vector2(x, -21f)); list.Add(new Vector2(x, 21f)); }
        for (int z = -16; z <= 16; z += 5) { list.Add(new Vector2(-21f, z)); list.Add(new Vector2(21f, z)); }
        return list.ToArray();
    }

    // ---- Lanes (DESIGN §2.1 — texture stamps, not geometry) ----
    // Lane A is the existing NS lane extended to the gate (stamped by
    // StampWetLane's z1 20.9, not by the polyline stamper, so it is listed
    // here for the validator only and never double-stamped).
    public static readonly Vector3[] LaneA = new Vector3[]
    {
        new Vector3(0f, 0f, -12f), new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 7f),
        new Vector3(0.5f, 0f, 12f), new Vector3(0f, 0f, 16f), new Vector3(0f, 0f, 20.5f),
    };
    public static readonly Vector3[] LaneB = new Vector3[]
    {
        new Vector3(0f, 0f, 0f), new Vector3(4f, 0f, -0.5f), new Vector3(7f, 0f, -2f),
        new Vector3(10f, 0f, -4.5f), new Vector3(12f, 0f, -7f), new Vector3(15f, 0f, -9f),
        new Vector3(18f, 0f, -12f), new Vector3(19f, 0f, -15f),
    };
    // Lane B2 is stamped ONLY if House_13 builds (DESIGN §2.1). House_13
    // is DROPPED (haveli 7.04 m), so Lane B2 is NEVER stamped.
    public static readonly Vector3[] LaneB2 = new Vector3[]
    {
        new Vector3(7f, 0f, -2f), new Vector3(10f, 0f, 0f), new Vector3(13f, 0f, 1f), new Vector3(16f, 0f, 2f),
    };
    public static readonly Vector3[] LaneC = new Vector3[]
    {
        new Vector3(0f, 0f, 0f), new Vector3(-2f, 0f, 3f), new Vector3(-4f, 0f, 6f),
        new Vector3(-5f, 0f, 8.5f), new Vector3(-8f, 0f, 9f), new Vector3(-12f, 0f, 8.9f),
        new Vector3(-15f, 0f, 10f), new Vector3(-18f, 0f, 12f), new Vector3(-18f, 0f, 16f),
    };
    public static readonly Vector3[] LaneD = new Vector3[]
    {
        new Vector3(0f, 0f, 0f), new Vector3(-2f, 0f, -4f), new Vector3(-4f, 0f, -8f),
        new Vector3(-6f, 0f, -12f), new Vector3(-7f, 0f, -16f), new Vector3(-8f, 0f, -18f),
    };

    // ---- New landmarks (final SAT-verified positions, 2026-11-27) ----
    // Every position below passed the joint check (landmark_recheck.py,
    // "FINAL joint set: 20 of 20 pass") against the TRUE footprints plus
    // the salvaged houses. Adjustments vs DESIGN §3 (all minimal moves):
    // - Field_2 (-12,-18) -> (-14.5,-19): school is 3.42 m longer than
    //   assumed; old spot penetrated House_10 by 1.00 m. New spot: min
    //   margin 0.48 (School).
    // - Field_3 (-12.5,19) -> (-2,16): dock is 3.00 m deeper; old spot
    //   penetrated CattleShed by 2.50 m. New spot: min margin 0.22 (Dock).
    //   NOTE: the field's east edge sits on Lane A's centreline, so the
    //   lane stamp's west fade (to x -2.2) is covered by grass for
    //   z 14.75-17.25 — a visual seam, not a collision failure. The
    //   Dock-to-lane gap (2.03 m) is narrower than any field width, so no
    //   position there avoids the stamp; see the build report.
    // - CattleShed (-14,18.75) -> (-13.5,18.75): House_5 is 9.04 m wide;
    //   old House_5 margin was 0.06 m; new margin 0.24 m.
    // - Haystack_1 (1.5,13.5) -> (0.5,12.5): old centre was INSIDE the
    //   Ambassador OBB (pen 0.92). New margin 0.17 (Ambassador).
    // - Haystack_2 (11,-5) -> (13,-8): old spot overlapped House_4's
    //   circle by 0.52 m. New margins: House_6 0.19, Haveli 0.38.
    // - Haystack_3 (-6,-14) -> (-3.75,-14): old spot penetrated House_10
    //   by 0.35 m. New margin 0.11 (House_3).
    // - Charpai_1 (-4.5,3) -> (-4.0,2.5): old spot penetrated the player
    //   wall box by 0.19 m. New margin 0.31. Also FIXED: the old row was
    //   transposed (w 1.8, d 0.9) vs the built geometry (long axis N-S:
    //   x 0.9, z 1.8); the row now matches the build.
    // - Clothesline_2 x 14.5 -> 14.0: old spot penetrated the pond by
    //   0.08 m. New margin 0.33 (House_2).
    // - LampPost_1 (-6,10) -> (-6,10.5): old spot penetrated House_3 by
    //   0.30 m and the 3.2 m pole sat under the player's roof overhang.
    //   New margin 0.20 (FenceW) and clears the overhang (gap 0.195).
    // - LampPost_3 (-10,-12) -> (-4.5,-12): old spot penetrated House_3
    //   by 0.30 m and the School by 0.17 m. New margin 0.11.
    // - CL_1 CORRECTED (see VillageBuilder.BuildExpansionLandmarks):
    //   DESIGN §3 lists its posts at x -6.5, which falls INSIDE the
    //   player-house OBB (x -12.5..-5.5, z 3..9) and 0.5 m from the door
    //   approach (-6, 7.5). The same row claims "clears the player house
    //   by 1.2 m" — the posts belong EAST of the east wall at x -4.3
    //   (-5.5 + 1.2). Built at -4.3.
    // - KiranaCounter (6.65,-4) -> (5.75,-4): the old C-model counter
    //   overlapped House_4 by 0.6 x 0.65 m (earlier audit missed it);
    //   the E-model (yaw -90) re-fit fixes it. New margin 0.20 (House_4).
    public struct LayoutProp
    {
        public string name;
        public float x, z, w, d, yaw;
        public LayoutProp(string n, float x, float z, float w, float d, float yaw)
        {
            name = n; this.x = x; this.z = z; this.w = w; this.d = d; this.yaw = yaw;
        }
    }

    public static readonly LayoutProp[] NewLandmarks = new LayoutProp[]
    {
        new LayoutProp("Handpump_2", -1f, 18f, 0.8f, 0.8f, 0f),
        new LayoutProp("Handpump_3", 10.5f, -3f, 0.8f, 0.8f, 30f),
        new LayoutProp("Chabutra", 19f, 5f, 3.0f, 3.0f, 0f),
        new LayoutProp("KiranaCounter", 5.75f, -4f, 0.6f, 1.8f, 0f),
        new LayoutProp("VillagePond", 16.1f, 8.2f, 3.4f, 3.0f, 0f),
        new LayoutProp("Field_2", -14.5f, -19f, 5.0f, 3.0f, 0f),
        new LayoutProp("Field_3", -2f, 16f, 4.0f, 2.5f, 0f),
        new LayoutProp("CattleShed", -13.5f, 18.75f, 4.0f, 3.0f, 0f),
        new LayoutProp("Haystack_1", 0.5f, 12.5f, 1.2f, 1.2f, 0f),
        new LayoutProp("Haystack_2", 13f, -8f, 1.2f, 1.2f, 0f),
        new LayoutProp("Haystack_3", -3.75f, -14f, 1.2f, 1.2f, 0f),
        new LayoutProp("Charpai_1", -4.0f, 2.5f, 0.9f, 1.8f, 0f),
        new LayoutProp("Charpai_2", 3f, 13f, 0.9f, 1.8f, 90f),
        new LayoutProp("Clothesline_1", -4.3f, 7f, 0.08f, 3.0f, 0f),
        new LayoutProp("Clothesline_2", 14.0f, 10.25f, 0.08f, 2.5f, 0f),
        new LayoutProp("LampPost_1", -6f, 10.5f, 0.3f, 0.3f, 0f),
        new LayoutProp("LampPost_2", 12f, -9f, 0.3f, 0.3f, 0f),
        new LayoutProp("LampPost_3", -4.5f, -12f, 0.3f, 0.3f, 0f),
        new LayoutProp("LampPost_4", 18f, 16f, 0.3f, 0.3f, 0f),
        new LayoutProp("LampPost_5", 0f, -6f, 0.3f, 0.3f, 0f),
    };

    // Banned object names (ScreenshotTest — DESIGN §10.2 R6/R11). No
    // expansion object may be named with any of these strings.
    public static readonly string[] BannedNames = new string[]
    {
        "Puddle", "WaterPuddle", "WetSheenNS", "WetSheenEW", "DirtPath", "DirtPathEW",
        "ChowkCobble", "GrassPatch", "GroundPatch", "SoilGround", "SandstoneGround",
        "ForestFloor", "FlatPatchDisc",
    };

    // ---- Runtime layout entry (one per object the expansion creates) ----
    // Recorded by VillageBuilder as it builds; VillageValidator reads it.
    public struct ExpansionEntry
    {
        public string name;
        public string file;
        public string kind; // "house" | "prop" | "landmark" | "animal" | "field" | "primitive"
        public Bounds bounds;
        public float yaw;
        public float expW, expD, expH;
        public float expMinY;
        public bool needsCollider;
    }
}
