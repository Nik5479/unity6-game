using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// OVERNIGHT R2 (2026-11-09): grid A* pathfinding over the village.
/// The monster used to walk straight-line (MoveTowards) through walls,
/// houses and trees (audit 4.6b / 6.5). There is no baked Unity NavMesh
/// in this project — the village is built at runtime from glTF — so the
/// pathfinder reuses the SAME collision registry the player uses
/// (VillageColliders.CanMoveTo) on a 1 m grid. Any path it returns is
/// walkable by the same rules the player obeys, so it can never cut a
/// wall, a house or a fence. 8-direction movement, no diagonal
/// corner-cutting, greedy line-of-sight smoothing. Deterministic;
/// working arrays are static and reused (no per-call allocation beyond
/// the caller's output list). If no path exists it returns false and the
/// caller falls back to straight-line movement (never worse than before)
/// and logs a warning.
/// </summary>
public static class VillagePathfinder
{
    private const float kCell = 1f;
    private const int kHalf = 26; // world x/z in [-26, 26]
    private const int kSize = 2 * kHalf + 1; // 53 cells per axis
    private const int kCount = kSize * kSize;

    // A* working arrays (static = reused across calls).
    private static readonly float[] s_g = new float[kCount];
    private static readonly int[] s_parent = new int[kCount];
    private static readonly byte[] s_state = new byte[kCount]; // 0 unseen, 1 open, 2 closed
    private static readonly List<int> s_open = new List<int>(256);

    private static int CellIndex(int ix, int iz) { return iz * kSize + ix; }
    private static int WorldToCell(float v) { return Mathf.Clamp(Mathf.RoundToInt(v / kCell) + kHalf, 0, kSize - 1); }
    private static float CellToWorld(int i) { return (i - kHalf) * kCell; }

    private static bool CellBlocked(int ix, int iz, float radius)
    {
        // Probe at y=1.0 (centre height): CanMoveTo's feet-based interval
        // is [0.5, 2.3], matching a ~2 m walker. Houses, fences, trees,
        // the well and props are all registered AABBs; open ground is free.
        return !VillageColliders.CanMoveTo(new Vector3(CellToWorld(ix), 1.0f, CellToWorld(iz)), radius);
    }

    private static float Heuristic(int cell, int gx, int gz)
    {
        int dx = Mathf.Abs(cell % kSize - gx);
        int dz = Mathf.Abs(cell / kSize - gz);
        int mn = Mathf.Min(dx, dz), mx = Mathf.Max(dx, dz);
        return mx + 0.4142f * mn; // octile distance
    }

    // Nearest free cell to (ix,iz) within 3 cells (expanding square).
    private static bool NearestFree(ref int ix, ref int iz, float radius)
    {
        for (int r = 1; r <= 3; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dz = -r; dz <= r; dz++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                    int nx = ix + dx, nz = iz + dz;
                    if (nx < 0 || nz < 0 || nx >= kSize || nz >= kSize) continue;
                    if (!CellBlocked(nx, nz, radius)) { ix = nx; iz = nz; return true; }
                }
            }
        }
        return false;
    }

    /// <summary>True when the straight segment a-&gt;b is walkable (checked
    /// every 0.4 m). Used for path smoothing and for the monster's vision.</summary>
    public static bool HasLineOfSight(Vector3 a, Vector3 b, float radius)
    {
        Vector3 flatA = new Vector3(a.x, 1.0f, a.z);
        Vector3 flatB = new Vector3(b.x, 1.0f, b.z);
        float dist = Vector3.Distance(flatA, flatB);
        int steps = Mathf.Max(1, Mathf.CeilToInt(dist / 0.4f));
        for (int i = 1; i < steps; i++)
        {
            Vector3 p = Vector3.Lerp(flatA, flatB, (float)i / steps);
            if (!VillageColliders.CanMoveTo(p, radius)) return false;
        }
        return VillageColliders.CanMoveTo(flatB, radius);
    }

    /// <summary>Find a walkable path from -&gt;to. Waypoints are world XZ
    /// positions (y = 0). Returns false when no path exists.</summary>
    public static bool FindPath(Vector3 from, Vector3 to, float radius, List<Vector3> outPath)
    {
        if (outPath == null) return false;
        outPath.Clear();
        int sx = WorldToCell(from.x), sz = WorldToCell(from.z);
        int gx = WorldToCell(to.x), gz = WorldToCell(to.z);
        // Goal inside a footprint (e.g. a doorway target)? Aim at the
        // nearest free cell instead of failing outright.
        if (CellBlocked(gx, gz, radius) && !NearestFree(ref gx, ref gz, radius)) return false;
        if (CellBlocked(sx, sz, radius) && !NearestFree(ref sx, ref sz, radius)) return false;
        if (sx == gx && sz == gz)
        {
            outPath.Add(new Vector3(CellToWorld(gx), 0f, CellToWorld(gz)));
            return true;
        }

        for (int i = 0; i < kCount; i++) { s_g[i] = float.MaxValue; s_state[i] = 0; s_parent[i] = -1; }
        s_open.Clear();
        int start = CellIndex(sx, sz), goal = CellIndex(gx, gz);
        s_g[start] = 0f;
        s_state[start] = 1;
        s_open.Add(start);
        int[] dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        int[] dz = { 0, 0, 1, -1, 1, -1, 1, -1 };
        float[] stepCost = { 1f, 1f, 1f, 1f, 1.4142f, 1.4142f, 1.4142f, 1.4142f };
        int found = -1;
        int guard = 0;
        while (s_open.Count > 0 && guard++ < kCount * 4)
        {
            // Linear min-scan: the grid is small (53x53) and a heap would
            // only save microseconds here.
            int best = -1;
            float bestF = float.MaxValue;
            for (int i = 0; i < s_open.Count; i++)
            {
                int c = s_open[i];
                float f = s_g[c] + Heuristic(c, gx, gz);
                if (f < bestF) { bestF = f; best = i; }
            }
            int cur = s_open[best];
            s_open.RemoveAt(best);
            s_state[cur] = 2;
            if (cur == goal) { found = cur; break; }
            int cx = cur % kSize, cz = cur / kSize;
            for (int d = 0; d < 8; d++)
            {
                int nx = cx + dx[d], nz = cz + dz[d];
                if (nx < 0 || nz < 0 || nx >= kSize || nz >= kSize) continue;
                int ni = CellIndex(nx, nz);
                if (s_state[ni] == 2) continue;
                if (CellBlocked(nx, nz, radius)) { s_state[ni] = 2; continue; }
                // No diagonal corner-cutting: both orthogonal neighbours
                // of a diagonal step must be free, or the path would clip
                // the corner of a wall.
                if (d >= 4 && (CellBlocked(cx + dx[d], cz, radius) || CellBlocked(cx, cz + dz[d], radius))) continue;
                float ng = s_g[cur] + stepCost[d];
                if (s_state[ni] != 1 || ng < s_g[ni])
                {
                    s_g[ni] = ng;
                    s_parent[ni] = cur;
                    if (s_state[ni] != 1) { s_state[ni] = 1; s_open.Add(ni); }
                }
            }
        }
        if (found < 0) return false;

        // Reconstruct goal -> start, then reverse.
        List<Vector3> raw = new List<Vector3>();
        int c2 = found;
        while (c2 >= 0)
        {
            raw.Add(new Vector3(CellToWorld(c2 % kSize), 0f, CellToWorld(c2 / kSize)));
            c2 = s_parent[c2];
        }
        raw.Reverse();
        // Greedy LOS smoothing: from each kept point, jump to the furthest
        // later point with a clear line of sight.
        outPath.Add(raw[0]);
        int idx = 0;
        while (idx < raw.Count - 1)
        {
            int furthest = idx + 1;
            for (int j = raw.Count - 1; j > idx + 1; j--)
            {
                if (HasLineOfSight(raw[idx], raw[j], radius)) { furthest = j; break; }
            }
            outPath.Add(raw[furthest]);
            idx = furthest;
        }
        return true;
    }
}
