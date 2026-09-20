using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// World-space measurement guide for the Guided tools, drawn like a CAD
/// dimension annotation so it can never be mistaken for a real part:
/// a THIN DASHED line between two endpoint dots and slim tick marks at every
/// catalogue length it can snap to.
///
/// Visual language ("precision instrument"): everything this draws is a
/// PREVIEW of something not yet placed, so lines, dots and the active snap
/// tick all take the one highlight colour that means "this is the thing you
/// are pointing at". The inactive ticks stay faint ink — they are a scale to
/// read, not a thing being pointed at. Blocked states use the danger tone,
/// which means "no" rather than "here". Text
/// (instructions and live sizes) is NOT drawn in the world: it goes to the
/// screen-space <see cref="CursorTooltip"/> pill by the cursor, so the scene
/// stays clean geometry. Colours come from <see cref="UIThemeController"/>
/// tokens every frame, so a theme switch restyles the guide instantly.
/// </summary>
public class TemplatePreviewGuide : MonoBehaviour
{
    // All visual sizes are in real millimetres and converted through
    // NeospaceUnits at draw time, so they stay proportional to the frames
    // regardless of the project's world scale. Style values are hard-applied
    // in Awake so stale serialized hosts can never resurrect an old look.
    [Header("Line (millimetres)")]
    public float lineWidthMm = 7f;
    public float tickWidthMm = 5f;
    public float tickHalfLengthMm = 26f;
    public float activeTickHalfLengthMm = 44f;
    public float endDotDiameterMm = 20f;
    [Tooltip("Length of one dash + gap cycle. 88 mm = one module.")]
    public float dashCycleMm = 88f;

    [Header("Area footprint")]
    [Tooltip("Fill opacity of the covered-area rectangle.")]
    public float areaFillAlpha = 0.10f;
    public float areaOutlineWidthMm = 5f;

    // No ground crosshair. It drew two lines the full width of the floor to
    // say which row and column a frame would land in — a fact that fits
    // inside one 88 mm square, which is what the ground grid now lights.

    [Tooltip("Visual-only lift (mm) so ground lines don't z-fight the floor.")]
    public float drawLiftMm = 15f;

    float LineWidth => NeospaceUnits.Mm(lineWidthMm);
    float TickWidth => NeospaceUnits.Mm(tickWidthMm);
    float TickHalfLength => NeospaceUnits.Mm(tickHalfLengthMm);
    float ActiveTickHalfLength => NeospaceUnits.Mm(activeTickHalfLengthMm);
    float EndDotDiameter => NeospaceUnits.Mm(endDotDiameterMm);
    float DrawLift => NeospaceUnits.Mm(drawLiftMm);

    // Theme tokens, read fresh every draw so theme switches restyle live.
    //
    // EVERYTHING THIS GUIDE DRAWS IS A PREVIEW — a span, a dot, a measure, an
    // outline of something that is not there yet — so it takes the one colour
    // that means "this is the thing you are pointing at". It used to draw in
    // ink, which is the colour of TEXT and of built structure, and the two
    // greys it chose between for "this one" and "one of these" were #3F3F3F
    // and #242424. A preview and a finished part looked the same, and the
    // difference between a marker you are on and a marker you are not was a
    // shade.
    //
    // Red is not part of this and stays as it is: it does not mean "here", it
    // means "no".
    static Color PreviewTone => UIThemeController.HighlightColor;
    static Color DangerTone => UIThemeController.DangerColor;

    /// <summary>
    /// The stops a measure can snap to, the ones you are not on. Faint, and in
    /// ink rather than the highlight: they are a scale to read, not a thing
    /// being pointed at, and a row of pale blue ticks beside a blue measure
    /// line reads as one smeared object.
    /// </summary>
    static Color TickTone
    {
        get
        {
            Color c = UIThemeController.InkColor;
            c.a = 0.22f;
            return c;
        }
    }

    LineRenderer _line;
    LineRenderer _dotA;
    LineRenderer _dotB;
    MeshFilter _areaFilter;
    MeshRenderer _areaRenderer;
    Mesh _areaMesh;
    LineRenderer _areaOutline;
    Material _solidMaterial;
    Material _dashMaterial;
    readonly List<LineRenderer> _ticks = new List<LineRenderer>();
    Camera _cam;

    void Awake()
    {
        ApplyStyleDefaults();

        _solidMaterial = new Material(Shader.Find("Sprites/Default"));

        // Dash pattern comes from a tiny tiled texture: opaque head, clear tail.
        _dashMaterial = new Material(Shader.Find("Sprites/Default"));
        _dashMaterial.mainTexture = BuildDashTexture();
        _dashMaterial.mainTextureScale =
            new Vector2(1f / Mathf.Max(NeospaceUnits.Mm(dashCycleMm), 1e-4f), 1f);

        _line = CreateLine("GuideLine", LineWidth, _dashMaterial);
        _line.textureMode = LineTextureMode.Tile;

        // Endpoint dots: near-zero-length lines with round caps read as circles.
        _dotA = CreateLine("GuideDotA", EndDotDiameter, _solidMaterial);
        _dotB = CreateLine("GuideDotB", EndDotDiameter, _solidMaterial);
        _dotA.numCapVertices = 8;
        _dotB.numCapVertices = 8;

        _cam = Camera.main;
        Hide();
    }

    /// <summary>
    /// The guide's look is code-owned: reset every style number so a stale
    /// serialized host can't keep the old thick/blue appearance.
    /// </summary>
    void ApplyStyleDefaults()
    {
        lineWidthMm = 7f;
        tickWidthMm = 5f;
        tickHalfLengthMm = 26f;
        activeTickHalfLengthMm = 44f;
        endDotDiameterMm = 20f;
        areaFillAlpha = 0.10f;
        areaOutlineWidthMm = 5f;
    }

    static Texture2D BuildDashTexture()
    {
        // 16×1: 9 opaque px + 7 clear px = dash ~56% of the cycle.
        var tex = new Texture2D(16, 1, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Point
        };
        for (int x = 0; x < 16; x++)
            tex.SetPixel(x, 0, x < 9 ? Color.white : Color.clear);
        tex.Apply();
        return tex;
    }

    LineRenderer CreateLine(string name, float width, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.startWidth = width;
        line.endWidth = width;
        line.useWorldSpace = true;
        line.positionCount = 0;
        line.numCapVertices = 4;
        return line;
    }

    /// <summary>
    /// Draw the measure line from anchor to the snapped end, with catalogue stop
    /// ticks along the axis and the active size labelled at the midpoint.
    /// </summary>
    public void ShowMeasure(
        Vector3 anchor,
        Vector3 end,
        Vector3 axis,
        string labelText,
        bool valid,
        IList<Vector3> stops,
        int activeStop)
    {
        Color mainColor = valid ? PreviewTone : DangerTone;
        Vector3 lift = Vector3.up * DrawLift;

        _line.sharedMaterial = _dashMaterial;
        _line.textureMode = LineTextureMode.Tile;
        _line.positionCount = 2;
        _line.SetPosition(0, anchor + lift);
        _line.SetPosition(1, end + lift);
        _line.startColor = mainColor;
        _line.endColor = mainColor;
        _line.startWidth = LineWidth;
        _line.endWidth = LineWidth;

        ShowDot(_dotA, anchor + lift, mainColor);
        ShowDot(_dotB, end + lift, mainColor);

        Vector3 tickDir = TickDirection(axis);
        int stopCount = stops != null ? stops.Count : 0;
        EnsureTickPool(stopCount);

        for (int i = 0; i < _ticks.Count; i++)
        {
            LineRenderer tick = _ticks[i];
            if (i >= stopCount)
            {
                tick.positionCount = 0;
                continue;
            }

            bool isActive = i == activeStop;
            float half = isActive ? ActiveTickHalfLength : TickHalfLength;
            tick.positionCount = 2;
            tick.SetPosition(0, stops[i] + lift - tickDir * half);
            tick.SetPosition(1, stops[i] + lift + tickDir * half);
            // The single accent in the whole guide: the tick you'd snap to.
            Color c = isActive ? (valid ? PreviewTone : DangerTone) : TickTone;
            tick.startColor = c;
            tick.endColor = c;
            tick.startWidth = isActive ? TickWidth * 1.6f : TickWidth;
            tick.endWidth = tick.startWidth;
        }

        CursorTooltip.Show(this, labelText, !valid);
    }

    /// <summary>Small marker + optional cursor-tooltip text for a hover point (first pick, AP snap).</summary>
    /// <summary>
    /// Where a tool is aiming on the ground when it has no ghost yet — the
    /// first click of a two-click part, before a size has been chosen and
    /// therefore before there is anything to preview.
    ///
    /// PUBLISHED, NOT DERIVED. The ground grid lights the cell a part will
    /// land in, and it finds that from the preview on the ghost layer. At this
    /// step there is no preview, and the only other way to know would be for
    /// the grid to raycast the floor and snap it itself — a second copy of
    /// what the tool has already worked out, free to disagree with it. This is
    /// the tool saying where it is aiming.
    ///
    /// Stamped with the frame it was set on, so it expires by itself. A tool
    /// that stops aiming has nothing to remember to clear, and a stale target
    /// cannot outlive the frame that wrote it.
    /// </summary>
    public static Vector3 GroundTarget { get; private set; }
    public static int GroundTargetFrame { get; private set; } = -1;

    /// <summary>True while a tool is aiming at the ground this frame.</summary>
    public static bool HasGroundTarget => Time.frameCount - GroundTargetFrame <= 1;

    /// <summary>
    /// The area a guided tool will cover, published for the same reason and
    /// expiring the same way. Its XZ extent is the footprint the ground grid
    /// outlines when the tool has no ghost to read.
    /// </summary>
    public static Bounds GroundArea { get; private set; }
    public static int GroundAreaFrame { get; private set; } = -1;
    public static bool HasGroundArea => Time.frameCount - GroundAreaFrame <= 1;

    /// <summary>
    /// Aim at a point on the ground, drawing NOTHING there.
    ///
    /// The ground grid draws it now — an 88 mm cell lit in the highlight
    /// colour, which is the same answer the Frames tool gives and is far
    /// easier to see than a dot. This used to be a dot plus a full-width
    /// crosshair across the whole floor, which was two marks for one fact and
    /// the larger of them was the width of the screen.
    /// </summary>
    public void ShowGroundTarget(Vector3 point, string labelText, bool valid = true)
    {
        GroundTarget = point;
        GroundTargetFrame = Time.frameCount;

        _line.positionCount = 0;
        _dotA.positionCount = 0;
        _dotB.positionCount = 0;
        for (int i = 0; i < _ticks.Count; i++)
            _ticks[i].positionCount = 0;

        CursorTooltip.Show(this, labelText, !valid);
    }

    public void ShowPoint(Vector3 point, string labelText, bool valid = true)
    {
        Color mainColor = valid ? PreviewTone : DangerTone;

        // Just the dot: the text rides the cursor tooltip, so the old stem
        // rising to a floating world label is gone.
        _line.positionCount = 0;
        ShowDot(_dotA, point + Vector3.up * DrawLift, mainColor);
        _dotB.positionCount = 0;

        for (int i = 0; i < _ticks.Count; i++)
            _ticks[i].positionCount = 0;

        CursorTooltip.Show(this, labelText, !valid);
    }

    public void Hide()
    {
        if (_line != null)
            _line.positionCount = 0;
        if (_dotA != null)
            _dotA.positionCount = 0;
        if (_dotB != null)
            _dotB.positionCount = 0;
        for (int i = 0; i < _ticks.Count; i++)
            _ticks[i].positionCount = 0;
        CursorTooltip.Hide(this);
        HideArea();
    }


    /// <summary>
    /// Rectangle showing the area the current tool will cover (corners in
    /// loop order): a thin dashed outline, optionally with a translucent fill
    /// (the fill marks a face a panel will occupy — footprint-only previews
    /// pass filled: false).
    /// </summary>
    public void ShowArea(Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool valid = true, bool filled = true)
    {
        EnsureArea();

        // Published for the same reason as GroundTarget: the guided tools draw
        // this area and NO ghost — the panel bay tool has nothing to preview
        // until it is built, so there is nothing on the ghost layer for the
        // ground grid to find. This is the tool saying what it will cover; the
        // grid projects it down and outlines the cells underneath.
        if (valid)
        {
            Vector3 min = Vector3.Min(Vector3.Min(a, b), Vector3.Min(c, d));
            Vector3 max = Vector3.Max(Vector3.Max(a, b), Vector3.Max(c, d));
            GroundArea = new Bounds((min + max) * 0.5f, max - min);
            GroundAreaFrame = Time.frameCount;
        }

        // Fill hints at the surface a panel will occupy: a whisper of accent,
        // not a paint bucket. Outline stays ink so the accent keeps meaning.
        Color fillBase = valid ? PreviewTone : DangerTone;
        Color outlineBase = valid ? PreviewTone : DangerTone;
        // Sits just under the measure line so neither z-fights the other.
        Vector3 lift = Vector3.up * (DrawLift * 0.6f);

        var verts = new[] { a + lift, b + lift, c + lift, d + lift };

        if (filled)
        {
            var fill = new Color(fillBase.r, fillBase.g, fillBase.b, Mathf.Clamp01(areaFillAlpha));
            _areaMesh.vertices = verts;
            _areaMesh.colors = new[] { fill, fill, fill, fill };
            _areaMesh.RecalculateBounds();
        }
        _areaRenderer.enabled = filled;

        Color outlineColor = new Color(outlineBase.r, outlineBase.g, outlineBase.b, 0.7f);
        _areaOutline.positionCount = 4;
        _areaOutline.SetPosition(0, verts[0]);
        _areaOutline.SetPosition(1, verts[1]);
        _areaOutline.SetPosition(2, verts[2]);
        _areaOutline.SetPosition(3, verts[3]);
        _areaOutline.startColor = outlineColor;
        _areaOutline.endColor = outlineColor;
        _areaOutline.startWidth = NeospaceUnits.Mm(areaOutlineWidthMm);
        _areaOutline.endWidth = _areaOutline.startWidth;
    }

    public void HideArea()
    {
        if (_areaRenderer != null)
            _areaRenderer.enabled = false;
        if (_areaOutline != null)
            _areaOutline.positionCount = 0;
    }

    void EnsureArea()
    {
        if (_areaFilter != null)
            return;

        var go = new GameObject("GuideArea");
        go.transform.SetParent(transform, false);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        _areaFilter = go.AddComponent<MeshFilter>();
        _areaRenderer = go.AddComponent<MeshRenderer>();
        _areaRenderer.sharedMaterial = _solidMaterial; // Sprites/Default: vertex-tinted, Cull Off
        _areaRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _areaRenderer.receiveShadows = false;

        _areaMesh = new Mesh { name = "GuideAreaQuad" };
        _areaMesh.vertices = new Vector3[4];
        _areaMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        _areaMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        _areaMesh.MarkDynamic();
        _areaFilter.sharedMesh = _areaMesh;

        _areaOutline = CreateLine("GuideAreaOutline", NeospaceUnits.Mm(areaOutlineWidthMm), _dashMaterial);
        _areaOutline.textureMode = LineTextureMode.Tile;
        _areaOutline.loop = true;
    }

    void ShowDot(LineRenderer dot, Vector3 position, Color color)
    {
        // Two points a hair apart + round caps = a small camera-facing disc.
        float epsilon = EndDotDiameter * 0.05f;
        dot.positionCount = 2;
        dot.SetPosition(0, position - Vector3.up * epsilon);
        dot.SetPosition(1, position + Vector3.up * epsilon);
        dot.startWidth = EndDotDiameter;
        dot.endWidth = EndDotDiameter;
        dot.startColor = color;
        dot.endColor = color;
    }

    Vector3 TickDirection(Vector3 axis)
    {
        if (Mathf.Abs(axis.y) > 0.7f)
        {
            // Vertical line: ticks run horizontally, roughly facing the camera.
            Vector3 flat = _cam != null ? _cam.transform.forward : Vector3.forward;
            flat.y = 0f;
            if (flat.sqrMagnitude < 1e-6f)
                flat = Vector3.forward;
            return Vector3.Cross(axis, flat.normalized).normalized;
        }

        Vector3 dir = Vector3.Cross(Vector3.up, axis);
        return dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.right;
    }

    void EnsureTickPool(int count)
    {
        while (_ticks.Count < count)
            _ticks.Add(CreateLine($"GuideTick_{_ticks.Count}", TickWidth, _solidMaterial));
    }
}
