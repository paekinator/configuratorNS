using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Small monochrome icon sprites drawn at runtime (96 px, white, antialiased)
/// for UI that is built from code — no texture assets to ship or keep in sync.
/// The Image tint provides the colour. Same SDF-style rasterizing as the
/// editor-built HistorySwirlIcon, so the visual language matches.
///
/// Available: "Dimension" (|←→| measure glyph), "Copy" (two offset sheets),
/// "Trash" (bin with lid), "Palette" (colour wheel with wells).
/// </summary>
public static class UIIcons
{
    const int Size = 96;

    static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

    public static Sprite Get(string name)
    {
        if (_cache.TryGetValue(name, out Sprite cached) && cached != null)
            return cached;

        float[] a = new float[Size * Size];
        switch (name)
        {
            case "Dimension": DrawDimension(a); break;
            case "Copy": DrawCopy(a); break;
            case "Trash": DrawTrash(a); break;
            case "Palette": DrawPalette(a); break;
            default: return null;
        }

        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        var px = new Color32[Size * Size];
        for (int i = 0; i < a.Length; i++)
            px[i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a[i]) * 255f));
        tex.SetPixels32(px);
        tex.Apply(false, true);

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        sprite.name = "UIIcon_" + name;
        _cache[name] = sprite;
        return sprite;
    }

    // ------------------------------------------------------------------
    // Glyphs
    // ------------------------------------------------------------------

    /// <summary>Two end stops with a double-headed arrow between: "measure".</summary>
    static void DrawDimension(float[] a)
    {
        const float stroke = 5f;
        Line(a, new Vector2(16f, 26f), new Vector2(16f, 70f), stroke);
        Line(a, new Vector2(80f, 26f), new Vector2(80f, 70f), stroke);
        Line(a, new Vector2(28f, 48f), new Vector2(68f, 48f), stroke);
        Triangle(a, new Vector2(21f, 48f), new Vector2(37f, 58f), new Vector2(37f, 38f));
        Triangle(a, new Vector2(75f, 48f), new Vector2(59f, 58f), new Vector2(59f, 38f));
    }

    /// <summary>Front sheet over a back sheet, back one clipped behind.</summary>
    static void DrawCopy(float[] a)
    {
        const float stroke = 4.5f;
        const float corner = 7f;
        // Back sheet first, then punch out the front sheet's footprint so the
        // front outline sits on clear ground.
        StrokeRoundRect(a, new Vector2(36f, 38f), new Vector2(80f, 82f), corner, stroke);
        EraseRoundRect(a, new Vector2(16f - stroke - 2f, 14f - stroke - 2f),
            new Vector2(60f + stroke + 2f, 58f + stroke + 2f), corner);
        StrokeRoundRect(a, new Vector2(16f, 14f), new Vector2(60f, 58f), corner, stroke);
    }

    /// <summary>Bin body, lid bar and handle.</summary>
    static void DrawTrash(float[] a)
    {
        FillRoundRect(a, new Vector2(28f, 16f), new Vector2(68f, 60f), 7f);
        // Slats punched out of the body so it reads as a bin, not a block.
        EraseRoundRect(a, new Vector2(40f, 24f), new Vector2(44f, 52f), 2f);
        EraseRoundRect(a, new Vector2(52f, 24f), new Vector2(56f, 52f), 2f);
        Line(a, new Vector2(22f, 68f), new Vector2(74f, 68f), 5f);
        Line(a, new Vector2(41f, 78f), new Vector2(55f, 78f), 5f);
    }

    /// <summary>Colour wheel: ring with three paint wells.</summary>
    static void DrawPalette(float[] a)
    {
        CircleStroke(a, new Vector2(48f, 48f), 32f, 4.5f);
        Dot(a, new Vector2(48f, 66f), 7f);
        Dot(a, new Vector2(33f, 39f), 7f);
        Dot(a, new Vector2(63f, 39f), 7f);
    }

    // ------------------------------------------------------------------
    // Antialiased rasterizing helpers (coverage = max blend)
    // ------------------------------------------------------------------

    static void Blend(float[] a, int x, int y, float alpha)
    {
        int i = y * Size + x;
        if (alpha > a[i])
            a[i] = alpha;
    }

    static void Line(float[] a, Vector2 from, Vector2 to, float halfWidth)
    {
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                Blend(a, x, y, halfWidth - SegmentDistance(p, from, to) + 0.5f);
            }
    }

    static void Dot(float[] a, Vector2 center, float radius)
    {
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                Blend(a, x, y, radius - (p - center).magnitude + 0.5f);
            }
    }

    static void CircleStroke(float[] a, Vector2 center, float radius, float halfWidth)
    {
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Mathf.Abs((p - center).magnitude - radius);
                Blend(a, x, y, halfWidth - d + 0.5f);
            }
    }

    static void Triangle(float[] a, Vector2 p0, Vector2 p1, Vector2 p2)
    {
        if (Cross(p1 - p0, p2 - p0) < 0f)
            (p1, p2) = (p2, p1);
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d1 = Cross(p1 - p0, p - p0) / (p1 - p0).magnitude;
                float d2 = Cross(p2 - p1, p - p1) / (p2 - p1).magnitude;
                float d3 = Cross(p0 - p2, p - p2) / (p0 - p2).magnitude;
                Blend(a, x, y, Mathf.Min(d1, Mathf.Min(d2, d3)) + 0.5f);
            }
    }

    static void FillRoundRect(float[] a, Vector2 min, Vector2 max, float corner)
    {
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                Blend(a, x, y, -RoundRectSdf(p, min, max, corner) + 0.5f);
            }
    }

    static void StrokeRoundRect(float[] a, Vector2 min, Vector2 max, float corner, float halfWidth)
    {
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Mathf.Abs(RoundRectSdf(p, min, max, corner));
                Blend(a, x, y, halfWidth - d + 0.5f);
            }
    }

    static void EraseRoundRect(float[] a, Vector2 min, Vector2 max, float corner)
    {
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float inside = Mathf.Clamp01(-RoundRectSdf(p, min, max, corner) + 0.5f);
                if (inside <= 0f)
                    continue;
                int i = y * Size + x;
                a[i] = Mathf.Min(a[i], 1f - inside);
            }
    }

    static float RoundRectSdf(Vector2 p, Vector2 min, Vector2 max, float corner)
    {
        Vector2 center = (min + max) * 0.5f;
        Vector2 half = (max - min) * 0.5f - Vector2.one * corner;
        Vector2 q = new Vector2(Mathf.Abs(p.x - center.x), Mathf.Abs(p.y - center.y)) - half;
        float outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude;
        float insideDist = Mathf.Min(Mathf.Max(q.x, q.y), 0f);
        return outside + insideDist - corner;
    }

    static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
        return (p - (a + ab * t)).magnitude;
    }

    static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;
}
