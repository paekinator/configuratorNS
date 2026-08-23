using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared pick / preview / cancel state for Guided template tools.</summary>
public class TemplateSession : MonoBehaviour
{
    public Camera cam;
    public LayerMask floorMask;
    public BuildController buildController;
    public TemplateSpawner spawner;

    [Header("Preview")]
    public TemplatePreviewGuide guide;
    public TemplateGhostPreview ghostPreview;

    [Header("Overlap rules")]
    [Tooltip("Rhino merge rules for Guided commits and clipboard stamps: equal/shorter " +
             "coaxial V posts are reused, a longer new post replaces shorter ones, and a " +
             "post landing inside an existing beam splits it into catalogue pieces " +
             "(H15 → 663+41+663) with divided panels reseated per sub-bay. When no valid " +
             "split exists the placement is refused. OFF = legacy physics-only behavior.")]
    public bool enableLongerFrameWins = true;

    public GuidedTemplateTool ActiveTool { get; private set; } = GuidedTemplateTool.None;
    public string StatusMessage { get; private set; } = "Choose a tool.";

    /// <summary>What the next click will mean for the Posts tool.</summary>
    enum PickKind
    {
        GroundFirst,    // free ground point (88 mm grid)
        GroundSpan,     // span locked to the dominant axis from the previous pick
        HeightOnly      // vertical, snapped to V sizes
    }

    readonly List<Vector3> _picks = new List<Vector3>();
    readonly List<AttachmentPoint> _pickedPoints = new List<AttachmentPoint>();

    public IReadOnlyList<Vector3> Picks => _picks;

    void Awake()
    {
        if (cam == null)
            cam = Camera.main;
        if (buildController == null)
            buildController = FindFirstObjectByType<BuildController>();
        if (spawner == null)
            spawner = GetComponent<TemplateSpawner>() ?? gameObject.AddComponent<TemplateSpawner>();
        if (spawner.buildController == null)
            spawner.buildController = buildController;
        if (spawner.panelSlotManager == null && buildController != null)
            spawner.panelSlotManager = buildController.panelSlotManager;
        if (floorMask == 0 && buildController != null)
            floorMask = buildController.floorMask;
        if (guide == null)
            guide = GetComponent<TemplatePreviewGuide>() ?? gameObject.AddComponent<TemplatePreviewGuide>();
        if (ghostPreview == null)
            ghostPreview = GetComponent<TemplateGhostPreview>() ?? gameObject.AddComponent<TemplateGhostPreview>();
        if (ghostPreview.buildController == null)
            ghostPreview.buildController = buildController;

        CalibrateWorldScale();
    }

    /// <summary>
    /// Measure world-units-per-mm from a real beam prefab so all template maths and
    /// visuals match the actual model scale (prefabs are ~1 unit = 100 mm).
    /// </summary>
    void CalibrateWorldScale()
    {
        if (buildController == null || buildController.partDatabase == null)
            return;

        string[] candidates = { "V3", "V5", "V7", "V9", "V13" };
        for (int i = 0; i < candidates.Length; i++)
        {
            GameObject prefab = buildController.partDatabase.GetRealPrefab(candidates[i]);
            if (prefab != null && NeospaceUnits.CalibrateFromPrefab(prefab))
            {
                Debug.Log(
                    $"TemplateSession: world scale calibrated from {candidates[i]} · " +
                    $"1 mm = {NeospaceUnits.UnitsPerMm:0.####} units " +
                    $"(module = {NeospaceUnits.ModuleMeters:0.###} units)");
                return;
            }
        }
    }

    void Update()
    {
        if (UIInteractionState.CurrentExperience != UIInteractionState.Experience.Guided)
            return;

        if (ActiveTool == GuidedTemplateTool.None)
            return;

        // Esc fully disarms the tool: picks are cancelled AND the cursor goes
        // back to a plain pointer with no placement following it.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetTool(GuidedTemplateTool.None);
            StatusMessage = "Tool put away · pick a tool on the left to keep building.";
            return;
        }

        if (EventSystemOverUI())
            return;

        // A copied structure following the cursor (or a beam length scale) owns clicks.
        if (StructureClipboard.StampingActive || BeamResizeSession.Busy)
        {
            guide.Hide();
            if (ghostPreview != null)
                ghostPreview.Hide();
            return;
        }

        if (ActiveTool == GuidedTemplateTool.ConnectorsT2)
        {
            UpdateConnectorPicking();
            return;
        }

        if (ActiveTool == GuidedTemplateTool.PanelBayT3)
        {
            UpdatePanelPicking();
            return;
        }

        PickKind kind = NextPickKind();
        if (!TryComputeHover(kind, out Vector3 world, out TemplateSnapping.Result snap))
        {
            guide.Hide();
            ShowPostGhostsFromPicksOnly();
            return;
        }

        ShowHover(kind, world, snap);
        UpdateFootprintPreview(kind, snap);
        ShowPostGhosts(kind, world, snap);

        // Click acts, drag selects: picks commit on a clean click release so a
        // left-drag can always start the selection marquee instead. A claimed
        // press belongs to another tool (e.g. the panel layer mover).
        if (LeftClickGesture.ClickReleased && LeftClickGesture.PressClaim == null)
            AcceptPick(world);
    }

    /// <summary>
    /// Frames tool: once the base edge exists, outline the ground area the
    /// raised frames will cover — live while aiming the depth, frozen during
    /// the height pick. Outline only: no fill, since no panel goes there.
    /// Skipping the depth (same spot) collapses the area, so it hides and the
    /// user sees they are building a flat row instead.
    /// </summary>
    void UpdateFootprintPreview(PickKind kind, TemplateSnapping.Result snap)
    {
        Vector3 depth = Vector3.zero;
        bool show = false;

        if (kind == PickKind.GroundSpan && _picks.Count == 2 && snap.HasLine && !snap.IsSameSpot)
        {
            depth = snap.End - _picks[1];   // live depth hover
            show = true;
        }
        else if (kind == PickKind.HeightOnly && _picks.Count >= 3)
        {
            depth = _picks[2] - _picks[1];  // depth already committed
            show = true;
        }

        depth.y = 0f;
        if (show && depth.sqrMagnitude > 1e-6f)
            guide.ShowArea(_picks[0], _picks[1], _picks[1] + depth, _picks[0] + depth,
                valid: true, filled: false);
        else
            guide.HideArea();
    }

    PickKind NextPickKind()
    {
        // Posts (T1): base 1 → base 2 → depth → height.
        if (_picks.Count == 0)
            return PickKind.GroundFirst;
        if (_picks.Count == 1)
            return PickKind.GroundSpan;
        if (_picks.Count == 2)
            return HorizontalNear(_picks[0], _picks[1])
                ? PickKind.HeightOnly   // single post: next pick sets height
                : PickKind.GroundSpan;  // depth pick
        return PickKind.HeightOnly;
    }

    /// <summary>
    /// Resolve the hover for the next pick. Ground spans are axis-locked and snapped
    /// to catalogue H spans; heights are cast against a camera-facing vertical plane
    /// (never stolen by the floor) and snapped to catalogue V sizes.
    /// </summary>
    bool TryComputeHover(
        PickKind kind, out Vector3 world, out TemplateSnapping.Result snap)
    {
        world = Vector3.zero;
        snap = default;

        if (cam == null)
            return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (kind == PickKind.GroundFirst)
        {
            if (!Physics.Raycast(ray, out RaycastHit floorHit, 500f, floorMask, QueryTriggerInteraction.Collide))
                return false;
            world = T1PostsPlanner.SnapGround(floorHit.point);
            world.y = floorHit.point.y;
            return true;
        }

        Vector3 anchor = _picks[_picks.Count - 1];

        if (kind == PickKind.GroundSpan)
        {
            // Prefer the real floor; fall back to the anchor's horizontal plane so
            // hovering past the floor edge still previews a span.
            Vector3 hover;
            if (Physics.Raycast(ray, out RaycastHit floorHit, 500f, floorMask, QueryTriggerInteraction.Collide))
                hover = floorHit.point;
            else
                hover = HoverOnHorizontalPlane(anchor);

            snap = TemplateSnapping.GroundSpan(anchor, hover);
            if (!snap.HasLine)
                return false;

            world = snap.End;
            world.y = anchor.y;
            return true;
        }

        // Height: vertical plane through the anchor, facing the camera.
        Vector3 flatCam = cam.transform.forward;
        flatCam.y = 0f;
        if (flatCam.sqrMagnitude < 1e-6f)
            flatCam = Vector3.forward;
        var plane = new Plane(flatCam.normalized, anchor);

        if (!plane.Raycast(ray, out float enter))
            return false;

        Vector3 planePoint = ray.GetPoint(enter);
        Vector3 heightBase = HeightBase();
        snap = TemplateSnapping.Height(heightBase, planePoint.y);
        world = snap.End;
        return true;
    }

    /// <summary>Ground point the vertical height line grows from.</summary>
    Vector3 HeightBase()
    {
        return _picks.Count > 0 ? _picks[_picks.Count - 1] : Vector3.zero;
    }

    void ShowHover(PickKind kind, Vector3 world, TemplateSnapping.Result snap)
    {
        if (kind == PickKind.GroundFirst)
        {
            guide.ShowPoint(world, null);
            return;
        }

        if (!snap.HasLine)
        {
            guide.Hide();
            return;
        }

        if (snap.IsSameSpot)
        {
            guide.ShowPoint(snap.End, snap.Label);
            StatusMessage = "Same spot · click here to skip this step.";
            return;
        }

        Vector3 anchor = snap.Axis == Vector3.up ? HeightBase() : _picks[_picks.Count - 1];
        guide.ShowMeasure(anchor, snap.End, snap.Axis, snap.Label, true, snap.Stops, snap.ActiveStop);

        StatusMessage = snap.Axis == Vector3.up
            ? $"Height {snap.Label} · click to confirm, or aim higher/lower to change it."
            : $"Distance {snap.Label} · click to confirm.";
    }

    /// <summary>
    /// Connectors are strict: every pick must land on a real, free HOLE of a
    /// placed V POST (never a beam, peg or free space). After the first pick,
    /// the preview line runs axis-locked from that point with ticks at every
    /// catalogue H length, exactly like the beams are built — never diagonally.
    /// </summary>
    void UpdateConnectorPicking()
    {
        bool found = TryPickConnectionPoint(out Vector3 snapped, out AttachmentPoint point);

        if (_pickedPoints.Count == 0)
        {
            if (found)
                guide.ShowPoint(snapped, "Frame connection point");
            else
                guide.Hide();
        }
        else
        {
            // Measure axis-to-axis, exactly like the commit does (a hole sits
            // half a profile off its post's axis).
            Vector3 anchor = TemplateSpawner.AxisPoint(_pickedPoints[0]);
            Vector3 hover = found ? snapped : HoverOnHorizontalPlane(anchor);
            TemplateSnapping.Result snap = TemplateSnapping.GroundSpan(anchor, hover);

            bool targetOk = found &&
                point != _pickedPoints[0] &&
                point.transform.root != _pickedPoints[0].transform.root &&
                Mathf.Abs(snapped.y - anchor.y) <= NeospaceUnits.Mm(30f);

            // Same catalogue check the spawner enforces on commit.
            bool spanOk = false;
            string label = snap.IsSameSpot ? "Pick a hole on another frame" : snap.Label;
            Vector3 targetAxis = snapped;
            if (targetOk)
            {
                targetAxis = TemplateSpawner.AxisPoint(point);
                float distMm = Vector3.Distance(anchor, targetAxis) * NeospaceUnits.MetersToMm;
                Catalogue.SnapEntry? entry = Catalogue.SnapToTable(
                    distMm, Catalogue.HSpanSnapTable(), spawner.spanSnapToleranceMm);
                spanOk = entry.HasValue;
                if (spanOk)
                    label = TemplateSnapping.HLabel(entry.Value.Size);
            }

            Vector3 end = targetOk ? targetAxis : snap.End;
            guide.ShowMeasure(anchor, end, snap.Axis, label, spanOk, snap.Stops, snap.ActiveStop);

            StatusMessage = spanOk
                ? $"{label} · click to place the beam."
                : "Step 2 of 2 · Aim at a ring marker on another frame at the SAME height · ticks show valid beam lengths.";
        }

        if (LeftClickGesture.ClickReleased && LeftClickGesture.PressClaim == null)
        {
            if (!found)
            {
                StatusMessage = "That wasn't a free hole · click one of the ring markers on a placed frame.";
                return;
            }
            AcceptConnectorPick(point);
        }
    }

    Vector3 HoverOnHorizontalPlane(Vector3 anchor)
    {
        if (cam == null)
            return anchor;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        var plane = new Plane(Vector3.up, anchor);
        return plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : anchor;
    }

    bool TryPickConnectionPoint(out Vector3 snapped, out AttachmentPoint point)
    {
        snapped = Vector3.zero;
        point = null;
        if (cam == null || buildController == null || spawner == null)
            return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(
                ray, out RaycastHit hit, 500f,
                buildController.placementRayMask, QueryTriggerInteraction.Ignore))
            return false;

        if (!spawner.TrySnapPostHole(hit.point, out point))
            return false;

        snapped = point.transform.position;
        return true;
    }

    void AcceptConnectorPick(AttachmentPoint point)
    {
        if (_pickedPoints.Count == 1 &&
            (_pickedPoints[0] == point || _pickedPoints[0].transform.root == point.transform.root))
        {
            StatusMessage = "Both holes are on the same frame · click a ring marker on a different frame.";
            return;
        }

        _pickedPoints.Add(point);

        if (_pickedPoints.Count < 2)
        {
            StatusMessage = "Step 2 of 2 · Click a ring marker on the other frame at the same height.";
            return;
        }

        spawner.TryPlaceConnectorBetween(_pickedPoints[0], _pickedPoints[1], out string message);
        StatusMessage = message;
        CancelPicks();
    }

    public void SetTool(GuidedTemplateTool tool)
    {
        // Picking up a tool drops whatever was selected/copied first.
        if (tool != GuidedTemplateTool.None)
            MarqueeSelectionController.CancelPending();

        ActiveTool = tool;
        CancelPicks();
        StatusMessage = tool switch
        {
            GuidedTemplateTool.PostsT1 =>
                "Step 1 of 4 · Click the ground where the first frame goes.",
            GuidedTemplateTool.ConnectorsT2 =>
                "Step 1 of 2 · Click a ring marker on a frame · ring markers mark free holes.",
            GuidedTemplateTool.PanelBayT3 =>
                "Step 1 of 3 · Click a ring marker on a frame · ring markers mark free holes.",
            _ => "Pick a tool: Frames raise the structure, Panels fill a bay."
        };
    }

    public void CancelPicks()
    {
        _picks.Clear();
        _pickedPoints.Clear();
        if (guide != null)
            guide.Hide();
        if (ghostPreview != null)
            ghostPreview.Hide();
    }

    public void SoftReset()
    {
        CancelPicks();
        ActiveTool = GuidedTemplateTool.None;
        StatusMessage = "Pick a tool: Frames raise the structure, Panels fill a bay.";
    }

    void AcceptPick(Vector3 world)
    {
        _picks.Add(world);
        if (ActiveTool == GuidedTemplateTool.PostsT1)
            HandleT1Pick();
    }

    void HandleT1Pick()
    {
        // Rhino-style: same-point skips a dimension.
        // 1 base → 2 gap (same=single, next is height) → 3 depth (same=2 posts) → height.
        if (_picks.Count == 1)
        {
            StatusMessage = "Step 2 of 4 · Click where the second frame goes · same spot = just one frame.";
            return;
        }

        bool singlePost = HorizontalNear(_picks[0], _picks[1]);

        if (singlePost)
        {
            if (_picks.Count < 3)
            {
                StatusMessage = "Final step · Aim up, click at the height you want.";
                return;
            }

            CommitT1(
                _picks[0],
                _picks[0],
                null,
                _picks[2].y);
            return;
        }

        if (_picks.Count == 2)
        {
            StatusMessage = "Step 3 of 4 · Click to set the depth · same spot again for a flat row.";
            return;
        }

        bool twoPosts = HorizontalNear(_picks[1], _picks[2]);
        if (twoPosts)
        {
            if (_picks.Count < 4)
            {
                // depth skipped — wait for height as 4th? We already have 3 picks: b1,b2,depth(=b2).
                // Height is next.
                if (_picks.Count == 3)
                {
                    StatusMessage = "Final step · Aim up, click at the height you want.";
                    return;
                }
            }
        }

        if (_picks.Count < 4)
        {
            StatusMessage = "Final step · Aim up, click at the height you want.";
            return;
        }

        CommitT1(
            _picks[0],
            _picks[1],
            twoPosts ? (Vector3?)null : _picks[2],
            _picks[3].y);
    }

    void CommitT1(Vector3 base1, Vector3 base2, Vector3? depth, float topY)
    {
        Quaternion vRot = Quaternion.Euler(buildController != null
            ? buildController.v3RotationEuler
            : new Vector3(90f, 0f, 0f));

        float floorY = base1.y;
        var plan = T1PostsPlanner.PlanFromPicks(base1, base2, depth, topY, floorY, vRot);

        if (!plan.IsValid)
        {
            StatusMessage = "Can't build that layout: " + plan.Message;
            CancelPicks();
            return;
        }

        var batch = spawner.SpawnFrames(plan.Parts, enableLongerFrameWins);
        StatusMessage = $"{plan.Message} · {batch.Message}";
        CancelPicks();
    }

    static bool HorizontalNear(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz) < NeospaceUnits.ModuleMeters * 0.25f;
    }

    // ------------------------------------------------------------------
    // Posts (T1) ghost preview: ghost V posts stand at every planned corner
    // while picking. The plan comes from the same planner the commit uses, so
    // ghost count, position and V size always match the real placement —
    // V1 while laying out the base, growing live during the height pick.
    // ------------------------------------------------------------------

    void ShowPostGhosts(PickKind kind, Vector3 world, TemplateSnapping.Result snap)
    {
        if (ghostPreview == null)
            return;

        float floorY = _picks.Count > 0 ? _picks[0].y : world.y;
        Vector3 base1 = _picks.Count > 0 ? _picks[0] : world;
        Vector3? base2 = null;
        Vector3? depth = null;
        float topY = floorY; // no height chosen yet → shortest post (V1)

        if (kind == PickKind.GroundSpan)
        {
            if (_picks.Count == 1)
            {
                base2 = snap.IsSameSpot ? _picks[0] : world;
            }
            else if (_picks.Count >= 2)
            {
                base2 = _picks[1];
                if (!snap.IsSameSpot)
                    depth = world;
            }
        }
        else if (kind == PickKind.HeightOnly)
        {
            if (_picks.Count >= 2) base2 = _picks[1];
            if (_picks.Count >= 3) depth = _picks[2];
            topY = snap.End.y; // snapped to the hovered V size
        }

        ShowPlannedGhosts(base1, base2, depth, topY, floorY);
    }

    /// <summary>Ghosts for the committed picks only (used when the hover is off-floor).</summary>
    void ShowPostGhostsFromPicksOnly()
    {
        if (ghostPreview == null)
            return;

        if (_picks.Count == 0)
        {
            ghostPreview.Hide();
            return;
        }

        float floorY = _picks[0].y;
        ShowPlannedGhosts(
            _picks[0],
            _picks.Count >= 2 ? _picks[1] : (Vector3?)null,
            _picks.Count >= 3 ? _picks[2] : (Vector3?)null,
            floorY, floorY);
    }

    void ShowPlannedGhosts(Vector3 base1, Vector3? base2, Vector3? depth, float topY, float floorY)
    {
        Quaternion vRot = Quaternion.Euler(buildController != null
            ? buildController.v3RotationEuler
            : new Vector3(90f, 0f, 0f));

        var plan = T1PostsPlanner.PlanFromPicks(base1, base2, depth, topY, floorY, vRot);
        if (plan.IsValid)
            ghostPreview.Show(plan.Parts);
        else
            ghostPreview.Hide();
    }

    // ------------------------------------------------------------------
    // Panel template (T3). Anchors on POST HOLES only, like the Rhino NST3:
    // picks 1–2 span two posts at one height (the base edge). Pick 3 chooses
    // the panel plane — UP/DOWN the post = vertical wall panel (2 beams +
    // panels), SIDEWAYS to a third post = horizontal panel (4-beam ring +
    // panels). Every beam runs through the strict Expert pipeline.
    // ------------------------------------------------------------------

    void UpdatePanelPicking()
    {
        bool found = TryPickConnectionPoint(out Vector3 snapped, out AttachmentPoint point);

        if (_pickedPoints.Count == 0)
        {
            if (found)
                guide.ShowPoint(snapped, "Frame connection point");
            else
                guide.Hide();
        }
        else if (_pickedPoints.Count == 1)
        {
            ShowPanelBaseHover(found, point, snapped);
        }
        else
        {
            ShowPanelDirectionHover(found, point);
        }

        if (LeftClickGesture.ClickReleased && LeftClickGesture.PressClaim == null)
        {
            if (!found)
            {
                StatusMessage = "That wasn't a free hole · click one of the ring markers on a placed frame.";
                return;
            }
            AcceptPanelPick(point);
        }
    }

    void ShowPanelBaseHover(bool found, AttachmentPoint point, Vector3 snapped)
    {
        Vector3 anchor = TemplateSpawner.AxisPoint(_pickedPoints[0]);
        Vector3 hover = found ? snapped : HoverOnHorizontalPlane(anchor);
        TemplateSnapping.Result snap = TemplateSnapping.GroundSpan(anchor, hover);

        int size = 0;
        bool ok = found && TryResolveBase(point, out size, out _);
        string label = snap.IsSameSpot ? "Pick a hole on another frame" : snap.Label;
        Vector3 end = snap.End;
        if (ok)
        {
            label = TemplateSnapping.HLabel(size);
            end = TemplateSpawner.AxisPoint(point);
        }

        guide.ShowMeasure(anchor, end, snap.Axis, label, ok, snap.Stops, snap.ActiveStop);
        StatusMessage = ok
            ? $"{label} · click to set the panel's base edge."
            : "Step 2 of 3 · Click a ring marker on a second frame at the SAME height.";
    }

    void ShowPanelDirectionHover(bool found, AttachmentPoint point)
    {
        if (!found)
        {
            guide.Hide();
            StatusMessage = "Step 3 of 3 · Aim up/down for a standing panel, sideways for a lying one · click.";
            return;
        }

        if (TryResolveVertical(point, out float yLow, out float yHigh,
                out int widthV, out int heightEdge, out string vertReason))
        {
            Vector3 axis = TemplateSpawner.AxisPoint(point);
            guide.ShowMeasure(
                new Vector3(axis.x, yLow, axis.z),
                new Vector3(axis.x, yHigh, axis.z),
                Vector3.up,
                $"Panel {Naming.PanelName(widthV, heightEdge)} · vertical",
                true, null, -1);

            // Tint the wall face the panel will fill, spanning the two anchor
            // frames between the low and high hole rows.
            Vector3 a0 = TemplateSpawner.AxisPoint(_pickedPoints[0]);
            Vector3 a1 = TemplateSpawner.AxisPoint(_pickedPoints[1]);
            guide.ShowArea(
                new Vector3(a0.x, yLow, a0.z),
                new Vector3(a1.x, yLow, a1.z),
                new Vector3(a1.x, yHigh, a1.z),
                new Vector3(a0.x, yHigh, a0.z));

            StatusMessage = "Standing panel · click to build it (adds 2 beams + panels).";
            return;
        }

        if (TryResolveHorizontal(point, out Vector3 r0, out Vector3 r1, out Vector3 r2, out Vector3 r3,
                out int widthH, out int depthSize, out string horizReason))
        {
            Vector3 depthDir = r2 - r1;
            guide.ShowMeasure(r1, r2, depthDir.normalized,
                $"Panel {Naming.PanelName(widthH, depthSize)} · horizontal",
                true, null, -1);

            // Tint the full bay the 4-beam ring will enclose.
            guide.ShowArea(r0, r1, r2, r3);

            StatusMessage = "Lying panel (shelf/floor) · click to build it (adds 4 beams + panels).";
            return;
        }

        Transform root = point.transform.root;
        bool onAnchorPosts = root == _pickedPoints[0].transform.root ||
                             root == _pickedPoints[1].transform.root;
        string reason = onAnchorPosts ? vertReason : horizReason;
        guide.ShowPoint(TemplateSpawner.AxisPoint(point), reason, false);
        guide.HideArea();
        StatusMessage = reason;
    }

    void AcceptPanelPick(AttachmentPoint point)
    {
        if (_pickedPoints.Count == 0)
        {
            _pickedPoints.Add(point);
            StatusMessage = "Step 2 of 3 · Click a ring marker on a second frame at the SAME height.";
            return;
        }

        if (_pickedPoints.Count == 1)
        {
            if (!TryResolveBase(point, out _, out string baseReason))
            {
                StatusMessage = baseReason;
                return;
            }
            _pickedPoints.Add(point);
            StatusMessage = "Step 3 of 3 · Aim up/down for a standing panel, sideways for a lying one · click.";
            return;
        }

        if (TryResolveVertical(point, out float yLow, out float yHigh,
                out int widthV, out int heightEdge, out string vertReason))
        {
            CommitVerticalPanel(yLow, yHigh, widthV, heightEdge);
            return;
        }

        if (TryResolveHorizontal(point, out Vector3 r0, out Vector3 r1, out Vector3 r2, out Vector3 r3,
                out int widthH, out int depthSize, out string horizReason))
        {
            CommitHorizontalPanel(r0, r1, r2, r3, widthH, depthSize);
            return;
        }

        Transform root = point.transform.root;
        bool onAnchorPosts = root == _pickedPoints[0].transform.root ||
                             root == _pickedPoints[1].transform.root;
        StatusMessage = onAnchorPosts ? vertReason : horizReason;
    }

    /// <summary>
    /// Validate a base-edge candidate against the first pick: it must be a hole
    /// on a DIFFERENT post at the SAME height, with an axis-to-axis gap that is
    /// a catalogue connector span.
    /// </summary>
    bool TryResolveBase(AttachmentPoint b, out int widthSize, out string reason)
    {
        widthSize = 0;
        reason = string.Empty;

        AttachmentPoint a = _pickedPoints[0];
        if (b == a || b.transform.root == a.transform.root)
        {
            reason = "Pick a hole on a different frame.";
            return false;
        }

        Vector3 pa = TemplateSpawner.AxisPoint(a);
        Vector3 pb = TemplateSpawner.AxisPoint(b);
        if (Mathf.Abs(pa.y - pb.y) > NeospaceUnits.Mm(30f))
        {
            reason = "Both anchor points must sit at the same height.";
            return false;
        }

        float spanMm = (pb - pa).magnitude * NeospaceUnits.MetersToMm;
        Catalogue.SnapEntry? entry = Catalogue.SnapToTable(
            spanMm, Catalogue.HSpanSnapTable(), spawner.spanSnapToleranceMm);
        if (!entry.HasValue)
        {
            reason = $"Frame gap {spanMm:0} mm is not a catalogue span.";
            return false;
        }

        widthSize = entry.Value.Size;
        return true;
    }

    /// <summary>
    /// Third pick on one of the two anchor posts, a whole number of modules up
    /// or down: a vertical wall panel between the anchor height and that row.
    /// </summary>
    bool TryResolveVertical(
        AttachmentPoint p, out float yLow, out float yHigh,
        out int widthSize, out int heightEdge, out string reason)
    {
        yLow = yHigh = 0f;
        widthSize = heightEdge = 0;
        reason = string.Empty;

        Transform root = p.transform.root;
        if (root != _pickedPoints[0].transform.root &&
            root != _pickedPoints[1].transform.root)
        {
            reason = "Not on the anchored frames.";
            return false;
        }

        if (!TryResolveBase(_pickedPoints[1], out widthSize, out reason))
            return false;

        float y1 = TemplateSpawner.AxisPoint(_pickedPoints[0]).y;
        float y3 = p.transform.position.y;
        float dy = Mathf.Abs(y3 - y1);
        int k = Mathf.RoundToInt(dy / NeospaceUnits.ModuleMeters);

        if (k < 2 || Mathf.Abs(dy - k * NeospaceUnits.ModuleMeters) > NeospaceUnits.Mm(30f))
        {
            reason = "Go further up or down the frame · a panel needs at least two hole rows of height.";
            return false;
        }

        // Panel sizes are unrestricted for now: any width x height combination
        // the beams can span is allowed, even outside the catalogue pairs.
        heightEdge = k - 1;

        yLow = Mathf.Min(y1, y3);
        yHigh = Mathf.Max(y1, y3);
        return true;
    }

    /// <summary>
    /// Third pick on a different post, directly sideways from one end of the
    /// base edge at the same height: a horizontal panel over the rectangle.
    /// Returns the ring corners r0→r1 (width), r1→r2 (depth), r2→r3, r3→r0.
    /// </summary>
    bool TryResolveHorizontal(
        AttachmentPoint p,
        out Vector3 r0, out Vector3 r1, out Vector3 r2, out Vector3 r3,
        out int widthSize, out int depthSize, out string reason)
    {
        r0 = r1 = r2 = r3 = Vector3.zero;
        widthSize = depthSize = 0;
        reason = string.Empty;

        Transform root = p.transform.root;
        if (root == _pickedPoints[0].transform.root ||
            root == _pickedPoints[1].transform.root)
        {
            reason = "Pick a third frame, or go up/down for a vertical panel.";
            return false;
        }

        if (!TryResolveBase(_pickedPoints[1], out widthSize, out reason))
            return false;

        Vector3 pa = TemplateSpawner.AxisPoint(_pickedPoints[0]);
        Vector3 pb = TemplateSpawner.AxisPoint(_pickedPoints[1]);
        Vector3 pc = TemplateSpawner.AxisPoint(p);

        if (Mathf.Abs(pc.y - pa.y) > NeospaceUnits.Mm(30f))
        {
            reason = "The third frame's hole must sit at the same height as the base edge.";
            return false;
        }

        Vector3 dir = pb - pa;
        dir.y = 0f;
        dir.Normalize();

        // The third post must sit directly sideways from one end of the base edge.
        Vector3 nearEnd, farEnd;
        if (Mathf.Abs(Vector3.Dot(pc - pb, dir)) <= NeospaceUnits.Mm(60f))
        {
            nearEnd = pb;
            farEnd = pa;
        }
        else if (Mathf.Abs(Vector3.Dot(pc - pa, dir)) <= NeospaceUnits.Mm(60f))
        {
            nearEnd = pa;
            farEnd = pb;
        }
        else
        {
            reason = "The third frame must sit directly sideways from one end of the base edge.";
            return false;
        }

        Vector3 depthVec = pc - nearEnd;
        depthVec.y = 0f;
        float depthMm = depthVec.magnitude * NeospaceUnits.MetersToMm;
        Catalogue.SnapEntry? entry = Catalogue.SnapToTable(
            depthMm, Catalogue.HSpanSnapTable(), spawner.spanSnapToleranceMm);
        if (!entry.HasValue)
        {
            reason = $"Sideways gap {depthMm:0} mm is not a catalogue span.";
            return false;
        }
        // Panel sizes are unrestricted for now: any width x depth combination
        // the beams can span is allowed, even outside the catalogue pairs.
        depthSize = entry.Value.Size;

        Vector3 fourth = farEnd + depthVec;
        if (!spawner.HasFreePostHoleAt(fourth, pa.y))
        {
            reason = "No frame with a free hole at the fourth corner of the rectangle.";
            return false;
        }

        r0 = farEnd;
        r1 = nearEnd;
        r2 = pc;
        r3 = fourth;
        return true;
    }

    /// <summary>Two beams between the anchor posts (top + bottom), panels between.</summary>
    void CommitVerticalPanel(float yLow, float yHigh, int widthSize, int heightEdge)
    {
        Vector3 pa = TemplateSpawner.AxisPoint(_pickedPoints[0]);
        Vector3 pb = TemplateSpawner.AxisPoint(_pickedPoints[1]);

        int beams = 0;
        string problem = string.Empty;
        if (spawner.TryPlaceConnectorAtHeight(pa, pb, yLow, widthSize, out string lowMsg))
            beams++;
        else
            problem = lowMsg;
        if (spawner.TryPlaceConnectorAtHeight(pa, pb, yHigh, widthSize, out string highMsg))
            beams++;
        else if (string.IsNullOrEmpty(problem))
            problem = highMsg;

        Vector3 center = (pa + pb) * 0.5f;
        center.y = (yLow + yHigh) * 0.5f;
        Vector3 span = pb - pa;
        Vector3 normal = new Vector3(-span.z, 0f, span.x).normalized;
        int panels = spawner.PlacePanelsNear(center, normal, bothSides: true);

        StatusMessage =
            $"Vertical panel {Naming.PanelName(widthSize, heightEdge)}: beams {beams}/2, panels {panels}" +
            (string.IsNullOrEmpty(problem) ? string.Empty : $" ({problem})");
        CancelPicks();
    }

    /// <summary>Four-beam ring over the rectangle at the anchor height, panels both sides.</summary>
    void CommitHorizontalPanel(
        Vector3 r0, Vector3 r1, Vector3 r2, Vector3 r3, int widthSize, int depthSize)
    {
        float y = r0.y;
        int beams = 0;
        string problem = string.Empty;

        Vector3[] ring = { r0, r1, r2, r3 };
        int[] sizes = { widthSize, depthSize, widthSize, depthSize };
        for (int i = 0; i < 4; i++)
        {
            if (spawner.TryPlaceConnectorAtHeight(ring[i], ring[(i + 1) % 4], y, sizes[i], out string msg))
                beams++;
            else if (string.IsNullOrEmpty(problem))
                problem = $"edge {i + 1}: {msg}";
        }

        Vector3 center = (r0 + r1 + r2 + r3) * 0.25f;
        center.y = y;
        int panels = spawner.PlacePanelsNear(center, Vector3.up, bothSides: true);

        StatusMessage =
            $"Horizontal panel {Naming.PanelName(widthSize, depthSize)}: beams {beams}/4, panels {panels}" +
            (string.IsNullOrEmpty(problem) ? string.Empty : $" ({problem})");
        CancelPicks();
    }

    static bool EventSystemOverUI()
    {
        if (UnityEngine.EventSystems.EventSystem.current == null)
            return false;
        return UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    }

    void OnDisable()
    {
        CancelPicks();
    }
}
