using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thin batch-place API for Guided templates. Does not alter Expert ghost placement.
/// </summary>
public partial class BuildController
{
    public struct BatchPlaceResult
    {
        public int Requested;
        public int Placed;
        public List<GameObject> Instances;
        public string Message;

        /// <summary>Parts not instantiated because an existing coaxial frame already covers them.</summary>
        public int Reused;

        /// <summary>Parts refused because of an unresolvable partial coaxial overlap.</summary>
        public int Blocked;

        /// <summary>Existing shorter coaxial frames removed by longer-wins replacement.</summary>
        public int Replaced;

        /// <summary>Existing beams split into catalogue pieces around new posts.</summary>
        public int SplitBeams;
    }

    /// <summary>
    /// Instantiate catalogue parts at absolute poses. Verticals are seated onto the floor
    /// using the same bounds logic as first placement. Occupancy is repaired via panel rescan.
    /// Pass validateOverlap: false when replaying a known-good scene state (undo/redo):
    /// those poses were already validated when first placed, and re-checking them without
    /// their original host exceptions would wrongly reject plugged-in beams.
    /// Pass resolveCoaxialOverlap: true (Guided longer-wins flag) to apply the Rhino
    /// coaxial rules to V posts before the physics check: equal/shorter spans reuse the
    /// existing frame, a longer new frame replaces the shorter ones it covers, and
    /// partial overlaps are refused. Default false = behavior unchanged.
    /// </summary>
    public BatchPlaceResult PlacePartsBatch(IList<TemplatePartPose> parts, bool seatVerticalsOnFloor = true,
        bool validateOverlap = true, bool resolveCoaxialOverlap = false)
    {
        var result = new BatchPlaceResult
        {
            Instances = new List<GameObject>(),
            Requested = parts != null ? parts.Count : 0
        };

        if (parts == null || parts.Count == 0)
        {
            result.Message = "No parts";
            return result;
        }

        if (partDatabase == null)
        {
            result.Message = "Missing PartDatabase";
            return result;
        }

        // Merge state (longer-wins flag only): posts that cross existing beams
        // are parked until the whole batch is known, so multiple posts cutting
        // the same beam resolve as ONE joint split — and one invalid split
        // refuses the whole merge instead of leaving it half done.
        List<(GameObject post, StructureMergeResolver.CrossingScan scan)> parkedPosts = null;
        List<StructureMergeResolver.PanelSnapshot> panelSnapshots =
            resolveCoaxialOverlap ? StructureMergeResolver.SnapshotPanels() : null;
        string blockNote = null;

        // Existing parts superseded by the merge are only DEACTIVATED during
        // the batch and destroyed on success — so an unbuildable merge can be
        // rolled back whole ("no valid split → the object may not be placed
        // there"), never leaving a half-merged wreck.
        List<GameObject> journalRemoved = resolveCoaxialOverlap ? new List<GameObject>() : null;
        bool abortMerge = false;

        if (resolveCoaxialOverlap)
        {
            // Posts first: when a beam is examined for coaxial merging, every
            // post of this batch is then already in the scene (placed or
            // parked), so the line rebuild sees all its junctions.
            var ordered = new List<TemplatePartPose>(parts.Count);
            foreach (TemplatePartPose p in parts)
                if (!string.IsNullOrWhiteSpace(p.PartId) && BeamPartUtility.IsVertical(p.PartId.Trim()))
                    ordered.Add(p);
            foreach (TemplatePartPose p in parts)
                if (string.IsNullOrWhiteSpace(p.PartId) || !BeamPartUtility.IsVertical(p.PartId.Trim()))
                    ordered.Add(p);
            parts = ordered;
        }

        for (int i = 0; i < parts.Count && !abortMerge; i++)
        {
            TemplatePartPose pose = parts[i];
            if (string.IsNullOrWhiteSpace(pose.PartId))
                continue;

            string partId = pose.PartId.Trim();
            GameObject prefab = partDatabase.GetRealPrefab(partId);
            if (prefab == null)
            {
                if (debugLogs)
                    Debug.LogWarning($"Template batch: no prefab for {partId}");
                continue;
            }

            Vector3 position = pose.Position;
            Quaternion rotation = pose.Rotation;

            GameObject instance = Instantiate(prefab, position, rotation);

            if (seatVerticalsOnFloor && BeamPartUtility.IsVertical(partId))
                SeatInstanceOnFloor(instance, ref position, rotation);

            // Rhino longer-wins coaxial rules (Guided flag only). Runs before the
            // physics check so a replaced shorter post no longer collides with its
            // longer successor; non-coaxial clipping still falls through to physics.
            if (resolveCoaxialOverlap && BeamPartUtility.IsVertical(partId))
            {
                FrameOverlapResolver.Decision decision =
                    FrameOverlapResolver.ResolveVertical(instance, ghostLayerMask.value);

                if (decision.Mode == Overlap.Mode.Skip)
                {
                    // Deactivate first: Destroy is deferred and the colliders would
                    // otherwise still answer physics queries for the rest of the batch.
                    instance.SetActive(false);
                    Destroy(instance);
                    result.Reused++;
                    continue;
                }
                if (decision.Mode == Overlap.Mode.Block)
                {
                    instance.SetActive(false);
                    Destroy(instance);
                    result.Blocked++;
                    blockNote ??= decision.Note;
                    abortMerge = true;
                    if (debugLogs)
                        Debug.Log($"Template batch blocked {partId}: {decision.Note}");
                    continue;
                }
                result.Replaced += FrameOverlapResolver.ApplyDeletes(decision, journalRemoved);

                // Does this post cross the interior of existing beams? Park it:
                // the beams get split (or the merge refused) after the loop.
                StructureMergeResolver.CrossingScan scan =
                    StructureMergeResolver.FindCrossings(instance, ghostLayerMask.value);
                if (scan.Blocked)
                {
                    instance.SetActive(false);
                    Destroy(instance);
                    result.Blocked++;
                    blockNote ??= scan.Note;
                    abortMerge = true;
                    if (debugLogs)
                        Debug.Log($"Template batch blocked {partId}: {scan.Note}");
                    continue;
                }
                if (scan.Crossings.Count > 0)
                {
                    parkedPosts ??= new List<(GameObject, StructureMergeResolver.CrossingScan)>();
                    parkedPosts.Add((instance, scan));
                    instance.SetActive(false); // hidden until the merge resolves
                    continue;
                }
            }

            // The other half of a two-way merge: a NEW beam whose interior
            // lands on EXISTING posts can never be placed whole — without
            // this it would just fail the physics gate below and vanish,
            // leaving a hole in the stamped structure. Replace it with
            // catalogue pieces meeting those posts (or refuse the pose when
            // no catalogue split exists), mirroring how existing beams are
            // split around new posts.
            if (resolveCoaxialOverlap && BeamPartUtility.IsHorizontalLike(partId))
            {
                // Coaxial partial overlap: this beam's span and existing beams
                // on the same line form ONE physical run — rebuild the whole
                // run as catalogue pieces meeting the posts on it, or refuse
                // the merge when a needed piece size isn't made. Parked posts
                // are inactive, so their measured records are passed along.
                List<FrameOverlapResolver.FrameRecord> parkedRecords = null;
                if (parkedPosts != null)
                {
                    parkedRecords = new List<FrameOverlapResolver.FrameRecord>(parkedPosts.Count);
                    foreach ((GameObject _, StructureMergeResolver.CrossingScan s) in parkedPosts)
                        parkedRecords.Add(s.Post);
                }

                StructureMergeResolver.LineMergePlan lineMerge =
                    StructureMergeResolver.PlanLineMerge(instance, ghostLayerMask.value, parkedRecords);
                if (lineMerge.Error != null)
                {
                    instance.SetActive(false);
                    Destroy(instance);
                    result.Blocked++;
                    blockNote ??= lineMerge.Error;
                    abortMerge = true;
                    if (debugLogs)
                        Debug.Log($"Template batch blocked {partId}: {lineMerge.Error}");
                    continue;
                }
                if (lineMerge.CoveredByExisting)
                {
                    // An identical or longer same-family beam already occupies
                    // this span — longer wins, keep the existing one.
                    instance.SetActive(false);
                    Destroy(instance);
                    result.Reused++;
                    if (debugLogs)
                        Debug.Log($"Template batch reused existing beam covering {partId}");
                    continue;
                }
                if (lineMerge.Applies)
                {
                    foreach (Transform old in lineMerge.RemoveBeams)
                    {
                        if (old == null)
                            continue;
                        old.gameObject.SetActive(false);
                        journalRemoved.Add(old.gameObject);
                    }
                    Physics.SyncTransforms();

                    instance.SetActive(false);
                    Destroy(instance);
                    int placedRun = PlaceBeamSegments(lineMerge.Template, lineMerge.Segments,
                        rotation, validateOverlap, ref result, ref blockNote);
                    if (placedRun < lineMerge.Segments.Count)
                    {
                        abortMerge = true;
                        blockNote ??= "a merged piece can't be placed there";
                    }
                    else
                    {
                        result.SplitBeams++;
                    }
                    continue;
                }

                StructureMergeResolver.SelfSplitScan scan =
                    StructureMergeResolver.FindPostCrossingsOnBeam(instance, ghostLayerMask.value);
                if (scan.Blocked)
                {
                    instance.SetActive(false);
                    Destroy(instance);
                    result.Blocked++;
                    blockNote ??= scan.Note;
                    abortMerge = true;
                    if (debugLogs)
                        Debug.Log($"Template batch blocked {partId}: {scan.Note}");
                    continue;
                }
                if (scan.Cuts.Count > 0)
                {
                    List<BeamSplit.Segment> segments = BeamSplit.Plan(scan.Beam.Size, scan.Cuts);
                    if (segments == null)
                    {
                        instance.SetActive(false);
                        Destroy(instance);
                        result.Blocked++;
                        blockNote ??= $"{partId} can't be split into available lengths there";
                        abortMerge = true;
                        if (debugLogs)
                            Debug.Log($"Template batch blocked {partId}: no catalogue split");
                        continue;
                    }

                    instance.SetActive(false);
                    Destroy(instance);
                    int placedPieces = PlaceBeamSegments(scan.Beam, segments, rotation,
                        validateOverlap, ref result, ref blockNote);
                    if (placedPieces < segments.Count)
                    {
                        abortMerge = true;
                        blockNote ??= "a split piece can't be placed there";
                    }
                    else
                    {
                        result.SplitBeams++;
                    }
                    continue;
                }
            }

            if (validateOverlap && HasIllegalOverlap(instance, null, null, out string overlapReason))
            {
                if (debugLogs)
                    Debug.Log($"Template batch blocked {partId}: {overlapReason}");
                Destroy(instance);
                continue;
            }

            if (instance.GetComponent<BeamConnections>() == null)
                instance.AddComponent<BeamConnections>();

            result.Instances.Add(instance);
            result.Placed++;
        }

        if (!abortMerge && parkedPosts != null &&
            !ResolveParkedMerges(parkedPosts, panelSnapshots, journalRemoved,
                ref result, validateOverlap, ref blockNote))
            abortMerge = true;

        if (abortMerge)
            return AbortMergedBatch(result, parkedPosts, journalRemoved, blockNote);

        // Merge committed — the superseded originals really go now.
        if (journalRemoved != null)
        {
            foreach (GameObject go in journalRemoved)
                if (go != null)
                    Destroy(go);
        }

        if (panelSlotManager != null)
            panelSlotManager.RebuildConnectionsAndRescanSlots();

        // Divided-bay panels are re-seated one frame later: this frame's slot
        // state still coexists with objects whose Destroy is deferred, so the
        // sub-slots created by the split are only reliably queryable after
        // the next per-frame scan runs on settled geometry.
        if (panelSnapshots != null && panelSlotManager != null && result.SplitBeams > 0)
            StartCoroutine(RefillPanelsNextFrame(panelSnapshots));

        if (result.Placed > 0 || result.Replaced > 0 || result.SplitBeams > 0)
            BuildHistory.NotifyChanged();

        result.Message = $"Placed {result.Placed}/{result.Requested}";
        if (result.Reused > 0)
            result.Message += $", reused {result.Reused} existing";
        if (result.Replaced > 0)
            result.Message += $", upgraded {result.Replaced} shorter";
        if (result.SplitBeams > 0)
            result.Message += $", split {result.SplitBeams} beam{(result.SplitBeams > 1 ? "s" : "")}";
        if (result.Blocked > 0)
            result.Message += $" · {result.Blocked} blocked: {blockNote ?? "partial overlap"}";
        if (debugLogs)
            Debug.Log($"Template batch: {result.Message}");

        return result;
    }

    /// <summary>
    /// Instantiate the catalogue pieces replacing one planned beam (same type
    /// and rotation, centres from the split plan), each committed through the
    /// normal physics gate. Returns how many pieces were placed.
    /// </summary>
    int PlaceBeamSegments(FrameOverlapResolver.FrameRecord beam, List<BeamSplit.Segment> segments,
        Quaternion rotation, bool validateOverlap, ref BatchPlaceResult result, ref string blockNote)
    {
        string prefix = beam.PartId;
        for (int c = 0; c < prefix.Length; c++)
        {
            if (char.IsDigit(prefix[c]))
            {
                prefix = prefix.Substring(0, c);
                break;
            }
        }

        int placed = 0;
        for (int s = 0; s < segments.Count; s++)
        {
            string pieceId = prefix + segments[s].Size;
            GameObject prefab = partDatabase.GetRealPrefab(pieceId);
            if (prefab == null)
            {
                if (debugLogs)
                    Debug.LogWarning($"Template batch: no prefab for split piece {pieceId}");
                continue;
            }

            Vector3 target = beam.Center + beam.LengthAxis * NeospaceUnits.Mm(segments[s].CenterOffsetMm);

            // An identical piece already sits exactly there (an earlier split
            // or flush stamp on the same line) — reuse it; nothing to place.
            if (HasEqualBeamAt(pieceId, target))
            {
                result.Reused++;
                placed++;
                continue;
            }

            GameObject piece = Instantiate(prefab, target, rotation);

            // The record centre is the body centre; align the piece's
            // rendered bounds onto the target in case the pivot differs.
            if (FrameOverlapResolver.TryWorldBounds(piece.transform, out Bounds pieceBounds))
                piece.transform.position += target - pieceBounds.center;
            Physics.SyncTransforms();

            if (validateOverlap && HasIllegalOverlap(piece, null, null, out string reason))
            {
                piece.SetActive(false);
                Destroy(piece);
                result.Blocked++;
                blockNote ??= reason;
                if (debugLogs)
                    Debug.Log($"Template batch blocked split piece {pieceId}: {reason}");
                continue;
            }

            if (piece.GetComponent<BeamConnections>() == null)
                piece.AddComponent<BeamConnections>();
            result.Instances.Add(piece);
            result.Placed++;
            placed++;
        }
        return placed;
    }

    /// <summary>An active placed beam of this exact part id with its body centre at this point.</summary>
    bool HasEqualBeamAt(string partId, Vector3 center)
    {
        float tol = NeospaceUnits.Mm(6f);
        foreach (FrameOverlapResolver.FrameRecord rec in
                 FrameOverlapResolver.CollectFrames(ghostLayerMask.value))
        {
            if (rec.PartId == partId && (rec.Center - center).sqrMagnitude <= tol * tol)
                return true;
        }
        return false;
    }

    System.Collections.IEnumerator RefillPanelsNextFrame(
        List<StructureMergeResolver.PanelSnapshot> panelSnapshots)
    {
        // Slots settle over a few frames after a merge (deferred Destroy,
        // composite-span rebuild), so retry the refill until it stops
        // finding new sub-slots. RefillOrphanedPanels is idempotent:
        // occupied sub-slots refuse a second panel.
        const int maxAttempts = 6;
        int refilled = 0;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            yield return null;
            if (panelSlotManager == null)
                yield break;

            panelSlotManager.RebuildConnectionsAndRescanSlots();
            refilled += StructureMergeResolver.RefillOrphanedPanels(panelSnapshots, panelSlotManager);
        }

        if (refilled > 0)
            BuildHistory.NotifyChanged();
    }

    /// <summary>
    /// Roll a merged batch back whole: destroy everything this batch created,
    /// discard the parked posts, and reactivate the superseded originals. The
    /// scene is exactly as before the attempt — "no valid split means the
    /// object may not be placed there".
    /// </summary>
    BatchPlaceResult AbortMergedBatch(BatchPlaceResult result,
        List<(GameObject post, StructureMergeResolver.CrossingScan scan)> parkedPosts,
        List<GameObject> journalRemoved, string note)
    {
        for (int i = 0; i < result.Instances.Count; i++)
        {
            GameObject go = result.Instances[i];
            if (go != null)
            {
                go.SetActive(false);
                Destroy(go);
            }
        }
        result.Instances.Clear();

        if (parkedPosts != null)
        {
            foreach ((GameObject post, _) in parkedPosts)
            {
                if (post != null)
                {
                    post.SetActive(false);
                    Destroy(post);
                }
            }
        }

        if (journalRemoved != null)
        {
            foreach (GameObject go in journalRemoved)
                if (go != null)
                    go.SetActive(true);
        }

        Physics.SyncTransforms();
        if (panelSlotManager != null)
            panelSlotManager.RebuildConnectionsAndRescanSlots();

        result.Placed = 0;
        result.Reused = 0;
        result.Replaced = 0;
        result.SplitBeams = 0;
        result.Blocked = Mathf.Max(1, result.Blocked);
        result.Message = $"Can't merge here · {note ?? "the structures don't line up"}";
        if (debugLogs)
            Debug.Log($"Template batch aborted: {result.Message}");
        return result;
    }

    /// <summary>
    /// Resolve posts that landed inside existing beams (longer-wins flag only).
    /// All cuts are grouped per beam and planned together first: if ANY beam
    /// cannot be split into catalogue pieces, the whole merge is refused
    /// (returns false, caller rolls the batch back) — geometry is never
    /// approximated. Otherwise each beam is replaced by its pieces (same type
    /// and roll, ends meeting the new post columns exactly,
    /// e.g. H15 → 663 + 41 + 663) and the posts are committed through the
    /// normal physics gate.
    /// </summary>
    bool ResolveParkedMerges(
        List<(GameObject post, StructureMergeResolver.CrossingScan scan)> parked,
        List<StructureMergeResolver.PanelSnapshot> panelSnapshots,
        List<GameObject> journalRemoved,
        ref BatchPlaceResult result, bool validateOverlap, ref string blockNote)
    {
        var cutsByBeam = new Dictionary<Transform, SortedSet<int>>();
        for (int i = 0; i < parked.Count; i++)
        {
            List<StructureMergeResolver.Crossing> crossings = parked[i].scan.Crossings;
            for (int c = 0; c < crossings.Count; c++)
            {
                Transform beam = crossings[c].Beam;
                // A beam already replaced by a coaxial line rebuild is inactive:
                // its cut is realised by the run's pieces, nothing left to split.
                if (beam == null || !beam.gameObject.activeInHierarchy)
                    continue;
                if (!cutsByBeam.TryGetValue(beam, out SortedSet<int> cuts))
                    cutsByBeam[beam] = cuts = new SortedSet<int>();
                cuts.Add(crossings[c].CutIndex);
            }
        }

        // Plan every split before touching anything.
        var plans = new List<(FrameOverlapResolver.FrameRecord beam, List<BeamSplit.Segment> segments)>();
        string fail = null;
        foreach (KeyValuePair<Transform, SortedSet<int>> entry in cutsByBeam)
        {
            if (entry.Key == null ||
                !FrameOverlapResolver.TryBuildRecord(entry.Key, out FrameOverlapResolver.FrameRecord rec))
            {
                fail = "a crossed beam disappeared during the merge";
                break;
            }

            List<BeamSplit.Segment> segments = BeamSplit.Plan(rec.Size, entry.Value);
            if (segments == null)
            {
                fail = $"{rec.PartId} can't be split into available lengths there";
                break;
            }
            plans.Add((rec, segments));
        }

        if (fail != null)
        {
            result.Blocked += parked.Count;
            blockNote ??= fail;
            if (debugLogs)
                Debug.Log($"Template batch merge refused: {fail}");
            return false;
        }

        // Replace each beam with its pieces.
        for (int p = 0; p < plans.Count; p++)
        {
            (FrameOverlapResolver.FrameRecord rec, List<BeamSplit.Segment> segments) = plans[p];

            string prefix = rec.PartId;
            for (int c = 0; c < prefix.Length; c++)
            {
                if (char.IsDigit(prefix[c]))
                {
                    prefix = prefix.Substring(0, c);
                    break;
                }
            }

            Quaternion rotation = rec.Root.rotation;
            rec.Root.gameObject.SetActive(false);
            if (journalRemoved != null)
                journalRemoved.Add(rec.Root.gameObject);
            else
                Destroy(rec.Root.gameObject);

            for (int s = 0; s < segments.Count; s++)
            {
                string pieceId = prefix + segments[s].Size;
                GameObject prefab = partDatabase.GetRealPrefab(pieceId);
                if (prefab == null)
                {
                    if (debugLogs)
                        Debug.LogWarning($"Template batch: no prefab for split piece {pieceId}");
                    continue;
                }

                Vector3 target = rec.Center + rec.LengthAxis * NeospaceUnits.Mm(segments[s].CenterOffsetMm);
                GameObject piece = Instantiate(prefab, target, rotation);

                // The record centre is the body centre; align the piece's
                // rendered bounds onto the target in case the pivot differs.
                if (FrameOverlapResolver.TryWorldBounds(piece.transform, out Bounds pieceBounds))
                    piece.transform.position += target - pieceBounds.center;

                if (piece.GetComponent<BeamConnections>() == null)
                    piece.AddComponent<BeamConnections>();
                result.Instances.Add(piece);
            }
            result.SplitBeams++;
        }

        // Bays a post column truly divides lose their panel and blocker NOW,
        // so the parked posts don't collide with them during validation. Bays
        // merely touched at an edge keep their panel — the slot scanner walks
        // the split beam's pieces as one side, so those slots survive. The
        // final rescan + refill reseats panels of divided bays per sub-bay.
        var postColumns = new List<StructureMergeResolver.PostColumn>(parked.Count);
        float halfProfile = NeospaceUnits.Mm(CatalogueData.ProfileMm * 0.5f);
        for (int i = 0; i < parked.Count; i++)
        {
            FrameOverlapResolver.FrameRecord rec = parked[i].scan.Post;
            postColumns.Add(new StructureMergeResolver.PostColumn
            {
                Position = rec.Center,
                BottomY = rec.EndA.y - halfProfile,
                TopY = rec.EndB.y + halfProfile
            });
        }
        StructureMergeResolver.ConsumeCrossedBays(postColumns, panelSnapshots);
        Physics.SyncTransforms();

        // Wake the parked posts and commit them through the normal gate.
        for (int i = 0; i < parked.Count; i++)
        {
            GameObject post = parked[i].post;
            if (post == null)
                continue;

            post.SetActive(true);
            if (validateOverlap && HasIllegalOverlap(post, null, null, out string reason))
            {
                post.SetActive(false);
                Destroy(post);
                result.Blocked++;
                blockNote ??= reason;
                if (debugLogs)
                    Debug.Log($"Template batch blocked parked post: {reason}");
                continue;
            }

            if (post.GetComponent<BeamConnections>() == null)
                post.AddComponent<BeamConnections>();
            result.Instances.Add(post);
            result.Placed++;
        }

        return true;
    }

    /// <summary>
    /// Strict connector placement for Guided templates: deterministically place
    /// one H/HT connector spanning corner A → corner B (post axis to post axis,
    /// both ends at the same height). A beam's span direction is dictated by the
    /// face of the hole hosting it, so this enumerates the real free holes
    /// hugging each end of the span, runs the exact Expert hole/peg + face +
    /// overlap pipeline anchored on each hole, and commits the first pose whose
    /// body truly spans the gap. Occupancy pairing and panel rescans then behave
    /// identically to Expert placements.
    /// </summary>
    public bool TryPlaceConnectorSpan(string partId, Vector3 a, Vector3 b, out string message)
    {
        if (!TryResolveConnectorSpanPose(partId, a, b, out GhostPlacementResult pose, out message))
            return false;

        PlaceRealBeamFromGhost(partId, pose);
        Physics.SyncTransforms();
        message = $"{partId} placed";
        return true;
    }

    /// <summary>
    /// Pose the span pipeline WOULD commit for this connector, without placing
    /// anything: powers the live beam ghost of the category part tools, so the
    /// preview is pixel-identical to the final placement.
    /// </summary>
    public bool TryPreviewConnectorSpan(
        string partId, Vector3 a, Vector3 b, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        if (!TryResolveConnectorSpanPose(partId, a, b, out GhostPlacementResult pose, out _))
            return false;

        position = pose.position;
        rotation = pose.rotation;
        return true;
    }

    bool TryResolveConnectorSpanPose(
        string partId, Vector3 a, Vector3 b, out GhostPlacementResult pose, out string message)
    {
        pose = default;
        message = string.Empty;
        if (partDatabase == null)
        {
            message = "Missing PartDatabase";
            return false;
        }

        Vector3 span = b - a;
        span.y = 0f;
        if (span.sqrMagnitude < 1e-8f)
        {
            message = "Span has no length";
            return false;
        }

        Vector3 spanDir = span.normalized;
        Vector3 expectedCenter = (a + b) * 0.5f;
        float centerTolerance = Mathf.Max(NeospaceUnits.Mm(150f), span.magnitude * 0.3f);

        List<AttachmentPoint> candidates = CollectSpanHostHoles(a, b, spanDir);
        if (candidates.Count == 0)
        {
            message = "No free connection hole at either end of the span";
            return false;
        }

        GameObject ghostPrefab = partDatabase.GetGhostPrefabOrFallback(partId);
        if (ghostPrefab == null)
        {
            message = $"No prefab for {partId}";
            return false;
        }

        GameObject ghost = Instantiate(ghostPrefab);
        ghost.name = $"{partId}_TemplateGhost";
        int ghostLayer = FirstLayerIndex(ghostLayerMask);
        if (ghostLayer >= 0)
            SetLayerRecursively(ghost.transform, ghostLayer);

        try
        {
            string lastReason = "no free hole yields a pose along the span";
            for (int i = 0; i < candidates.Count; i++)
            {
                AttachmentPoint hole = candidates[i];
                if (hole == null || hole.isOccupied)
                    continue;

                GhostPlacementResult result =
                    ComputeGhostHAtPoint(partId, ghost, hole.transform.position);
                if (!result.hasPose || !result.isValid)
                {
                    lastReason = result.debugInfo;
                    continue;
                }

                ghost.transform.SetPositionAndRotation(result.position, result.rotation);
                Physics.SyncTransforms();

                Vector3 actualCenter = TryGetWorldBoundsForFloor(ghost, out Bounds bounds)
                    ? bounds.center
                    : result.position;
                if (Vector3.Distance(actualCenter, expectedCenter) > centerTolerance)
                {
                    lastReason = "candidate pose extends away from the gap";
                    continue;
                }

                // A beam pointing AWAY from the gap sits one full span off along
                // the span axis. For short spans (H1 = 88 mm) that is still
                // inside the general distance tolerance above, so check the
                // along-span displacement explicitly.
                float alongSpan = Mathf.Abs(Vector3.Dot(actualCenter - expectedCenter, spanDir));
                if (alongSpan > span.magnitude * 0.35f)
                {
                    lastReason = "candidate pose points away from the gap";
                    continue;
                }

                pose = result;
                return true;
            }

            message = lastReason;
            return false;
        }
        finally
        {
            Destroy(ghost);
        }
    }

    /// <summary>
    /// Free scene holes hugging either end of the span at its height, best first:
    /// holes whose face mapping yields a beam running along the span are tried
    /// before the rest (the caller validates each pose, so ordering only affects
    /// how many attempts are needed).
    /// </summary>
    List<AttachmentPoint> CollectSpanHostHoles(Vector3 a, Vector3 b, Vector3 spanDir)
    {
        // XZ radius covers the half-profile hole offset (20.5 mm) plus room for
        // off-centre FBX pivots, while staying below the minimum post gap.
        float yTolerance = NeospaceUnits.Mm(30f);
        float xzTolerance = NeospaceUnits.Mm(90f);

        var scored = new List<(AttachmentPoint hole, float alignment)>();
        List<AttachmentPoint> all = AttachmentPoint.Live;
        for (int i = 0; i < all.Count; i++)
        {
            AttachmentPoint ap = all[i];
            if (ap == null || ap.isOccupied || ap.role != AttachmentPoint.PointRole.Hole)
                continue;

            Transform root = ap.transform.root;
            if (root == null || (ghostLayerMask.value & (1 << root.gameObject.layer)) != 0)
                continue;

            Vector3 p = ap.transform.position;
            if (Mathf.Abs(p.y - a.y) > yTolerance)
                continue;

            if (HorizontalDistance(p, a) > xzTolerance && HorizontalDistance(p, b) > xzTolerance)
                continue;

            // Predict the span direction this hole's face would give a beam
            // (same mapping the placement pipeline uses).
            int faceIndex = ConnectorNameUtility.GetFaceIndex(ap.name);
            if (faceIndex == 0)
                faceIndex = 2;
            Vector3 faceOut = root.TransformDirection(
                GetSideOutLocal(InferHostKindFromRoot(root), faceIndex));
            Vector3 beamDir = Vector3.Cross(Vector3.up, faceOut);

            float alignment = beamDir.sqrMagnitude > 1e-6f
                ? Mathf.Abs(Vector3.Dot(beamDir.normalized, spanDir))
                : 0f;
            scored.Add((ap, alignment));
        }

        scored.Sort((l, r) => r.alignment.CompareTo(l.alignment));

        var result = new List<AttachmentPoint>(scored.Count);
        for (int i = 0; i < scored.Count; i++)
            result.Add(scored[i].hole);
        return result;
    }

    static float HorizontalDistance(Vector3 p, Vector3 q)
    {
        float dx = p.x - q.x;
        float dz = p.z - q.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
    }

    static int FirstLayerIndex(LayerMask mask)
    {
        int value = mask.value;
        for (int i = 0; i < 32; i++)
        {
            if ((value & (1 << i)) != 0)
                return i;
        }
        return -1;
    }

    void SeatInstanceOnFloor(GameObject instance, ref Vector3 position, Quaternion rotation)
    {
        if (instance == null)
            return;

        // Raycast down to floor from above the footprint.
        Vector3 origin = position + Vector3.up * 5f;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 20f, floorMask, QueryTriggerInteraction.Collide))
            return;

        instance.transform.SetPositionAndRotation(position, rotation);
        Physics.SyncTransforms();

        if (!TryGetWorldBoundsForFloor(instance, out Bounds bounds))
        {
            position.y = hit.point.y + Mathf.Max(0f, firstSurfaceClearance);
            instance.transform.position = position;
            return;
        }

        float targetMinY = hit.point.y + Mathf.Max(0f, firstSurfaceClearance);
        float deltaY = targetMinY - bounds.min.y;
        if (!float.IsNaN(deltaY) && !float.IsInfinity(deltaY))
            position.y += deltaY;

        // Keep planned XZ; only correct vertical seating.
        position.x = instance.transform.position.x;
        position.z = instance.transform.position.z;
        instance.transform.SetPositionAndRotation(position, rotation);
        Physics.SyncTransforms();
    }
}
