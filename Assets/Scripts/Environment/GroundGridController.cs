using UnityEngine;

/// <summary>
/// Keeps the ground's H7 lattice under the camera, and tells the shader where
/// to fade.
///
/// The grid itself is infinite — NeospaceGroundGrid computes its lines from
/// world position, so there is no texture to run out and no size to choose.
/// All this does is make sure there is a surface under whatever the camera is
/// looking at, and hand the shader two facts it cannot work out for itself:
/// where the viewer's attention is, and where the real ground stops.
///
/// WHY IT FOLLOWS THE LOOK-AT POINT RATHER THAN THE CAMERA. The fade is meant
/// to read as "the world dissolves out there", which means it has to be
/// centred on what is being looked at. Centred on the camera instead, a
/// shallow view would fade out the near ground under your feet — which is the
/// part you are working on — and leave the far distance lit.
///
/// WHY IT RUNS IN EDIT MODE. Every value on it is a matter of taste, found by
/// looking. Drawing only in Play mode meant the choice was either to set
/// numbers blind, or to tune them live and lose them on exit — Unity discards
/// play-mode changes, and these are exactly the values someone would spend ten
/// minutes finding. It draws into the Game view in edit mode instead, so
/// dragging a slider shows the result and keeps it.
///
/// The quad it creates is HideAndDontSave: it is a preview, not scene content,
/// and must never be serialised into the scene file or turn up in a diff.
/// </summary>
[ExecuteAlways]
public class GroundGridController : MonoBehaviour
{
    [Tooltip("The ground the grid is drawn on. Found by name when unset.")]
    public Renderer groundPlane;

    [Tooltip("Where the build is read from, and where the ghost layer is named. "
             + "Found in the scene when unset.")]
    public BuildController buildController;

    [Header("Fog of vision")]
    // FRACTIONS OF WHAT THE CAMERA CAN SEE, not world distances.
    //
    // These were absolute world units, and an absolute radius can only be
    // right at one zoom: at 26 units it read as a small island on screen the
    // moment the camera pulled back, and it would have overrun the screen on
    // the way in. The visible ground is measured every frame instead, and
    // these say how much of it the grid fills — so the same numbers hold at
    // every zoom, which is what makes the plane read as endless.
    [Tooltip("Grid is at full strength out to this fraction of the visible ground.")]
    [Range(0.05f, 1f)] public float fadeStart = 0.35f;
    [Tooltip("Grid is completely gone by this fraction. Keep under 1 so it never "
             + "reaches the edge of the screen.")]
    [Range(0.1f, 1.4f)] public float fadeEnd = 0.7f;

    [Header("Fog of vision · limits (world units; 1 unit = 100 mm)")]
    // Guard rails on the MEASUREMENT, not on the grid. They exist so a hard
    // zoom or a camera pointed at the horizon cannot ask for a grid of no size
    // or of absurd size; they are not where you go to make the grid bigger.
    //
    // The grid's real limit is the ground plane itself, which the shader fades
    // against so that "there is grid here" never promises ground the placement
    // ray cannot find. Raising maxRadius past the plane does nothing at all —
    // enlarge the plane instead (ConfiguratorEnvironmentStyler.GroundPlaneScale).
    [Tooltip("The grid never shrinks below this radius, however far in you zoom. "
             + "Keep it small — a large value pins the grid to a fixed size and "
             + "undoes the whole point of measuring the view.")]
    public float minRadius = 6f;
    [Tooltip("And never grows past this, however far out.")]
    public float maxRadius = 400f;

    [Header("Ground lattice · 704 mm, everywhere")]
    public Color lineColor = new Color(0.42f, 0.42f, 0.42f, 1f);
    [Range(0f, 1f)] public float opacity = 0.3f;
    [Tooltip("Thickness in screen pixels — constant at every zoom.")]
    [Range(0.5f, 4f)] public float lineWidthPixels = 1.1f;

    [Header("Module grid · 88 mm, around the build")]
    public Color moduleColor = new Color(0.42f, 0.42f, 0.42f, 1f);
    // Matched to the lattice rather than louder. Every eighth module line IS a
    // 704 mm line, so where they coincide the two alphas add and the bay
    // rhythm appears without either layer being told about the other.
    [Range(0f, 1f)] public float moduleOpacity = 0.3f;
    [Tooltip("Thickness in screen pixels, separate from the 704 mm lattice's.")]
    [Range(0.5f, 4f)] public float moduleLineWidthPixels = 1.0f;
    [Tooltip("Free 88 mm modules of grid kept around the structure on every side.")]
    public float marginModules = 6f;
    [Tooltip("Half-size of the island when nothing is placed yet, in modules.")]
    public float emptyHalfModules = 10f;
    // The soft edge, as two numbers rather than one: full strength out to
    // START past the footprint, gone by END. One number could only ever begin
    // fading the instant the footprint ended, which puts the gradient's
    // steepest part right where the build is.
    //
    // The SAME width all the way round — a circle around one module, that
    // circle dragged along a wall.
    [Tooltip("Grid stays at full strength this far past the build, in modules.")]
    public float islandFadeStartModules = 1f;
    [Tooltip("And has faded to nothing by here.")]
    public float islandFadeEndModules = 8f;
    [Tooltip("How quickly the island follows the build as it changes (1/s).")]
    public float followSpeed = 6f;

    [Header("World centre · the two lattice lines through the origin")]
    // Not an RGB gizmo and not a symbol: the two 704 mm lines that already
    // pass through the origin, drawn a little stronger near it and fading back
    // into the lattice. The centre is where they cross.
    public Color originColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    [Tooltip("Like the zone, these lie ON lattice lines, so strengths add — "
             + "keep it modest.")]
    [Range(0f, 1f)] public float originOpacity = 0.5f;
    [Tooltip("Thickness in screen pixels.")]
    [Range(0.5f, 4f)] public float originLineWidthPixels = 1.4f;
    [Tooltip("Full strength out to this distance from the centre, in modules. "
             + "8 is one 704 mm lattice cell.")]
    public float originFadeStartModules = 8f;
    [Tooltip("Back to plain lattice by here, in modules.")]
    public float originFadeEndModules = 24f;

    [Header("Built zone · the boundary around what is placed")]
    // Four 704 mm lattice lines, picked out. The zone is the module island
    // including its whole fade, grown by the padding, then snapped OUTWARD to
    // the next lattice line on every side — so it is always larger than the
    // gradient grid, and its edges are never anywhere a lattice line is not.
    //
    // Shown only once something is placed. On an empty scene there is no
    // built zone to indicate, and a rectangle around nothing would say there
    // was.
    public Color zoneColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    [Tooltip("Keep this low. The line coincides with a lattice line, so their "
             + "strengths ADD — a little is plenty to lift it out.")]
    [Range(0f, 1f)] public float zoneOpacity = 0.45f;
    [Tooltip("Thickness in screen pixels.")]
    [Range(0.5f, 4f)] public float zoneLineWidthPixels = 1.4f;
    [Tooltip("Extra room past the module grid's fade before snapping out to the "
             + "lattice, in modules. Zero still leaves a gap: the snap only ever "
             + "goes outward.")]
    public float zonePaddingModules = 2f;
    [Tooltip("Seconds for the zone to appear when the first part is placed, and "
             + "to disappear when the last is removed.")]
    public float zoneEase = 0.25f;

    [Header("Highlight · where the part will land")]
    [Tooltip("Set from UIThemeController.HighlightColor by Style Environment.")]
    public Color highlightColor = new Color(0.153f, 0.463f, 0.918f, 1f);
    [Tooltip("How strongly the highlighted grid reads. Its own value, not the "
             + "module grid's — this one has to be seen, and the grid under it "
             + "has to not be.")]
    [Range(0f, 1f)] public float hoverOpacity = 0.75f;
    [Tooltip("Opacity of the solid highlight-colour fill inside the exact footprint outline.")]
    [Range(0f, 1f)] public float hoverFillOpacity = 0.18f;
    [Tooltip("Highlight stays at full strength this far past the hitbox, in modules.")]
    public float hoverFadeStartModules = 0.5f;
    [Tooltip("And has faded to nothing by here.")]
    public float hoverFadeEndModules = 6f;
    [Tooltip("Thickness of the hitbox outline, in screen pixels.")]
    [Range(0.5f, 6f)] public float outlineWidthPixels = 2f;
    [Tooltip("Length of one dash + gap of the hitbox outline, in modules. Zero is a solid line. "
             + "Measured in WORLD units, so the dashes stay put on the ground "
             + "rather than crawling as the camera orbits.")]
    [Min(0f)]
    public float outlineDashModules = 0.5f;
    [Tooltip("Seconds for the highlight to appear and disappear. Short — this "
             + "follows the cursor, and anything slower feels like lag.")]
    public float hoverEase = 0.09f;

    [Header("Placement")]
    [Tooltip("Grid lift above the ground, millimetres.")]
    public float liftMm = 1.0f;

    /// <summary>
    /// Vertical offset applied to the visible grid (world units). Finish mode
    /// sinks the ground by one foot height so the build stands on its feet,
    /// and the grid must ride along with the floor it decorates.
    ///
    /// Moved here from AdaptiveGridController, which drew the module grid
    /// until this took the job over.
    /// </summary>
    public static float FloorVisualOffset;

    const string ShaderName = "NEOSPACE/Ground Grid";
    const string QuadName = "GroundGrid";
    const int GizmoLayer = 2; // Ignore Raycast

    /// <summary>Eight 88 mm modules — the H7 span the bold lines mark.</summary>
    const int ModulesPerLine = 8;

    /// <summary>
    /// How far past the fade the quad extends. The shader has already faded
    /// the grid to nothing by fadeEnd, so this only has to guarantee there is
    /// geometry under those pixels — a little slack costs nothing and removes
    /// any chance of the mesh edge becoming visible during a fast pan.
    /// </summary>
    const float MeshSlack = 1.25f;

    /// <summary>How often the set of ghost renderers is re-discovered.</summary>
    const float GhostPollInterval = 0.25f;

    Camera _camera;
    Material _material;
    Transform _quad;
    float _groundY;
    Vector2 _boundsCenter;
    Vector2 _boundsHalf;

    // The island, smoothed toward the build's footprint.
    Vector2 _islandCenter, _islandHalf;
    Vector2 _islandTargetCenter, _islandTargetHalf;

    // The built zone. NOT smoothed in position: its edges are meant to lie on
    // lattice lines, and a rectangle easing between them would spend every
    // transition lying about that. It jumps a whole lattice cell when the
    // build outgrows it — a readable event — and only its strength eases.
    Vector2 _zoneCenter, _zoneHalf;
    bool _hasStructure;
    float _zoneStrength;
    float _nextIslandPoll;

    // The ghosts, re-discovered on a slow poll but READ every frame: a ghost
    // appears when a tool is picked, which is rare, but it moves with the
    // cursor, which is not.
    readonly System.Collections.Generic.List<Renderer> _ghosts =
        new System.Collections.Generic.List<Renderer>();
    float _nextGhostPoll;
    Vector2 _hoverCenter, _hoverHalf;
    float _hoverStrength;

    static readonly int LineColorId = Shader.PropertyToID("_LineColor");
    static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    static readonly int SpacingId = Shader.PropertyToID("_Spacing");
    static readonly int LineWidthId = Shader.PropertyToID("_LineWidthPx");
    static readonly int FadeCenterId = Shader.PropertyToID("_FadeCenter");
    static readonly int FadeStartId = Shader.PropertyToID("_FadeStart");
    static readonly int FadeEndId = Shader.PropertyToID("_FadeEnd");
    static readonly int BoundsCenterId = Shader.PropertyToID("_BoundsCenter");
    static readonly int BoundsHalfId = Shader.PropertyToID("_BoundsHalf");
    static readonly int BoundsFadeId = Shader.PropertyToID("_BoundsFade");
    static readonly int OriginColorId = Shader.PropertyToID("_OriginColor");
    static readonly int OriginOpacityId = Shader.PropertyToID("_OriginOpacity");
    static readonly int OriginLineWidthId = Shader.PropertyToID("_OriginLineWidthPx");
    static readonly int OriginFadeStartId = Shader.PropertyToID("_OriginFadeStart");
    static readonly int OriginFadeEndId = Shader.PropertyToID("_OriginFadeEnd");
    static readonly int ZoneColorId = Shader.PropertyToID("_ZoneColor");
    static readonly int ZoneOpacityId = Shader.PropertyToID("_ZoneOpacity");
    static readonly int ZoneLineWidthId = Shader.PropertyToID("_ZoneLineWidthPx");
    static readonly int ZoneCenterId = Shader.PropertyToID("_ZoneCenter");
    static readonly int ZoneHalfId = Shader.PropertyToID("_ZoneHalf");
    static readonly int ZoneStrengthId = Shader.PropertyToID("_ZoneStrength");
    static readonly int ModuleColorId = Shader.PropertyToID("_ModuleColor");
    static readonly int ModuleLineWidthId = Shader.PropertyToID("_ModuleLineWidthPx");
    static readonly int HoverOpacityId = Shader.PropertyToID("_HoverOpacity");
    static readonly int HoverFillOpacityId = Shader.PropertyToID("_HoverFillOpacity");
    static readonly int ModuleSpacingId = Shader.PropertyToID("_ModuleSpacing");
    static readonly int ModuleOpacityId = Shader.PropertyToID("_ModuleOpacity");
    static readonly int IslandCenterId = Shader.PropertyToID("_IslandCenter");
    static readonly int IslandHalfId = Shader.PropertyToID("_IslandHalf");
    static readonly int IslandFadeStartId = Shader.PropertyToID("_IslandFadeStart");
    static readonly int IslandFadeEndId = Shader.PropertyToID("_IslandFadeEnd");
    static readonly int HighlightColorId = Shader.PropertyToID("_HighlightColor");
    static readonly int HoverCenterId = Shader.PropertyToID("_HoverCenter");
    static readonly int HoverHalfId = Shader.PropertyToID("_HoverHalf");
    static readonly int HoverFadeStartId = Shader.PropertyToID("_HoverFadeStart");
    static readonly int HoverFadeEndId = Shader.PropertyToID("_HoverFadeEnd");
    static readonly int OutlineDashId = Shader.PropertyToID("_OutlineDash");
    static readonly int HoverStrengthId = Shader.PropertyToID("_HoverStrength");
    static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidthPx");

    /// <summary>The grid currently drawing, for anything that needs its rectangles.</summary>
    public static GroundGridController Active { get; private set; }

    /// <summary>
    /// The rectangle on the ground the camera must always be able to zoom out
    /// far enough to see whole.
    ///
    /// Once anything is placed: the built zone — the outline that says where
    /// the work is. Before that: the empty module island INCLUDING its fade,
    /// the patch of 88 mm grid a new scene opens on.
    ///
    /// The camera reads this rather than working out its own idea of "the
    /// build", so the zoom limit and the outline can never disagree about how
    /// big the zone is.
    /// </summary>
    public bool TryGetViewFrame(out Vector2 center, out Vector2 halfSize)
    {
        if (_hasStructure && _zoneStrength > 0.001f)
        {
            center = _zoneCenter;
            halfSize = _zoneHalf;
            return true;
        }

        float module = NeospaceUnits.ModuleMeters;
        float reach = (emptyHalfModules + Mathf.Max(islandFadeEndModules, 0f)) * module;
        center = _islandTargetCenter;
        halfSize = new Vector2(reach, reach);
        return true;
    }

    void OnEnable()
    {
        Active = this;
        // Not Start: in edit mode Start never runs, and OnEnable also fires
        // again after every script recompile — which is when a stale preview
        // quad from the previous domain would otherwise be left behind.
        DiscardPreviousQuad();

        if (groundPlane == null)
        {
            GameObject floor = GameObject.Find("GridFloor");
            groundPlane = floor != null ? floor.GetComponent<Renderer>() : null;
        }

        if (groundPlane == null)
        {
            Debug.LogWarning("[GroundGrid] No 'GridFloor' renderer · ground grid disabled.");
            enabled = false;
            return;
        }

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogWarning("[GroundGrid] Shader \"" + ShaderName + "\" not found · ground grid "
                             + "disabled. Check the console for a compile error in "
                             + "Assets/Shaders/NeospaceGroundGrid.shader.");
            enabled = false;
            return;
        }

        Bounds b = groundPlane.bounds;
        _groundY = b.max.y;
        _boundsCenter = new Vector2(b.center.x, b.center.z);
        _boundsHalf = new Vector2(b.extents.x, b.extents.z);

        _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        BuildQuad();
        // A placeholder radius for the one frame before LateUpdate measures
        // the real one.
        PushSettings(minRadius);
    }

    void OnDisable()
    {
        if (Active == this)
            Active = null;

        DiscardPreviousQuad();
        DestroyAnywhere(_material);
        _material = null;
    }

    /// <summary>
    /// Remove any preview quad left over — from a previous enable, or from a
    /// script recompile, which re-runs OnEnable without ever running OnDisable
    /// on the object that came before.
    /// </summary>
    void DiscardPreviousQuad()
    {
        _quad = null;
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.name == QuadName)
                DestroyAnywhere(child.gameObject);
        }
    }

    /// <summary>
    /// Destroy works in play mode and does nothing useful in edit mode;
    /// DestroyImmediate is the reverse, and throws if called during play from
    /// certain callbacks. One helper so no call site has to remember which.
    /// </summary>
    static void DestroyAnywhere(Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    void BuildQuad()
    {
        var go = new GameObject(QuadName, typeof(MeshFilter), typeof(MeshRenderer));
        // A preview, not scene content: never serialised into the scene file,
        // never in a diff, and gone the moment this component is.
        go.hideFlags = HideFlags.HideAndDontSave;
        go.layer = GizmoLayer;
        go.transform.SetParent(transform, false);
        _quad = go.transform;

        // A UNIT quad, resized by the transform each frame. The visible radius
        // changes with every zoom, and scaling a transform is free where
        // rewriting a mesh is not.
        var mesh = new Mesh { name = QuadName, hideFlags = HideFlags.HideAndDontSave };
        mesh.vertices = new[]
        {
            new Vector3(-1f, 0f, -1f),
            new Vector3(1f, 0f, -1f),
            new Vector3(1f, 0f, 1f),
            new Vector3(-1f, 0f, 1f)
        };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        // Generous bounds: the quad is re-centred and resized every frame, and
        // a mesh whose bounds lag its transform gets culled at the wrong moment.
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(4f, 1f, 4f));
        go.GetComponent<MeshFilter>().sharedMesh = mesh;

        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = _material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    void PushSettings(float viewRadius)
    {
        if (_material == null)
            return;

        _material.SetColor(LineColorId, lineColor);
        _material.SetFloat(OpacityId, opacity);
        _material.SetFloat(SpacingId, NeospaceUnits.ModuleMeters * ModulesPerLine);
        _material.SetFloat(LineWidthId, lineWidthPixels);
        // fadeStart is clamped below fadeEnd rather than trusted: smoothstep
        // with its edges crossed inverts, and a grid that is solid where it
        // should be absent is a confusing way to learn you typed 0.8 and 0.4.
        float end = Mathf.Max(fadeEnd, 0.05f) * viewRadius;
        float start = Mathf.Min(fadeStart, fadeEnd - 0.02f) * viewRadius;
        _material.SetFloat(FadeStartId, start);
        _material.SetFloat(FadeEndId, end);
        _material.SetVector(BoundsCenterId, new Vector4(_boundsCenter.x, _boundsCenter.y, 0f, 0f));
        _material.SetVector(BoundsHalfId, new Vector4(_boundsHalf.x, _boundsHalf.y, 0f, 0f));
        // Softened over roughly one H7 span, so the ground's own edge arrives
        // as a fade rather than as a cut.
        _material.SetFloat(BoundsFadeId, NeospaceUnits.ModuleMeters * ModulesPerLine);

        float module = NeospaceUnits.ModuleMeters;
        _material.SetColor(ModuleColorId, moduleColor);
        _material.SetFloat(ModuleSpacingId, module);
        _material.SetFloat(ModuleOpacityId, moduleOpacity);
        _material.SetFloat(ModuleLineWidthId, moduleLineWidthPixels);
        _material.SetVector(IslandCenterId, new Vector4(_islandCenter.x, _islandCenter.y, 0f, 0f));
        _material.SetVector(IslandHalfId, new Vector4(_islandHalf.x, _islandHalf.y, 0f, 0f));
        // Start clamped below end rather than trusted: smoothstep with its
        // edges crossed inverts, and a grid that is solid where it should be
        // absent is a confusing way to discover the two were typed backwards.
        float islandEnd = Mathf.Max(islandFadeEndModules, 0.2f) * module;
        float islandStart = Mathf.Clamp(islandFadeStartModules * module, 0f, islandEnd - 0.01f);
        _material.SetFloat(IslandFadeStartId, islandStart);
        _material.SetFloat(IslandFadeEndId, islandEnd);

        float originEnd = Mathf.Max(originFadeEndModules, 0.2f) * module;
        float originStart = Mathf.Clamp(originFadeStartModules * module, 0f, originEnd - 0.01f);
        _material.SetColor(OriginColorId, originColor);
        _material.SetFloat(OriginOpacityId, originOpacity);
        _material.SetFloat(OriginLineWidthId, originLineWidthPixels);
        _material.SetFloat(OriginFadeStartId, originStart);
        _material.SetFloat(OriginFadeEndId, originEnd);

        _material.SetColor(ZoneColorId, zoneColor);
        _material.SetFloat(ZoneOpacityId, zoneOpacity);
        _material.SetFloat(ZoneLineWidthId, zoneLineWidthPixels);
        _material.SetVector(ZoneCenterId, new Vector4(_zoneCenter.x, _zoneCenter.y, 0f, 0f));
        _material.SetVector(ZoneHalfId, new Vector4(_zoneHalf.x, _zoneHalf.y, 0f, 0f));
        _material.SetFloat(ZoneStrengthId, _zoneStrength);

        _material.SetColor(HighlightColorId, highlightColor);
        _material.SetFloat(HoverOpacityId, hoverOpacity);
        _material.SetFloat(HoverFillOpacityId, hoverFillOpacity);
        _material.SetVector(HoverCenterId, new Vector4(_hoverCenter.x, _hoverCenter.y, 0f, 0f));
        _material.SetVector(HoverHalfId, new Vector4(_hoverHalf.x, _hoverHalf.y, 0f, 0f));
        float hoverEnd = Mathf.Max(hoverFadeEndModules, 0.2f) * module;
        float hoverStart = Mathf.Clamp(hoverFadeStartModules * module, 0f, hoverEnd - 0.01f);
        _material.SetFloat(HoverFadeStartId, hoverStart);
        _material.SetFloat(HoverFadeEndId, hoverEnd);
        _material.SetFloat(HoverStrengthId, _hoverStrength);
        _material.SetFloat(OutlineWidthId, outlineWidthPixels);
        // Preserve zero exactly: the shader treats it as a continuous line.
        // The previous 0.05-module minimum turned zero into a very short dash
        // cycle, which only became visible when the camera was close enough.
        _material.SetFloat(OutlineDashId, Mathf.Max(0f, outlineDashModules) * module);
    }

    /// <summary>
    /// Where the module grid should be: around everything built, or around the
    /// origin when nothing is. Polled rather than watched, because the answer
    /// only changes when a part appears or disappears.
    /// </summary>
    void RetargetIsland()
    {
        float module = NeospaceUnits.ModuleMeters;
        float minHalf = emptyHalfModules * module;

        if (buildController == null ||
            !StructureBounds.TryCompute(buildController, out StructureBounds.Info info))
        {
            _islandTargetCenter = Vector2.zero;
            _islandTargetHalf = Vector2.one * minHalf;
            _hasStructure = false;
            return;
        }

        _hasStructure = true;

        Bounds b = info.WorldBounds;
        float margin = marginModules * module;

        // Snapped OUTWARD to whole modules, so the island's own edge always
        // runs along real grid lines rather than cutting a cell in half.
        float minX = Mathf.Floor((b.min.x - margin) / module) * module;
        float maxX = Mathf.Ceil((b.max.x + margin) / module) * module;
        float minZ = Mathf.Floor((b.min.z - margin) / module) * module;
        float maxZ = Mathf.Ceil((b.max.z + margin) / module) * module;

        _islandTargetCenter = new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
        _islandTargetHalf = new Vector2(
            Mathf.Max((maxX - minX) * 0.5f, minHalf),
            Mathf.Max((maxZ - minZ) * 0.5f, minHalf));

        RetargetZone();
    }

    /// <summary>
    /// The built zone: the module island INCLUDING its whole fade, grown by
    /// the padding, then snapped outward to the 704 mm lattice.
    ///
    /// Measured from the island's TARGET rather than its eased position, so
    /// the zone is decided by where the build is and not by how far through
    /// an animation the island happens to be.
    ///
    /// Snapping outward on every side is what makes both promises hold at
    /// once. "Bigger than the module grid" — the snap can only enlarge. "Edges
    /// on the lattice" — a multiple of 704 mm from the origin IS a lattice
    /// line, because the lattice is drawn from the origin too.
    /// </summary>
    void RetargetZone()
    {
        float module = NeospaceUnits.ModuleMeters;
        float lattice = module * ModulesPerLine;

        float reach = (Mathf.Max(islandFadeEndModules, 0f) + Mathf.Max(zonePaddingModules, 0f)) * module;
        Vector2 min = _islandTargetCenter - _islandTargetHalf - Vector2.one * reach;
        Vector2 max = _islandTargetCenter + _islandTargetHalf + Vector2.one * reach;

        float minX = Mathf.Floor(min.x / lattice) * lattice;
        float maxX = Mathf.Ceil(max.x / lattice) * lattice;
        float minZ = Mathf.Floor(min.y / lattice) * lattice;
        float maxZ = Mathf.Ceil(max.y / lattice) * lattice;

        _zoneCenter = new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
        _zoneHalf = new Vector2((maxX - minX) * 0.5f, (maxZ - minZ) * 0.5f);
    }

    /// <summary>
    /// Where the thing being placed will land, as a box of whole 88 mm cells.
    ///
    /// READ FROM THE GHOST, which is the existing answer to both halves of the
    /// question. Every placement tool — beams, panels, guided templates, whole
    /// blocks — puts its preview on the ghost layer and positions it where the
    /// part will go; and each one HIDES that preview when the placement is not
    /// valid. So "a ghost is visible" already means "something can go here,
    /// and here is exactly where". Deriving a second answer from the cursor
    /// would be a second thing to keep in step with five tools.
    ///
    /// The box is the ghost's footprint rounded out to the module cells it
    /// covers, then grown by half a module on every side — the cell around an
    /// anchor, not the anchor. That half-module offset lives here and in the
    /// shader's outline, and nowhere that snaps or places.
    /// </summary>
    bool TryHoverFootprint(out Vector2 center, out Vector2 half)
    {
        center = Vector2.zero;
        half = Vector2.zero;

        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        bool any = false;

        for (int i = 0; i < _ghosts.Count; i++)
        {
            Renderer r = _ghosts[i];
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy)
                continue;

            Bounds b = r.bounds;
            min = Vector2.Min(min, new Vector2(b.min.x, b.min.z));
            max = Vector2.Max(max, new Vector2(b.max.x, b.max.z));
            any = true;
        }

        if (!any)
        {
            // No preview to read, which is not the same as nothing happening.
            // Two tools have no ghost at all: a two-click part on its first
            // click, before a size has been chosen, and the guided panel bay,
            // which draws only its measure and the face it will fill. Both say
            // what they are aiming at, and this reads that rather than
            // raycasting and snapping a second time.
            if (TemplatePreviewGuide.HasGroundArea)
            {
                Bounds area = TemplatePreviewGuide.GroundArea;
                min = new Vector2(area.min.x, area.min.z);
                max = new Vector2(area.max.x, area.max.z);
            }
            else if (TemplatePreviewGuide.HasGroundTarget)
            {
                Vector3 target = TemplatePreviewGuide.GroundTarget;
                min = max = new Vector2(target.x, target.z);
            }
            else
            {
                return false;
            }
        }

        float module = NeospaceUnits.ModuleMeters;
        float halfModule = module * 0.5f;

        // Which anchor cells the ghost touches. Rounding, not flooring: an
        // anchor sits at the CENTRE of its cell, so the nearest anchor to a
        // coordinate is the cell that coordinate falls in.
        var minCell = new Vector2(Mathf.Round(min.x / module), Mathf.Round(min.y / module));
        var maxCell = new Vector2(Mathf.Round(max.x / module), Mathf.Round(max.y / module));

        center = (minCell + maxCell) * 0.5f * module;
        half = (maxCell - minCell) * 0.5f * module + new Vector2(halfModule, halfModule);
        return true;
    }

    /// <summary>
    /// Re-find the ghost renderers. Slow poll: a ghost is created when a tool
    /// is picked up, which is rare. Its POSITION is read every frame, which is
    /// not.
    /// </summary>
    void PollGhosts()
    {
        _ghosts.Clear();

        int mask = buildController != null ? buildController.ghostLayerMask.value : 0;
        if (mask == 0)
            return;

        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Include,
                                                           FindObjectsSortMode.None))
        {
            if (r == null || (mask & (1 << r.gameObject.layer)) == 0)
                continue;

            // Tested once, here, rather than every frame: a preview does not
            // change its mind about whether it belongs on the ground.
            if (r.GetComponentInParent<NoGroundHighlight>() != null)
                continue;

            _ghosts.Add(r);
        }
    }

    void LateUpdate()
    {
        if (_material == null || _quad == null)
            return;

        // After the camera controllers have moved the camera, so the grid is
        // never a frame behind during a pan.
        if (_camera == null || !_camera.isActiveAndEnabled)
            _camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        if (_camera == null)
            return;

        // Unscaled, so the island and the highlight still settle while the
        // game is paused — and so they work at all in edit mode, where
        // deltaTime is whatever the editor last felt like.
        float dt = Mathf.Max(Time.unscaledDeltaTime, 1f / 240f);
        float now = Time.unscaledTime;

        if (buildController == null)
            buildController = FindFirstObjectByType<BuildController>();

        if (now >= _nextIslandPoll)
        {
            _nextIslandPoll = now + GhostPollInterval;
            RetargetIsland();
            PollGhosts();
        }

        // Exponential settle: frame-rate independent, and it never quite
        // arrives, which is what makes a grid that resizes feel like it is
        // breathing rather than stepping.
        float follow = 1f - Mathf.Exp(-Mathf.Max(followSpeed, 0.01f) * dt);
        _islandCenter = Vector2.Lerp(_islandCenter, _islandTargetCenter, follow);
        _islandHalf = Vector2.Lerp(_islandHalf, _islandTargetHalf, follow);

        _zoneStrength = Mathf.MoveTowards(_zoneStrength, _hasStructure ? 1f : 0f,
                                          dt / Mathf.Max(zoneEase, 0.01f));

        if (TryHoverFootprint(out Vector2 hoverCenter, out Vector2 hoverHalf))
        {
            // Snapped, not eased: the box must sit exactly on the cell the
            // part will occupy, and a box sliding into place would be telling
            // a small lie for a few frames about where the part is going.
            _hoverCenter = hoverCenter;
            _hoverHalf = hoverHalf;
            _hoverStrength = Mathf.MoveTowards(_hoverStrength, 1f, dt / Mathf.Max(hoverEase, 0.01f));
        }
        else
        {
            _hoverStrength = Mathf.MoveTowards(_hoverStrength, 0f, dt / Mathf.Max(hoverEase, 0.01f));
        }

        Vector2 focus = GroundFocus(_camera);
        float viewRadius = VisibleGroundRadius(_camera, focus);

        // The grid rides with the ground the finish mode sinks: it is drawn on
        // the floor and must move with it.
        float y = _groundY + FloorVisualOffset + NeospaceUnits.Mm(liftMm);
        _quad.position = new Vector3(focus.x, y, focus.y);

        // Geometry only has to reach past where the shader has already faded
        // the grid to nothing. The slack is for fast pans, where a frame of
        // lag would otherwise expose the mesh edge.
        float reach = Mathf.Max(viewRadius * fadeEnd, minRadius) * MeshSlack;
        _quad.localScale = new Vector3(reach, 1f, reach);

        _material.SetVector(FadeCenterId, new Vector4(focus.x, focus.y, 0f, 0f));

        // Cheap enough to re-push every frame, and it means changing a value
        // in the inspector while playing does something. There is no second
        // copy of these numbers to fall out of step.
        PushSettings(viewRadius);
    }

    /// <summary>
    /// How much ground the camera can actually see: the distance from the
    /// focus point out to the furthest screen corner, projected onto the
    /// ground plane. This is what makes one set of fade numbers correct at
    /// every zoom.
    ///
    /// A corner whose ray runs ABOVE the horizon sees ground all the way to
    /// infinity, so there is no distance to measure. When any corner does
    /// that, the answer is "as far as we are allowed" — the ground's own edge
    /// fade takes over from there.
    /// </summary>
    float VisibleGroundRadius(Camera cam, Vector2 focus)
    {
        float radius = 0f;

        for (int i = 0; i < 4; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? 0f : cam.pixelWidth,
                                     (i & 2) == 0 ? 0f : cam.pixelHeight, 0f);
            Ray ray = cam.ScreenPointToRay(corner);

            if (ray.direction.y > -1e-3f)
                return maxRadius;

            float distance = (ray.origin.y - _groundY) / -ray.direction.y;
            if (distance <= 0f || distance > 10000f)
                return maxRadius;

            Vector3 hit = ray.origin + ray.direction * distance;
            radius = Mathf.Max(radius, Vector2.Distance(new Vector2(hit.x, hit.z), focus));
        }

        return Mathf.Clamp(radius, minRadius, maxRadius);
    }

    /// <summary>
    /// Where the camera is looking at the ground. Falls back to the camera's
    /// own position when it is looking away from the plane — level with it, or
    /// up at the sky — where a look-at point is either infinitely far away or
    /// behind the viewer.
    /// </summary>
    Vector2 GroundFocus(Camera cam)
    {
        // Shared with CameraControlManager, which needs the same answer to
        // carry the view across a switch between schemes.
        CameraMath.GroundFocus(cam, _groundY, out Vector3 focus);
        return new Vector2(focus.x, focus.z);
    }

    // No OnDestroy. OnDisable already clears the quad and the material, and it
    // runs on destruction too — as well as on the recompile that destruction
    // does not cover.
}
