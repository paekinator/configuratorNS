using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// World-space measurement guide for the Guided tools, drawn like a CAD
/// dimension annotation so it can never be mistaken for a real part:
/// a THIN DASHED line between two endpoint dots and slim tick marks at every
/// catalogue length it can snap to.
///
/// Visual language ("precision instrument"): all structure — lines, dots,
/// ticks — draws in the theme's ink tone; the single accent colour marks only
/// the ACTIVE snap tick; blocked states use the theme danger tone. Text
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

    [Header("Ground crosshair")]
    [Tooltip("Half-length of each crosshair line, in 88 mm modules.")]
    public float crosshairHalfLengthModules = 24f;
    [Tooltip("Crosshair line width in millimetres (grid minor lines are ~3 mm).")]
    public float crosshairWidthMm = 9f;
    [Tooltip("Emphasized-grid-line tone: a darker version of the floor's own line color, not an accent.")]
    public Color crosshairColor = new Color(0.46f, 0.43f, 0.38f, 0.65f);

    [Tooltip("Visual-only lift (mm) so ground lines don't z-fight the floor.")]
    public float drawLiftMm = 15f;

    float LineWidth => NeospaceUnits.Mm(lineWidthMm);
    float TickWidth => NeospaceUnits.Mm(tickWidthMm);
    float TickHalfLength => NeospaceUnits.Mm(tickHalfLengthMm);
    float ActiveTickHalfLength => NeospaceUnits.Mm(activeTickHalfLengthMm);
    float EndDotDiameter => NeospaceUnits.Mm(endDotDiameterMm);
    float DrawLift => NeospaceUnits.Mm(drawLiftMm);

    // Theme tokens, read fresh every draw so theme switches restyle live.
    static Color InkTone => UIThemeController.InkColor;
    static Color AccentTone => UIThemeController.AccentColor;
    static Color DangerTone => UIThemeController.DangerColor;
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
    LineRenderer _crossX;
    LineRenderer _crossZ;
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
        crosshairWidthMm = 9f;
        crosshairColor = new Color(0.46f, 0.43f, 0.38f, 0.65f);
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
        Color mainColor = valid ? InkTone : DangerTone;
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
            Color c = isActive ? (valid ? AccentTone : DangerTone) : TickTone;
            tick.startColor = c;
            tick.endColor = c;
            tick.startWidth = isActive ? TickWidth * 1.6f : TickWidth;
            tick.endWidth = tick.startWidth;
        }

        CursorTooltip.Show(this, labelText, !valid);
    }

    /// <summary>Small marker + optional cursor-tooltip text for a hover point (first pick, AP snap).</summary>
    public void ShowPoint(Vector3 point, string labelText, bool valid = true)
    {
        Color mainColor = valid ? InkTone : DangerTone;

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
        HideCrosshair();
    }

    /// <summary>
    /// Emphasize the grid row and column under the cursor: two long SOLID
    /// lines in the floor grid's own (darker) tone, hugging the ground, so
    /// they read as "this grid line got thicker" rather than as an extra
    /// annotation drawn on top. The point is module-snapped by the caller,
    /// so the lines land exactly on real grid lines.
    /// </summary>
    public void ShowCrosshair(Vector3 point)
    {
        float width = NeospaceUnits.Mm(crosshairWidthMm);
        if (_crossX == null)
        {
            _crossX = CreateLine("CrossX", width, _solidMaterial);
            _crossZ = CreateLine("CrossZ", width, _solidMaterial);
            _crossX.numCapVertices = 0;
            _crossZ.numCapVertices = 0;
        }

        _crossX.startWidth = width;
        _crossX.endWidth = width;
        _crossZ.startWidth = width;
        _crossZ.endWidth = width;

        float half = crosshairHalfLengthModules * NeospaceUnits.ModuleMeters;
        // Barely above the floor: enough to avoid z-fighting, low enough to
        // still read as part of the grid itself.
        Vector3 lift = Vector3.up * NeospaceUnits.Mm(3f);
        // Emphasized-grid-line tone per theme: darker than the floor in light
        // mode, lighter than it in dark mode.
        Color c = UIThemeController.IsDarkTheme
            ? new Color(0.62f, 0.58f, 0.52f, 0.6f)
            : crosshairColor;

        _crossX.positionCount = 2;
        _crossX.SetPosition(0, point + lift - Vector3.right * half);
        _crossX.SetPosition(1, point + lift + Vector3.right * half);
        _crossX.startColor = c;
        _crossX.endColor = c;

        _crossZ.positionCount = 2;
        _crossZ.SetPosition(0, point + lift - Vector3.forward * half);
        _crossZ.SetPosition(1, point + lift + Vector3.forward * half);
        _crossZ.startColor = c;
        _crossZ.endColor = c;
    }

    public void HideCrosshair()
    {
        if (_crossX != null)
            _crossX.positionCount = 0;
        if (_crossZ != null)
            _crossZ.positionCount = 0;
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

        // Fill hints at the surface a panel will occupy: a whisper of accent,
        // not a paint bucket. Outline stays ink so the accent keeps meaning.
        Color fillBase = valid ? AccentTone : DangerTone;
        Color outlineBase = valid ? InkTone : DangerTone;
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
