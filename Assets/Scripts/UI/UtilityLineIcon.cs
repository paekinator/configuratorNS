using UnityEngine;
using UnityEngine.UI;

/// <summary>Continuous round-joined strokes with antialiased coverage.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class UtilityLineIcon : MaskableGraphic
{
    public enum Glyph { Dimensions, Undo, Redo, Fullscreen, ChevronUp, ChevronDown, ChevronLeft, ChevronRight }
    public Glyph glyph;
    [Range(.5f, 6f)] public float stroke = 1.8f;

    const int Resolution = 128;
    Texture2D _coverage;
    Glyph _builtGlyph;
    float _builtStroke = -1f;
    public override Texture mainTexture => _coverage != null ? _coverage : Texture2D.whiteTexture;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        EnsureCoverage();
        vh.Clear();
        Rect r = rectTransform.rect;
        float half = Mathf.Min(r.width, r.height) * .5f;
        Vector2 c = r.center;
        vh.AddVert(c + new Vector2(-half, -half), color, new Vector2(0, 0));
        vh.AddVert(c + new Vector2(-half, half), color, new Vector2(0, 1));
        vh.AddVert(c + new Vector2(half, half), color, new Vector2(1, 1));
        vh.AddVert(c + new Vector2(half, -half), color, new Vector2(1, 0));
        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(0, 2, 3);
    }

    void EnsureCoverage()
    {
        float width = Mathf.Clamp(stroke, .5f, 6f);
        if (_coverage != null && _builtGlyph == glyph && Mathf.Approximately(_builtStroke, width)) return;
        if (_coverage == null)
        {
            _coverage = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, true, true)
            {
                name = "Rail stroke coverage",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear
            };
        }
        var pixels = new Color32[Resolution * Resolution];
        float pixel = 24f / Resolution;
        for (int y = 0; y < Resolution; y++)
        for (int x = 0; x < Resolution; x++)
        {
            Vector2 p = new Vector2((x + .5f) * pixel - 12f, (y + .5f) * pixel - 12f);
            float coverage = Mathf.Clamp01(.5f + (width * .5f - Distance(p)) / pixel);
            pixels[y * Resolution + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255));
        }
        _coverage.SetPixels32(pixels);
        _coverage.Apply(true, false);
        _builtGlyph = glyph;
        _builtStroke = width;
    }

    float Distance(Vector2 p)
    {
        if (glyph == Glyph.ChevronUp || glyph == Glyph.ChevronDown)
        {
            // Both glyphs share exactly the same horizontal centre and shape.
            if (glyph == Glyph.ChevronDown) p.y = -p.y;
            p.x = Mathf.Abs(p.x);
            return Segment(p, new Vector2(0, 3.5f), new Vector2(6, -3.5f));
        }
        if (glyph == Glyph.ChevronLeft || glyph == Glyph.ChevronRight)
        {
            // The side-button version is the same chevron rotated 90 degrees.
            if (glyph == Glyph.ChevronLeft) p.x = -p.x;
            p.y = Mathf.Abs(p.y);
            return Segment(p, new Vector2(3.5f, 0), new Vector2(-3.5f, 6));
        }
        if (glyph == Glyph.Dimensions)
        {
            p.x = Mathf.Abs(p.x);
            p.y = Mathf.Abs(p.y);
            return Mathf.Min(Segment(p, Vector2.zero, new Vector2(9, 0)),
                Segment(p, new Vector2(9, 0), new Vector2(5, 4)));
        }
        if (glyph == Glyph.Fullscreen)
        {
            p = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y));
            return Mathf.Min(Segment(p, new Vector2(9, 3), new Vector2(9, 9)),
                Segment(p, new Vector2(9, 9), new Vector2(3, 9)));
        }
        if (glyph == Glyph.Redo) p.x = -p.x;
        const float radius = 7.5f;
        const float start = -130f;
        const float end = 135f;
        Vector2 first = Circle(start, radius), tip = Circle(end, radius);
        float angle = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
        float arc = angle >= start && angle <= end
            ? Mathf.Abs(p.magnitude - radius)
            : Mathf.Min(Vector2.Distance(p, first), Vector2.Distance(p, tip));
        // Union the arc and arrowhead before rasterisation: no cracks or
        // darker overlapping joins. Endpoint distances give round caps.
        return Mathf.Min(arc,
            Mathf.Min(Segment(p, tip, tip + new Vector2(0, 3.6f)),
                      Segment(p, tip, tip + new Vector2(3.6f, 0))));
    }

    static Vector2 Circle(float degrees, float radius)
    {
        float a = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
    }

    static float Segment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 d = b - a;
        return Vector2.Distance(p, a + d * Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude));
    }

    protected override void OnDestroy()
    {
        if (_coverage != null)
        {
            if (Application.isPlaying) Destroy(_coverage);
            else DestroyImmediate(_coverage);
        }
        base.OnDestroy();
    }
}
