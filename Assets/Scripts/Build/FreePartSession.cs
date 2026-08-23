using System.Collections.Generic;
using UnityEngine;

public enum FreePartKind { None, Vertical, Horizontal, Twist }

/// <summary>
/// Category part tools for the Parts tab: one tool per part TYPE instead of
/// one button per catalogue size, so nobody needs to know what a "V13" is.
///
///  - Vertical: click open ground (frames stand anywhere on the grid, no
///    bridging rule) or an amber dot (peg) to stack — then a scale appears
///    and EVERY catalogue height is a tick; click when the label shows the
///    size you want. Aiming below a peg hangs the frame instead.
///  - Horizontal / Twist: click a blue ring (free hole on a frame) — then
///    aim along a direction and every catalogue length is a tick; click to
///    place. The far end may land on another frame or hang free.
///
/// All placements run through the strict Expert pipeline (holes/pegs, faces,
/// overlap, occupancy, undo), so the freedom is in the interaction, not in
/// the rules.
/// </summary>
public class FreePartSession : MonoBehaviour
{
    public BuildController buildController;
    public Camera cam;
    public TemplatePreviewGuide guide;
    public TemplateGhostPreview ghosts;
    public TemplateSpawner spawner;

    public FreePartKind ActiveKind { get; private set; }
    public string StatusMessage { get; private set; } = string.Empty;

    /// <summary>True while the tool waits for its first (anchor) click.</summary>
    public bool AwaitingAnchor => ActiveKind != FreePartKind.None && !_hasAnchor;

    // Anchor state
    bool _hasAnchor;
    Vector3 _anchor;                 // ground point (Vertical) / hole axis point (H, T)
    AttachmentPoint _anchorPeg;      // Vertical only: stack anchor
    AttachmentPoint _anchorHole;     // H/T anchor
    float _floorY;                   // floor under the anchor (scale limit for hanging)

    readonly List<Vector3> _stops = new List<Vector3>();
    readonly List<TemplatePartPose> _ghostPoses = new List<TemplatePartPose>();

    // Cached span-ghost pose: the pipeline probe is only re-run when the
    // snapped end actually moves to another stop, not every frame.
    string _spanGhostPartId;
    Vector3 _spanGhostEnd;
    bool _spanGhostValid;
    Vector3 _spanGhostPos;
    Quaternion _spanGhostRot;

    public void SetKind(FreePartKind kind)
    {
        // Picking up a tool drops whatever was selected/copied first.
        if (kind != FreePartKind.None)
            MarqueeSelectionController.CancelPending();

        ActiveKind = kind;
        ClearAnchor();
        HidePreviews();   // no armed tool = no ground dot / line / ghost left behind

        if (kind != FreePartKind.None && buildController != null)
            buildController.SetCurrentPart(null);   // category tools replace per-size arming

        StatusMessage = kind switch
        {
            FreePartKind.Vertical =>
                "Step 1 of 2 · Click the ground where the frame goes · or an amber dot to stack on it.",
            FreePartKind.Horizontal =>
                "Step 1 of 2 · Click a blue ring on a frame · the beam starts there.",
            FreePartKind.Twist =>
                "Step 1 of 2 · Click a blue ring on a frame · the twist beam starts there.",
            _ => string.Empty
        };
    }

    void Update()
    {
        if (ActiveKind == FreePartKind.None)
            return;

        // Another tool took over (e.g. the Panel button arms PANEL).
        if (buildController != null && !string.IsNullOrEmpty(buildController.currentPartId))
        {
            SetKind(FreePartKind.None);
            return;
        }
        if (StructureClipboard.StampingActive || BeamResizeSession.Busy ||
            UIInteractionState.CurrentExperience == UIInteractionState.Experience.Guided)
        {
            HidePreviews();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_hasAnchor)
            {
                ClearAnchor();
                HidePreviews();
                SetKindStatusOnly();
            }
            else
            {
                SetKind(FreePartKind.None);
                StatusMessage = "Tool put away · pick a part type on the left.";
            }
            return;
        }

        if (cam == null || buildController == null)
            return;

        switch (ActiveKind)
        {
            case FreePartKind.Vertical:
                UpdateVertical();
                break;
            case FreePartKind.Horizontal:
            case FreePartKind.Twist:
                UpdateSpan(ActiveKind == FreePartKind.Twist);
                break;
        }
    }

    void OnEnable()
    {
        UIInteractionState.OnExperienceChanged += HandleExperienceChanged;
    }

    void OnDisable()
    {
        UIInteractionState.OnExperienceChanged -= HandleExperienceChanged;
        HidePreviews();
    }

    void HandleExperienceChanged(UIInteractionState.Experience experience)
    {
        // Switching to the Guided Tools tab puts the category tool away.
        if (experience == UIInteractionState.Experience.Guided && ActiveKind != FreePartKind.None)
        {
            SetKind(FreePartKind.None);
            HidePreviews();
        }
    }

    // ------------------------------------------------------------------
    // Vertical: ground (free grid) or peg (stack), then a height scale
    // ------------------------------------------------------------------

    void UpdateVertical()
    {
        if (!_hasAnchor)
        {
            HoverVerticalAnchor();
            return;
        }

        // Height scale. Peg anchors can also scale DOWNWARD to hang.
        float hoverY = HoverHeightY();
        bool hangDown = _anchorPeg != null &&
                        hoverY < _anchor.y - NeospaceUnits.Mm(10f);

        int size;
        Vector3 end;
        int active;
        if (hangDown)
            size = DownwardHeightSnap(hoverY, out end, out active);
        else
        {
            TemplateSnapping.Result snap = TemplateSnapping.Height(_anchor, hoverY);
            size = snap.Size;
            end = snap.End;
            active = snap.ActiveStop;
            _stops.Clear();
            _stops.AddRange(snap.Stops);
        }

        string label = TemplateSnapping.VLabel(size);
        guide.ShowMeasure(_anchor, end, hangDown ? Vector3.down : Vector3.up,
            label, true, _stops, active);
        if (_anchorPeg == null)
            guide.ShowCrosshair(_anchor);   // keep the grid cross while scaling

        // Live ghost for ground placements (pose math matches the spawner).
        if (_anchorPeg == null)
        {
            _ghostPoses.Clear();
            _ghostPoses.Add(new TemplatePartPose("V" + size, _anchor, VRotation(), "free part preview"));
            ghosts.Show(_ghostPoses);
        }
        else
            ghosts.Hide();

        StatusMessage = _anchorPeg != null
            ? $"Step 2 of 2 · {label} · click to place it. Aim UP to stand, DOWN to hang. Esc goes back."
            : $"Step 2 of 2 · {label} · click when the label shows the height you want. Esc goes back.";

        if (LeftClickGesture.ClickReleased && LeftClickGesture.PressClaim == null)
            CommitVertical(size, hangDown);
    }

    void HoverVerticalAnchor()
    {
        ghosts.Hide();

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        // A nearby free peg wins over the ground: stacking anchor.
        AttachmentPoint peg = null;
        if (Physics.Raycast(ray, out RaycastHit probe, 500f,
                buildController.placementRayMask, QueryTriggerInteraction.Ignore))
        {
            peg = AttachmentPointSceneQuery.FindNearestFree(
                probe.point,
                buildController.v3MaxSnapDistanceToPeg,
                AttachmentPoint.PointRole.Peg,
                buildController.ghostLayerMask);
        }

        if (peg != null)
        {
            guide.HideCrosshair();
            guide.ShowPoint(peg.transform.position, "Stack here · click to anchor");
            if (LeftClickGesture.ClickReleased && LeftClickGesture.PressClaim == null)
            {
                _hasAnchor = true;
                _anchorPeg = peg;
                _anchor = peg.transform.position;
                _floorY = FloorYUnder(_anchor);
            }
            return;
        }

        if (!Physics.Raycast(ray, out RaycastHit floorHit, 500f,
                buildController.floorMask, QueryTriggerInteraction.Collide))
        {
            guide.Hide();
            return;
        }

        Vector3 spot = T1PostsPlanner.SnapGround(floorHit.point);
        guide.ShowPoint(spot, "Click to start a frame here");
        guide.ShowCrosshair(spot);   // grid row + column the frame will land on

        if (LeftClickGesture.ClickReleased && LeftClickGesture.PressClaim == null)
        {
            _hasAnchor = true;
            _anchorPeg = null;
            _anchor = spot;
            _floorY = floorHit.point.y;
        }
    }

    void CommitVertical(int size, bool hangDown)
    {
        string partId = "V" + size;

        if (_anchorPeg != null)
        {
            buildController.TryStackVerticalOnPeg(partId, _anchorPeg, hangDown, out string message);
            StatusMessage = $"{message} Click to place another, Esc to stop.";
        }
        else
        {
            var pose = new TemplatePartPose(partId, _anchor, VRotation(), "free part");
            BuildController.BatchPlaceResult batch = spawner.SpawnFrames(new[] { pose });
            StatusMessage = batch.Placed > 0
                ? $"{partId} placed · click to start another, Esc to stop."
                : $"{partId} didn't fit there · try another spot.";
        }

        ClearAnchor();
        HidePreviews();
    }

    /// <summary>Downward height scale for hanging from a peg (stops stay above the floor).</summary>
    int DownwardHeightSnap(float hoverY, out Vector3 end, out int activeStop)
    {
        List<Catalogue.SnapEntry> table = Catalogue.VHeightSnapTable();
        float drop = Mathf.Max(0f, _anchor.y - hoverY);

        _stops.Clear();
        activeStop = -1;
        int size = table[0].Size;
        float bestDiff = float.PositiveInfinity;

        foreach (Catalogue.SnapEntry entry in table)
        {
            float dist = NeospaceUnits.Mm(entry.DistanceMm);
            if (_anchor.y - dist < _floorY - NeospaceUnits.Mm(12f))
                break; // longer sizes would reach below the floor

            _stops.Add(_anchor + Vector3.down * dist);

            float diff = Mathf.Abs(drop - dist);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                activeStop = _stops.Count - 1;
                size = entry.Size;
            }
        }

        if (_stops.Count == 0)
        {
            // No room to hang anything: fall back to the shortest size upward.
            _stops.Add(_anchor + Vector3.up * NeospaceUnits.Mm(table[0].DistanceMm));
            activeStop = 0;
        }

        end = _stops[Mathf.Clamp(activeStop, 0, _stops.Count - 1)];
        return size;
    }

    // ------------------------------------------------------------------
    // Horizontal / Twist: anchor at a hole, then a length scale
    // ------------------------------------------------------------------

    void UpdateSpan(bool twist)
    {
        string noun = twist ? "twist beam" : "beam";

        if (!_hasAnchor)
        {
            ghosts.Hide();
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f,
                    buildController.placementRayMask, QueryTriggerInteraction.Ignore) ||
                !spawner.TrySnapPostHole(hit.point, out AttachmentPoint hole, includeBeamHoles: true))
            {
                guide.Hide();
                if (LeftClickGesture.ClickReleased && LeftClickGesture.PressClaim == null)
                    StatusMessage = "That wasn't a free hole · click one of the blue rings on a frame.";
                return;
            }

            guide.ShowPoint(TemplateSpawner.AxisPoint(hole), $"Click to start the {noun} here");

            if (LeftClickGesture.ClickReleased && LeftClickGesture.PressClaim == null)
            {
                _hasAnchor = true;
                _anchorHole = hole;
                _anchor = TemplateSpawner.AxisPoint(hole);
            }
            return;
        }

        // Length scale along the aimed direction, at the anchor's height.
        Vector3 hover = HoverOnAnchorPlane();
        TemplateSnapping.Result snap = TemplateSnapping.GroundSpan(_anchor, hover);

        if (snap.IsSameSpot)
        {
            ghosts.Hide();
            guide.ShowPoint(_anchor, "Aim away from the frame to pick a length");
            StatusMessage = $"Step 2 of 2 · Aim along a direction · ticks show every {noun} length.";
            return;
        }

        // Live ghost of the exact beam the pipeline would commit here.
        string previewId = (twist ? "T" : "H") + snap.Size;
        if (previewId != _spanGhostPartId ||
            (snap.End - _spanGhostEnd).sqrMagnitude > 1e-8f)
        {
            _spanGhostPartId = previewId;
            _spanGhostEnd = snap.End;
            _spanGhostValid = buildController.TryPreviewConnectorSpan(
                previewId, _anchor, snap.End, out _spanGhostPos, out _spanGhostRot);
        }

        if (_spanGhostValid)
        {
            _ghostPoses.Clear();
            _ghostPoses.Add(new TemplatePartPose(previewId, _spanGhostPos, _spanGhostRot, "free part preview"));
            ghosts.Show(_ghostPoses);
        }
        else
            ghosts.Hide();

        string label = twist ? TwistLabel(snap.Size) : TemplateSnapping.HLabel(snap.Size);
        guide.ShowMeasure(_anchor, snap.End, snap.Axis, label, _spanGhostValid, snap.Stops, snap.ActiveStop);
        StatusMessage = _spanGhostValid
            ? $"Step 2 of 2 · {label} · click to place the {noun}. Esc goes back."
            : $"Step 2 of 2 · {label} doesn't fit here · aim another way or pick a different length.";

        if (LeftClickGesture.ClickReleased && LeftClickGesture.PressClaim == null)
        {
            string partId = (twist ? "T" : "H") + snap.Size;
            bool ok = buildController.TryPlaceConnectorSpan(partId, _anchor, snap.End, out string message);
            if (ok)
            {
                StatusMessage = $"{partId} placed · click a blue ring to start another, Esc to stop.";
                ClearAnchor();
                HidePreviews();
            }
            else
            {
                // Keep the anchor so the user can simply aim elsewhere.
                StatusMessage = $"Couldn't place {partId}: {message}";
            }
        }
    }

    static string TwistLabel(int n) => $"T{n} · {Skeleton.HBodyLength(n):0} mm";

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    void SetKindStatusOnly()
    {
        FreePartKind kind = ActiveKind;
        StatusMessage = kind switch
        {
            FreePartKind.Vertical =>
                "Step 1 of 2 · Click the ground where the frame goes · or an amber dot to stack on it.",
            FreePartKind.Horizontal =>
                "Step 1 of 2 · Click a blue ring on a frame · the beam starts there.",
            FreePartKind.Twist =>
                "Step 1 of 2 · Click a blue ring on a frame · the twist beam starts there.",
            _ => string.Empty
        };
    }

    Quaternion VRotation() => Quaternion.Euler(buildController.v3RotationEuler);

    float HoverHeightY()
    {
        // Vertical plane through the anchor, facing the camera: stable "aim up".
        Vector3 normal = cam.transform.forward;
        normal.y = 0f;
        if (normal.sqrMagnitude < 1e-4f)
            normal = Vector3.forward;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        var plane = new Plane(normal.normalized, _anchor);
        return plane.Raycast(ray, out float t) ? ray.GetPoint(t).y : _anchor.y;
    }

    Vector3 HoverOnAnchorPlane()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        var plane = new Plane(Vector3.up, _anchor);
        return plane.Raycast(ray, out float t) ? ray.GetPoint(t) : _anchor;
    }

    float FloorYUnder(Vector3 point)
    {
        return Physics.Raycast(point + Vector3.up * 0.1f, Vector3.down,
            out RaycastHit hit, 200f, buildController.floorMask, QueryTriggerInteraction.Collide)
            ? hit.point.y
            : 0f;
    }

    void ClearAnchor()
    {
        _hasAnchor = false;
        _anchorPeg = null;
        _anchorHole = null;
        _stops.Clear();
        _spanGhostPartId = null;
        _spanGhostValid = false;
    }

    void HidePreviews()
    {
        if (guide != null)
        {
            guide.Hide();
            guide.HideArea();
        }
        if (ghosts != null)
            ghosts.Hide();
    }
}
