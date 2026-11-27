// VILLAGE EXPANSION — validation (DESIGN.md §7).
// Two entry points, one geometry core (mirrors checks/layout.py exactly —
// same SAT, same thresholds, including the two fixed checker bugs: SAT
// returns +penetration / -gap, and a pen == 0 axis does NOT stop the test):
//   ValidateLayoutData() — edit mode (Tools > Validate Village with no
//     PlayMode): pure geometry over VillageLayoutData. No scene objects.
//   ValidateLive(builder) — runtime step + PlayMode menu: V1–V8 over the
//     expansion's recorded layout entries and the live scene. Logs AND
//     fixes floating/sunk only; never throws, never blocks FinishBuild.
// Thresholds (DESIGN §0.2/§7, as corrected 2026-11-27): OBB gap
// >= 0.05 m (House_13 vs House_2: 0.01 m, explicitly accepted §1.5b —
// retained only for the gated-off House_13 path; House_13 is DROPPED,
// haveli 7.04 m > 6.0 m); monster-path >= 0.45 m; free points >= 0.50 m;
// Meera spot >= 1.50 m (the checker code value — DESIGN §0.2 text says
// 1.00 m; the code is stricter and all five pass it anyway); NPC
// waypoints >= 0.30 m; hard roam discs: no overlap; ring trunks
// >= 0.35 m; grounding band expMinY +/- 0.07 m (floating > +0.07, sunk
// < -0.07 — the DESIGN §7 absolute band 0.02 / -0.12 around the -0.05 sink).

using System.Collections.Generic;
using UnityEngine;

public static class VillageValidator
{
    public class CheckResult
    {
        public int checks;
        public int errors;
        public int fixedCount;
        public List<string> lines = new List<string>();
        public void Add(string line) { lines.Add(line); Debug.Log(line); }
    }

    // ---- Geometry (mirror of checks/layout.py) ----
    private static Vector2 Rot(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    private static Vector2[] ObbCorners(float cx, float cz, float w, float d, float yaw)
    {
        Vector2[] c = new Vector2[4];
        float hw = w / 2f, hd = d / 2f;
        Vector2[] local = new Vector2[]
        {
            new Vector2(hw, hd), new Vector2(hw, -hd), new Vector2(-hw, -hd), new Vector2(-hw, hd),
        };
        for (int i = 0; i < 4; i++)
        {
            Vector2 p = Rot(local[i], yaw);
            c[i] = new Vector2(cx + p.x, cz + p.y);
        }
        return c;
    }

    // SAT: +penetration (>0) if overlapping, -gap (<0) if separated.
    // Returns on the FIRST separating axis (a lower bound on the true gap);
    // a pen == 0 axis does NOT stop the test (fixed checker bug #2).
    public static float SatOverlap(float ax, float az, float aw, float ad, float aYaw,
        float bx, float bz, float bw, float bd, float bYaw)
    {
        Vector2[] axes = new Vector2[]
        {
            Rot(new Vector2(1f, 0f), aYaw), Rot(new Vector2(0f, 1f), aYaw),
            Rot(new Vector2(1f, 0f), bYaw), Rot(new Vector2(0f, 1f), bYaw),
        };
        Vector2[] ca = ObbCorners(ax, az, aw, ad, aYaw);
        Vector2[] cb = ObbCorners(bx, bz, bw, bd, bYaw);
        float minPen = 1e9f;
        foreach (Vector2 axis in axes)
        {
            Vector2 n = axis.normalized;
            float minA = float.MaxValue, maxA = float.MinValue;
            float minB = float.MaxValue, maxB = float.MinValue;
            foreach (Vector2 p in ca) { float d = p.x * n.x + p.y * n.y; minA = Mathf.Min(minA, d); maxA = Mathf.Max(maxA, d); }
            foreach (Vector2 p in cb) { float d = p.x * n.x + p.y * n.y; minB = Mathf.Min(minB, d); maxB = Mathf.Max(maxB, d); }
            float pen = Mathf.Min(maxA, maxB) - Mathf.Max(minA, minB);
            if (pen < 0f) return pen;
            minPen = Mathf.Min(minPen, pen);
        }
        return minPen;
    }

    public static float ObbCircleGap(float ox, float oz, float ow, float od, float oYaw, float cx, float cz, float r)
    {
        Vector2 local = Rot(new Vector2(cx - ox, cz - oz), -oYaw);
        float qx = Mathf.Max(Mathf.Abs(local.x) - ow / 2f, 0f);
        float qz = Mathf.Max(Mathf.Abs(local.y) - od / 2f, 0f);
        return Mathf.Sqrt(qx * qx + qz * qz) - r;
    }

    public static float PtObbDist(float px, float pz, float ox, float oz, float ow, float od, float oYaw)
    {
        Vector2 local = Rot(new Vector2(px - ox, pz - oz), -oYaw);
        float qx = Mathf.Max(Mathf.Abs(local.x) - ow / 2f, 0f);
        float qz = Mathf.Max(Mathf.Abs(local.y) - od / 2f, 0f);
        return Mathf.Sqrt(qx * qx + qz * qz);
    }

    public static bool PtInObb(float px, float pz, float ox, float oz, float ow, float od, float oYaw)
    {
        Vector2 local = Rot(new Vector2(px - ox, pz - oz), -oYaw);
        return Mathf.Abs(local.x) <= ow / 2f && Mathf.Abs(local.y) <= od / 2f;
    }

    public static float PtSegDist(float px, float pz, float ax, float az, float bx, float bz)
    {
        float vx = bx - ax, vz = bz - az;
        float l2 = vx * vx + vz * vz;
        float t = l2 <= 0f ? 0f : Mathf.Clamp01(((px - ax) * vx + (pz - az) * vz) / l2);
        float dx = px - (ax + t * vx), dz = pz - (az + t * vz);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    public static float ObbSegDist(float ox, float oz, float ow, float od, float oYaw,
        float ax, float az, float bx, float bz)
    {
        float d = Mathf.Min(PtObbDist(ax, az, ox, oz, ow, od, oYaw), PtObbDist(bx, bz, ox, oz, ow, od, oYaw));
        for (int i = 0; i <= 20; i++)
        {
            float t = i / 20f;
            d = Mathf.Min(d, PtObbDist(ax + (bx - ax) * t, az + (bz - az) * t, ox, oz, ow, od, oYaw));
        }
        return d;
    }

    private static Bounds GetBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(root.transform.position, new Vector3(6f, 4f, 4f));
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    // ---- House-pair check with the House_13 exceptions (DESIGN §1.5) ----
    // Retained for the gated-off House_13 path only — House_13 is DROPPED
    // (haveli 7.04 m > 6.0 m), so isH13Pair never fires in practice.
    // Returns an error line, or null. thresholdPair is the SAT gap required.
    private static string CheckHousePair(VillageLayoutData.LayoutHouse a, VillageLayoutData.LayoutHouse b)
    {
        float need = 0.05f;
        bool isH13Pair = (a.name == "House_13" && b.name == "House_2") || (a.name == "House_2" && b.name == "House_13");
        if (isH13Pair) need = 0.01f; // §1.5(b): the 0.04 m House_2 gap is explicitly accepted.
        float s = SatOverlap(a.x, a.z, a.w, a.d, a.yaw, b.x, b.z, b.w, b.d, b.yaw);
        if (s > -need) return "OVERLAP " + a.name + " vs " + b.name + " pen/gap " + s.ToString("+#0.00;-#0.00;0.00") + " (need gap >= " + need.ToString("0.00") + ")";
        return null;
    }

    private static void CheckHouseAgainstKeepOuts(CheckResult r, VillageLayoutData.LayoutHouse h)
    {
        r.checks++;
        foreach (VillageLayoutData.LayoutHouse e in VillageLayoutData.ExistingHouses)
        {
            string err = CheckHousePair(h, e);
            if (err != null) { r.errors++; r.Add("[ValidateVillage] " + err); }
        }
        foreach (VillageLayoutData.LayoutCircle c in VillageLayoutData.ExistingCircles)
        {
            // House_13 vs the banyan: measure to the trunk BLOCK, not the
            // conservative r 0.8 root circle (DESIGN §1.5c).
            if (h.name == "House_13" && c.name == "Tree_Banyan") continue;
            float g = ObbCircleGap(h.x, h.z, h.w, h.d, h.yaw, c.x, c.z, c.r);
            if (g < 0.05f) { r.errors++; r.Add("[ValidateVillage] CIRC " + h.name + " vs " + c.name + " gap " + g.ToString("+#0.00;-#0.00;0.00")); }
        }
        if (h.name == "House_13")
        {
            float g = PtObbDist(VillageLayoutData.BanyanTrunkX, VillageLayoutData.BanyanTrunkZ,
                h.x, h.z, h.w, h.d, h.yaw);
            // Distance from the trunk Block centre to the house OBB, minus
            // the Block half-diagonal-ish margin: use the axis-aligned gap
            // (Block is 0.7 x 0.7, house is axis-aligned here).
            float blockGap = Mathf.Max(
                Mathf.Abs(VillageLayoutData.BanyanTrunkX - h.x) - (h.w / 2f + VillageLayoutData.BanyanTrunkHalf),
                Mathf.Abs(VillageLayoutData.BanyanTrunkZ - h.z) - (h.d / 2f + VillageLayoutData.BanyanTrunkHalf));
            // The true edge gap is the max-axis separation when separated
            // on one axis; report the centre-distance form as well.
            float edgeGap = blockGap;
            if (edgeGap < 0.05f) { r.errors++; r.Add("[ValidateVillage] CIRC House_13 vs BanyanTrunkBlock gap " + edgeGap.ToString("+#0.00;-#0.00;0.00") + " (centre-dist " + g.ToString("0.00") + ")"); }
        }
        for (int i = 0; i + 1 < VillageLayoutData.MonsterPath.Length; i++)
        {
            Vector2 a = VillageLayoutData.MonsterPath[i], b = VillageLayoutData.MonsterPath[i + 1];
            float d = ObbSegDist(h.x, h.z, h.w, h.d, h.yaw, a.x, a.y, b.x, b.y);
            if (d < 0.45f) { r.errors++; r.Add("[ValidateVillage] MONSTER-PATH " + h.name + " seg (" + a.x + "," + a.y + ")-(" + b.x + "," + b.y + ") dist " + d.ToString("0.00")); }
        }
        foreach (Vector2 p in VillageLayoutData.FreePoints)
        {
            float d = PtObbDist(p.x, p.y, h.x, h.z, h.w, h.d, h.yaw);
            if (d < 0.5f) { r.errors++; r.Add("[ValidateVillage] FREEPT " + h.name + " (" + p.x + "," + p.y + ") dist " + d.ToString("0.00")); }
        }
        float meera = PtObbDist(VillageLayoutData.MeeraSpot.x, VillageLayoutData.MeeraSpot.y, h.x, h.z, h.w, h.d, h.yaw);
        if (meera < 1.5f) { r.errors++; r.Add("[ValidateVillage] MEERA-SPOT " + h.name + " dist " + meera.ToString("0.00") + " (need >= 1.50)"); }
        foreach (Vector2 p in VillageLayoutData.NpcWaypoints)
        {
            float d = PtObbDist(p.x, p.y, h.x, h.z, h.w, h.d, h.yaw);
            if (d < 0.3f) { r.errors++; r.Add("[ValidateVillage] NPCWP " + h.name + " (" + p.x + "," + p.y + ") dist " + d.ToString("0.00")); }
        }
        foreach (VillageLayoutData.LayoutCircle roam in VillageLayoutData.RoamHard)
        {
            float g = ObbCircleGap(h.x, h.z, h.w, h.d, h.yaw, roam.x, roam.z, roam.r);
            if (g < 0.0f) { r.errors++; r.Add("[ValidateVillage] ROAM-HARD " + h.name + " vs " + roam.name + " gap " + g.ToString("+#0.00;-#0.00;0.00")); }
        }
        foreach (Vector2 t in VillageLayoutData.Ring1Trunks())
        {
            float d = PtObbDist(t.x, t.y, h.x, h.z, h.w, h.d, h.yaw);
            if (d < 0.35f) { r.errors++; r.Add("[ValidateVillage] RING-TRUNK " + h.name + " (" + t.x + "," + t.y + ") dist " + d.ToString("0.00")); }
        }
        if (Mathf.Abs(h.x) > 20f || Mathf.Abs(h.z) > 20f)
        {
            r.errors++; r.Add("[ValidateVillage] BOUNDARY " + h.name + " centre outside |20|");
        }
        foreach (VillageLayoutData.LayoutCircle roam in VillageLayoutData.RoamSoft)
        {
            float g = ObbCircleGap(h.x, h.z, h.w, h.d, h.yaw, roam.x, roam.z, roam.r);
            if (g < 0.0f) r.Add("[ValidateVillage] (soft) ROAM-SOFT " + h.name + " vs " + roam.name + " overlap " + g.ToString("+#0.00;-#0.00;0.00"));
        }
    }

    // ---- Edit-mode validation: seeded layout data only (DESIGN §7.2) ----
    public static CheckResult ValidateLayoutData()
    {
        CheckResult r = new CheckResult();
        r.Add("[ValidateVillage] Edit-mode: validating seeded layout data (no scene objects)");
        r.Add("[ValidateVillage] House_13 gate: HaveliTrueNSExtentM=" + VillageLayoutData.HaveliTrueNSExtentM.ToString("0.00")
            + " m (measured) -> House_13 " + (VillageLayoutData.House13Builds ? "BUILDS" : "SKIPPED (DROPPED)"));

        // V3 — scale ranges on the seeded rows.
        foreach (VillageLayoutData.LayoutHouse h in VillageLayoutData.NewHouses)
        {
            r.checks++;
            if (h.height < 3.5f || h.height > 4.5f) { r.errors++; r.Add("[ValidateVillage] SCALE " + h.name + " h=" + h.height.ToString("0.00") + " expected [3.5, 4.5]"); }
            float longest = Mathf.Max(h.w, h.d);
            if (longest < 4.4f || longest > 7.0f) { r.errors++; r.Add("[ValidateVillage] SCALE " + h.name + " longest footprint side=" + longest.ToString("0.00") + " expected [4.4, 7.0]"); }
        }
        if (VillageLayoutData.House13Builds)
        {
            r.checks++;
            VillageLayoutData.LayoutHouse h = VillageLayoutData.ConditionalHouse;
            if (h.height < 3.5f || h.height > 4.5f) { r.errors++; r.Add("[ValidateVillage] SCALE House_13 h=" + h.height.ToString("0.00")); }
        }

        // V4 — SAT: new vs existing + new vs new.
        List<VillageLayoutData.LayoutHouse> all = new List<VillageLayoutData.LayoutHouse>(VillageLayoutData.NewHouses);
        if (VillageLayoutData.House13Builds) all.Add(VillageLayoutData.ConditionalHouse);
        foreach (VillageLayoutData.LayoutHouse h in all) CheckHouseAgainstKeepOuts(r, h);
        for (int i = 0; i < all.Count; i++)
        {
            for (int j = i + 1; j < all.Count; j++)
            {
                r.checks++;
                string err = CheckHousePair(all[i], all[j]);
                if (err != null) { r.errors++; r.Add("[ValidateVillage] " + err); }
            }
        }

        // V4b — landmark footprints vs house OBBs (SAT) and circles.
        List<VillageLayoutData.LayoutHouse> houses = new List<VillageLayoutData.LayoutHouse>(VillageLayoutData.ExistingHouses);
        houses.AddRange(VillageLayoutData.NewHouses);
        if (VillageLayoutData.House13Builds) houses.Add(VillageLayoutData.ConditionalHouse);
        foreach (VillageLayoutData.LayoutProp p in VillageLayoutData.NewLandmarks)
        {
            r.checks++;
            foreach (VillageLayoutData.LayoutHouse h in houses)
            {
                float s = SatOverlap(p.x, p.z, p.w, p.d, p.yaw, h.x, h.z, h.w, h.d, h.yaw);
                if (s > 0f) { r.errors++; r.Add("[ValidateVillage] OVERLAP landmark " + p.name + " vs " + h.name + " pen " + s.ToString("0.00")); }
            }
            foreach (VillageLayoutData.LayoutCircle c in VillageLayoutData.ExistingCircles)
            {
                // Documented exception (DESIGN §3): the chabutra is built
                // AROUND the banyan trunk — the trunk passes through the
                // platform by design, so the trunk circle is not an overlap.
                if (p.name == "Chabutra" && c.name == "Tree_Banyan") continue;
                float g = ObbCircleGap(p.x, p.z, p.w, p.d, p.yaw, c.x, c.z, c.r);
                if (g < 0.0f) { r.errors++; r.Add("[ValidateVillage] OVERLAP landmark " + p.name + " vs circle " + c.name + " gap " + g.ToString("+#0.00;-#0.00;0.00")); }
            }
        }

        // V4b (implementer-added, beyond DESIGN §7): landmark vs landmark.
        // DESIGN §3 as written placed Field_3 over CattleShed (pen 2.50)
        // and VillagePond under Clothesline_2 (pen 0.08); both were fixed
        // by the 2026-11-27 salvaged landmark positions (Field_3 -> (-2,16),
        // CattleShed -> (-13.5,18.75), Clothesline_2 x -> 14.0), so this
        // check now passes — it stays as a regression guard.
        for (int i = 0; i < VillageLayoutData.NewLandmarks.Length; i++)
        {
            for (int j = i + 1; j < VillageLayoutData.NewLandmarks.Length; j++)
            {
                r.checks++;
                VillageLayoutData.LayoutProp a = VillageLayoutData.NewLandmarks[i];
                VillageLayoutData.LayoutProp b = VillageLayoutData.NewLandmarks[j];
                float s = SatOverlap(a.x, a.z, a.w, a.d, a.yaw, b.x, b.z, b.w, b.d, b.yaw);
                if (s > 0f) { r.errors++; r.Add("[ValidateVillage] OVERLAP landmark " + a.name + " vs landmark " + b.name + " pen " + s.ToString("0.00")); }
            }
        }

        // V5 — prop/landmark centres inside any house OBB (DESIGN §7).
        // Exemption: the kirana counter is its own house's shop front — its
        // centre is outside House_12's OBB anyway; the exemption is kept so
        // a future 0.0 m nudge does not false-positive.
        foreach (VillageLayoutData.LayoutProp p in VillageLayoutData.NewLandmarks)
        {
            r.checks++;
            foreach (VillageLayoutData.LayoutHouse h in houses)
            {
                if (p.name == "KiranaCounter" && h.name == "House_12") continue;
                if (PtInObb(p.x, p.z, h.x, h.z, h.w, h.d, h.yaw))
                {
                    r.errors++; r.Add("[ValidateVillage] PROP-IN-WALL " + p.name + " in " + h.name);
                }
            }
        }

        // Banned names on the seeded rows.
        foreach (VillageLayoutData.LayoutHouse h in all)
        {
            r.checks++;
            foreach (string banned in VillageLayoutData.BannedNames)
            {
                if (h.name.Contains(banned)) { r.errors++; r.Add("[ValidateVillage] BANNED-NAME " + h.name + " contains '" + banned + "'"); }
            }
        }
        foreach (VillageLayoutData.LayoutProp p in VillageLayoutData.NewLandmarks)
        {
            r.checks++;
            foreach (string banned in VillageLayoutData.BannedNames)
            {
                if (p.name.Contains(banned)) { r.errors++; r.Add("[ValidateVillage] BANNED-NAME " + p.name + " contains '" + banned + "'"); }
            }
        }

        r.Add("--- ValidateVillage (edit-mode layout data): " + r.checks + " checks, " + r.errors + " errors ---");
        return r;
    }

    // ---- Live validation: V1–V8 over the built village (DESIGN §7.1) ----
    // Runs at the end of the build (after Moon, before FinishBuild) and
    // from the editor menu in PlayMode. Never throws; errors never block
    // FinishBuild. Fixes floating/sunk only.
    public static int ValidateLive(VillageBuilder builder)
    {
        CheckResult r = new CheckResult();
        r.Add("[ValidateVillage] Live validation started");
        List<VillageLayoutData.ExpansionEntry> entries = builder.GetExpansionEntries();
        HashSet<string> colliderNames = builder.GetExpansionColliderNames();

        // V1/V2 — grounding (fix floating/sunk by shifting to expMinY).
        foreach (VillageLayoutData.ExpansionEntry e in entries)
        {
            r.checks++;
            try
            {
                GameObject go = GameObject.Find(e.name);
                if (go == null) { r.errors++; r.Add("[ValidateVillage] MISSING " + e.name + " (in layout list, not in scene)"); continue; }
                Bounds b = GetBounds(go);
                float minY = b.min.y;
                if (minY > e.expMinY + 0.07f)
                {
                    go.transform.position -= Vector3.up * (minY - e.expMinY);
                    r.fixedCount++;
                    r.Add("[ValidateVillage] FLOATING " + e.name + " min.y " + minY.ToString("0.00") + " -> " + e.expMinY.ToString("0.00"));
                }
                else if (minY < e.expMinY - 0.07f)
                {
                    go.transform.position -= Vector3.up * (minY - e.expMinY);
                    r.fixedCount++;
                    r.Add("[ValidateVillage] SUNK " + e.name + " min.y " + minY.ToString("0.00") + " -> " + e.expMinY.ToString("0.00"));
                }
            }
            catch (System.Exception ex) { r.errors++; r.Add("[ValidateVillage] V1/V2 threw on " + e.name + ": " + ex.Message); }
        }

        // V3 — house scale vs the §1.1 table (measured live bounds).
        foreach (VillageLayoutData.ExpansionEntry e in entries)
        {
            if (e.kind != "house") continue;
            r.checks++;
            try
            {
                GameObject go = GameObject.Find(e.name);
                if (go == null) continue; // already reported missing above
                Bounds b = GetBounds(go);
                // yaw 90 OR -90 swaps the footprint axes (House_12 is yaw
                // -90: its front faces east, so its live x-size is expD).
                bool swapped = Mathf.Abs(Mathf.Abs(e.yaw) - 90f) < 1f;
                float expX = swapped ? e.expD : e.expW;
                float expZ = swapped ? e.expW : e.expD;
                if (Mathf.Abs(b.size.y - e.expH) > 0.10f)
                {
                    r.errors++;
                    r.Add("[ValidateVillage] SCALE " + e.name + " h=" + b.size.y.ToString("0.00") + " expected " + e.expH.ToString("0.00") + " +/-0.10");
                }
                if (Mathf.Abs(b.size.x - expX) > 0.20f || Mathf.Abs(b.size.z - expZ) > 0.20f)
                {
                    r.errors++;
                    r.Add("[ValidateVillage] SCALE " + e.name + " fp=" + b.size.x.ToString("0.00") + "x" + b.size.z.ToString("0.00")
                        + " expected " + expX.ToString("0.00") + "x" + expZ.ToString("0.00") + " +/-0.20");
                }
            }
            catch (System.Exception ex) { r.errors++; r.Add("[ValidateVillage] V3 threw on " + e.name + ": " + ex.Message); }
        }

        // V4 — SAT over live house centres (table w/d/yaw) + existing houses.
        try
        {
            r.checks++;
            List<VillageLayoutData.LayoutHouse> live = new List<VillageLayoutData.LayoutHouse>();
            foreach (VillageLayoutData.ExpansionEntry e in entries)
            {
                if (e.kind != "house") continue;
                GameObject go = GameObject.Find(e.name);
                if (go == null) continue;
                Bounds b = GetBounds(go);
                live.Add(new VillageLayoutData.LayoutHouse(e.name, e.file, b.center.x, b.center.z, e.yaw, e.expH, e.expW, e.expD, true, e.name == "House_13"));
            }
            foreach (VillageLayoutData.LayoutHouse h in live)
            {
                foreach (VillageLayoutData.LayoutHouse ex in VillageLayoutData.ExistingHouses)
                {
                    string err = CheckHousePair(h, ex);
                    if (err != null) { r.errors++; r.Add("[ValidateVillage] " + err); }
                }
            }
            for (int i = 0; i < live.Count; i++)
            {
                for (int j = i + 1; j < live.Count; j++)
                {
                    string err = CheckHousePair(live[i], live[j]);
                    if (err != null) { r.errors++; r.Add("[ValidateVillage] " + err); }
                }
            }
        }
        catch (System.Exception ex) { r.errors++; r.Add("[ValidateVillage] V4 threw: " + ex.Message); }

        // V4b — live landmark footprints vs houses / circles / each other.
        // Implementer-added (DESIGN §7 has no landmark-footprint SAT; its
        // V5 is centre-only). DESIGN §3 as written placed Field_2 over
        // House_10 (pen 1.00), Haystack_1 inside the Ambassador (pen 0.92),
        // Haystack_2 over House_4's circle (-0.52), Haystack_3 over
        // House_10 (pen 0.35), LampPost_5 clipping House_12 (pen 0.05),
        // Field_3 over the cattle shed (pen 2.50) and the pond under
        // Clothesline_2 (pen 0.08). All seven were fixed by the 2026-11-27
        // salvaged landmark positions (see VillageLayoutData) — the
        // validator reports any regression so the parent can order nudges;
        // the build does not move anything to hide them (except
        // Clothesline_1, see the builder).
        try
        {
            r.checks++;
            List<VillageLayoutData.LayoutHouse> houses4b = new List<VillageLayoutData.LayoutHouse>(VillageLayoutData.ExistingHouses);
            List<VillageLayoutData.ExpansionEntry> lmEntries = new List<VillageLayoutData.ExpansionEntry>();
            foreach (VillageLayoutData.ExpansionEntry e in entries)
            {
                if (e.kind == "house")
                {
                    GameObject go = GameObject.Find(e.name);
                    if (go == null) continue;
                    Bounds b = GetBounds(go);
                    houses4b.Add(new VillageLayoutData.LayoutHouse(e.name, e.file, b.center.x, b.center.z, e.yaw, e.expH, e.expW, e.expD, true, false));
                }
                else if (e.kind != "animal")
                {
                    lmEntries.Add(e);
                }
            }
            foreach (VillageLayoutData.ExpansionEntry e in lmEntries)
            {
                GameObject go = GameObject.Find(e.name);
                if (go == null) continue;
                Bounds b = GetBounds(go);
                float cx = b.center.x, cz = b.center.z;
                float w = b.size.x, d = b.size.z;
                foreach (VillageLayoutData.LayoutHouse h in houses4b)
                {
                    float s = SatOverlap(cx, cz, w, d, e.yaw, h.x, h.z, h.w, h.d, h.yaw);
                    if (s > 0f) { r.errors++; r.Add("[ValidateVillage] OVERLAP landmark " + e.name + " vs " + h.name + " pen " + s.ToString("0.00")); }
                }
                foreach (VillageLayoutData.LayoutCircle c in VillageLayoutData.ExistingCircles)
                {
                    if (e.name == "Chabutra" && c.name == "Tree_Banyan") continue;
                    float g = ObbCircleGap(cx, cz, w, d, e.yaw, c.x, c.z, c.r);
                    if (g < 0f) { r.errors++; r.Add("[ValidateVillage] OVERLAP landmark " + e.name + " vs circle " + c.name + " gap " + g.ToString("+#0.00;-#0.00;0.00")); }
                }
            }
            for (int i = 0; i < lmEntries.Count; i++)
            {
                for (int j = i + 1; j < lmEntries.Count; j++)
                {
                    GameObject goA = GameObject.Find(lmEntries[i].name);
                    GameObject goB = GameObject.Find(lmEntries[j].name);
                    if (goA == null || goB == null) continue;
                    Bounds ba = GetBounds(goA), bb = GetBounds(goB);
                    // Vertical separation: baskets sit ON the counter, the
                    // hanging lantern hangs UNDER the awning, the shed roof
                    // sits ON its posts — 2D overlap there is stacking,
                    // not a collision. Only flag pairs that overlap in Y.
                    if (ba.min.y >= bb.max.y - 0.02f || bb.min.y >= ba.max.y - 0.02f) continue;
                    float s = SatOverlap(ba.center.x, ba.center.z, ba.size.x, ba.size.z, lmEntries[i].yaw,
                        bb.center.x, bb.center.z, bb.size.x, bb.size.z, lmEntries[j].yaw);
                    if (s > 0f) { r.errors++; r.Add("[ValidateVillage] OVERLAP landmark " + lmEntries[i].name + " vs landmark " + lmEntries[j].name + " pen " + s.ToString("0.00")); }
                }
            }
        }
        catch (System.Exception ex) { r.errors++; r.Add("[ValidateVillage] V4b threw: " + ex.Message); }

        // V5 — prop centres inside any house OBB (live centres; animals skip —
        // they roam, so a static point test is meaningless for them).
        try
        {
            r.checks++;
            List<VillageLayoutData.LayoutHouse> houses = new List<VillageLayoutData.LayoutHouse>(VillageLayoutData.ExistingHouses);
            foreach (VillageLayoutData.ExpansionEntry e in entries)
            {
                if (e.kind != "house") continue;
                GameObject go = GameObject.Find(e.name);
                if (go == null) continue;
                Bounds b = GetBounds(go);
                houses.Add(new VillageLayoutData.LayoutHouse(e.name, e.file, b.center.x, b.center.z, e.yaw, e.expH, e.expW, e.expD, true, false));
            }
            foreach (VillageLayoutData.ExpansionEntry e in entries)
            {
                if (e.kind == "house" || e.kind == "animal") continue;
                GameObject go = GameObject.Find(e.name);
                if (go == null) continue;
                Bounds b = GetBounds(go);
                foreach (VillageLayoutData.LayoutHouse h in houses)
                {
                    if (e.name == "KiranaCounter" && h.name == "House_12") continue;
                    if (PtInObb(b.center.x, b.center.z, h.x, h.z, h.w, h.d, h.yaw))
                    {
                        r.errors++; r.Add("[ValidateVillage] PROP-IN-WALL " + e.name + " in " + h.name);
                    }
                }
            }
        }
        catch (System.Exception ex) { r.errors++; r.Add("[ValidateVillage] V5 threw: " + ex.Message); }

        // V6 — every entry that needs a collider has a recorded registration.
        foreach (VillageLayoutData.ExpansionEntry e in entries)
        {
            if (!e.needsCollider) continue;
            r.checks++;
            try
            {
                if (!colliderNames.Contains(e.name))
                {
                    GameObject go = GameObject.Find(e.name);
                    if (go != null)
                    {
                        VillageColliders.Register(GetBounds(go));
                        r.Add("[ValidateVillage] NO-COLLIDER " + e.name + " — late-registered from live bounds");
                    }
                    else
                    {
                        r.errors++; r.Add("[ValidateVillage] NO-COLLIDER " + e.name + " — object missing, cannot register");
                    }
                }
            }
            catch (System.Exception ex) { r.errors++; r.Add("[ValidateVillage] V6 threw on " + e.name + ": " + ex.Message); }
        }

        // V7 — monster path clear (live CanMoveTo probes on the free points
        // + seeded SAT distances for the new houses).
        try
        {
            r.checks++;
            Vector3[] free = new Vector3[]
            {
                new Vector3(0f, 0.5f, 0f), new Vector3(0f, 0.5f, 10f), new Vector3(-3.5f, 0.5f, -12f),
            };
            foreach (Vector3 p in free)
            {
                if (!VillageColliders.CanMoveTo(p, 0.45f))
                {
                    r.errors++; r.Add("[ValidateVillage] MONSTER-PATH free point blocked: " + p.ToString("F1"));
                }
            }
            foreach (VillageLayoutData.ExpansionEntry e in entries)
            {
                if (e.kind != "house") continue;
                GameObject go = GameObject.Find(e.name);
                if (go == null) continue;
                Bounds b = GetBounds(go);
                for (int i = 0; i + 1 < VillageLayoutData.MonsterPath.Length; i++)
                {
                    Vector2 a = VillageLayoutData.MonsterPath[i], c = VillageLayoutData.MonsterPath[i + 1];
                    float d = ObbSegDist(b.center.x, b.center.z, e.expW, e.expD, e.yaw, a.x, a.y, c.x, c.y);
                    if (d < 0.45f)
                    {
                        r.errors++; r.Add("[ValidateVillage] MONSTER-PATH " + e.name + " dist " + d.ToString("0.00") + " to segment (" + a.x + "," + a.y + ")-(" + c.x + "," + c.y + ")");
                    }
                }
            }
        }
        catch (System.Exception ex) { r.errors++; r.Add("[ValidateVillage] V7 threw: " + ex.Message); }

        // V8 — test-point regression (blocked must stay blocked, Meera free).
        try
        {
            r.checks++;
            Vector3[] blocked = new Vector3[]
            {
                new Vector3(9f, 0.5f, 8f), new Vector3(2f, 0.5f, -10.5f),
                new Vector3(0f, 0.5f, 20.5f), new Vector3(-4f, 0.5f, -3.5f),
            };
            foreach (Vector3 p in blocked)
            {
                if (VillageColliders.CanMoveTo(p, 0.45f))
                {
                    r.errors++; r.Add("[ValidateVillage] REGRESSION blocked point now free: " + p.ToString("F1"));
                }
            }
            if (!VillageColliders.CanMoveTo(new Vector3(-2f, 0.5f, 4f), 0.45f))
            {
                r.errors++; r.Add("[ValidateVillage] REGRESSION Meera spot (-2,4) now statically blocked");
            }
        }
        catch (System.Exception ex) { r.errors++; r.Add("[ValidateVillage] V8 threw: " + ex.Message); }

        // Banned names — live scan of the whole Village root (any hit is an
        // error no matter who created it; the expansion's own names are also
        // checked via the entries above).
        try
        {
            r.checks++;
            Transform root = builder.VillageRootTransform;
            if (root != null)
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    foreach (string banned in VillageLayoutData.BannedNames)
                    {
                        if (t.name.Contains(banned))
                        {
                            r.errors++; r.Add("[ValidateVillage] BANNED-NAME live object '" + t.name + "' contains '" + banned + "'");
                        }
                    }
                }
            }
        }
        catch (System.Exception ex) { r.errors++; r.Add("[ValidateVillage] banned-name scan threw: " + ex.Message); }

        // Perf §8.1 — new-material count (pond water + awning cloth are the
        // budget of 2; per-clone wear tints are logged, not failed — see the
        // build report for the deviation note).
        try
        {
            List<string> newMats = builder.GetExpansionNewMaterialNames();
            r.Add("[ValidateVillage] New materials created by the expansion: " + newMats.Count
                + " [" + string.Join(", ", newMats.ToArray()) + "] (budget <= 2 per DESIGN §8.1)");
            if (newMats.Count > 2)
                r.Add("[ValidateVillage] WARNING: new-material count " + newMats.Count + " exceeds the §8.1 budget of 2 (wear tints — see build report)");
        }
        catch (System.Exception ex) { r.Add("[ValidateVillage] material count skipped: " + ex.Message); }

        // Pond still active after CleanupStrays (DESIGN §10.2 R11).
        try
        {
            r.checks++;
            GameObject pond = GameObject.Find("VillagePond");
            if (pond == null) { r.errors++; r.Add("[ValidateVillage] VillagePond missing after build (CleanupStrays risk, R11)"); }
            else if (!pond.activeInHierarchy) { r.errors++; r.Add("[ValidateVillage] VillagePond DISABLED after build (CleanupStrays risk, R11)"); }
        }
        catch (System.Exception ex) { r.errors++; r.Add("[ValidateVillage] pond check threw: " + ex.Message); }

        r.Add("--- ValidateVillage: " + r.checks + " checks, " + r.errors + " errors, " + r.fixedCount + " fixed ---");
        return r.errors;
    }
}
