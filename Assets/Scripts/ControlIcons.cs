using UnityEngine;

/// <summary>
/// Procedurally generated control icons + round button sprite.
/// The UI is built entirely at runtime (no asset files), and the
/// built-in font has no icon glyphs, so icons are rasterised here into
/// white-on-transparent textures (256x256, mipmapped, trilinear) with
/// 1 px soft edges. Button backgrounds use the same rasteriser for a
/// clean circle sprite instead of the stretched 1x1 white square.
/// </summary>
public static class ControlIcons
{
    public enum IconKind { Lock, Flame, Run, Jump, Hand }

    private const int S = 256;
    private static Sprite s_circle;
    private static Sprite s_ring;
    private static Sprite s_triangle;
    private static readonly System.Collections.Generic.Dictionary<IconKind, Sprite> s_icons
        = new System.Collections.Generic.Dictionary<IconKind, Sprite>();

    // White filled circle with a soft rim — button / joystick background.
    public static Sprite CircleSprite()
    {
        if (s_circle == null)
        {
            float[] a = new float[S * S];
            Disc(a, S / 2f, S / 2f, S / 2f - 2f);
            s_circle = SpriteFromAlpha(a, "ControlCircle");
        }
        return s_circle;
    }

    // Thin white ring — rim for buttons / joystick base (gives the flat
    // disc definition, like the reference image's outlined controls).
    public static Sprite RingSprite()
    {
        if (s_ring == null)
        {
            float[] a = new float[S * S];
            Ring(a, 128f, 128f, 116f, 10f, 0f, 360f);
            s_ring = SpriteFromAlpha(a, "ControlRing");
        }
        return s_ring;
    }

    // Filled up-pointing triangle — joystick directional indicator.
    // Rotate the RectTransform for down/left/right.
    public static Sprite TriangleSprite()
    {
        if (s_triangle == null)
        {
            float[] a = new float[S * S];
            Triangle(a, 128f, 128f, 78f);
            s_triangle = SpriteFromAlpha(a, "ControlTriangle");
        }
        return s_triangle;
    }

    public static Sprite IconSprite(IconKind kind)
    {
        if (s_icons.TryGetValue(kind, out Sprite s)) return s;
        float[] a = new float[S * S];
        switch (kind)
        {
            case IconKind.Lock: DrawLock(a); break;
            case IconKind.Flame: DrawFlame(a); break;
            case IconKind.Run: DrawRun(a); break;
            case IconKind.Jump: DrawJump(a); break;
            case IconKind.Hand: DrawHand(a); break;
        }
        s = SpriteFromAlpha(a, "Icon_" + kind);
        s_icons[kind] = s;
        return s;
    }

    // ------------------------------------------------------------------
    // Icon compositions (canvas coords: x right, y DOWN, origin top-left)
    // ------------------------------------------------------------------

    private static void DrawLock(float[] a)
    {
        // Shackle: upper-half ring.
        Ring(a, 128f, 112f, 40f, 15f, 180f, 360f);
        // Body: rounded rect.
        RoundRect(a, 86f, 116f, 170f, 200f, 10f);
        // Keyhole: punched circle + slot.
        PunchDisc(a, 128f, 148f, 10f);
        PunchRect(a, 122f, 148f, 134f, 176f);
    }

    private static void DrawFlame(float[] a)
    {
        // Teardrop flame: three stacked discs, tip on top.
        Disc(a, 128f, 152f, 54f);
        Disc(a, 128f, 102f, 36f);
        Disc(a, 128f, 72f, 18f);
        // Inner flame core (punched hole shows the dark button behind).
        PunchDisc(a, 128f, 164f, 20f);
    }

    private static void DrawRun(float[] a)
    {
        // Running figure, facing right. Head + leaning torso + swinging
        // arms + bent legs (two segments each).
        Disc(a, 152f, 60f, 17f);                       // head
        Capsule(a, 140f, 80f, 118f, 134f, 17f);        // torso
        Capsule(a, 134f, 94f, 166f, 80f, 13f);         // front arm
        Capsule(a, 134f, 94f, 102f, 116f, 13f);        // back arm
        Capsule(a, 118f, 134f, 152f, 178f, 15f);       // front thigh
        Capsule(a, 152f, 178f, 172f, 202f, 13f);       // front shin
        Capsule(a, 118f, 134f, 86f, 170f, 15f);        // back thigh
        Capsule(a, 86f, 170f, 66f, 196f, 13f);         // back shin
    }

    private static void DrawJump(float[] a)
    {
        // Bold chevron ^ over a ground line = jump.
        Capsule(a, 82f, 152f, 128f, 88f, 24f);
        Capsule(a, 128f, 88f, 174f, 152f, 24f);
        RoundRect(a, 96f, 182f, 160f, 196f, 6f);
    }

    private static void DrawHand(float[] a)
    {
        // Raised hand: palm + 4 fingers + thumb.
        RoundRect(a, 102f, 126f, 154f, 198f, 10f);     // palm
        Capsule(a, 110f, 128f, 110f, 74f, 14f);        // index
        Capsule(a, 124f, 128f, 124f, 66f, 14f);        // middle
        Capsule(a, 138f, 128f, 138f, 70f, 14f);        // ring
        Capsule(a, 152f, 128f, 152f, 80f, 14f);        // pinky
        Capsule(a, 104f, 162f, 80f, 142f, 14f);        // thumb
    }

    // ------------------------------------------------------------------
    // Rasteriser (alpha buffer, 1 px soft edges via signed distance)
    // ------------------------------------------------------------------

    private static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }

    private static void Disc(float[] a, float cx, float cy, float r)
    {
        int x0 = Mathf.Max(0, (int)(cx - r - 2));
        int x1 = Mathf.Min(S - 1, (int)(cx + r + 2));
        int y0 = Mathf.Max(0, (int)(cy - r - 2));
        int y1 = Mathf.Min(S - 1, (int)(cy + r + 2));
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
                float cov = Clamp01(1f - d);
                int i = y * S + x;
                if (cov > a[i]) a[i] = cov;
            }
        }
    }

    // Ring arc: angle in degrees, 0 = +x, 90 = DOWN (y-down coords),
    // so the upper half is 180..360.
    private static void Ring(float[] a, float cx, float cy, float r, float w, float a0deg, float a1deg)
    {
        int x0 = Mathf.Max(0, (int)(cx - r - w));
        int x1 = Mathf.Min(S - 1, (int)(cx + r + w));
        int y0 = Mathf.Max(0, (int)(cy - r - w));
        int y1 = Mathf.Min(S - 1, (int)(cy + r + w));
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                float dx = x - cx, dy = y - cy;
                float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                if (ang < 0f) ang += 360f;
                if (ang < a0deg || ang > a1deg) continue;
                float d = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - r) - w / 2f;
                float cov = Clamp01(1f - d);
                int i = y * S + x;
                if (cov > a[i]) a[i] = cov;
            }
        }
    }

    // Filled up-pointing triangle (y-down coords): apex top, base
    // bottom. Barycentric test; coverage softens 1 px at the edges
    // (min barycentric weight scaled by the triangle height).
    private static void Triangle(float[] a, float cx, float cy, float s)
    {
        float ax = cx, ay = cy - s;                 // apex
        float bx = cx - s * 0.85f, by = cy + s * 0.65f; // bottom-left
        float cxx = cx + s * 0.85f, cyy = cy + s * 0.65f; // bottom-right
        float den = (by - cyy) * (ax - cxx) + (cxx - bx) * (ay - cyy);
        if (Mathf.Abs(den) < 0.001f) return;
        int x0 = Mathf.Max(0, (int)(cx - s - 2));
        int x1 = Mathf.Min(S - 1, (int)(cx + s + 2));
        int y0 = Mathf.Max(0, (int)(cy - s - 2));
        int y1 = Mathf.Min(S - 1, (int)(cy + s + 2));
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                float w0 = ((by - cyy) * (x - cxx) + (cxx - bx) * (y - cyy)) / den;
                float w1 = ((cyy - ay) * (x - cxx) + (ax - cxx) * (y - cyy)) / den;
                float w2 = 1f - w0 - w1;
                float m = Mathf.Min(w0, Mathf.Min(w1, w2));
                if (m < 0f) continue;
                float cov = Clamp01(1f + m * s * 1.6f);
                int i = y * S + x;
                if (cov > a[i]) a[i] = cov;
            }
        }
    }

    private static void RoundRect(float[] a, float x0, float y0, float x1, float y1, float r)
    {
        // Box SDF (negative inside) + corner rounding via r.
        int ix0 = Mathf.Max(0, (int)(x0 - 2));
        int ix1 = Mathf.Min(S - 1, (int)(x1 + 2));
        int iy0 = Mathf.Max(0, (int)(y0 - 2));
        int iy1 = Mathf.Min(S - 1, (int)(y1 + 2));
        float cx = (x0 + x1) / 2f, cy = (y0 + y1) / 2f;
        float hx = (x1 - x0) / 2f - r, hy = (y1 - y0) / 2f - r;
        for (int y = iy0; y <= iy1; y++)
        {
            for (int x = ix0; x <= ix1; x++)
            {
                float qx = Mathf.Abs(x - cx) - hx;
                float qy = Mathf.Abs(y - cy) - hy;
                float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
                float d = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
                float cov = Clamp01(1f - d);
                int i = y * S + x;
                if (cov > a[i]) a[i] = cov;
            }
        }
    }

    private static void Capsule(float[] a, float x0, float y0, float x1, float y1, float w)
    {
        int ix0 = Mathf.Max(0, (int)(Mathf.Min(x0, x1) - w));
        int ix1 = Mathf.Min(S - 1, (int)(Mathf.Max(x0, x1) + w));
        int iy0 = Mathf.Max(0, (int)(Mathf.Min(y0, y1) - w));
        int iy1 = Mathf.Min(S - 1, (int)(Mathf.Max(y0, y1) + w));
        float dx = x1 - x0, dy = y1 - y0;
        float len2 = dx * dx + dy * dy;
        for (int y = iy0; y <= iy1; y++)
        {
            for (int x = ix0; x <= ix1; x++)
            {
                float t = len2 > 0f ? ((x - x0) * dx + (y - y0) * dy) / len2 : 0f;
                t = Mathf.Clamp01(t);
                float px = x0 + dx * t, py = y0 + dy * t;
                float d = Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py)) - w / 2f;
                float cov = Clamp01(1f - d);
                int i = y * S + x;
                if (cov > a[i]) a[i] = cov;
            }
        }
    }

    // Punch a hole (keyhole / flame core): multiply alpha towards 0.
    private static void PunchDisc(float[] a, float cx, float cy, float r)
    {
        int x0 = Mathf.Max(0, (int)(cx - r - 2));
        int x1 = Mathf.Min(S - 1, (int)(cx + r + 2));
        int y0 = Mathf.Max(0, (int)(cy - r - 2));
        int y1 = Mathf.Min(S - 1, (int)(cy + r + 2));
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
                float keep = Clamp01(d + 0.5f);
                int i = y * S + x;
                a[i] *= keep;
            }
        }
    }

    private static void PunchRect(float[] a, float x0, float y0, float x1, float y1)
    {
        int ix0 = Mathf.Max(0, (int)x0);
        int ix1 = Mathf.Min(S - 1, (int)x1);
        int iy0 = Mathf.Max(0, (int)y0);
        int iy1 = Mathf.Min(S - 1, (int)y1);
        for (int y = iy0; y <= iy1; y++)
        {
            for (int x = ix0; x <= ix1; x++)
            {
                a[y * S + x] = 0f;
            }
        }
    }

    private static Sprite SpriteFromAlpha(float[] a, string name)
    {
        Texture2D t = new Texture2D(S, S, TextureFormat.RGBA32, true);
        t.filterMode = FilterMode.Trilinear;
        t.wrapMode = TextureWrapMode.Clamp;
        Color32[] px = new Color32[S * S];
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                // Buffer row 0 is the TOP; texture row 0 is the BOTTOM.
                float cov = a[(S - 1 - y) * S + x];
                byte v = (byte)Mathf.RoundToInt(Clamp01(cov) * 255f);
                px[y * S + x] = new Color32(255, 255, 255, v);
            }
        }
        t.SetPixels32(px);
        t.Apply(true, false);
        return Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
    }
}
