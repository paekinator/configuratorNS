using UnityEngine;

/// <summary>
/// One placed piece in Space Mode: a rigid, frozen copy of a saved piece
/// (renderers only — no beam scripts, no attachment points), selectable and
/// movable as a single unit. The pivot sits at the piece's footprint centre
/// on the floor, so grid snapping and yaw rotation are trivial.
///
/// Selection feedback is a translucent footprint pad drawn under the piece.
/// </summary>
public class SpaceInstance : MonoBehaviour
{
    [HideInInspector] public string pieceId;
    [HideInInspector] public string pieceName;
    [HideInInspector] public string code;
    [HideInInspector] public float price;

    /// <summary>Footprint (X, Z) and height in world units, at identity yaw.</summary>
    [HideInInspector] public Vector3 size;

    /// <summary>
    /// Local offset from the pivot to the true footprint centre. The pivot
    /// is quantized to the 88 mm lattice, so pieces an odd number of
    /// modules wide carry up to half a module of offset here — overlap
    /// tests must use pivot + this, not the pivot alone.
    /// </summary>
    [HideInInspector] public Vector3 footprintCenter;

    // Pads draw in the theme accent so selection/grouping share one visual
    // language with the rest of the UI. Read live: theme can switch mid-run.
    static Color PadColor => WithAlpha(UIThemeController.AccentColor, 0.18f);
    static Color PadEdgeColor => WithAlpha(UIThemeController.AccentColor, 0.75f);
    static Color HintColor => WithAlpha(UIThemeController.AccentColor, 0.08f);
    static Color HintEdgeColor => WithAlpha(UIThemeController.AccentColor, 0.38f);

    static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }
    static Material _padMaterial;

    GameObject _pad;
    Mesh _padMesh;
    LineRenderer _padEdge;
    bool _groupHint;
    bool _groupHintStrong;

    public bool IsSelected { get; private set; }

    /// <summary>
    /// Bumped whenever a piece appears or disappears — pieces carry no
    /// attachment points, so pollers use this instead of the structure
    /// version to notice Space Mode changes without scene scans.
    /// </summary>
    public static int Version { get; private set; } = 1;

    void OnEnable() { Version++; }
    void OnDisable() { Version++; }

    void Awake()
    {
        // A duplicate cloned from a selected instance carries a copy of the
        // pad; its mesh would be SHARED with the source's pad, so restyling
        // one would repaint the other. Drop it and rebuild fresh when needed.
        if (_pad == null)
        {
            Transform pad = transform.Find("SelectionPad");
            if (pad != null)
                Destroy(pad.gameObject);
        }
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        RefreshPad();
    }

    /// <summary>
    /// Pad shown on every member of a merged group (pieces whose frames
    /// fused into one structure). Soft while a group member is selected;
    /// <paramref name="strong"/> full-strength during the snap flash, so
    /// joining visibly reads as "these are one now".
    /// </summary>
    public void SetGroupHint(bool hint, bool strong = false)
    {
        if (_groupHint == hint && _groupHintStrong == strong)
            return;
        _groupHint = hint;
        _groupHintStrong = strong;
        RefreshPad();
    }

    void RefreshPad()
    {
        bool show = IsSelected || _groupHint;
        if (show && _pad == null)
            BuildPad();
        if (_pad == null)
            return;

        _pad.SetActive(show);
        if (!show)
            return;

        bool full = IsSelected || (_groupHint && _groupHintStrong);
        Color fill = full ? PadColor : HintColor;
        Color edge = full ? PadEdgeColor : HintEdgeColor;
        if (_padMesh != null)
            _padMesh.colors = new[] { fill, fill, fill, fill };
        if (_padEdge != null)
            _padEdge.startColor = _padEdge.endColor = edge;
    }

    /// <summary>World-space centre of the piece's body (not the pivot).</summary>
    public Vector3 Center => transform.TransformPoint(footprintCenter);

    /// <summary>World-space footprint centre at floor level.</summary>
    public Vector3 FootprintCenterWorld
    {
        get
        {
            Vector3 c = transform.position +
                transform.rotation * new Vector3(footprintCenter.x, 0f, footprintCenter.z);
            c.y = 0f;
            return c;
        }
    }

    void BuildPad()
    {
        if (_padMaterial == null)
            _padMaterial = new Material(Shader.Find("Sprites/Default"));

        float margin = NeospaceUnits.Mm(30f);
        float halfX = size.x * 0.5f + margin;
        float halfZ = size.z * 0.5f + margin;
        float y = 0.012f;   // just above the floor to avoid z-fighting

        _pad = new GameObject("SelectionPad");
        _pad.transform.SetParent(transform, false);
        // Around the piece's BODY, not the pivot — the pivot is quantized to
        // the lattice and can sit up to half a module off the visual centre.
        _pad.transform.localPosition = new Vector3(footprintCenter.x, 0f, footprintCenter.z);

        // Filled quad
        var quad = new GameObject("Fill", typeof(MeshFilter), typeof(MeshRenderer));
        quad.transform.SetParent(_pad.transform, false);
        var mesh = new Mesh
        {
            vertices = new[]
            {
                new Vector3(-halfX, y, -halfZ), new Vector3(halfX, y, -halfZ),
                new Vector3(halfX, y, halfZ), new Vector3(-halfX, y, halfZ)
            },
            triangles = new[] { 0, 2, 1, 0, 3, 2 },
            colors = new[] { PadColor, PadColor, PadColor, PadColor }
        };
        mesh.RecalculateBounds();
        _padMesh = mesh;
        quad.GetComponent<MeshFilter>().sharedMesh = mesh;
        var rend = quad.GetComponent<MeshRenderer>();
        rend.sharedMaterial = _padMaterial;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;

        // Outline
        var lineGo = new GameObject("Edge", typeof(LineRenderer));
        lineGo.transform.SetParent(_pad.transform, false);
        var line = lineGo.GetComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 4;
        line.SetPositions(new[]
        {
            new Vector3(-halfX, y, -halfZ), new Vector3(halfX, y, -halfZ),
            new Vector3(halfX, y, halfZ), new Vector3(-halfX, y, halfZ)
        });
        line.startWidth = line.endWidth = NeospaceUnits.Mm(8f);
        line.sharedMaterial = _padMaterial;
        line.startColor = line.endColor = PadEdgeColor;
        _padEdge = line;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
    }
}
