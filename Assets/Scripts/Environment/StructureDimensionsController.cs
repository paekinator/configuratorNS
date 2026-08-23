using TMPro;
using UnityEngine;

/// <summary>
/// Quiet CAD-style dimension annotations for the current build.
///
/// When the structure CHANGES, three thin dimension lines appear just
/// outside its bounding box — width and depth along the ground, height up
/// the nearest corner — each with small end ticks and a millimetre label,
/// plus a small cross at the footprint centre. They hold for a few seconds,
/// then fade away so the canvas stays clean; the next change brings them
/// back with fresh numbers.
///
/// The corner ruler button (<see cref="DimensionsToggleBootstrap"/>) can PIN
/// the annotations: while <see cref="Pinned"/> they stay visible whenever
/// something is built, on top of the flash-on-change behaviour.
///
/// Data comes from <see cref="StructureBounds"/>, the same source a saved
/// configuration will use.
/// </summary>
public class StructureDimensionsController : MonoBehaviour
{
    public BuildController buildController;

    [Header("Look (millimetres)")]
    public float lineWidthMm = 5f;
    public float tickLengthMm = 45f;
    [Tooltip("Gap between the structure and the dimension lines.")]
    public float standoffMm = 130f;
    public float labelHeightMm = 75f;
    public Color lineColor = new Color(0.55f, 0.52f, 0.47f, 0.75f);
    public Color labelColor = new Color(0.42f, 0.40f, 0.36f, 0.95f);

    [Header("Timing (seconds)")]
    [Tooltip("How long the annotations stay after a change before fading.")]
    public float holdSeconds = 4f;
    public float fadeSeconds = 0.6f;

    const float PollInterval = 0.25f;
    const int GizmoLayer = 2; // Ignore Raycast

    LineRenderer _widthLine, _depthLine, _heightLine;
    readonly LineRenderer[] _ticks = new LineRenderer[6];
    LineRenderer _centerA, _centerB;
    TextMeshPro _widthLabel, _depthLabel, _heightLabel;
    TMP_FontAsset _font;
    Material _material;
    Camera _cam;

    const string PinnedPrefKey = "neospace.dimensions.pinned";

    static StructureDimensionsController _instance;
    static bool _pinned;
    static bool _pinnedLoaded;

    /// <summary>User pin: keep the dimensions visible instead of only flashing them.</summary>
    public static bool Pinned
    {
        get
        {
            if (!_pinnedLoaded)
            {
                _pinnedLoaded = true;
                _pinned = PlayerPrefs.GetInt(PinnedPrefKey, 0) == 1;
            }
            return _pinned;
        }
        set
        {
            _pinnedLoaded = true;
            _pinned = value;
            PlayerPrefs.SetInt(PinnedPrefKey, value ? 1 : 0);
            if (_instance != null)
                _instance._nextPoll = 0f; // redraw right away, don't wait a poll
        }
    }

    float _nextPoll;
    bool _visible;
    string _signature;
    float _holdUntil = -1f;
    float _alpha;
    bool _hasStructure;
    int _seenVersion;
    bool _refreshQueued = true;

    void OnEnable()
    {
        _instance = this;
        BuildHistory.Changed += QueueRefresh;
    }

    void OnDisable()
    {
        if (_instance == this)
            _instance = null;
        BuildHistory.Changed -= QueueRefresh;
    }

    void QueueRefresh() { _refreshQueued = true; }

    void Awake()
    {
        var preset = Resources.Load<Evo.UI.StylerPreset>("Styler Presets/Default");
        if (preset != null && !preset.TryGetFont("Inter - Semi Bold", out _font))
            preset.TryGetFont("Inter - Bold", out _font);

        _material = new Material(Shader.Find("Sprites/Default"));

        _widthLine = CreateLine("DimWidth");
        _depthLine = CreateLine("DimDepth");
        _heightLine = CreateLine("DimHeight");
        for (int i = 0; i < _ticks.Length; i++)
            _ticks[i] = CreateLine("DimTick" + i);
        _centerA = CreateLine("DimCenterA");
        _centerB = CreateLine("DimCenterB");

        _widthLabel = CreateLabel("DimWidthLabel");
        _depthLabel = CreateLabel("DimDepthLabel");
        _heightLabel = CreateLabel("DimHeightLabel");

        _cam = Camera.main;
        _visible = true; // force the initial hide through the guard
        SetVisible(false);
    }

    LineRenderer CreateLine(string name)
    {
        var go = new GameObject(name);
        go.layer = GizmoLayer;
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = _material;
        float w = NeospaceUnits.Mm(lineWidthMm);
        line.startWidth = w;
        line.endWidth = w;
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.numCapVertices = 0;
        line.startColor = lineColor;
        line.endColor = lineColor;
        return line;
    }

    TextMeshPro CreateLabel(string name)
    {
        var go = new GameObject(name);
        go.layer = GizmoLayer;
        go.transform.SetParent(transform, false);
        var label = go.AddComponent<TextMeshPro>();
        label.fontSize = NeospaceUnits.Mm(labelHeightMm) * 10f;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.color = labelColor;
        if (_font != null)
            label.font = _font;
        return label;
    }

    void Update()
    {
        // Measure only when something could have changed: spawns/destroys
        // (structure version), any tool mutation incl. moves (history ping) —
        // plus the old cadence WHILE the annotations show, because the lines
        // re-seat against the orbiting camera each poll. Pinned counts as
        // showing so the lines keep tracking the camera.
        bool showing = Pinned || _alpha > 0.01f ||
                       (_holdUntil > 0f && Time.time < _holdUntil);
        bool changed = _refreshQueued || _seenVersion != AttachmentPoint.StructureVersion;
        if ((changed || showing) && Time.time >= _nextPoll)
        {
            _nextPoll = Time.time + PollInterval;
            _refreshQueued = false;
            _seenVersion = AttachmentPoint.StructureVersion;
            Refresh();
        }

        // Fade toward full strength while holding (or pinned with something
        // built), toward zero afterwards.
        float target = ((Pinned && _hasStructure) ||
                        (_holdUntil > 0f && Time.time < _holdUntil)) ? 1f : 0f;
        float step = Time.deltaTime / Mathf.Max(fadeSeconds, 0.05f);
        _alpha = Mathf.MoveTowards(_alpha, target, step);
        SetVisible(_alpha > 0.01f);
        if (_visible)
            ApplyAlpha(_alpha);

        if (_visible && _cam != null)
        {
            Quaternion face = Quaternion.LookRotation(
                _cam.transform.forward, _cam.transform.up);
            _widthLabel.transform.rotation = face;
            _depthLabel.transform.rotation = face;
            _heightLabel.transform.rotation = face;
        }
    }

    void Refresh()
    {
        if (_cam == null)
            _cam = Camera.main;

        if (!StructureBounds.TryCompute(buildController, out StructureBounds.Info info))
        {
            _signature = null;
            _holdUntil = -1f;
            _hasStructure = false;
            return;
        }
        _hasStructure = true;

        // Re-show only when the measurements actually change.
        string sig = $"{Mathf.RoundToInt(info.WidthMm)}x{Mathf.RoundToInt(info.DepthMm)}x{Mathf.RoundToInt(info.HeightMm)}" +
                     $"@{info.MinModule}{info.MaxModule}";
        if (sig != _signature)
        {
            _signature = sig;
            _holdUntil = Time.time + holdSeconds;
        }

        // Pinned keeps drawing regardless of the flash window.
        if (!Pinned && (_holdUntil < 0f || Time.time >= _holdUntil && _alpha <= 0f))
            return;

        Bounds b = info.WorldBounds;
        float off = NeospaceUnits.Mm(standoffMm);
        float tick = NeospaceUnits.Mm(tickLengthMm);
        float ground = b.min.y;

        // Put the ground lines on the box sides facing the camera so they
        // are never hidden behind the structure.
        Vector3 camPos = _cam != null ? _cam.transform.position : Vector3.forward * 10f;
        float zEdge = camPos.z < b.center.z ? b.min.z - off : b.max.z + off;
        float xEdge = camPos.x < b.center.x ? b.min.x - off : b.max.x + off;
        float zTickDir = camPos.z < b.center.z ? -1f : 1f;
        float xTickDir = camPos.x < b.center.x ? -1f : 1f;

        // Width (world X) along the near ground edge.
        Vector3 w0 = new Vector3(b.min.x, ground, zEdge);
        Vector3 w1 = new Vector3(b.max.x, ground, zEdge);
        Set(_widthLine, w0, w1);
        Set(_ticks[0], w0, w0 + new Vector3(0f, 0f, zTickDir * tick));
        Set(_ticks[1], w1, w1 + new Vector3(0f, 0f, zTickDir * tick));
        PlaceLabel(_widthLabel, (w0 + w1) * 0.5f + new Vector3(0f, 0f, zTickDir * off * 0.9f),
                   info.WidthMm);

        // Depth (world Z) along the near side edge.
        Vector3 d0 = new Vector3(xEdge, ground, b.min.z);
        Vector3 d1 = new Vector3(xEdge, ground, b.max.z);
        Set(_depthLine, d0, d1);
        Set(_ticks[2], d0, d0 + new Vector3(xTickDir * tick, 0f, 0f));
        Set(_ticks[3], d1, d1 + new Vector3(xTickDir * tick, 0f, 0f));
        PlaceLabel(_depthLabel, (d0 + d1) * 0.5f + new Vector3(xTickDir * off * 0.9f, 0f, 0f),
                   info.DepthMm);

        // Height (world Y) up the corner shared by both ground lines.
        Vector3 h0 = new Vector3(xEdge, ground, zEdge);
        Vector3 h1 = new Vector3(xEdge, b.max.y, zEdge);
        Set(_heightLine, h0, h1);
        Set(_ticks[4], h0, h0 + new Vector3(xTickDir * tick, 0f, zTickDir * tick) * 0.7f);
        Set(_ticks[5], h1, h1 + new Vector3(xTickDir * tick, 0f, zTickDir * tick) * 0.7f);
        PlaceLabel(_heightLabel,
                   (h0 + h1) * 0.5f + new Vector3(xTickDir * off * 0.9f, 0f, zTickDir * off * 0.9f),
                   info.HeightMm);

        // Small cross at the footprint centre.
        float arm = NeospaceUnits.Mm(60f);
        Vector3 c = info.GroundCenter + Vector3.up * NeospaceUnits.Mm(2f);
        Set(_centerA, c + Vector3.left * arm, c + Vector3.right * arm);
        Set(_centerB, c + Vector3.back * arm, c + Vector3.forward * arm);
    }

    void Set(LineRenderer line, Vector3 a, Vector3 b)
    {
        line.SetPosition(0, a);
        line.SetPosition(1, b);
    }

    void PlaceLabel(TextMeshPro label, Vector3 pos, float mm)
    {
        label.transform.position = pos + Vector3.up * NeospaceUnits.Mm(40f);
        label.text = $"{Mathf.RoundToInt(mm)} mm";
    }

    void ApplyAlpha(float a)
    {
        Color lc = lineColor;
        lc.a *= a;
        SetLineColor(_widthLine, lc);
        SetLineColor(_depthLine, lc);
        SetLineColor(_heightLine, lc);
        foreach (LineRenderer t in _ticks)
            SetLineColor(t, lc);
        SetLineColor(_centerA, lc);
        SetLineColor(_centerB, lc);

        Color tc = labelColor;
        tc.a *= a;
        _widthLabel.color = tc;
        _depthLabel.color = tc;
        _heightLabel.color = tc;
    }

    static void SetLineColor(LineRenderer line, Color c)
    {
        line.startColor = c;
        line.endColor = c;
    }

    void SetVisible(bool on)
    {
        if (_visible == on)
            return;
        _visible = on;

        _widthLine.gameObject.SetActive(on);
        _depthLine.gameObject.SetActive(on);
        _heightLine.gameObject.SetActive(on);
        foreach (LineRenderer t in _ticks)
            t.gameObject.SetActive(on);
        _centerA.gameObject.SetActive(on);
        _centerB.gameObject.SetActive(on);
        _widthLabel.gameObject.SetActive(on);
        _depthLabel.gameObject.SetActive(on);
        _heightLabel.gameObject.SetActive(on);
    }
}
