using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Copy buffer + stamp loop for whole structures — beams AND panels — fed by
/// the marquee selection's "Copy" action. The buffered structure follows the
/// cursor as a ghost snapped to the 88 mm grid; every click stamps a copy.
/// Beams that land where an identical beam already stands are skipped by the
/// batch overlap check (shared posts between adjacent copies), and panels are
/// re-placed onto the slots the freshly stamped beams form.
/// </summary>
public class StructureClipboard : MonoBehaviour
{
    public BuildController buildController;
    public TemplateSpawner spawner;
    public PanelSlotManager panelSlotManager;
    public Camera cam;
    public TemplateGhostPreview ghostPreview;

    struct BeamEntry
    {
        public string PartId;
        public Vector3 Offset;      // from the ground anchor (XZ); Y stays absolute
        public Quaternion Rotation;
    }

    struct PanelEntry
    {
        public Vector3 CenterOffset; // panel center relative to the anchor
        public Vector3 Normal;       // world normal of the panel's slot
        public int Side;             // +1 / -1 relative to that normal
        public Vector3 Right, Up;    // in-plane sheet axes (world; stamps never rotate)
        public float HalfW, HalfH;   // sheet half extents along Right / Up
    }

    readonly List<BeamEntry> _beams = new List<BeamEntry>();
    readonly List<PanelEntry> _panels = new List<PanelEntry>();

    // Single-horizontal copies get more freedom than whole structures: the
    // copy can attach to ANY free hole (any post, any height), not just
    // re-stamp at its original level. Set by CopyFromSelection.
    string _spanPartId;
    float _spanLength;   // axis-to-axis world length of that span

    TemplateSession _session;

    /// <summary>
    /// Stamps use the same merge rules as Guided commits (posts landing inside
    /// an existing beam split it into catalogue pieces, invalid splits refuse
    /// the part). Controlled by the TemplateSession flag; on when none exists.
    /// </summary>
    bool MergeEnabled
    {
        get
        {
            if (_session == null)
                _session = FindFirstObjectByType<TemplateSession>();
            return _session == null || _session.enableLongerFrameWins;
        }
    }

    public static StructureClipboard Active { get; private set; }

    /// <summary>True while a copied structure is following the cursor.</summary>
    public bool IsActive => _beams.Count > 0 || _panels.Count > 0;

    /// <summary>Global gate other click consumers check to stay out of the way.</summary>
    public static bool StampingActive => Active != null && Active.IsActive;

    void OnEnable() { Active = this; }

    void OnDisable()
    {
        if (Active == this)
            Active = null;
    }

    /// <summary>"V13(Clone)" → "V13"; null when the name is not a V/H/T part.</summary>
    public static string CleanPartId(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        string id = name.Replace("(Clone)", string.Empty).Trim();
        return BeamPartUtility.IsBeam(id) ? id : null;
    }

    /// <summary>Fill the buffer from selected part roots. Returns parts captured.</summary>
    public int CopyFromSelection(IReadOnlyList<Transform> roots)
    {
        Clear();
        if (roots == null || roots.Count == 0)
            return 0;

        // Anchor: grid-snapped ground point under the selection center. Copies
        // are pure XZ translations of it, so all heights are preserved.
        Bounds bounds = default;
        bool hasBounds = false;
        foreach (Transform root in roots)
        {
            if (root == null) continue;
            Renderer r = root.GetComponentInChildren<Renderer>();
            if (r == null) continue;
            if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
            else bounds.Encapsulate(r.bounds);
        }
        if (!hasBounds)
            return 0;

        Vector3 anchor = T1PostsPlanner.SnapGround(bounds.center);
        anchor.y = 0f;

        foreach (Transform root in roots)
        {
            if (root == null)
                continue;

            var pi = root.GetComponentInChildren<PanelInstance>();
            if (pi != null)
            {
                Vector3 normal = pi.transform.forward; // panels face along their slot normal
                Vector3 scale = pi.transform.lossyScale;
                _panels.Add(new PanelEntry
                {
                    CenterOffset = pi.transform.position - anchor,
                    Normal = normal,
                    Side = pi.side >= 0 ? 1 : -1,
                    Right = pi.transform.right,
                    Up = pi.transform.up,
                    HalfW = Mathf.Abs(scale.x) * 0.5f,
                    HalfH = Mathf.Abs(scale.y) * 0.5f
                });
                continue;
            }

            string id = CleanPartId(root.name);
            if (id == null)
                continue;

            _beams.Add(new BeamEntry
            {
                PartId = id,
                Offset = root.position - anchor,
                Rotation = root.rotation
            });
        }

        // One horizontal beam and nothing else: unlock hole-attach stamping.
        if (_beams.Count == 1 && _panels.Count == 0 &&
            BeamPartUtility.IsHorizontalLike(_beams[0].PartId))
        {
            float lengthMm = SpanLengthMm(_beams[0].PartId);
            if (lengthMm > 0f)
            {
                _spanPartId = _beams[0].PartId;
                _spanLength = NeospaceUnits.Mm(lengthMm);
            }
        }

        if (IsActive)
        {
            SelectionStatus.Set(_spanPartId != null
                ? $"Copied {_spanPartId} · hover a blue ring and click to attach · Esc to finish"
                : $"Copied {_beams.Count + _panels.Count} parts · click to stamp copies · Esc to finish");
        }

        return _beams.Count + _panels.Count;
    }

    /// <summary>Catalogue axis-to-axis span (mm) for an H/T part id, 0 when unknown.</summary>
    static float SpanLengthMm(string partId)
    {
        if (!int.TryParse(partId.Substring(1), out int size))
            return 0f;
        foreach (Catalogue.SnapEntry entry in Catalogue.HSpanSnapTable())
        {
            if (entry.Size == size)
                return entry.DistanceMm;
        }
        return 0f;
    }

    public void Clear()
    {
        _beams.Clear();
        _panels.Clear();
        _spanPartId = null;
        _spanLength = 0f;
        if (ghostPreview != null)
            ghostPreview.Hide();
    }

    void Update()
    {
        if (!IsActive)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Clear();
            SelectionStatus.Set("Copy finished.", 3f);
            return;
        }

        if (cam == null || spawner == null)
            return;

        // A single copied horizontal beam always attaches to holes: the ghost
        // sits on the nearest free hole rather than floating with the cursor.
        if (_spanPartId != null)
        {
            HoleStamp();
            return;
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        LayerMask floorMask = buildController != null ? buildController.floorMask : (LayerMask)0;
        if (!Physics.Raycast(ray, out RaycastHit floorHit, 500f, floorMask, QueryTriggerInteraction.Collide))
        {
            if (ghostPreview != null)
                ghostPreview.Hide();
            return;
        }

        Vector3 target = T1PostsPlanner.SnapGround(floorHit.point);
        target.y = 0f;

        List<TemplatePartPose> poses = BuildBeamPoses(target);
        if (ghostPreview != null)
            ghostPreview.Show(poses);

        if (LeftClickGesture.ClickReleased)
            Stamp(target, poses);
    }

    /// <summary>
    /// Hole-attach stamping for a single copied horizontal beam: the ghost
    /// snaps to the nearest free hole to the cursor (any post or beam, any
    /// height — no snap radius, so it never floats loose), tries the four
    /// cardinal directions through the real placement pipeline, and lets the
    /// hover side steer which valid direction wins.
    /// </summary>
    void HoleStamp()
    {
        if (buildController == null)
            return;

        // Cursor reference: whatever the ray strikes (a frame, the floor), or
        // a point along the ray when it hits nothing at all.
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Vector3 reference;
        if (Physics.Raycast(ray, out RaycastHit hit, 500f,
                buildController.placementRayMask, QueryTriggerInteraction.Ignore))
            reference = hit.point;
        else if (Physics.Raycast(ray, out RaycastHit floorHit, 500f,
                buildController.floorMask, QueryTriggerInteraction.Collide))
            reference = floorHit.point;
        else
            reference = ray.GetPoint(6f);

        if (!spawner.TrySnapPostHole(reference, out AttachmentPoint hole,
                includeBeamHoles: true, maxDistance: float.PositiveInfinity))
        {
            // No free hole anywhere in the scene.
            if (ghostPreview != null)
                ghostPreview.Hide();
            SelectionStatus.Set(
                $"No free hole for {_spanPartId} · place a frame first. Esc to finish.");
            return;
        }

        Vector3 anchor = TemplateSpawner.AxisPoint(hole);

        // Hover point on the hole's horizontal plane: which side of the post
        // the cursor sits on decides the direction the copy extends.
        Vector3 aim = reference;
        var plane = new Plane(Vector3.up, anchor);
        if (plane.Raycast(ray, out float t))
            aim = ray.GetPoint(t);
        Vector3 aimDir = aim - anchor;
        aimDir.y = 0f;

        Vector3 bestEnd = default, bestPos = default;
        Quaternion bestRot = Quaternion.identity;
        float bestScore = float.NegativeInfinity;
        bool found = false;

        Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        foreach (Vector3 dir in dirs)
        {
            Vector3 end = anchor + dir * _spanLength;
            if (!buildController.TryPreviewConnectorSpan(
                    _spanPartId, anchor, end, out Vector3 pos, out Quaternion rot))
                continue;

            float score = Vector3.Dot(dir, aimDir);
            if (!found || score > bestScore)
            {
                found = true;
                bestScore = score;
                bestEnd = end;
                bestPos = pos;
                bestRot = rot;
            }
        }

        if (!found)
        {
            if (ghostPreview != null)
                ghostPreview.Hide();
            SelectionStatus.Set(
                $"{_spanPartId} doesn't fit at that hole · hover another blue ring. Esc to finish.");
            return;
        }

        var poses = new List<TemplatePartPose>
        {
            new TemplatePartPose(_spanPartId, bestPos, bestRot, "clipboard")
        };
        if (ghostPreview != null)
            ghostPreview.Show(poses);

        if (LeftClickGesture.ClickReleased)
        {
            bool ok = buildController.TryPlaceConnectorSpan(_spanPartId, anchor, bestEnd, out string message);
            SelectionStatus.Set(ok
                ? $"{_spanPartId} attached · hover another hole for more copies, Esc to finish."
                : $"Couldn't attach {_spanPartId}: {message}");
        }
    }

    void Stamp(Vector3 target, List<TemplatePartPose> poses)
    {
        var batch = spawner.SpawnFrames(poses, MergeEnabled);

        // The whole stamp was refused (unbuildable merge, rolled back) — no
        // beams went down, so no panels may go down either.
        if (batch.Placed == 0 && batch.Blocked > 0)
        {
            SelectionStatus.Set($"{batch.Message} Shift the stamp and try again, Esc to finish.");
            return;
        }

        // Panels are seated one frame later: a merge stamp splits/destroys
        // beams and slots this frame, and Unity's deferred Destroy means the
        // same-frame slot state can still contain dying slots. Next frame the
        // per-frame scan runs on settled geometry, so the copied panels land
        // on the slots the freshly stamped (and merged) beams really form.
        StartCoroutine(PlacePanelsNextFrame(target, new List<PanelEntry>(_panels), batch));
    }

    System.Collections.IEnumerator PlacePanelsNextFrame(
        Vector3 target, List<PanelEntry> panels, BuildController.BatchPlaceResult batch)
    {
        // Slots settle over a few frames after a merge stamp: Destroy is
        // deferred, the per-frame scan rebuilds composite spans, and blockers
        // of dying slots stop answering queries at different times. One
        // attempt was ~80% reliable, so retry each unplaced panel until the
        // slot state stops changing.
        const int maxAttempts = 8;
        var pending = new List<PanelEntry>(panels);
        int panelsPlaced = 0;

        for (int attempt = 0; attempt < maxAttempts && pending.Count > 0; attempt++)
        {
            yield return null;
            if (panelSlotManager == null)
                break;
            panelSlotManager.RebuildConnectionsAndRescanSlots();

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PanelEntry entry = pending[i];
                int placedHere = PlaceIntoSlotsInRect(entry, target + entry.CenterOffset);
                if (placedHere > 0)
                {
                    panelsPlaced += placedHere;
                    pending.RemoveAt(i);
                }
            }
        }

        int skippedBeams = batch.Requested - batch.Placed - batch.Blocked;
        string beamPart = skippedBeams > 0
            ? $"{batch.Placed}/{batch.Requested} beams ({skippedBeams} shared with existing)"
            : $"{batch.Placed} beams";
        string panelPart = panels.Count > 0 ? $", {panelsPlaced}/{panels.Count} panels" : string.Empty;
        string mergePart = batch.SplitBeams > 0
            ? $" Split {batch.SplitBeams} existing beam{(batch.SplitBeams > 1 ? "s" : "")} to merge."
            : string.Empty;
        string blockPart = batch.Blocked > 0
            ? $" {batch.Blocked} part{(batch.Blocked > 1 ? "s" : "")} refused (no valid split there)."
            : string.Empty;
        SelectionStatus.Set(
            $"Stamped {beamPart}{panelPart}.{mergePart}{blockPart} Click to stamp another, Esc to finish.");
    }

    List<TemplatePartPose> BuildBeamPoses(Vector3 targetAnchor)
    {
        var poses = new List<TemplatePartPose>(_beams.Count);
        for (int i = 0; i < _beams.Count; i++)
        {
            BeamEntry entry = _beams[i];
            poses.Add(new TemplatePartPose(
                entry.PartId, targetAnchor + entry.Offset, entry.Rotation, "clipboard"));
        }
        return poses;
    }

    /// <summary>
    /// Seat a copied panel into EVERY compatible slot inside its transported
    /// rectangle — not just the nearest one. When the stamp merges into an
    /// existing structure, a post can divide the stamped bay in two: the
    /// sub-slot centres sit half a bay away from the original panel centre,
    /// far outside any nearest-point radius, but both lie inside the old
    /// sheet's rectangle. Slots already filled on that side (shared bays)
    /// are skipped, which is exactly the dedup behaviour flush stamps need.
    /// </summary>
    int PlaceIntoSlotsInRect(in PanelEntry entry, Vector3 worldCenter)
    {
        return PlacePanelIntoRect(panelSlotManager, worldCenter, entry.Normal,
            entry.Right, entry.Up, entry.HalfW, entry.HalfH, entry.Side);
    }

    /// <summary>
    /// Seat a panel into every compatible slot whose centre lies inside the
    /// sheet rectangle described by (center, right, up, halfW, halfH). Used by
    /// clipboard stamps and gizmo moves alike. Returns how many panels were
    /// placed; occupied sides (shared bays) are skipped — that is the dedup
    /// flush merges need.
    /// </summary>
    public static int PlacePanelIntoRect(PanelSlotManager manager, Vector3 center, Vector3 normal,
        Vector3 right, Vector3 up, float halfW, float halfH, int side,
        List<GameObject> placedOut = null)
    {
        if (manager == null)
            return 0;

        // Wall sheets hang ~60 mm off the slot plane (outset + half thickness).
        float planeTol = NeospaceUnits.Mm(75f);
        float edgeTol = NeospaceUnits.Mm(12f);
        int placed = 0;

        foreach (PanelSlotHandle slot in FindObjectsByType<PanelSlotHandle>(FindObjectsSortMode.None))
        {
            if (slot == null)
                continue;
            if (Mathf.Abs(Vector3.Dot(slot.normal.normalized, normal.normalized)) < 0.7f)
                continue;

            Vector3 d = slot.center - center;
            if (Mathf.Abs(Vector3.Dot(d, normal)) > planeTol)
                continue;
            if (Mathf.Abs(Vector3.Dot(d, right)) > halfW + edgeTol)
                continue;
            if (Mathf.Abs(Vector3.Dot(d, up)) > halfH + edgeTol)
                continue;

            // A big surrounding bay whose centre happens to fall inside this
            // small sheet is not a match — the slot must fit the sheet.
            float slotMax = Mathf.Max(slot.sizeXY.x, slot.sizeXY.y);
            float sheetMax = Mathf.Max(halfW, halfH) * 2f;
            if (slotMax > sheetMax + NeospaceUnits.Mm(100f))
                continue;

            // Keep the panel on the same world-facing side even if the new
            // slot's normal came out flipped.
            int sideHere = Vector3.Dot(slot.normal, normal) >= 0f ? side : -side;
            if (!manager.CanPlacePanel(slot, sideHere))
                continue;
            GameObject go = manager.PlacePanel(slot, sideHere);
            if (go != null)
            {
                placed++;
                placedOut?.Add(go);
            }
        }
        return placed;
    }

    /// <summary>Nearest live slot to a world point with a compatible plane normal.</summary>
    public static PanelSlotHandle FindSlotNear(Vector3 worldPoint, Vector3 normal)
    {
        PanelSlotHandle best = null;
        float bestDist = NeospaceUnits.ModuleMeters * 0.75f;

        foreach (PanelSlotHandle slot in FindObjectsByType<PanelSlotHandle>(FindObjectsSortMode.None))
        {
            if (slot == null)
                continue;
            if (Mathf.Abs(Vector3.Dot(slot.normal.normalized, normal.normalized)) < 0.7f)
                continue;

            float dist = Vector3.Distance(slot.center, worldPoint);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = slot;
            }
        }
        return best;
    }
}
