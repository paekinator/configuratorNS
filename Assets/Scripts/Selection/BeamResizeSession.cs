using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The "Length" action for a single selected vertical frame: the familiar
/// height scale appears over the post with every catalogue size as a tick —
/// click when the label shows the length you want and the post is swapped in
/// place. The anchored end stays fixed (the base of a standing post, the top
/// of a hanging one) and nothing around it is touched: neighbouring frames,
/// beams and panels keep their exact poses, with connections re-paired by the
/// usual proximity rebuild.
/// </summary>
public class BeamResizeSession : MonoBehaviour
{
    public BuildController buildController;
    public Camera cam;
    public TemplatePreviewGuide guide;
    public TemplateGhostPreview ghosts;
    public PanelSlotManager panelSlotManager;

    Transform _beam;
    string _oldPartId;
    int _oldSize;
    Quaternion _rotation;
    Vector3 _rootPos;        // original root position (pose position convention)
    Vector3 _anchorPoint;    // fixed end of the scale (post axis, bottom or top)
    bool _growUp;            // true = base anchored (standing), false = top anchored (hanging)
    bool _pivotNearBottom;   // which end the prefab pivot sits at
    float _floorY;

    int _blockFrame = -1;
    readonly List<Vector3> _stops = new List<Vector3>();
    readonly List<int> _stopSizes = new List<int>();
    readonly List<TemplatePartPose> _ghostPoses = new List<TemplatePartPose>();

    public static BeamResizeSession Instance { get; private set; }

    void OnEnable() { Instance = this; }

    void OnDisable()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool IsActive => _beam != null;

    /// <summary>
    /// Gate for other click consumers: true while the scale is up, and for one
    /// extra frame after the commit click so it can't double as a pick.
    /// </summary>
    public static bool Busy =>
        Instance != null && (Instance.IsActive || Time.frameCount <= Instance._blockFrame);

    /// <summary>Only single vertical frames can have their length adjusted.</summary>
    public static bool CanResize(Transform root)
    {
        if (root == null)
            return false;
        string id = StructureClipboard.CleanPartId(root.name);
        return id != null && BeamPartUtility.IsVertical(id);
    }

    public void Begin(Transform beamRoot)
    {
        string id = beamRoot != null ? StructureClipboard.CleanPartId(beamRoot.name) : null;
        if (id == null || !BeamPartUtility.IsVertical(id) ||
            !int.TryParse(id.Substring(1), out int size))
            return;
        if (!TryGetWorldBounds(beamRoot, out Bounds bounds))
            return;

        _beam = beamRoot;
        _oldPartId = id;
        _oldSize = size;
        _rotation = beamRoot.rotation;
        _rootPos = beamRoot.position;

        // The prefab pivot sits at a structural point near one end; its offset
        // to that end is size-independent, so a swap only shifts the root when
        // the pivot rides the end that grows.
        float frac = Mathf.Clamp01(
            (beamRoot.position.y - bounds.min.y) / Mathf.Max(bounds.size.y, 1e-4f));
        _pivotNearBottom = frac < 0.5f;

        _growUp = HasSupportBelow(beamRoot, bounds);
        _anchorPoint = new Vector3(bounds.center.x,
            _growUp ? bounds.min.y : bounds.max.y, bounds.center.z);
        _floorY = FloorYUnder(_anchorPoint);

        SelectionStatus.Set(
            $"Adjust length · {_oldPartId} · move up or down the scale, click when the label " +
            "shows the length you want. Esc cancels.");
    }

    void Update()
    {
        if (ReferenceEquals(_beam, null))
            return;

        // The beam vanished under us (undo, delete elsewhere) — stand down.
        if (_beam == null || !_beam.gameObject.activeInHierarchy)
        {
            Cancel();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cancel();
            SelectionStatus.Set("Length unchanged.", 3f);
            return;
        }

        if (cam == null || buildController == null)
            return;

        float hoverY = HoverHeightY();
        int size = SnapSize(hoverY, out Vector3 end, out int active);
        string label = TemplateSnapping.VLabel(size);

        guide.ShowMeasure(_anchorPoint, end, _growUp ? Vector3.up : Vector3.down,
            label, true, _stops, active);

        _ghostPoses.Clear();
        _ghostPoses.Add(new TemplatePartPose(
            "V" + size, PoseFor(size), _rotation, "resize preview"));
        ghosts.Show(_ghostPoses);

        SelectionStatus.Set(size == _oldSize
            ? $"Adjust length · {label} (current) · pick another tick, Esc cancels."
            : $"Adjust length · {label} · click to apply. Esc cancels.");

        if (LeftClickGesture.ClickReleased && LeftClickGesture.PressClaim == null)
        {
            if (size == _oldSize)
            {
                Cancel();
                SelectionStatus.Set("Length unchanged.", 3f);
            }
            else
            {
                StartCoroutine(Commit(size));
            }
        }
    }

    /// <summary>Catalogue height scale along the anchored direction.</summary>
    int SnapSize(float hoverY, out Vector3 end, out int activeStop)
    {
        _stops.Clear();
        _stopSizes.Clear();
        activeStop = -1;
        int size = _oldSize;
        float bestDiff = float.PositiveInfinity;

        foreach (Catalogue.SnapEntry entry in Catalogue.VHeightSnapTable())
        {
            float dist = NeospaceUnits.Mm(entry.DistanceMm);
            Vector3 stop = _anchorPoint + (_growUp ? Vector3.up : Vector3.down) * dist;

            // Hanging posts may not grow through the floor.
            if (!_growUp && stop.y < _floorY - NeospaceUnits.Mm(12f))
                break;

            _stops.Add(stop);
            _stopSizes.Add(entry.Size);

            float diff = Mathf.Abs(hoverY - stop.y);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                activeStop = _stops.Count - 1;
                size = entry.Size;
            }
        }

        if (_stops.Count == 0)
        {
            _stops.Add(_anchorPoint);
            _stopSizes.Add(_oldSize);
            activeStop = 0;
            size = _oldSize;
        }

        end = _stops[Mathf.Clamp(activeStop, 0, _stops.Count - 1)];
        return size;
    }

    /// <summary>
    /// Root position for the swapped size, keeping the anchored end fixed.
    /// Only the length DIFFERENCE moves the root, and only when the pivot
    /// rides the growing end — so the connector offsets cancel out exactly.
    /// </summary>
    Vector3 PoseFor(int size)
    {
        float delta = NeospaceUnits.Mm(
            Skeleton.VSkeletonLength(size) - Skeleton.VSkeletonLength(_oldSize));

        Vector3 pos = _rootPos;
        if (_growUp)
            pos.y += _pivotNearBottom ? 0f : delta;   // base fixed, top moves
        else
            pos.y -= _pivotNearBottom ? delta : 0f;   // top fixed, base moves
        return pos;
    }

    System.Collections.IEnumerator Commit(int newSize)
    {
        Transform beam = _beam;
        string oldId = _oldPartId;
        string newId = "V" + newSize;
        Vector3 newPos = PoseFor(newSize);
        Vector3 oldPos = _rootPos;
        Quaternion rot = _rotation;
        EndSession();

        var conn = beam.GetComponentInParent<BeamConnections>();
        if (conn != null)
            conn.ReleaseAll();
        // Deactivate first: Destroy is deferred and the old post's colliders
        // would otherwise still answer the new post's overlap check.
        beam.gameObject.SetActive(false);
        Destroy(beam.gameObject);

        yield return null;

        var pose = new TemplatePartPose(newId, newPos, rot, "resize");
        BuildController.BatchPlaceResult batch =
            buildController.PlacePartsBatch(new[] { pose }, seatVerticalsOnFloor: false);
        bool ok = batch.Placed > 0;

        if (!ok)
        {
            // The new length collides with something — bring the original back.
            buildController.PlacePartsBatch(
                new[] { new TemplatePartPose(oldId, oldPos, rot, "resize revert") },
                seatVerticalsOnFloor: false);
        }

        if (panelSlotManager != null)
            panelSlotManager.RebuildConnectionsAndRescanSlots();
        BuildHistory.NotifyChanged();

        SelectionStatus.Set(ok
            ? $"Length adjusted: {oldId} → {newId}. Everything around it stayed in place."
            : $"{newId} doesn't fit there · kept {oldId}.", 5f);
    }

    public void Cancel() => EndSession();

    void EndSession()
    {
        _beam = null;
        _blockFrame = Time.frameCount + 1;
        if (guide != null)
        {
            guide.Hide();
            guide.HideArea();
        }
        if (ghosts != null)
            ghosts.Hide();
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    static bool TryGetWorldBounds(Transform root, out Bounds bounds)
    {
        if (root.TryGetComponent(out Collider col))
        {
            bounds = col.bounds;
            return true;
        }
        Renderer r = root.GetComponentInChildren<Renderer>();
        if (r != null)
        {
            bounds = r.bounds;
            foreach (Renderer child in root.GetComponentsInChildren<Renderer>())
                bounds.Encapsulate(child.bounds);
            return true;
        }
        bounds = default;
        return false;
    }

    /// <summary>
    /// Standing posts rest on the floor or on something solid just below;
    /// hanging posts have open air under them.
    /// </summary>
    static bool HasSupportBelow(Transform root, in Bounds bounds)
    {
        Vector3 origin = new Vector3(bounds.center.x, bounds.min.y + 0.01f, bounds.center.z);
        RaycastHit[] hits = Physics.RaycastAll(
            origin, Vector3.down, 0.01f + NeospaceUnits.Mm(40f),
            ~0, QueryTriggerInteraction.Collide);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider != null && hit.collider.transform.root != root)
                return true;
        }
        return false;
    }

    float HoverHeightY()
    {
        // Vertical plane through the anchor, facing the camera: stable "aim up".
        Vector3 normal = cam.transform.forward;
        normal.y = 0f;
        if (normal.sqrMagnitude < 1e-4f)
            normal = Vector3.forward;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        var plane = new Plane(normal.normalized, _anchorPoint);
        return plane.Raycast(ray, out float t) ? ray.GetPoint(t).y : _anchorPoint.y;
    }

    float FloorYUnder(Vector3 point)
    {
        if (buildController != null &&
            Physics.Raycast(point + Vector3.up * 0.1f, Vector3.down,
                out RaycastHit hit, 200f, buildController.floorMask, QueryTriggerInteraction.Collide))
            return hit.point.y;
        return 0f;
    }
}
