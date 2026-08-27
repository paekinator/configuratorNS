using UnityEngine;

/// <summary>
/// Makes the grid a living surface instead of one big baked slab:
///
///  - the floor keeps only a FAINT reference grid (the bold 704 mm lines,
///    installed on a runtime material instance), so the world always reads
///    as a buildable surface — never a void — while staying quiet;
///  - a GRID PATCH — a transparent quad drawn just above the floor — carries
///    the full 88 mm module grid. It sizes itself to the current build plus
///    a margin, growing and shrinking smoothly as the structure changes, and
///    is centred on the build (or the world origin when nothing is placed).
///
/// The patch's UVs are anchored to WORLD coordinates, so its lines always
/// sit exactly on the module grid the placement tools snap to, no matter
/// where the patch edges are.
/// </summary>
public class AdaptiveGridController : MonoBehaviour
{
    public BuildController buildController;

    [Header("Sizing (88 mm modules)")]
    [Tooltip("Free grid modules kept around the structure on every side.")]
    public float marginModules = 5f;
    [Tooltip("Half-size of the default patch when nothing is built yet.")]
    public float emptyHalfModules = 10f;
    [Tooltip("How quickly the patch follows the target rect (1/s).")]
    public float followSpeed = 6f;

    [Header("Look")]
    [Tooltip("88 mm module line color (alpha = line strength).")]
    public Color minorLineColor = new Color(0.62f, 0.59f, 0.54f, 0.55f);
    [Tooltip("Bold line every 8 modules (704 mm).")]
    public Color majorLineColor = new Color(0.55f, 0.52f, 0.47f, 0.85f);
    [Tooltip("Outline marking the edge of the adaptive grid area.")]
    public Color borderColor = new Color(0.55f, 0.52f, 0.47f, 0.6f);
    [Tooltip("Patch lift above the floor surface, millimetres.")]
    public float liftMm = 1.5f;

    const float PollInterval = 0.25f;
    const int GizmoLayer = 2; // Ignore Raycast

    /// <summary>
    /// Vertical offset applied to the visible grid (world units). Finish
    /// mode sinks the ground by one foot height so the build stands on its
    /// feet; the module grid must ride along with the floor it decorates.
    /// </summary>
    public static float FloorVisualOffset;

    Renderer _floorRenderer;
    float _floorTopY;
    float _appliedOffset;

    MeshFilter _patchFilter;
    Mesh _patchMesh;
    LineRenderer _border;

    // Current and target patch rect (world XZ): center + half extents.
    Vector2 _center, _halfSize;
    Vector2 _targetCenter, _targetHalfSize;
    float _nextPoll;
    bool _ready;

    void Start()
    {
        GameObject floor = GameObject.Find("GridFloor");
        if (floor == null)
        {
            Debug.LogWarning("[AdaptiveGrid] No 'GridFloor' object found · adaptive grid disabled.");
            return;
        }

        _floorRenderer = floor.GetComponent<Renderer>();
        if (_floorRenderer == null)
        {
            Debug.LogWarning("[AdaptiveGrid] GridFloor has no Renderer · adaptive grid disabled.");
            return;
        }

        _floorTopY = _floorRenderer.bounds.max.y;
        InstallFaintFloorGrid();

        BuildPatch();

        _center = _targetCenter = Vector2.zero;
        _halfSize = _targetHalfSize = Vector2.one * (emptyHalfModules * NeospaceUnits.ModuleMeters);
        RebuildMesh();
        _ready = true;
        Debug.Log("[AdaptiveGrid] Active · floor grid replaced by adaptive patch.");
    }

    void Update()
    {
        if (!_ready)
            return;

        if (!Mathf.Approximately(_appliedOffset, FloorVisualOffset))
        {
            _appliedOffset = FloorVisualOffset;
            RebuildMesh();
        }

        if (Time.time >= _nextPoll)
        {
            _nextPoll = Time.time + PollInterval;
            RetargetToStructure();
        }

        float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        Vector2 newCenter = Vector2.Lerp(_center, _targetCenter, t);
        Vector2 newHalf = Vector2.Lerp(_halfSize, _targetHalfSize, t);

        if ((newCenter - _center).sqrMagnitude > 1e-10f ||
            (newHalf - _halfSize).sqrMagnitude > 1e-10f)
        {
            _center = newCenter;
            _halfSize = newHalf;
            RebuildMesh();
        }
    }

    void RetargetToStructure()
    {
        float module = NeospaceUnits.ModuleMeters;

        if (!StructureBounds.TryCompute(buildController, out StructureBounds.Info info))
        {
            _targetCenter = Vector2.zero;
            _targetHalfSize = Vector2.one * (emptyHalfModules * module);
            return;
        }

        Bounds b = info.WorldBounds;
        float margin = marginModules * module;

        // Snap the edges OUTWARD to whole modules so the patch boundary
        // always runs along real grid lines.
        float minX = Mathf.Floor((b.min.x - margin) / module) * module;
        float maxX = Mathf.Ceil((b.max.x + margin) / module) * module;
        float minZ = Mathf.Floor((b.min.z - margin) / module) * module;
        float maxZ = Mathf.Ceil((b.max.z + margin) / module) * module;

        // Never smaller than the empty default.
        float minHalf = emptyHalfModules * module;
        Vector2 center = new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
        Vector2 half = new Vector2(
            Mathf.Max((maxX - minX) * 0.5f, minHalf),
            Mathf.Max((maxZ - minZ) * 0.5f, minHalf));

        _targetCenter = center;
        _targetHalfSize = half;
    }

    // ------------------------------------------------------------------
    // Faint full-floor reference grid
    // ------------------------------------------------------------------

    /// <summary>
    /// Swap the floor's baked grid for a much quieter one: only the bold
    /// 704 mm lines, barely darker than the floor tint. Applied to a runtime
    /// material INSTANCE so the shared asset and dark-mode tinting are
    /// untouched. Same tiling/offset math as the editor styler, so the faint
    /// lines sit exactly under the patch's bold lines.
    /// </summary>
    void InstallFaintFloorGrid()
    {
        const int size = 128;
        const int linePx = 2;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 8
        };
        var pixels = new Color32[size * size];
        Color32 fill = new Color32(255, 255, 255, 255);
        Color32 line = new Color32(240, 238, 235, 255); // whisper of a line
        // Lines are CENTRED on the tile coordinate (half the width on each
        // side, wrapping over the tile edge) so a post snapped to a module
        // point sits exactly astride the line, not beside it.
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            bool on = (x + linePx / 2) % size < linePx ||
                      (y + linePx / 2) % size < linePx;
            pixels[y * size + x] = on ? line : fill;
        }
        tex.SetPixels32(pixels);
        tex.Apply(true);

        float tileWorld = NeospaceUnits.ModuleMeters * 8f; // one tile = 704 mm
        Transform floorTf = _floorRenderer.transform;
        float floorSize = 10f * Mathf.Max(floorTf.localScale.x, floorTf.localScale.z);
        float tiles = floorSize / tileWorld;
        var offset = new Vector2(
            Mathf.Repeat((floorTf.position.x - floorSize * 0.5f) / tileWorld, 1f),
            Mathf.Repeat((floorTf.position.z - floorSize * 0.5f) / tileWorld, 1f));

        Material floorMat = _floorRenderer.material;
        string prop = floorMat.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
        floorMat.SetTexture(prop, tex);
        floorMat.SetTextureScale(prop, new Vector2(tiles, tiles));
        floorMat.SetTextureOffset(prop, offset);
    }

    // ------------------------------------------------------------------
    // Patch mesh & texture
    // ------------------------------------------------------------------

    void BuildPatch()
    {
        var go = new GameObject("GridPatch", typeof(MeshFilter), typeof(MeshRenderer));
        go.layer = GizmoLayer;
        go.transform.SetParent(transform, false);

        _patchFilter = go.GetComponent<MeshFilter>();
        _patchMesh = new Mesh { name = "GridPatch" };
        _patchFilter.sharedMesh = _patchMesh;

        var mr = go.GetComponent<MeshRenderer>();
        var material = new Material(Shader.Find("Sprites/Default"));
        material.mainTexture = BuildLineTexture();
        material.renderQueue = 2900; // over the floor, under transparents
        mr.sharedMaterial = material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        // Thin outline that travels with the patch edge so the adaptive
        // extent of the grid is visible.
        var borderGo = new GameObject("GridPatchBorder");
        borderGo.layer = GizmoLayer;
        borderGo.transform.SetParent(go.transform, false);
        _border = borderGo.AddComponent<LineRenderer>();
        _border.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        _border.loop = true;
        _border.positionCount = 4;
        _border.useWorldSpace = true;
        float bw = NeospaceUnits.Mm(9f);
        _border.startWidth = bw;
        _border.endWidth = bw;
        _border.numCapVertices = 0;
        _border.startColor = borderColor;
        _border.endColor = borderColor;
        _border.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>
    /// One texture tile spans 8 modules (704 mm) — the same rhythm as the old
    /// baked floor grid: a bold line on the tile edge, thin lines at every
    /// 88 mm module in between, transparent everywhere else.
    /// </summary>
    Texture2D BuildLineTexture()
    {
        const int modulesPerTile = 8;
        const int pxPerModule = 32;
        const int size = modulesPerTile * pxPerModule; // 256
        const int majorPx = 4;
        const int minorPx = 2;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 8
        };

        var pixels = new Color32[size * size];
        Color32 major = majorLineColor;
        Color32 minor = minorLineColor;
        Color32 clear = new Color32(0, 0, 0, 0);
        // Every line is CENTRED on its module coordinate (half the width on
        // each side, wrapping across tile/module boundaries): parts snap
        // their centres to these coordinates, so the drawn line must sit
        // astride the coordinate for a snapped post to look centred on it.
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            bool isMajor = (x + majorPx / 2) % size < majorPx ||
                           (y + majorPx / 2) % size < majorPx;
            bool isMinor = (x + minorPx / 2) % pxPerModule < minorPx ||
                           (y + minorPx / 2) % pxPerModule < minorPx;
            pixels[y * size + x] = isMajor ? major : (isMinor ? minor : clear);
        }

        tex.SetPixels32(pixels);
        tex.Apply(true);
        return tex;
    }

    void RebuildMesh()
    {
        float y = _floorTopY + _appliedOffset + NeospaceUnits.Mm(liftMm);
        float module = NeospaceUnits.ModuleMeters;

        float minX = _center.x - _halfSize.x;
        float maxX = _center.x + _halfSize.x;
        float minZ = _center.y - _halfSize.y;
        float maxZ = _center.y + _halfSize.y;

        var verts = new Vector3[]
        {
            new Vector3(minX, y, minZ),
            new Vector3(maxX, y, minZ),
            new Vector3(maxX, y, maxZ),
            new Vector3(minX, y, maxZ)
        };

        // UVs in WORLD coordinates: 1 UV unit = 1 texture tile = 8 modules,
        // anchored at the world origin, so the lines (and the bold every-8th
        // line) land exactly on the snap grid.
        float tile = module * 8f;
        var uvs = new Vector2[]
        {
            new Vector2(minX / tile, minZ / tile),
            new Vector2(maxX / tile, minZ / tile),
            new Vector2(maxX / tile, maxZ / tile),
            new Vector2(minX / tile, maxZ / tile)
        };

        var colors = new Color32[] { Color.white, Color.white, Color.white, Color.white };
        var tris = new int[] { 0, 2, 1, 0, 3, 2 };

        _patchMesh.Clear();
        _patchMesh.vertices = verts;
        _patchMesh.uv = uvs;
        _patchMesh.colors32 = colors;
        _patchMesh.triangles = tris;
        _patchMesh.RecalculateNormals();
        _patchMesh.RecalculateBounds();

        if (_border != null)
        {
            float by = y + NeospaceUnits.Mm(0.5f);
            _border.SetPosition(0, new Vector3(minX, by, minZ));
            _border.SetPosition(1, new Vector3(maxX, by, minZ));
            _border.SetPosition(2, new Vector3(maxX, by, maxZ));
            _border.SetPosition(3, new Vector3(minX, by, maxZ));
        }
    }
}
