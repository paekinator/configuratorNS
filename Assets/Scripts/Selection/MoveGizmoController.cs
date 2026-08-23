using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Translate gizmo for the current selection, with NEOSPACE-aware movement
/// rules. The arrows offered depend on what is selected:
///
///  - Selection contains vertical frames (a structure / full configuration):
///    X and Z arrows only — the whole thing slides around the grid in 88 mm
///    steps while staying on the ground. The destination is validated on
///    release; a spot that clips into another structure reverts the move.
///
///  - Selection is only horizontal/twist beams (and panels): a single Y arrow
///    — the beams slide UP/Down along the frames they are plugged into,
///    snapping exclusively to hole rows where every peg lands on a free hole
///    (same rule as the panel layer mover).
///
/// One axis at a time, never diagonally. On release the move commits: beam
/// connections re-pair, panels re-seat into the bay at their destination, and
/// one undo step is recorded.
/// </summary>
public class MoveGizmoController : MonoBehaviour
{
    public Camera cam;
    public MarqueeSelectionController selection;
    public BuildController buildController;
    public PanelSlotManager panelSlotManager;

    [Tooltip("Gizmo world size per metre of camera distance (keeps a constant screen size).")]
    public float screenScale = 0.11f;

    // Built-in Ignore Raycast layer: placement/floor masks never include it,
    // so the gizmo cannot swallow the tools' raycasts.
    const int GizmoLayer = 2;

    // Handle proportions in gizmo-local units (root is scaled per frame).
    const float ShaftLength = 1.0f;
    const float ShaftRadius = 0.030f;
    const float TipLength = 0.26f;
    const float TipRadius = 0.085f;
    const float PickRadius = 0.14f;

    class AxisHandle
    {
        public Vector3 dir;
        public string label;
        public GameObject go;
        public Collider collider;
        public Material material;
        public Color baseColor;
    }

    Transform _root;
    AxisHandle[] _axes;
    static Mesh _coneMesh;

    // Drag state
    bool _dragging;
    bool _slideMode;                 // no vertical frames selected → slide along posts
    int _dragAxis = -1;
    float _startParam;
    float _applied;                  // world-unit offset applied so far along the axis
    int _minSteps;                   // floor clamp for free-stepping moves
    readonly List<float> _slideDeltas = new List<float>();
    readonly List<Transform> _dragRoots = new List<Transform>();

    float StepWorld => NeospaceUnits.Mm(88f);

    void Update()
    {
        if (selection == null || cam == null)
        {
            HideGizmo();
            return;
        }

        selection.PruneSelection();

        bool usable = !StructureClipboard.StampingActive &&
                      !BeamResizeSession.Busy &&
                      !selection.PlacementArmed &&
                      selection.Selected.Count > 0;

        if (!usable)
        {
            if (_dragging)
                EndDrag(commit: false);
            HideGizmo();
            return;
        }

        EnsureGizmo();

        if (_dragging)
        {
            UpdateDrag();
            return;
        }

        _slideMode = ComputeSlideMode();
        UpdateAxisVisibility();

        _root.position = SelectionCenter();
        UpdateScale();
        _root.gameObject.SetActive(true);

        int hover = PickAxis();
        UpdateHighlight(hover);

        if (hover >= 0 &&
            LeftClickGesture.PressedThisFrame &&
            !LeftClickGesture.PressStartedOverUI &&
            LeftClickGesture.PressClaim == null)
        {
            LeftClickGesture.PressClaim = this;
            BeginDrag(hover);
        }
    }

    void OnDisable()
    {
        if (_dragging)
            EndDrag(commit: false);
        HideGizmo();
    }

    // ------------------------------------------------------------------
    // Movement rules
    // ------------------------------------------------------------------

    /// <summary>
    /// A selection WITHOUT vertical frames can only slide along the posts it
    /// is plugged into (Y). A selection WITH vertical frames is a structure
    /// and moves around the grid (X/Z), staying on the ground.
    /// </summary>
    bool ComputeSlideMode()
    {
        foreach (SelectableBeam sel in selection.Selected)
        {
            if (sel == null)
                continue;
            Transform root = MarqueeSelectionController.PartRootOf(sel);
            if (root != null && BeamPartUtility.IsVertical(root.name))
                return false;
        }
        return true;
    }

    void UpdateAxisVisibility()
    {
        if (_axes == null)
            return;
        for (int i = 0; i < _axes.Length; i++)
        {
            bool isY = _axes[i].dir == Vector3.up;
            bool wanted = _slideMode ? isY : !isY;
            if (_axes[i].go.activeSelf != wanted)
                _axes[i].go.SetActive(wanted);
        }
    }

    // ------------------------------------------------------------------
    // Dragging
    // ------------------------------------------------------------------

    void BeginDrag(int axis)
    {
        _dragAxis = axis;
        _applied = 0f;
        _slideDeltas.Clear();

        _dragRoots.Clear();
        var seen = new HashSet<Transform>();
        var beamRoots = new List<Transform>();
        float minY = float.MaxValue;
        foreach (SelectableBeam sel in selection.Selected)
        {
            if (sel == null)
                continue;
            Transform partRoot = MarqueeSelectionController.PartRootOf(sel);
            if (partRoot == null || !seen.Add(partRoot))
                continue;
            _dragRoots.Add(partRoot);
            minY = Mathf.Min(minY, PartBoundsMinY(partRoot));
            if (partRoot.GetComponent<PanelInstance>() == null)
                beamRoots.Add(partRoot);
        }

        if (_slideMode)
        {
            // Beams plugged into frames may only stop at valid hole rows.
            if (BeamSlideRules.CollectValidDeltas(beamRoots, _slideDeltas) &&
                _slideDeltas.Count <= 1)
            {
                _dragRoots.Clear();
                _slideDeltas.Clear();
                SelectionStatus.Set(
                    "No other free hole rows on these frames · the beams can't slide.", 4f);
                return; // press stays claimed so nothing else grabs this drag
            }
        }

        // Free-stepping fallback (structures on the grid, unplugged beams):
        // moving down stops at the floor.
        _minSteps = _axes[axis].dir == Vector3.up && minY < float.MaxValue
            ? -Mathf.Max(0, Mathf.FloorToInt((minY + 0.001f) / StepWorld))
            : int.MinValue;

        _startParam = AxisParam(cam.ScreenPointToRay(Input.mousePosition), _root.position, _axes[axis].dir);
        _dragging = true;

        selection.HideActionCard();
        UpdateHighlight(axis);
        SelectionStatus.Set(_slideMode
            ? "Sliding along the frames · snaps to free hole rows, release to place"
            : $"Moving along {_axes[axis].label} · 88 mm steps, release to place");
    }

    void UpdateDrag()
    {
        if (!Input.GetMouseButton(0))
        {
            EndDrag(commit: Mathf.Abs(_applied) > 1e-4f);
            return;
        }

        AxisHandle axis = _axes[_dragAxis];
        float param = AxisParam(cam.ScreenPointToRay(Input.mousePosition), _root.position, axis.dir);
        float travelled = param - _startParam + _applied;

        float target;
        if (_slideDeltas.Count > 0)
        {
            target = NearestSlideDelta(travelled);
        }
        else
        {
            int steps = Mathf.RoundToInt(travelled / StepWorld);
            if (_minSteps != int.MinValue)
                steps = Mathf.Max(steps, _minSteps);
            target = steps * StepWorld;
        }

        if (Mathf.Abs(target - _applied) > 1e-5f)
        {
            Vector3 delta = axis.dir * (target - _applied);
            foreach (Transform partRoot in _dragRoots)
            {
                if (partRoot != null)
                    partRoot.position += delta;
            }
            _root.position += delta;
            _applied = target;
        }

        float mm = _applied / Mathf.Max(NeospaceUnits.UnitsPerMm, 1e-9f);
        SelectionStatus.Set(Mathf.Abs(_applied) > 1e-4f
            ? $"Move {axis.label} {(mm > 0 ? "+" : "")}{mm:0} mm · release to place"
            : (_slideMode
                ? "Sliding along the frames · snaps to free hole rows, release to place"
                : $"Moving along {axis.label} · 88 mm steps, release to place"));
    }

    float NearestSlideDelta(float travelled)
    {
        float best = 0f;
        float bestErr = float.PositiveInfinity;
        foreach (float delta in _slideDeltas)
        {
            float err = Mathf.Abs(delta - travelled);
            if (err < bestErr)
            {
                bestErr = err;
                best = delta;
            }
        }
        return best;
    }

    void EndDrag(bool commit)
    {
        float applied = _applied;
        int axisIndex = _dragAxis;

        _dragging = false;
        _dragAxis = -1;
        _applied = 0f;
        _slideDeltas.Clear();

        if (!commit)
        {
            // Put everything back where it started.
            if (Mathf.Abs(applied) > 1e-4f && axisIndex >= 0)
            {
                Vector3 back = _axes[axisIndex].dir * -applied;
                foreach (Transform partRoot in _dragRoots)
                {
                    if (partRoot != null)
                        partRoot.position += back;
                }
                Physics.SyncTransforms();
            }
            _dragRoots.Clear();

            selection.PruneSelection();
            if (!selection.PlacementArmed && selection.Selected.Count > 0)
                selection.ShowActionCard(Input.mousePosition);
            else
                SelectionStatus.Clear();
            return;
        }

        CommitMove(_axes[axisIndex].dir * applied);
        _dragRoots.Clear();

        selection.PruneSelection();
        if (selection.Selected.Count > 0)
            selection.ShowActionCard(Input.mousePosition);
    }

    // ------------------------------------------------------------------
    // Commit
    // ------------------------------------------------------------------

    /// <summary>
    /// After the transforms have moved: pull moved panels out of their old
    /// slots, verify the destination doesn't clip into foreign structures,
    /// then re-pair beam connections and re-seat the panels. Invalid
    /// destinations revert the whole move.
    /// </summary>
    void CommitMove(Vector3 totalMove)
    {
        // Panels can't live outside a bay — pull them out of their old slots
        // first, remembering where (and how big) they should re-appear. The
        // sheet rectangle matters: if the destination merge divides the bay,
        // the panel must reseat into EVERY sub-slot inside its old footprint.
        var reseat = new List<PanelReseat>();
        foreach (Transform partRoot in _dragRoots)
        {
            if (partRoot == null)
                continue;
            var pi = partRoot.GetComponent<PanelInstance>();
            if (pi == null)
                continue;

            Vector3 scale = pi.transform.lossyScale;
            var entry = new PanelReseat
            {
                Normal = pi.transform.forward,
                Right = pi.transform.right,
                Up = pi.transform.up,
                HalfW = Mathf.Abs(scale.x) * 0.5f,
                HalfH = Mathf.Abs(scale.y) * 0.5f,
                Side = pi.side
            };

            if (panelSlotManager != null &&
                panelSlotManager.TryGetSlot(pi.slotId, out PanelSlotHandle slot) && slot != null)
            {
                entry.Center = slot.center;
                reseat.Add(entry);
                panelSlotManager.RemovePanel(slot, pi.side);
            }
            else
            {
                // The panel's slot id has churned (the per-frame scan drops a
                // bay's slot as soon as its beams start moving, and merges
                // reshape ids too). The panel's own transform still says which
                // bay it belongs to — lift it like a normal panel and re-seat
                // it at the destination instead of destroying it. Positions
                // are stored pre-move because the re-seat adds totalMove.
                entry.Center = pi.transform.position - totalMove;
                reseat.Add(entry);
                Destroy(pi.gameObject);
            }
        }

        Physics.SyncTransforms();

        // Destination check: the same overlap rules as placement, so flush
        // peg-in-hole contacts pass but clipping into another structure fails.
        if (buildController != null)
        {
            foreach (Transform partRoot in _dragRoots)
            {
                if (partRoot == null || partRoot.GetComponent<PanelInstance>() != null)
                    continue;
                if (buildController.MovedPartOverlaps(partRoot.gameObject, out _))
                {
                    RevertMove(totalMove, reseat);
                    return;
                }
            }
        }

        foreach (Transform partRoot in _dragRoots)
        {
            if (partRoot == null || partRoot.GetComponent<PanelInstance>() != null)
                continue;
            var conn = partRoot.GetComponent<BeamConnections>();
            if (conn != null)
                conn.ReleaseAll();
        }

        Physics.SyncTransforms();
        if (panelSlotManager != null)
            panelSlotManager.RebuildConnectionsAndRescanSlots();

        int panelsBack = 0, panelsLost = 0;
        PlacePanelsNear(reseat, totalMove, ref panelsBack, ref panelsLost);

        BuildHistory.NotifyChanged();

        float mm = totalMove.magnitude / Mathf.Max(NeospaceUnits.UnitsPerMm, 1e-9f);
        string msg = $"Moved the selection {mm:0} mm";
        if (panelsBack > 0)
            msg += $" with {panelsBack} panel{(panelsBack == 1 ? "" : "s")}";
        if (panelsLost > 0)
            msg += $" · {panelsLost} panel{(panelsLost == 1 ? " had" : "s had")} no bay there and was removed";
        SelectionStatus.Set(msg + ". Ctrl+Z undoes it.", 5f);
    }

    struct PanelReseat
    {
        public Vector3 Center;      // slot/sheet centre, pre-move
        public Vector3 Normal;
        public Vector3 Right, Up;   // in-plane sheet axes
        public float HalfW, HalfH;  // sheet half extents
        public int Side;
    }

    void RevertMove(Vector3 totalMove, List<PanelReseat> reseat)
    {
        foreach (Transform partRoot in _dragRoots)
        {
            if (partRoot != null && partRoot.GetComponent<PanelInstance>() == null)
                partRoot.position -= totalMove;
        }
        Physics.SyncTransforms();

        // The per-frame scan dropped these bays' slots while the beams were
        // displaced mid-drag; now that everything is back, rescan so the
        // original slots exist again before the panels are re-seated.
        if (panelSlotManager != null)
            panelSlotManager.RebuildConnectionsAndRescanSlots();

        // Panels were already lifted out of their slots — put them back home.
        int back = 0, lost = 0;
        PlacePanelsNear(reseat, Vector3.zero, ref back, ref lost);

        SelectionStatus.Set(
            "Can't move there · it would clash with another structure. Move reverted.", 5f);
    }

    void PlacePanelsNear(List<PanelReseat> reseat,
        Vector3 offset, ref int placed, ref int lost)
    {
        if (panelSlotManager == null)
            return;

        var spawned = new List<GameObject>();
        foreach (PanelReseat p in reseat)
        {
            spawned.Clear();
            int placedHere = StructureClipboard.PlacePanelIntoRect(panelSlotManager,
                p.Center + offset, p.Normal, p.Right, p.Up, p.HalfW, p.HalfH, p.Side, spawned);
            foreach (GameObject go in spawned)
                selection.AddToSelection(go);
            if (placedHere > 0)
                placed += placedHere;
            else
                lost++;
        }
    }

    // ------------------------------------------------------------------
    // Geometry
    // ------------------------------------------------------------------

    Vector3 SelectionCenter()
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (SelectableBeam sel in selection.Selected)
        {
            if (sel == null)
                continue;
            Transform partRoot = MarqueeSelectionController.PartRootOf(sel);
            if (partRoot == null)
                continue;
            sum += MarqueeSelectionController.PartCenter(partRoot);
            count++;
        }
        return count > 0 ? sum / count : Vector3.zero;
    }

    static float PartBoundsMinY(Transform partRoot)
    {
        float min = float.MaxValue;
        foreach (Renderer r in partRoot.GetComponentsInChildren<Renderer>())
            min = Mathf.Min(min, r.bounds.min.y);
        return min == float.MaxValue ? partRoot.position.y : min;
    }

    /// <summary>
    /// Parameter (world units from <paramref name="origin"/>) of the point on
    /// the axis line closest to the mouse ray.
    /// </summary>
    static float AxisParam(Ray ray, Vector3 origin, Vector3 dir)
    {
        Vector3 w = origin - ray.origin;
        float b = Vector3.Dot(dir, ray.direction);
        float d = Vector3.Dot(dir, w);
        float e = Vector3.Dot(ray.direction, w);
        float denom = 1f - b * b;
        if (Mathf.Abs(denom) < 1e-5f)
            return -d; // looking straight down the axis — degenerate, hold position
        return (b * e - d) / denom;
    }

    int PickAxis()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return -1;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        int best = -1;
        float bestDist = float.MaxValue;
        for (int i = 0; i < _axes.Length; i++)
        {
            if (_axes[i].go.activeSelf &&
                _axes[i].collider != null &&
                _axes[i].collider.Raycast(ray, out RaycastHit hit, 1000f) &&
                hit.distance < bestDist)
            {
                bestDist = hit.distance;
                best = i;
            }
        }
        return best;
    }

    // ------------------------------------------------------------------
    // Visuals
    // ------------------------------------------------------------------

    void EnsureGizmo()
    {
        if (_root != null)
            return;

        _root = new GameObject("MoveGizmo").transform;
        _root.gameObject.layer = GizmoLayer;

        _axes = new[]
        {
            BuildAxis(Vector3.right,   new Color(0.86f, 0.26f, 0.22f), "X"),
            BuildAxis(Vector3.up,      new Color(0.28f, 0.70f, 0.30f), "Y (up/down)"),
            BuildAxis(Vector3.forward, new Color(0.20f, 0.45f, 0.95f), "Z"),
        };
    }

    AxisHandle BuildAxis(Vector3 dir, Color color, string label)
    {
        var go = new GameObject("Axis_" + label[0]);
        go.layer = GizmoLayer;
        go.transform.SetParent(_root, false);
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);

        var mat = new Material(Shader.Find("Sprites/Default")) { color = color };
        mat.renderQueue = 3100; // after regular transparents so arrows read on top

        var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(shaft.GetComponent<Collider>());
        shaft.name = "Shaft";
        shaft.layer = GizmoLayer;
        shaft.transform.SetParent(go.transform, false);
        shaft.transform.localScale = new Vector3(ShaftRadius * 2f, ShaftLength * 0.5f, ShaftRadius * 2f);
        shaft.transform.localPosition = new Vector3(0f, ShaftLength * 0.5f, 0f);
        shaft.GetComponent<MeshRenderer>().sharedMaterial = mat;

        var tip = new GameObject("Tip", typeof(MeshFilter), typeof(MeshRenderer));
        tip.layer = GizmoLayer;
        tip.transform.SetParent(go.transform, false);
        tip.transform.localPosition = new Vector3(0f, ShaftLength, 0f);
        tip.transform.localScale = new Vector3(TipRadius, TipLength, TipRadius);
        tip.GetComponent<MeshFilter>().sharedMesh = ConeMesh();
        tip.GetComponent<MeshRenderer>().sharedMaterial = mat;

        var capsule = go.AddComponent<CapsuleCollider>();
        capsule.direction = 1; // local Y
        capsule.radius = PickRadius;
        capsule.height = ShaftLength + TipLength + PickRadius;
        capsule.center = new Vector3(0f, (ShaftLength + TipLength) * 0.5f, 0f);

        return new AxisHandle
        {
            dir = dir, label = label, go = go,
            collider = capsule, material = mat, baseColor = color,
        };
    }

    void UpdateScale()
    {
        float dist = Vector3.Distance(cam.transform.position, _root.position);
        _root.localScale = Vector3.one * Mathf.Clamp(dist * screenScale, 0.35f, 8f);
    }

    void UpdateHighlight(int active)
    {
        for (int i = 0; i < _axes.Length; i++)
        {
            Color c = _axes[i].baseColor;
            if (i == active)
                c = Color.Lerp(c, Color.white, 0.35f);
            else if (_dragging)
                c.a = 0.25f;
            _axes[i].material.color = c;
        }
    }

    void HideGizmo()
    {
        if (_root != null && _root.gameObject.activeSelf)
            _root.gameObject.SetActive(false);
    }

    static Mesh ConeMesh()
    {
        if (_coneMesh != null)
            return _coneMesh;

        const int seg = 20;
        var verts = new List<Vector3> { Vector3.up, Vector3.zero };
        var tris = new List<int>();
        for (int i = 0; i < seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            verts.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
        }
        for (int i = 0; i < seg; i++)
        {
            int cur = 2 + i;
            int next = 2 + (i + 1) % seg;
            tris.AddRange(new[] { 0, next, cur }); // side
            tris.AddRange(new[] { 1, cur, next }); // base
        }

        _coneMesh = new Mesh { name = "MoveGizmoCone" };
        _coneMesh.SetVertices(verts);
        _coneMesh.SetTriangles(tris, 0);
        _coneMesh.RecalculateNormals();
        _coneMesh.RecalculateBounds();
        return _coneMesh;
    }
}
