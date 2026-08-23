using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes the invisible connection system visible while building. Whenever a
/// placement tool is armed, every FREE attachment point that the armed part
/// could target lights up near the cursor:
///
///   - rings = free HOLES (a beam's peg plugs into a hole)
///   - dots  = free PEGS  (a frame seats onto a peg)
///
/// Only one role is ever shown at a time (it depends on the armed tool), so
/// shape alone distinguishes them. All markers draw in the theme ink tone;
/// the nearest candidate — the one a click would connect to — is the single
/// accent-coloured element and pulses gently. Markers fade with distance from
/// the cursor so the scene never turns into confetti; they live on the
/// Ignore Raycast layer and never block picking.
/// </summary>
public class AttachmentMarkerController : MonoBehaviour
{
    public Camera cam;
    public BuildController buildController;

    [Header("Look")]
    [Tooltip("Marker diameter for holes, millimetres.")]
    public float holeDiameterMm = 28f;
    [Tooltip("Marker diameter for pegs, millimetres.")]
    public float pegDiameterMm = 20f;

    [Header("Culling")]
    [Tooltip("Markers farther than this many 88 mm modules from the cursor ray are hidden.")]
    public float showRangeModules = 6f;
    [Tooltip("The nearest marker within this many modules of the ray pulses as the active target.")]
    public float targetRangeModules = 0.5f;

    const int GizmoLayer = 2;   // built-in Ignore Raycast
    const int MaxMarkers = 150;

    TemplateSession _session;
    FreePartSession _freeSession;
    Transform _rootGo;
    readonly List<MeshRenderer> _pool = new List<MeshRenderer>();
    MaterialPropertyBlock _mpb;
    Material _material;
    static Mesh _ringMesh;
    static Mesh _discMesh;
    static readonly int ColorId = Shader.PropertyToID("_Color");

    struct Candidate
    {
        public Vector3 posSum;   // summed positions of merged points
        public int count;        // merged point count (marker sits at the mean)
        public float rayDist;    // best (smallest) ray distance in the group
    }

    readonly List<Candidate> _candidates = new List<Candidate>();
    readonly Dictionary<long, int> _groups = new Dictionary<long, int>();
    readonly Dictionary<Transform, int> _rootIds = new Dictionary<Transform, int>();

    void Update()
    {
        AttachmentPoint.PointRole? role = ActiveTargetRole();
        if (role == null || cam == null)
        {
            HideAll(0);
            return;
        }

        EnsurePool();

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        float module = NeospaceUnits.ModuleMeters;
        float showRange = showRangeModules * module;
        float targetRange = targetRangeModules * module;
        int ghostMask = buildController != null ? buildController.ghostLayerMask.value : 0;

        // Gather every free point of the wanted role near the cursor ray.
        // Points sharing the same part, height level and grid cell merge into
        // ONE marker (a V frame has four holes per level — one ring reads far
        // cleaner than four, and any of the four accepts the connection).
        _candidates.Clear();
        _groups.Clear();
        _rootIds.Clear();
        int bestIndex = -1;
        float bestDist = float.MaxValue;
        foreach (AttachmentPoint ap in AttachmentPoint.Live)
        {
            if (ap == null || ap.role != role.Value || ap.isOccupied)
                continue;
            Transform root = ap.transform.root;
            if (root == null || (ghostMask & (1 << root.gameObject.layer)) != 0)
                continue;

            Vector3 p = ap.transform.position;
            Vector3 toPoint = p - ray.origin;
            float along = Vector3.Dot(toPoint, ray.direction);
            if (along < 0f)
                continue; // behind the camera
            float rayDist = Vector3.Cross(ray.direction, toPoint).magnitude;
            if (rayDist > showRange)
                continue;

            long key = GroupKey(root, p, module);
            if (_groups.TryGetValue(key, out int idx))
            {
                Candidate merged = _candidates[idx];
                merged.posSum += p;
                merged.count++;
                if (rayDist < merged.rayDist)
                    merged.rayDist = rayDist;
                _candidates[idx] = merged;

                if (merged.rayDist < bestDist)
                {
                    bestDist = merged.rayDist;
                    bestIndex = idx;
                }
                continue;
            }

            if (rayDist < bestDist)
            {
                bestDist = rayDist;
                bestIndex = _candidates.Count;
            }
            _groups[key] = _candidates.Count;
            _candidates.Add(new Candidate { posSum = p, count = 1, rayDist = rayDist });
        }

        // Closest candidates first when there are more than we can show.
        if (_candidates.Count > MaxMarkers)
        {
            int targetIdx = bestIndex;
            _candidates.Sort((a, b) => a.rayDist.CompareTo(b.rayDist));
            if (targetIdx >= 0)
                bestIndex = 0; // after sorting, the nearest is first
            _candidates.RemoveRange(MaxMarkers, _candidates.Count - MaxMarkers);
        }

        bool holes = role.Value == AttachmentPoint.PointRole.Hole;
        Color baseColor = UIThemeController.InkColor;
        Color targetColor = UIThemeController.AccentColor;
        float diameter = NeospaceUnits.Mm(holes ? holeDiameterMm : pegDiameterMm);
        Mesh mesh = holes ? RingMesh() : DiscMesh();
        Quaternion facing = cam.transform.rotation;
        float pulse = 1f + 0.10f * Mathf.Sin(Time.unscaledTime * 6f);

        int used = 0;
        for (int i = 0; i < _candidates.Count && used < _pool.Count; i++)
        {
            Candidate c = _candidates[i];
            if (c.count <= 0)
                continue;

            bool isTarget = i == bestIndex && c.rayDist <= targetRange;

            MeshRenderer mr = _pool[used++];
            Transform t = mr.transform;

            // Lift toward the camera so the marker isn't swallowed by the part.
            Vector3 pos = c.posSum / c.count;
            Vector3 toCam = (cam.transform.position - pos).normalized;
            t.SetPositionAndRotation(pos + toCam * NeospaceUnits.Mm(22f), facing);
            t.localScale = Vector3.one * (diameter * (isTarget ? 1.3f * pulse : 1f));

            mr.GetComponent<MeshFilter>().sharedMesh = mesh;

            float fade = Mathf.Lerp(0.55f, 0.10f, Mathf.Clamp01(c.rayDist / showRange));
            Color color = isTarget ? targetColor : baseColor;
            color.a = isTarget ? 1f : fade;

            _mpb.SetColor(ColorId, color);
            mr.SetPropertyBlock(_mpb);
            if (!mr.gameObject.activeSelf)
                mr.gameObject.SetActive(true);
        }

        HideAll(used);
    }

    /// <summary>
    /// Merge key: same part + same 88 mm grid cell = one marker. The four
    /// holes of a V frame level share a cell (they sit ±20.5 mm around the
    /// post axis); pegs on opposite beam ends land in different cells.
    /// Roots get a frame-local sequential id (GetInstanceID is obsolete in
    /// this Unity version).
    /// </summary>
    long GroupKey(Transform root, Vector3 p, float module)
    {
        if (!_rootIds.TryGetValue(root, out int rootId))
        {
            rootId = _rootIds.Count;
            _rootIds[root] = rootId;
        }

        int x = Mathf.RoundToInt(p.x / module);
        int y = Mathf.RoundToInt(p.y / module);
        int z = Mathf.RoundToInt(p.z / module);
        return ((long)rootId << 32)
             ^ ((long)(x & 0x3FF) << 20)
             ^ ((long)(y & 0x3FF) << 10)
             ^ (long)(z & 0x3FF);
    }

    /// <summary>
    /// The attachment role the armed tool connects to — or null when nothing
    /// relevant is armed (markers hidden).
    /// </summary>
    AttachmentPoint.PointRole? ActiveTargetRole()
    {
        if (StructureClipboard.StampingActive)
            return null;

        if (UIInteractionState.CurrentExperience == UIInteractionState.Experience.Guided)
        {
            if (_session == null)
                _session = FindFirstObjectByType<TemplateSession>();
            if (_session == null)
                return null;

            // Beams bridge two frame holes; Panels start from a frame hole.
            return _session.ActiveTool == GuidedTemplateTool.ConnectorsT2 ||
                   _session.ActiveTool == GuidedTemplateTool.PanelBayT3
                ? AttachmentPoint.PointRole.Hole
                : (AttachmentPoint.PointRole?)null;
        }

        // Category part tools (Upright / Crossbar / Twist bar): markers show
        // while the tool waits for its anchor click.
        if (_freeSession == null)
            _freeSession = FindFirstObjectByType<FreePartSession>();
        if (_freeSession != null && _freeSession.ActiveKind != FreePartKind.None)
        {
            if (!_freeSession.AwaitingAnchor)
                return null; // scale phase: the guide line has the focus
            return _freeSession.ActiveKind == FreePartKind.Vertical
                ? AttachmentPoint.PointRole.Peg     // frames seat onto pegs
                : AttachmentPoint.PointRole.Hole;   // bars plug into holes
        }

        string partId = buildController != null ? buildController.currentPartId : null;
        if (string.IsNullOrEmpty(partId))
            return null;
        if (BeamPartUtility.IsVertical(partId))
            return AttachmentPoint.PointRole.Peg;    // frames seat onto pegs
        if (BeamPartUtility.IsHorizontalLike(partId))
            return AttachmentPoint.PointRole.Hole;   // beams plug into holes
        return null;                                 // PANEL etc: slot hover handles it
    }

    // ------------------------------------------------------------------
    // Pool & meshes
    // ------------------------------------------------------------------

    void EnsurePool()
    {
        if (_rootGo != null)
            return;

        _rootGo = new GameObject("AttachmentMarkers").transform;
        _rootGo.gameObject.layer = GizmoLayer;
        _mpb = new MaterialPropertyBlock();

        _material = new Material(Shader.Find("Sprites/Default"));
        _material.renderQueue = 3150; // over the beams, under the move gizmo

        for (int i = 0; i < MaxMarkers; i++)
        {
            var go = new GameObject("Marker", typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = GizmoLayer;
            go.transform.SetParent(_rootGo, false);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.SetActive(false);
            _pool.Add(mr);
        }
    }

    void HideAll(int fromIndex)
    {
        for (int i = fromIndex; i < _pool.Count; i++)
        {
            if (_pool[i] != null && _pool[i].gameObject.activeSelf)
                _pool[i].gameObject.SetActive(false);
        }
    }

    /// <summary>Flat annulus facing +Z, outer diameter 1.</summary>
    static Mesh RingMesh()
    {
        if (_ringMesh != null)
            return _ringMesh;

        const int seg = 32;
        const float outer = 0.5f, inner = 0.30f;
        var verts = new List<Vector3>(seg * 2);
        var tris = new List<int>(seg * 6);
        for (int i = 0; i < seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            verts.Add(dir * outer);
            verts.Add(dir * inner);
        }
        for (int i = 0; i < seg; i++)
        {
            int o0 = i * 2, i0 = i * 2 + 1;
            int o1 = ((i + 1) % seg) * 2, i1 = o1 + 1;
            tris.AddRange(new[] { o0, o1, i0, i0, o1, i1 });
            tris.AddRange(new[] { o0, i0, o1, i0, i1, o1 }); // back face
        }

        _ringMesh = new Mesh { name = "HoleRing" };
        _ringMesh.SetVertices(verts);
        _ringMesh.SetTriangles(tris, 0);
        _ringMesh.RecalculateNormals();
        _ringMesh.RecalculateBounds();
        return _ringMesh;
    }

    /// <summary>Flat disc facing +Z, diameter 1.</summary>
    static Mesh DiscMesh()
    {
        if (_discMesh != null)
            return _discMesh;

        const int seg = 24;
        var verts = new List<Vector3> { Vector3.zero };
        var tris = new List<int>();
        for (int i = 0; i < seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            verts.Add(new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, 0f));
        }
        for (int i = 0; i < seg; i++)
        {
            int cur = 1 + i, next = 1 + (i + 1) % seg;
            tris.AddRange(new[] { 0, next, cur });
            tris.AddRange(new[] { 0, cur, next }); // back face
        }

        _discMesh = new Mesh { name = "PegDisc" };
        _discMesh.SetVertices(verts);
        _discMesh.SetTriangles(tris, 0);
        _discMesh.RecalculateNormals();
        _discMesh.RecalculateBounds();
        return _discMesh;
    }
}
