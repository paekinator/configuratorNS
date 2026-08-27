using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Merge solving for Guided commits (longer-wins flag only): when a new V post
/// lands strictly inside an existing H/HT beam's span, the beam must be
/// replaced by catalogue pieces that keep the overall geometry
/// (e.g. H15 → H7 + post + H7 = 663 + 41 + 663). When no valid replacement
/// exists the placement is refused — never approximated. Panels whose bay is
/// divided by the split are re-seated into the resulting sub-bays.
/// Pure detection/reporting here; <c>BuildController</c> applies the changes.
/// </summary>
public static class StructureMergeResolver
{
    /// <summary>Post axis must sit this close to the beam's skeleton line.</summary>
    const float LateralToleranceMm = 4f;

    /// <summary>And this close to an exact 88 mm connection point.</summary>
    const float CutSnapToleranceMm = 4f;

    /// <summary>Height slack when testing whether a beam lies within a post's column.</summary>
    const float HeightToleranceMm = 30f;

    public struct Crossing
    {
        public Transform Beam;
        public int CutIndex;
    }

    public struct CrossingScan
    {
        public bool Blocked;
        public string Note;
        public List<Crossing> Crossings;

        /// <summary>The measured record of the post that was scanned.</summary>
        public FrameOverlapResolver.FrameRecord Post;
    }

    /// <summary>The physical column a new post occupies, for bay-division tests.</summary>
    public struct PostColumn
    {
        public Vector3 Position; // axis XZ (Y unused)
        public float BottomY;    // body bottom, metres
        public float TopY;       // body top, metres
    }

    /// <summary>Cuts a NEW beam needs because existing posts stand inside its span.</summary>
    public struct SelfSplitScan
    {
        public bool Blocked;
        public string Note;
        public SortedSet<int> Cuts;
        public FrameOverlapResolver.FrameRecord Beam;
    }

    /// <summary>
    /// Find every existing H/HT beam whose interior is crossed by this
    /// (not yet committed) V post. Blocked = the post lands inside a beam but
    /// off its 88 mm points, so no split can ever be built there.
    /// Posts standing at a beam's joint ends are the normal shared column and
    /// are not crossings.
    /// </summary>
    public static CrossingScan FindCrossings(GameObject postInstance, int ghostLayerMask)
    {
        var scan = new CrossingScan { Crossings = new List<Crossing>() };
        if (postInstance == null)
            return scan;

        if (!FrameOverlapResolver.TryBuildRecord(postInstance.transform, out FrameOverlapResolver.FrameRecord post) ||
            !BeamPartUtility.IsVertical(post.PartId))
            return scan;

        scan.Post = post;

        float heightTol = NeospaceUnits.Mm(HeightToleranceMm);
        float postBottom = post.EndA.y - heightTol;
        float postTop = post.EndB.y + heightTol;
        var postXZ = new Vector2(post.Center.x, post.Center.z);

        foreach (FrameOverlapResolver.FrameRecord beam in
                 FrameOverlapResolver.CollectFrames(ghostLayerMask, postInstance.transform))
        {
            if (!BeamPartUtility.IsHorizontalLike(beam.PartId))
                continue;
            if (beam.Center.y < postBottom || beam.Center.y > postTop)
                continue;

            var beamXZ = new Vector2(beam.Center.x, beam.Center.z);
            var axisXZ = new Vector2(beam.LengthAxis.x, beam.LengthAxis.z);
            Vector2 d = postXZ - beamXZ;
            float along = Vector2.Dot(d, axisXZ);
            float lateral = (d - axisXZ * along).magnitude;
            if (NeospaceUnits.ToMm(lateral) > LateralToleranceMm)
                continue;

            // At or beyond the joint ends: the post shares the beam's end
            // column (standard construction), not a crossing.
            float alongMm = NeospaceUnits.ToMm(along);
            float halfSkeletonMm = Skeleton.HSkeletonLength(beam.Size) * 0.5f;
            if (Mathf.Abs(alongMm) >= halfSkeletonMm - CatalogueData.ModuleMm * 0.5f)
                continue;

            if (!BeamSplit.TryCutIndexFromOffset(beam.Size, alongMm, CutSnapToleranceMm, out int cut))
            {
                scan.Blocked = true;
                scan.Note = $"{post.PartId} lands inside {beam.PartId} off its 88 mm points";
                return scan;
            }

            scan.Crossings.Add(new Crossing { Beam = beam.Root, CutIndex = cut });
        }

        return scan;
    }

    /// <summary>
    /// The INVERSE of <see cref="FindCrossings"/>: find every EXISTING V post
    /// standing strictly inside this (not yet committed) H/HT beam's span.
    /// Such a beam cannot be placed whole — it must be replaced by catalogue
    /// pieces meeting the post (the other half of a two-way merge, where each
    /// structure splits the other's beams). Blocked = a post sits inside the
    /// beam but off its 88 mm points, so no split can ever be built.
    /// </summary>
    public static SelfSplitScan FindPostCrossingsOnBeam(GameObject beamInstance, int ghostLayerMask)
    {
        var scan = new SelfSplitScan { Cuts = new SortedSet<int>() };
        if (beamInstance == null)
            return scan;

        if (!FrameOverlapResolver.TryBuildRecord(beamInstance.transform, out FrameOverlapResolver.FrameRecord beam) ||
            !BeamPartUtility.IsHorizontalLike(beam.PartId))
            return scan;

        scan.Beam = beam;

        float heightTol = NeospaceUnits.Mm(HeightToleranceMm);
        var beamXZ = new Vector2(beam.Center.x, beam.Center.z);
        var axisXZ = new Vector2(beam.LengthAxis.x, beam.LengthAxis.z);
        float halfSkeletonMm = Skeleton.HSkeletonLength(beam.Size) * 0.5f;

        foreach (FrameOverlapResolver.FrameRecord post in
                 FrameOverlapResolver.CollectFrames(ghostLayerMask, beamInstance.transform))
        {
            if (!BeamPartUtility.IsVertical(post.PartId))
                continue;
            if (beam.Center.y < post.EndA.y - heightTol || beam.Center.y > post.EndB.y + heightTol)
                continue;   // beam level not on this post's column

            var postXZ = new Vector2(post.Center.x, post.Center.z);
            Vector2 d = postXZ - beamXZ;
            float along = Vector2.Dot(d, axisXZ);
            float lateral = (d - axisXZ * along).magnitude;
            if (NeospaceUnits.ToMm(lateral) > LateralToleranceMm)
                continue;   // not on the beam's line

            // At or beyond the joint ends: the beam ends at the post's
            // column (standard construction), not a crossing.
            float alongMm = NeospaceUnits.ToMm(along);
            if (Mathf.Abs(alongMm) >= halfSkeletonMm - CatalogueData.ModuleMm * 0.5f)
                continue;

            if (!BeamSplit.TryCutIndexFromOffset(beam.Size, alongMm, CutSnapToleranceMm, out int cut))
            {
                scan.Blocked = true;
                scan.Note = $"{beam.PartId} lands across {post.PartId} off its 88 mm points";
                return scan;
            }

            scan.Cuts.Add(cut);
        }

        return scan;
    }

    /// <summary>A partially-overlapping coaxial run rebuilt as one row of catalogue pieces.</summary>
    public struct LineMergePlan
    {
        /// <summary>False = no coaxial overlap on this line; the caller proceeds normally.</summary>
        public bool Applies;

        /// <summary>
        /// True = a same-family existing beam already fully covers the planned
        /// span (Rhino "longer wins": equal or containing span keeps the
        /// existing beam). The caller should drop the planned beam and count
        /// it as reused instead of letting the physics gate report a block.
        /// </summary>
        public bool CoveredByExisting;

        /// <summary>Non-null = an overlap exists but the run can't be built; refuse the merge.</summary>
        public string Error;

        /// <summary>Existing beams replaced by the run.</summary>
        public List<Transform> RemoveBeams;

        public List<BeamSplit.Segment> Segments;

        /// <summary>Synthetic record centred on the union, for segment placement.</summary>
        public FrameOverlapResolver.FrameRecord Template;
    }

    /// <summary>
    /// A planned H/HT beam whose span PARTIALLY overlaps existing coaxial
    /// beams — two structures pushed together with an offset. Physically that
    /// line is one continuous run with posts standing on it, so plan the run a
    /// fitter would build: catalogue pieces meeting every post. Equal or fully
    /// contained spans are NOT taken (the physics gate dedups those); only a
    /// genuine partial overlap engages the rebuild. <paramref name="extraPosts"/>
    /// carries batch posts that are parked (inactive) and thus invisible to the
    /// scene scan.
    /// </summary>
    public static LineMergePlan PlanLineMerge(GameObject beamInstance, int ghostLayerMask,
        IReadOnlyList<FrameOverlapResolver.FrameRecord> extraPosts)
    {
        var plan = new LineMergePlan();
        if (beamInstance == null)
            return plan;

        if (!FrameOverlapResolver.TryBuildRecord(beamInstance.transform, out FrameOverlapResolver.FrameRecord beam) ||
            !BeamPartUtility.IsHorizontalLike(beam.PartId))
            return plan;

        float lateralTol = NeospaceUnits.Mm(6f);
        float yTol = NeospaceUnits.Mm(HeightToleranceMm);
        float spanTolMm = 6f;
        Vector3 axis = beam.LengthAxis;
        float plannedS = Vector3.Dot(beam.Center, axis);
        float plannedHalf = NeospaceUnits.Mm(Skeleton.HSkeletonLength(beam.Size)) * 0.5f;

        List<FrameOverlapResolver.FrameRecord> all =
            FrameOverlapResolver.CollectFrames(ghostLayerMask, beamInstance.transform);

        // Beams on the same line and level.
        var lineBeams = new List<(FrameOverlapResolver.FrameRecord rec, float lo, float hi)>();
        foreach (FrameOverlapResolver.FrameRecord other in all)
        {
            if (!BeamPartUtility.IsHorizontalLike(other.PartId))
                continue;
            if (Vector3.Dot(other.LengthAxis, axis) < 0.9f)
                continue;
            if (Mathf.Abs(other.Center.y - beam.Center.y) > yTol)
                continue;
            Vector3 d = other.Center - beam.Center;
            Vector3 lateral = d - axis * Vector3.Dot(d, axis);
            lateral.y = 0f;
            if (lateral.magnitude > lateralTol)
                continue;

            float s = Vector3.Dot(other.Center, axis);
            float half = NeospaceUnits.Mm(Skeleton.HSkeletonLength(other.Size)) * 0.5f;
            lineBeams.Add((other, s - half, s + half));
        }
        if (lineBeams.Count == 0)
            return plan;

        // Members = existing beams whose span overlaps the growing union.
        float unionLo = plannedS - plannedHalf;
        float unionHi = plannedS + plannedHalf;
        float overlapTol = NeospaceUnits.Mm(spanTolMm);
        var members = new List<(FrameOverlapResolver.FrameRecord rec, float lo, float hi)>();
        bool grew = true;
        while (grew)
        {
            grew = false;
            for (int i = lineBeams.Count - 1; i >= 0; i--)
            {
                (FrameOverlapResolver.FrameRecord rec, float lo, float hi) = lineBeams[i];
                if (Mathf.Min(unionHi, hi) - Mathf.Max(unionLo, lo) <= overlapTol)
                    continue;
                members.Add((rec, lo, hi));
                lineBeams.RemoveAt(i);
                unionLo = Mathf.Min(unionLo, lo);
                unionHi = Mathf.Max(unionHi, hi);
                grew = true;
            }
        }
        if (members.Count == 0)
            return plan;

        // When an existing beam fully covers the planned span, the planned
        // beam is redundant — the physics gate dedups it (flush re-stamps).
        // Anything else (partial overlap, or the planned beam covering a
        // shorter existing one) is one physical run that must be rebuilt.
        bool needRebuild = false;
        foreach ((_, float lo, float hi) in members)
        {
            bool covers = lo <= plannedS - plannedHalf + overlapTol &&
                          hi >= plannedS + plannedHalf - overlapTol;
            if (!covers)
            {
                needRebuild = true;
                break;
            }
        }
        string family = FamilyPrefix(beam.PartId);

        if (!needRebuild)
        {
            // Every member covers the planned span, so the planned beam adds
            // nothing. If the family matches, this is a clean reuse; if not,
            // fall through to the physics gate which blocks it as today.
            bool sameFamily = true;
            foreach ((FrameOverlapResolver.FrameRecord rec, _, _) in members)
                if (FamilyPrefix(rec.PartId) != family) { sameFamily = false; break; }
            plan.CoveredByExisting = sameFamily;
            return plan;
        }

        plan.Applies = true;

        foreach ((FrameOverlapResolver.FrameRecord rec, _, _) in members)
        {
            if (FamilyPrefix(rec.PartId) != family)
            {
                plan.Error = $"{beam.PartId} and {rec.PartId} can't merge · different beam types on one line";
                return plan;
            }
        }

        // Posts standing on this line at this level: placed ones from the
        // scene scan plus parked batch posts passed in by the caller.
        var posts = new List<float>();
        void AddPost(in FrameOverlapResolver.FrameRecord post)
        {
            if (!BeamPartUtility.IsVertical(post.PartId))
                return;
            if (beam.Center.y < post.EndA.y - yTol || beam.Center.y > post.EndB.y + yTol)
                return;
            Vector3 d = post.Center - beam.Center;
            Vector3 lateral = d - axis * Vector3.Dot(d, axis);
            lateral.y = 0f;
            if (lateral.magnitude > lateralTol)
                return;
            posts.Add(NeospaceUnits.ToMm(Vector3.Dot(post.Center, axis)));
        }

        foreach (FrameOverlapResolver.FrameRecord rec in all)
            AddPost(rec);
        if (extraPosts != null)
            foreach (FrameOverlapResolver.FrameRecord rec in extraPosts)
                AddPost(rec);

        var spans = new List<(float lo, float hi)>(members.Count + 1)
        {
            (NeospaceUnits.ToMm(plannedS - plannedHalf), NeospaceUnits.ToMm(plannedS + plannedHalf))
        };
        foreach ((_, float lo, float hi) in members)
            spans.Add((NeospaceUnits.ToMm(lo), NeospaceUnits.ToMm(hi)));

        List<BeamSplit.Segment> segments =
            BeamSplit.PlanLine(spans, posts, spanTolMm, out float centerMm, out string error);
        if (segments == null)
        {
            plan.Error = $"{beam.PartId} can't merge onto that line: {error}";
            return plan;
        }

        plan.Segments = segments;
        plan.RemoveBeams = new List<Transform>(members.Count);
        foreach ((FrameOverlapResolver.FrameRecord rec, _, _) in members)
            plan.RemoveBeams.Add(rec.Root);

        float centerS = NeospaceUnits.Mm(centerMm);
        plan.Template = beam;
        plan.Template.Center = beam.Center + axis * (centerS - plannedS);
        return plan;
    }

    static string FamilyPrefix(string id)
    {
        if (string.IsNullOrEmpty(id))
            return string.Empty;
        for (int c = 0; c < id.Length; c++)
            if (char.IsDigit(id[c]))
                return id.Substring(0, c);
        return id;
    }

    // ------------------------------------------------------------------
    // Panels: a split replaces one bay with two, so a panel spanning the
    // original bay must become one panel per sub-bay. Snapshot before the
    // merge, then after the slot rescan re-seat panels whose slot vanished
    // into every new slot inside the old rectangle.
    // ------------------------------------------------------------------

    public struct PanelSnapshot
    {
        public GameObject Panel;
        public string SlotId;
        public int Side;
        public Vector3 Corner0;
        public Vector3 EdgeU;      // corner0 → corner1
        public Vector3 EdgeV;      // corner0 → corner3
        public Vector3 Normal;
    }

    /// <summary>Capture every placed panel that currently resolves to a live slot.</summary>
    public static List<PanelSnapshot> SnapshotPanels()
    {
        var handles = SlotsById();
        var result = new List<PanelSnapshot>();

        foreach (PanelInstance panel in Object.FindObjectsByType<PanelInstance>(FindObjectsSortMode.None))
        {
            if (panel == null || string.IsNullOrEmpty(panel.slotId))
                continue;
            if (!handles.TryGetValue(panel.slotId, out PanelSlotHandle slot) || slot == null)
                continue;

            result.Add(new PanelSnapshot
            {
                Panel = panel.gameObject,
                SlotId = panel.slotId,
                Side = panel.side >= 0 ? 1 : -1,
                Corner0 = slot.corner0,
                EdgeU = slot.corner1 - slot.corner0,
                EdgeV = slot.corner3 - slot.corner0,
                Normal = slot.normal.sqrMagnitude > 1e-6f ? slot.normal.normalized : Vector3.forward
            });
        }
        return result;
    }

    /// <summary>
    /// After a merge + slot rescan: panels whose slot no longer exists are
    /// replaced by panels in every new slot lying inside their old rectangle
    /// (same side relative to the old normal). Returns panels re-seated.
    /// </summary>
    public static int RefillOrphanedPanels(List<PanelSnapshot> snapshots, PanelSlotManager manager)
    {
        if (snapshots == null || snapshots.Count == 0 || manager == null)
            return 0;

        var handles = SlotsById();
        int placed = 0;

        for (int i = 0; i < snapshots.Count; i++)
        {
            PanelSnapshot snap = snapshots[i];
            // "Bay untouched" needs BOTH the slot and the panel intact. The
            // merge can consume the panel while a stale big-rect slot id still
            // regenerates for a frame or two — skipping on the id alone would
            // silently drop that panel.
            if (handles.ContainsKey(snap.SlotId) && snap.Panel != null && snap.Panel.activeSelf)
                continue;

            int placedHere = 0;
            foreach (PanelSlotHandle slot in handles.Values)
            {
                if (slot == null || !InsideOldRect(snap, slot.center))
                    continue;

                int side = snap.Side;
                if (Vector3.Dot(slot.normal, snap.Normal) < 0f)
                    side = -side;

                if (manager.CanPlacePanel(slot, side) && manager.PlacePanel(slot, side) != null)
                    placedHere++;
            }
            placed += placedHere;

            // Destroy the old panel only when the merge consumed it (already
            // deactivated) or sub-bay replacements were actually placed. A
            // panel whose bay merely lost its slot id in this scan (unrelated
            // churn) stays preserved so it can reattach when its slot returns.
            if (snap.Panel != null && (placedHere > 0 || !snap.Panel.activeSelf))
            {
                snap.Panel.SetActive(false);
                Object.Destroy(snap.Panel);
            }
        }
        return placed;
    }

    /// <summary>
    /// Consume the bays a merge truly DIVIDES, before the parked posts are
    /// physics-validated: their panels are destroyed and their slot blockers
    /// disarmed immediately (Destroy alone is deferred, so the colliders would
    /// still reject the posts this frame).
    ///
    /// A bay is divided only when a post column passes through the PANEL'S
    /// INTERIOR — a wall bay with the post crossing its full height in-plane,
    /// or a floor bay a post punches straight through. A post standing on a
    /// bay's EDGE beam splits only that beam; the bay itself survives (the
    /// slot scanner walks the collinear H7+H7 chain as one side), so its panel
    /// must be left alone. The final rescan removes dead slots and
    /// <see cref="RefillOrphanedPanels"/> reseats panels per sub-bay.
    /// </summary>
    public static int ConsumeCrossedBays(List<PostColumn> posts, List<PanelSnapshot> snapshots)
    {
        if (posts == null || posts.Count == 0 || snapshots == null || snapshots.Count == 0)
            return 0;

        var handles = SlotsById();
        int consumed = 0;

        for (int i = 0; i < snapshots.Count; i++)
        {
            PanelSnapshot snap = snapshots[i];

            bool divided = false;
            for (int p = 0; p < posts.Count && !divided; p++)
                divided = DividesBay(snap, posts[p]);
            if (!divided)
                continue;

            if (handles.TryGetValue(snap.SlotId, out PanelSlotHandle slot) && slot != null)
            {
                if (slot.blocker != null)
                    slot.blocker.gameObject.SetActive(false);
                if (slot.panelPlus == snap.Panel)
                    slot.panelPlus = null;
                if (slot.panelMinus == snap.Panel)
                    slot.panelMinus = null;
            }

            if (snap.Panel != null)
            {
                snap.Panel.SetActive(false);
                Object.Destroy(snap.Panel);
            }
            consumed++;
        }
        return consumed;
    }

    static bool DividesBay(in PanelSnapshot snap, in PostColumn post)
    {
        float edgeMargin = NeospaceUnits.Mm(40f);  // interior means clear of the bounding frames
        float planeTol = NeospaceUnits.Mm(30f);

        if (Mathf.Abs(snap.Normal.y) > 0.5f)
        {
            // Floor/roof bay: divided only when the column punches through it.
            float planeY = snap.Corner0.y;
            if (post.BottomY + planeTol > planeY || post.TopY - planeTol < planeY)
                return false;

            var q = new Vector3(post.Position.x, planeY, post.Position.z);
            return InsideRectInterior(snap, q, edgeMargin);
        }

        // Wall bay: the post must stand in the wall's plane and span its full
        // height (both bounding beams were crossed); part-height posts cannot
        // produce a buildable panel split and stay blocked by physics.
        Vector3 d = post.Position - snap.Corner0;
        d.y = 0f;
        if (Mathf.Abs(Vector3.Dot(d, snap.Normal)) > planeTol)
            return false;

        float minY = snap.Corner0.y, maxY = snap.Corner0.y;
        MinMaxY(snap.Corner0 + snap.EdgeU, ref minY, ref maxY);
        MinMaxY(snap.Corner0 + snap.EdgeV, ref minY, ref maxY);
        MinMaxY(snap.Corner0 + snap.EdgeU + snap.EdgeV, ref minY, ref maxY);
        if (post.BottomY > minY + planeTol || post.TopY < maxY - planeTol)
            return false;

        var mid = new Vector3(post.Position.x, (minY + maxY) * 0.5f, post.Position.z);
        return InsideRectInterior(snap, mid, edgeMargin);
    }

    static void MinMaxY(Vector3 point, ref float minY, ref float maxY)
    {
        minY = Mathf.Min(minY, point.y);
        maxY = Mathf.Max(maxY, point.y);
    }

    /// <summary>Point strictly inside the rect, at least marginMeters from every edge.</summary>
    static bool InsideRectInterior(in PanelSnapshot snap, Vector3 point, float marginMeters)
    {
        Vector3 p = point - snap.Corner0;
        float uLen = snap.EdgeU.magnitude;
        float vLen = snap.EdgeV.magnitude;
        if (uLen < 1e-4f || vLen < 1e-4f)
            return false;

        float a = Vector3.Dot(p, snap.EdgeU / uLen);
        float b = Vector3.Dot(p, snap.EdgeV / vLen);
        return a > marginMeters && a < uLen - marginMeters &&
               b > marginMeters && b < vLen - marginMeters;
    }

    static Dictionary<string, PanelSlotHandle> SlotsById()
    {
        var handles = new Dictionary<string, PanelSlotHandle>();
        foreach (PanelSlotHandle slot in Object.FindObjectsByType<PanelSlotHandle>(FindObjectsSortMode.None))
        {
            if (slot != null && !string.IsNullOrEmpty(slot.slotId))
                handles[slot.slotId] = slot;
        }
        return handles;
    }

    static bool InsideOldRect(in PanelSnapshot snap, Vector3 point)
    {
        Vector3 p = point - snap.Corner0;
        if (Mathf.Abs(Vector3.Dot(p, snap.Normal)) > NeospaceUnits.Mm(30f))
            return false;

        float u2 = snap.EdgeU.sqrMagnitude;
        float v2 = snap.EdgeV.sqrMagnitude;
        if (u2 < 1e-6f || v2 < 1e-6f)
            return false;

        float s = Vector3.Dot(p, snap.EdgeU) / u2;
        float t = Vector3.Dot(p, snap.EdgeV) / v2;
        return s > -0.02f && s < 1.02f && t > -0.02f && t < 1.02f;
    }
}
