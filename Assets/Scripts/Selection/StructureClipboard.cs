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

        /// <summary>
        /// The entry names a slot rather than describing a sheet: seat into
        /// whatever slot sits at this centre, whatever size it is. Set for a
        /// block's panels, which carry a centre and a normal and no extents.
        /// </summary>
        public bool AnySize;
    }

    readonly List<BeamEntry> _beams = new List<BeamEntry>();
    readonly List<PanelEntry> _panels = new List<PanelEntry>();

    // Single-horizontal copies get more freedom than whole structures: the
    // copy can attach to ANY free hole (any post, any height), not just
    // re-stamp at its original level. Set by CopyFromSelection.
    string _spanPartId;
    float _spanLength;   // axis-to-axis world length of that span

    /// <summary>
    /// The block being placed, when the buffer came from one rather than from
    /// a copied selection. Null for a copy. It decides the three things a
    /// block does differently: it may be turned with R, it shows the block's
    /// own complete ghost instead of loose beam ghosts, and it says "placing"
    /// rather than "copied".
    /// </summary>
    string _blockName;

    /// <summary>
    /// The anchor LoadFromModel measured, in the block's own coordinates.
    /// Kept so the offsets can be moved onto the master's pivot once that is
    /// known — see RebaseOntoMasterPivot.
    /// </summary>
    Vector3 _anchorModel;
    bool _rebased;

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

    /// <summary>
    /// Load a saved block's parts to stamp, instead of a copied selection.
    ///
    /// This is how a block is placed in Pro mode, and why Pro does not need
    /// its own placement code at all. What Pro wants from a block — real
    /// parts, merged into the build, still editable afterwards — is exactly
    /// what a clipboard stamp already does: it splits an existing beam that a
    /// stamped post lands inside, refuses a part where no catalogue split
    /// exists, and retries panels while the slots settle. Lite's placement is
    /// the opposite by design: one frozen, sealed clone.
    ///
    /// The anchor is the model's own footprint centre, lattice-snapped, so
    /// the ghost sits under the cursor the way a copied selection does.
    ///
    /// Beams whose part id is not in the registry are skipped and counted:
    /// a code written by a newer version can name parts this build has never
    /// heard of, and stamping the rest is better than refusing the block.
    /// </summary>
    public int LoadFromModel(ConfigurationModel model, string blockName, out int skipped)
    {
        Clear();
        _blockName = blockName;
        skipped = 0;
        if (model == null)
            return 0;

        // Footprint centre in XZ, ground at Y=0 — the same anchor shape
        // CopyFromSelection produces, so BuildBeamPoses needs no special case.
        var min = new Vector3(float.MaxValue, 0f, float.MaxValue);
        var max = new Vector3(float.MinValue, 0f, float.MinValue);
        bool any = false;

        foreach (BeamRecord b in model.Beams)
        {
            Vector3 p = WorldOf(b.XMm, b.YMm, b.ZMm);
            min.x = Mathf.Min(min.x, p.x); min.z = Mathf.Min(min.z, p.z);
            max.x = Mathf.Max(max.x, p.x); max.z = Mathf.Max(max.z, p.z);
            any = true;
        }
        if (!any)
            return 0;

        // Provisional: good enough to build offsets from, and replaced by the
        // master's own pivot as soon as one exists. Only a block with no
        // master to consult keeps it — and then the beam ghosts, built from
        // these same offsets, agree with the stamp anyway.
        Vector3 anchor = T1PostsPlanner.SnapGround((min + max) * 0.5f);
        anchor.y = 0f;
        _anchorModel = anchor;
        _rebased = false;

        foreach (BeamRecord b in model.Beams)
        {
            if (!PartRegistry.TryGetPart(b.PartCode, out string partId, out _))
            {
                skipped++;
                continue;
            }

            _beams.Add(new BeamEntry
            {
                PartId = partId,
                Offset = WorldOf(b.XMm, b.YMm, b.ZMm) - anchor,
                Rotation = PoseQuantizer.IsValidOrientationIndex(b.OrientIndex)
                    ? PoseQuantizer.FromOrientationIndex(b.OrientIndex)
                    : PoseQuantizer.FromEulerDeci(b.EulerXDeci, b.EulerYDeci, b.EulerZDeci),
            });
        }

        foreach (PanelRecord p in model.Panels)
        {
            Vector3 normal = PoseQuantizer.FromSlotAxis(p.Axis);

            // No rectangle at all: the entry names a slot rather than
            // describing a sheet. A copied selection carries the sheet's real
            // extents because a merge may divide the bay it spans and both
            // halves must be filled; a block stores one record per panel with
            // no size, so each seats into the single slot at its own centre —
            // which the rect test's 12 mm tolerance resolves exactly, once
            // the size guard is told not to apply.
            Vector3 right = Vector3.Cross(normal, Vector3.up);
            if (right.sqrMagnitude < 1e-6f)
                right = Vector3.right;   // a floor or ceiling panel
            right.Normalize();

            _panels.Add(new PanelEntry
            {
                CenterOffset = WorldOf(p.XMm, p.YMm, p.ZMm) - anchor,
                Normal = normal,
                Side = p.SideMinus ? -1 : 1,
                Right = right,
                Up = Vector3.Cross(right, normal).normalized,
                HalfW = 0f,
                HalfH = 0f,
                AnySize = true,
            });
        }

        // Never the single-span freedom: that is for one copied horizontal
        // beam, which may re-attach to any free hole at any height. A block is
        // a structure and stamps on the floor where the cursor is.
        //
        // Active is not touched: it is the singleton reference, owned by
        // OnEnable/OnDisable, and what makes a stamp live is IsActive — which
        // is now true because there are entries.
        _spanPartId = null;
        return _beams.Count + _panels.Count;
    }

    static Vector3 WorldOf(int xMm, int yMm, int zMm) =>
        new Vector3(NeospaceUnits.Mm(xMm), NeospaceUnits.Mm(yMm), NeospaceUnits.Mm(zMm));

    public void Clear()
    {
        _beams.Clear();
        _panels.Clear();
        _spanPartId = null;
        _spanLength = 0f;
        _blockName = null;
        _stampYaw = 0f;
        _anchorModel = Vector3.zero;
        _rebased = false;
        if (ghostPreview != null)
            ghostPreview.Hide();
        ClearBlockGhost();
    }

    void Update()
    {
        if (!IsActive)
            return;

        // Esc only. NOT right-click: the right button orbits the camera in
        // every scheme this app has (CadCameraController, FlyCameraController,
        // OrbitCamera all read GetMouseButton(1)), so cancelling on it would
        // throw the placement away the moment you looked around to decide
        // where to put it.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            bool block = _blockName != null;
            Clear();
            SelectionStatus.Set(block ? "Placement finished." : "Copy finished.", 3f);
            return;
        }

        // R turns the stamp a quarter at a time, as Lite turns a block. Only
        // for a block: a copied selection is a piece of an existing build and
        // is stamped square with it.
        if (_blockName != null && Input.GetKeyDown(KeyCode.R))
            _stampYaw = Mathf.Repeat(_stampYaw + 90f, 360f);

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

        // A block shows its own complete ghost once the master is ready; the
        // loose beam ghosts stand in until then, and are the only preview a
        // copied selection ever has.
        bool blockGhostReady = _blockGhost != null;
        if (ghostPreview != null)
        {
            if (blockGhostReady)
                ghostPreview.Hide();
            else
                ghostPreview.Show(poses);
        }
        MoveBlockGhost(target);

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
        StartCoroutine(PlacePanelsNextFrame(target, new List<PanelEntry>(_panels), batch, StampTurn));
    }

    System.Collections.IEnumerator PlacePanelsNextFrame(
        Vector3 target, List<PanelEntry> panels, BuildController.BatchPlaceResult batch,
        Quaternion turn)
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
                int placedHere = PlaceIntoSlotsInRect(entry, target + turn * entry.CenterOffset, turn);
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

    /// <summary>
    /// The stamp's turn about its own anchor, in quarters. Zero for a copied
    /// selection — "stamps never rotate" was true while the only stamp was a
    /// piece of an existing build, square with what it came from. A block is
    /// a thing you are placing, and turning it is the first thing you try.
    /// </summary>
    float _stampYaw;

    Quaternion StampTurn => Quaternion.Euler(0f, _stampYaw, 0f);

    // ------------------------------------------------------------------
    // The block ghost
    //
    // TemplateGhostPreview shows one ghost per BEAM, rented from the part
    // prefabs by id. Panels are not parts and have no prefab to rent — they
    // are sheets the slot manager generates — so a block previewed that way
    // came up as a bare frame with nothing in it, which is not what you are
    // about to place.
    //
    // PieceInstanceFactory already builds the whole block, panels included,
    // and already knows how to tint one as a ghost: it is what Lite shows
    // while placing. Using the same thing here means the preview matches the
    // result in BOTH modes, and by construction rather than by two pieces of
    // code agreeing.
    // ------------------------------------------------------------------

    GameObject _blockGhost;

    void ClearBlockGhost()
    {
        if (_blockGhost != null)
            Destroy(_blockGhost);
        _blockGhost = null;
    }

    /// <summary>
    /// Ask for the block's ghost. The master is built off-camera and cached,
    /// so the first request for a block costs a frame or two and later ones
    /// are free; until it arrives the beam ghosts stand in rather than
    /// nothing at all.
    /// </summary>
    public void ShowBlockGhost(string blockId, string code) => RequestBlockGhost(blockId, code);

    /// <summary>Index of the ghost layer, or -1 when it is not configured.</summary>
    int GhostLayerIndex()
    {
        int mask = buildController != null ? buildController.ghostLayerMask.value : 0;
        for (int i = 0; i < 32; i++)
            if ((mask & (1 << i)) != 0)
                return i;
        return -1;
    }

    static void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null || layer < 0)
            return;

        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
    }

    void RequestBlockGhost(string blockId, string code)
    {
        var factory = FindFirstObjectByType<PieceInstanceFactory>();
        if (factory == null)
            return;

        factory.GetMaster(blockId, code, master =>
        {
            // The stamp may have been put down while the master was building.
            if (master == null || _blockName == null)
                return;

            RebaseOntoMasterPivot(master);

            ClearBlockGhost();
            _blockGhost = factory.CreateGhost(master);
            if (_blockGhost != null)
            {
                // Onto the GHOST LAYER, like every other preview in the app.
                // It was not, and that is a quieter fault than it looks: the
                // ghost layer is how anything else in the scene recognises a
                // preview as a preview rather than as built structure. The
                // ground grid finds what is about to be placed by that layer,
                // so a block was the one thing that could be placed without
                // the ground ever lighting up for it.
                SetLayerRecursively(_blockGhost.transform, GhostLayerIndex());
                _blockGhost.SetActive(false);
            }
        });
    }

    /// <summary>
    /// Move every offset onto the MASTER's pivot, so the ghost and the parts
    /// are measured from the same point.
    ///
    /// They were not. LoadFromModel takes the midpoint of the beam ROOT
    /// positions and snaps it; the master takes its renderer-bounds centre
    /// and snaps that onto the block's own post-centre lattice. Those are two
    /// different points — a part's root is a pivot, not the middle of its
    /// geometry, and a horizontal beam's root sits at one end — so the
    /// stamped block landed a constant distance from its preview.
    ///
    /// The master's rule cannot be reproduced from the code alone: it needs
    /// renderer bounds and a post's position. So the factory records where
    /// its pivot fell, and this shifts the offsets to match rather than
    /// guessing at the same answer a second time.
    ///
    /// Idempotent: the master is cached, so the same one arrives again on the
    /// next placement of the same block.
    /// </summary>
    void RebaseOntoMasterPivot(GameObject master)
    {
        var info = master.GetComponent<PieceMasterInfo>();
        if (info == null || _rebased)
            return;

        Vector3 delta = _anchorModel - info.ModelPivot;
        if (delta.sqrMagnitude < 1e-8f)
        {
            _rebased = true;
            return;
        }

        for (int i = 0; i < _beams.Count; i++)
        {
            BeamEntry entry = _beams[i];
            entry.Offset += delta;
            _beams[i] = entry;
        }

        for (int i = 0; i < _panels.Count; i++)
        {
            PanelEntry entry = _panels[i];
            entry.CenterOffset += delta;
            _panels[i] = entry;
        }

        _rebased = true;
    }

    /// <summary>
    /// Put the ghost where the stamp would land — the same point, now that
    /// the offsets have been re-based onto the master's own pivot.
    /// </summary>
    void MoveBlockGhost(Vector3 target)
    {
        if (_blockGhost == null)
            return;

        if (!_blockGhost.activeSelf)
            _blockGhost.SetActive(true);
        _blockGhost.transform.SetPositionAndRotation(target, StampTurn);
    }

    List<TemplatePartPose> BuildBeamPoses(Vector3 targetAnchor)
    {
        Quaternion turn = StampTurn;
        var poses = new List<TemplatePartPose>(_beams.Count);
        for (int i = 0; i < _beams.Count; i++)
        {
            BeamEntry entry = _beams[i];

            // The offset turns about the anchor and the part turns with it,
            // so the block pivots in place rather than swinging around it.
            poses.Add(new TemplatePartPose(
                entry.PartId,
                targetAnchor + turn * entry.Offset,
                turn * entry.Rotation,
                "clipboard"));
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
    /// <summary>
    /// The sheet's plane turns with the stamp: normal and both in-plane axes
    /// go through the same rotation the beams did, or a turned block would
    /// look for its panels on the plane they used to be in.
    /// </summary>
    int PlaceIntoSlotsInRect(in PanelEntry entry, Vector3 worldCenter, Quaternion turn)
    {
        return PlacePanelIntoRect(panelSlotManager, worldCenter, turn * entry.Normal,
            turn * entry.Right, turn * entry.Up, entry.HalfW, entry.HalfH, entry.Side,
            placedOut: null, anySize: entry.AnySize);
    }

    /// <summary>
    /// Seat a panel into every compatible slot whose centre lies inside the
    /// sheet rectangle described by (center, right, up, halfW, halfH). Used by
    /// clipboard stamps and gizmo moves alike. Returns how many panels were
    /// placed; occupied sides (shared bays) are skipped — that is the dedup
    /// flush merges need.
    /// </summary>
    /// <param name="anySize">
    /// Skip the "the slot must fit the sheet" guard below. That guard exists
    /// for a COPIED sheet, which knows its own extents and must not match a
    /// bigger surrounding bay. A block's panel record carries no size — it
    /// names a centre and a normal — and the slot at that centre is by
    /// definition the right one, because the block's own frames were stamped
    /// there and made it. Without this, every block placed as parts arrived
    /// with its frames and none of its panels: a zero-size sheet made every
    /// real bay "too big".
    /// </param>
    public static int PlacePanelIntoRect(PanelSlotManager manager, Vector3 center, Vector3 normal,
        Vector3 right, Vector3 up, float halfW, float halfH, int side,
        List<GameObject> placedOut = null, bool anySize = false)
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
            if (!anySize)
            {
                float slotMax = Mathf.Max(slot.sizeXY.x, slot.sizeXY.y);
                float sheetMax = Mathf.Max(halfW, halfH) * 2f;
                if (slotMax > sheetMax + NeospaceUnits.Mm(100f))
                    continue;
            }

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
