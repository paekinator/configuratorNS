using System.Collections.Generic;
using UnityEngine;

/// <summary>Spawns planned template parts through <see cref="BuildController"/> and fills panels.</summary>
public class TemplateSpawner : MonoBehaviour
{
    public BuildController buildController;
    public PanelSlotManager panelSlotManager;

    [Tooltip("Max distance from bay center to accept a detected panel slot. <= 0 = auto (350 mm).")]
    public float panelSlotMatchDistance = -1f;

    [Tooltip("Tolerance (mm) when validating a picked span against catalogue connector sizes.")]
    public float spanSnapToleranceMm = 30f;

    public BuildController.BatchPlaceResult SpawnFrames(IList<TemplatePartPose> parts,
        bool resolveCoaxialOverlap = false)
    {
        if (buildController == null)
        {
            return new BuildController.BatchPlaceResult
            {
                Instances = new List<GameObject>(),
                Message = "Missing BuildController"
            };
        }

        return buildController.PlacePartsBatch(parts, seatVerticalsOnFloor: true,
            resolveCoaxialOverlap: resolveCoaxialOverlap);
    }

    /// <summary>
    /// Snap a world position to the nearest FREE HOLE, using the same radius
    /// as the Expert mouse ghost. By default only holes on V posts qualify —
    /// Guided connector/panel picks may never anchor on beams, pegs or free
    /// space. The category Part tools pass includeBeamHoles: true so beams can
    /// also start from the holes of an existing H/T beam (the strict Expert
    /// pipeline downstream already supports beam hosts).
    /// </summary>
    public bool TrySnapPostHole(Vector3 near, out AttachmentPoint hole, bool includeBeamHoles = false,
        float maxDistance = -1f)
    {
        hole = null;
        if (buildController == null)
            return false;

        float best = maxDistance > 0f ? maxDistance : buildController.h3MaxSnapDistance;
        best *= best;

        List<AttachmentPoint> all = AttachmentPoint.Live;
        for (int i = 0; i < all.Count; i++)
        {
            AttachmentPoint ap = all[i];
            if (ap == null || ap.isOccupied || ap.role != AttachmentPoint.PointRole.Hole)
                continue;

            Transform root = ap.transform.root;
            if (root == null)
                continue;
            if (includeBeamHoles ? !BeamPartUtility.IsBeam(root.name)
                                 : !BeamPartUtility.IsVertical(root.name))
                continue;
            if ((buildController.ghostLayerMask.value & (1 << root.gameObject.layer)) != 0)
                continue;

            float d2 = (ap.transform.position - near).sqrMagnitude;
            if (d2 <= best)
            {
                best = d2;
                hole = ap;
            }
        }
        return hole != null;
    }

    /// <summary>Is there a V post with a free hole at this axis position and height?</summary>
    public bool HasFreePostHoleAt(Vector3 axisPoint, float y)
    {
        float xzTolerance = NeospaceUnits.Mm(90f);
        float yTolerance = NeospaceUnits.Mm(20f);

        List<AttachmentPoint> all = AttachmentPoint.Live;
        for (int i = 0; i < all.Count; i++)
        {
            AttachmentPoint ap = all[i];
            if (ap == null || ap.isOccupied || ap.role != AttachmentPoint.PointRole.Hole)
                continue;

            Transform root = ap.transform.root;
            if (root == null || !BeamPartUtility.IsVertical(root.name))
                continue;

            Vector3 p = ap.transform.position;
            if (Mathf.Abs(p.y - y) > yTolerance)
                continue;

            float dx = p.x - axisPoint.x;
            float dz = p.z - axisPoint.z;
            if (dx * dx + dz * dz <= xzTolerance * xzTolerance)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Strictly place one connector between two picked connection points:
    /// the span must snap to a catalogue H size, and the actual placement runs
    /// through the Expert hole/peg + overlap pipeline (no freehand poses).
    /// </summary>
    public bool TryPlaceConnectorBetween(AttachmentPoint a, AttachmentPoint b, out string message)
    {
        message = string.Empty;
        if (buildController == null)
        {
            message = "Missing BuildController";
            return false;
        }
        if (a == null || b == null)
        {
            message = "Pick two connection points on placed frames";
            return false;
        }
        if (a == b || a.transform.root == b.transform.root)
        {
            message = "Points must be on two different frames";
            return false;
        }

        // Measure axis-to-axis like the Rhino skeleton spans: a hole sits half a
        // profile (20.5 mm) off its post's axis, which would skew the size check.
        Vector3 pa = AxisPoint(a);
        Vector3 pb = AxisPoint(b);

        if (Mathf.Abs(pa.y - pb.y) > NeospaceUnits.Mm(30f))
        {
            message = "Beams are horizontal · pick two points at the same height";
            return false;
        }

        float spanMm = (pb - pa).magnitude * NeospaceUnits.MetersToMm;

        Catalogue.SnapEntry? snap = Catalogue.SnapToTable(
            spanMm, Catalogue.HSpanSnapTable(), spanSnapToleranceMm);
        if (!snap.HasValue)
        {
            message = $"Gap {spanMm:0} mm is not a catalogue span (nearest sizes are fixed 88 mm steps)";
            return false;
        }

        string partId = "H" + snap.Value.Size;
        bool ok = buildController.TryPlaceConnectorSpan(partId, pa, pb, out string pipeMessage);
        message = ok ? $"{partId} placed" : $"{partId} blocked: {pipeMessage}";
        return ok;
    }

    /// <summary>
    /// Picked connection point projected onto its beam's axis: for V posts the
    /// XZ snaps to the post's geometric centre (matching how spans are planned
    /// and how the slot scanner keys corners); other roots keep the point as is.
    /// </summary>
    public static Vector3 AxisPoint(AttachmentPoint ap)
    {
        Vector3 p = ap.transform.position;
        Transform root = ap.transform.root;
        if (root == null)
            return p;

        bool vertical = BeamPartUtility.IsVertical(root.name);
        bool horizontalLike = !vertical && BeamPartUtility.IsHorizontalLike(root.name);
        if (!vertical && !horizontalLike)
            return p;

        Bounds bounds;
        if (root.TryGetComponent(out BoxCollider box))
            bounds = box.bounds;
        else
        {
            Renderer renderer = root.GetComponentInChildren<Renderer>();
            if (renderer == null)
                return p;
            bounds = renderer.bounds;
        }

        if (vertical)
        {
            p.x = bounds.center.x;
            p.z = bounds.center.z;
            return p;
        }

        // Beam host: pull the hole onto the beam's centre line across its
        // width, keeping the along-beam module position, so span measuring
        // starts from the same lattice the catalogue spans are built on.
        if (bounds.size.x >= bounds.size.z)
            p.z = bounds.center.z;
        else
            p.x = bounds.center.x;
        return p;
    }

    /// <summary>
    /// Place a connector between two post axes at a given height row, strictly
    /// through the Expert pipeline (host hole chosen deterministically).
    /// </summary>
    public bool TryPlaceConnectorAtHeight(
        Vector3 cornerA, Vector3 cornerB, float y, int hSize, out string message)
    {
        if (buildController == null)
        {
            message = "Missing BuildController";
            return false;
        }

        Vector3 a = new Vector3(cornerA.x, y, cornerA.z);
        Vector3 b = new Vector3(cornerB.x, y, cornerB.z);
        return buildController.TryPlaceConnectorSpan("H" + hSize, a, b, out message);
    }

    /// <summary>
    /// After frames are placed and slots rescanned, place both panel sides on the nearest matching slot.
    /// </summary>
    public int PlacePanelsNear(Vector3 bayCenter, Vector3 bayNormal, bool bothSides = true)
    {
        if (panelSlotManager == null)
            panelSlotManager = buildController != null ? buildController.panelSlotManager : null;

        if (panelSlotManager == null)
            return 0;

        panelSlotManager.RebuildConnectionsAndRescanSlots();

        PanelSlotHandle best = FindNearestSlot(bayCenter, bayNormal);
        if (best == null)
            return 0;

        // Any size may be placed (design freedom); non-catalogue openings are
        // named with a "(custom)" note by PlacePanel.
        int placed = 0;
        if (panelSlotManager.CanPlacePanel(best, 1))
        {
            if (panelSlotManager.PlacePanel(best, 1) != null)
                placed++;
        }

        if (bothSides && panelSlotManager.CanPlacePanel(best, -1))
        {
            if (panelSlotManager.PlacePanel(best, -1) != null)
                placed++;
        }

        return placed;
    }

    PanelSlotHandle FindNearestSlot(Vector3 bayCenter, Vector3 bayNormal)
    {
        PanelSlotHandle[] slots = FindObjectsByType<PanelSlotHandle>(FindObjectsSortMode.None);
        PanelSlotHandle best = null;
        float bestScore = float.PositiveInfinity;
        float maxDist = panelSlotMatchDistance > 0f ? panelSlotMatchDistance : NeospaceUnits.Mm(350f);

        for (int i = 0; i < slots.Length; i++)
        {
            PanelSlotHandle slot = slots[i];
            if (slot == null)
                continue;

            float dist = Vector3.Distance(slot.center, bayCenter);
            if (dist > maxDist)
                continue;

            float normalAlign = 0f;
            if (bayNormal.sqrMagnitude > 1e-6f && slot.normal.sqrMagnitude > 1e-6f)
                normalAlign = 1f - Mathf.Abs(Vector3.Dot(slot.normal.normalized, bayNormal.normalized));

            float score = dist + normalAlign * 0.1f;
            if (score < bestScore)
            {
                bestScore = score;
                best = slot;
            }
        }

        return best;
    }
}
