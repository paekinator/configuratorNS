using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plans the finishing parts (veneers, Cap Sides, Cap Ends, Feet) a placed
/// build needs, adapted from Rhino neospace_rhino/finishing_gen.py using the
/// supplied simplified H1-H15 meshes (one model per size, no chamfer variants)
/// and a Foot under every grounded post bottom.
///
/// Planning is pure — it reads frame/panel records and emits placements.
/// <see cref="FinishController"/> turns placements into scene objects.
///
/// Per frame:
///  - V posts: Cap End on the top body end (and on a floating bottom), a
///    Foot under a grounded bottom. Every channel is divided at the two end
///    holes plus every connection level of the post (the Rhino rule — see
///    <see cref="SplitAllChannelsAtConnections"/>). A Cap Side sits at each
///    divider (unless the joint itself occupies that face, or a panel hides
///    it) and veneers fill the sections between, split by a mid Cap Side
///    where one veneer cannot reach.
///  - H/HT/T beams: each of the four channels gets default coverage across
///    the body — one veneer, or two with a mid Cap Side. Beam ends are
///    joints, never capped. The covering breaks where a body presses on a
///    face: stacked posts, and connectors (H from either end, twist Peg A)
///    plugging into the beam's side holes.
///  - Panel masking: a channel facing into a panelled space is skipped over
///    the length range actually behind the panel, never the whole channel.
/// </summary>
public static class FinishGenerator
{
    /// <summary>Half the frame profile: channel faces sit here (mm).</summary>
    const float HalfProfileMm = 20.5f;

    /// <summary>
    /// Rhino NSFINISH divides every channel of a post at every connection
    /// level (a Cap Side on each free face, veneers split around the ring).
    /// False keeps a connector's level on the channel it plugs into only,
    /// letting the other three faces run one long veneer past it (fewer
    /// parts, but a different finish from the Rhino tool).
    /// </summary>
    public static readonly bool SplitAllChannelsAtConnections = true;

    /// <summary>Position tolerance for joints/holes, generous for meshes.</summary>
    const float TolMm = 6f;

    /// <summary>
    /// How far a board edge may reach over a frame face and still count as
    /// lying BESIDE it rather than covering it (mm). Restored designs and
    /// Space pieces carry up to ±0.5 mm of code quantization per part, so a
    /// board nominally 20.64 mm from a beam axis can sit at 20.1 mm; a
    /// hard 20.5 mm edge test then stripped whole outward channels (the
    /// bottom ring of a panelled plinth). A board that truly covers a face
    /// overlaps it by tens of millimetres, far beyond this allowance.
    /// </summary>
    public const float SeamToleranceMm = 2f;

    // Numerical contact tolerance, not a fitting allowance: live boards can
    // be only 1 mm thick, so a 1 mm tolerance would ignore an entire shelf.
    const float MaskTolMm = 0.001f;

    /// <summary>
    /// Measured visible plate dimensions. Nominal contact lengths describe
    /// the modular seats; collision checks must use the actual imported mesh.
    /// </summary>
    public struct PartDimensions
    {
        public float HalfLengthMm;
        public float HalfWidthMm;
        public float HalfThicknessMm;
    }

    static PartDimensions Dimensions(string model, Dictionary<string, PartDimensions> measured)
    {
        if (measured != null && measured.TryGetValue(model, out PartDimensions value))
            return value;

        // Bounds of the supplied Simplified Veneers FBXs. Runtime planning
        // supplies measured imported bounds instead; these defaults also let
        // offline planning use the real visible plate, not its longer nominal
        // contact length. Cap Side similarly overhangs the 41 mm profile.
        bool veneer = model.StartsWith("Veneer H", System.StringComparison.Ordinal);
        int size = veneer ? int.Parse(model.Substring(8)) : 0;
        return new PartDimensions
        {
            HalfLengthMm = veneer ? (Skeleton.VeneerContactLength(size) - 2.08575f) * 0.5f : 21.1504f,
            HalfWidthMm = veneer ? 21.12959f : 21.15f,
            HalfThicknessMm = 0.5f
        };
    }

    public struct Placement
    {
        public string Model;      // "Veneer H7" | "Cap Side" | "Cap End"
        public Vector3 Center;    // world plate centre BEFORE thickness offset
        public Vector3 LengthDir; // world dir the part runs along (veneer/cap side)
        public Vector3 Normal;    // world outward dir the plate faces
    }

    public sealed class Result
    {
        public readonly List<Placement> Parts = new List<Placement>();
        public readonly List<string> Warnings = new List<string>();
        readonly HashSet<(string, int, int, int)> _seen = new HashSet<(string, int, int, int)>();

        internal void Add(string model, Vector3 center, Vector3 lengthDir, Vector3 normal)
        {
            // Dedupe by model + position (0.5 mm grid) so shared corners
            // never stack duplicate caps.
            var key = (model,
                Mathf.RoundToInt(center.x * 2000f),
                Mathf.RoundToInt(center.y * 2000f),
                Mathf.RoundToInt(center.z * 2000f));
            if (!_seen.Add(key))
                return;
            Parts.Add(new Placement { Model = model, Center = center, LengthDir = lengthDir, Normal = normal });
        }
    }

    /// <summary>A placed panel reduced to an oriented box (world units).</summary>
    public struct PanelBox
    {
        public Vector3 Center;
        public Vector3 Normal;   // plate normal
        public Vector3 AxisU;    // in-plane
        public Vector3 AxisV;    // in-plane
        public float HalfU;
        public float HalfV;
        public float HalfN;
    }

    // ------------------------------------------------------------------
    // Entry point
    // ------------------------------------------------------------------

    public static Result Plan(List<FrameOverlapResolver.FrameRecord> frames, List<PanelBox> panels,
                              Dictionary<string, PartDimensions> modelDimensions = null)
    {
        var result = new Result();
        if (frames == null || frames.Count == 0)
            return result;

        foreach (FrameOverlapResolver.FrameRecord frame in frames)
        {
            if (BeamPartUtility.IsVertical(frame.PartId))
            {
                PlanVEnds(frame, frames, panels, result, modelDimensions);
                PlanVChannels(frame, frames, panels, result, modelDimensions);
            }
            else
            {
                PlanHChannels(frame, frames, panels, result, modelDimensions);
            }
        }
        return result;
    }

    // ------------------------------------------------------------------
    // V frame ends (exposed Cap End, grounded Foot)
    // ------------------------------------------------------------------

    static void PlanVEnds(in FrameOverlapResolver.FrameRecord v,
                          List<FrameOverlapResolver.FrameRecord> all, List<PanelBox> panels,
                          Result result, Dictionary<string, PartDimensions> modelDimensions)
    {
        float halfBody = NeospaceUnits.Mm(Skeleton.VBodyLength(v.Size)) * 0.5f;
        Vector3 axis = v.LengthAxis;
        Vector3 topEnd = v.Center + axis * halfBody;
        Vector3 bottomEnd = v.Center - axis * halfBody;
        float tol = NeospaceUnits.Mm(TolMm);

        // Square end parts follow the post's own faces: a yawed post needs its
        // Cap End and Foot yawed with it, or their corners stick out past the
        // 41 mm profile. (A world-fixed perpendicular only matched posts on
        // the grid axes.)
        Vector3 across = FaceNormals(v)[0];

        if (!ContinuedBeyond(v, all, topEnd, axis, tol) && !RestsOnBeamFace(v, all, topEnd, axis))
            AddEndCapIfExposed(topEnd, across, axis, panels, result, modelDimensions);

        bool grounded = bottomEnd.y < tol;
        if (grounded)
        {
            // Normal points up. The controller seats the foot under the post
            // and lowers the visible floor without moving the build geometry.
            result.Add("Foot", bottomEnd, across, axis);
        }
        else if (!ContinuedBeyond(v, all, bottomEnd, -axis, tol) &&
                 !RestsOnBeamFace(v, all, bottomEnd, -axis))
        {
            AddEndCapIfExposed(bottomEnd, across, -axis, panels, result, modelDimensions);
        }
    }

    static void AddEndCapIfExposed(Vector3 end, Vector3 lengthDir, Vector3 outward,
        List<PanelBox> panels, Result result, Dictionary<string, PartDimensions> modelDimensions)
    {
        PartDimensions size = Dimensions("Cap End", modelDimensions);
        // Match Place: the actual cap sits beyond the post's body end by its
        // half thickness plus the 0.2 mm visual lift. A shelf can cover that
        // end even when no other frame continues past it.
        Vector3 center = end + outward * NeospaceUnits.Mm(size.HalfThicknessMm + 0.2f);
        Vector3 widthDir = Vector3.Cross(lengthDir, outward).normalized;
        if (panels != null)
            foreach (PanelBox panel in panels)
            {
                // A board parallel to the extrusion end masks the nominal
                // 41 mm end face, not the simplified cap's overhanging lip.
                // At a normal shelf corner the board edge is 20.6415 mm from
                // the post center: the 20.5 mm profile remains exposed even
                // though the wider cap mesh's envelope reaches into the seam,
                // and the same placement allowance as for channels keeps a
                // slightly off-grid board from stealing the cap. Keep actual
                // cap thickness/offset and retain full measured bounds for
                // panels crossing the end at other orientations.
                bool parallelEnd = Mathf.Abs(Vector3.Dot(panel.Normal.normalized, outward.normalized)) >= 0.9999f;
                float halfLengthMm = parallelEnd ? HalfProfileMm - SeamToleranceMm : size.HalfLengthMm;
                float halfWidthMm = parallelEnd ? HalfProfileMm - SeamToleranceMm : size.HalfWidthMm;
                if (FinishPanelMasking.Intersects(center, lengthDir, widthDir, outward,
                    NeospaceUnits.Mm(halfLengthMm), NeospaceUnits.Mm(halfWidthMm),
                    NeospaceUnits.Mm(size.HalfThicknessMm), panel))
                    return;
            }
        result.Add("Cap End", end, lengthDir, outward);
    }

    /// <summary>
    /// True when a post end PRESSES flush against the face of a horizontal
    /// beam from outside (stacked levels): the joint is a mounted connection,
    /// so no Cap End belongs between the two bodies. Direction matters — a
    /// side-entry post whose top merely sits flush BESIDE a beam is not
    /// resting on anything and keeps its cap.
    /// </summary>
    static bool RestsOnBeamFace(in FrameOverlapResolver.FrameRecord v,
                                List<FrameOverlapResolver.FrameRecord> all,
                                Vector3 end, Vector3 outward)
    {
        float tol = NeospaceUnits.Mm(TolMm);
        float halfProfile = NeospaceUnits.Mm(HalfProfileMm);
        foreach (FrameOverlapResolver.FrameRecord h in all)
        {
            if (h.Root == v.Root || BeamPartUtility.IsVertical(h.PartId))
                continue;
            Vector3 to = end - h.Center;
            float t = Vector3.Dot(to, h.LengthAxis);
            if (Mathf.Abs(t) > (h.EndB - h.EndA).magnitude * 0.5f + tol)
                continue;
            Vector3 off = to - h.LengthAxis * t;

            // The beam's face plane must lie just BEYOND the end (the beam
            // body on the far side), i.e. off ≈ -outward * halfProfile.
            float press = Vector3.Dot(off, outward);
            Vector3 lateral = off - outward * press;
            if (Mathf.Abs(press + halfProfile) <= tol && lateral.magnitude <= tol)
                return true;
        }
        return false;
    }

    /// <summary>
    /// True when another coaxial V frame occupies the space just past this
    /// body end (stacked posts) — capping between them would be wrong.
    /// </summary>
    static bool ContinuedBeyond(in FrameOverlapResolver.FrameRecord v,
                                List<FrameOverlapResolver.FrameRecord> all,
                                Vector3 end, Vector3 outward, float tol)
    {
        Vector3 probe = end + outward * tol * 2f;
        foreach (FrameOverlapResolver.FrameRecord other in all)
        {
            if (other.Root == v.Root || !BeamPartUtility.IsVertical(other.PartId))
                continue;
            Vector3 d = other.Center - end;
            d -= outward * Vector3.Dot(d, outward);
            if (d.magnitude > tol)
                continue; // not coaxial
            float halfBody = NeospaceUnits.Mm(Skeleton.VBodyLength(other.Size)) * 0.5f;
            float lo = Vector3.Dot(other.Center - probe, outward) - halfBody;
            float hi = Vector3.Dot(other.Center - probe, outward) + halfBody;
            if (lo <= 0f && hi >= 0f)
                return true;
        }
        return false;
    }

    // ------------------------------------------------------------------
    // V frame channels
    // ------------------------------------------------------------------

    static void PlanVChannels(in FrameOverlapResolver.FrameRecord v,
                              List<FrameOverlapResolver.FrameRecord> all,
                              List<PanelBox> panels, Result result,
                              Dictionary<string, PartDimensions> modelDimensions)
    {
        int n = v.Size;
        Vector3 axis = v.LengthAxis;
        Vector3[] faceNormals = FaceNormals(v);

        // The (level, face) each joint occupies.
        var joints = new HashSet<(int level, int face)>();
        float tol = NeospaceUnits.Mm(TolMm);
        float module = NeospaceUnits.ModuleMeters;

        foreach (FrameOverlapResolver.FrameRecord h in all)
        {
            if (h.Root == v.Root || BeamPartUtility.IsVertical(h.PartId))
                continue;
            for (int e = 0; e < 2; e++)
            {
                // Twist Peg A attaches to an H, never a V channel. Counting it
                // as a V joint stripped finish from the attached frame.
                if (e == 0 && BeamPartUtility.IsTwist(h.PartId))
                    continue;
                Vector3 end = e == 0 ? h.EndA : h.EndB;
                float off = Vector3.Dot(end - v.Center, axis);
                int i = Mathf.RoundToInt(off / module + (n - 1) / 2f);
                if (i < 0 || i > n - 1)
                    continue;
                Vector3 hole = v.Center + axis * HoleOffset(i, n);
                // Prefab peg markers sit near the post FACE (about 20 mm
                // from its axis), not necessarily at the hole's centreline.
                // Accept a tip entering that face, while still requiring it
                // to line up with the actual hole in both transverse axes.
                // Otherwise a valid split-frame joint gets capped/veneered.
                int face = DominantFace(h.Center - hole, faceNormals);
                Vector3 delta = end - hole;
                float depth = Vector3.Dot(delta, faceNormals[face]);
                Vector3 across = delta - faceNormals[face] * depth;
                if (depth < -tol || depth > NeospaceUnits.Mm(HalfProfileMm) + tol || across.magnitude > tol)
                    continue;
                joints.Add((i, face));
            }
        }

        var masked = MaskedRanges(v, axis, faceNormals, panels);

        for (int fi = 0; fi < 4; fi++)
        {
            Vector3 fn = faceNormals[fi];
            var mask = new ChannelMask(v.Center, axis, fn, fi, masked, panels, modelDimensions);

            // Dividers follow the Rhino NSFINISH rule: a connection point is
            // a position on the post, not a hole, so every connection level
            // divides all four channels — Cap Sides ring the post at each
            // level (except on the face the joint itself occupies) and the
            // veneer joints line up around it. The joints on THIS face are
            // mandatory dividers; the levels of the other faces are optional
            // ones that a free-placing Unity build may have to skip: two
            // levels one module apart (a placement Rhino would have refused)
            // would leave an uncoverable 1-module section between two caps,
            // so the optional divider gives way and the veneer runs on.
            var mandatory = new SortedSet<int> { 0, n - 1 };
            var optional = new SortedSet<int>();
            foreach ((int level, int face) in joints)
            {
                if (face == fi)
                    mandatory.Add(level);
                else if (SplitAllChannelsAtConnections)
                    optional.Add(level);
            }
            List<int> dividers = DividerLevels(mandatory, optional);

            // Veneers first: each cap exists to seat the veneer runs meeting
            // at its hole, so caps are decided after we know which veneers
            // actually landed. A cap with no veneer on either side (both
            // hidden behind boards) would float alone in the seam between
            // two panels — physically nothing sits there.
            var capHoles = new SortedSet<int>(dividers);
            var seated = new HashSet<int>();

            var runs = new List<(int start, int size)>();
            for (int s = 0; s + 1 < dividers.Count; s++)
            {
                int a = dividers[s], b = dividers[s + 1];
                if (!TileSection(a, b, runs, capHoles, (pos, size) =>
                    !mask.Overlaps($"Veneer H{size}",
                        (HoleOffsetMm(pos, n) + HoleOffsetMm(pos + size + 1, n)) * 0.5f)))
                {
                    result.Warnings.Add($"{v.PartId}: a {b - a}-module channel section has no veneer combination.");
                    continue;
                }

                foreach ((int pos, int m) in runs)
                {
                    float centreMm = (HoleOffsetMm(pos, n) + HoleOffsetMm(pos + m + 1, n)) * 0.5f;
                    result.Add($"Veneer H{m}", FacePoint(v.Center, axis, fn, centreMm), axis, fn);
                    seated.Add(pos);
                    seated.Add(pos + m + 1);
                }

                // A bare 1-interval section has no veneer to seat its caps:
                // seat both holes so the caps still close the lone module.
                if (b - a == 1)
                {
                    seated.Add(a);
                    seated.Add(b);
                }
            }

            // A V1 is all divider and no section: its lone hole takes a Cap
            // Side on every free face (as in Rhino, where caps go at every
            // divider) — the seated rule can't apply with no veneers at all.
            bool lone = dividers.Count == 1;

            foreach (int d in capHoles)
            {
                if (joints.Contains((d, fi)) || (!lone && !seated.Contains(d)))
                    continue;
                float offMm = HoleOffsetMm(d, n);
                if (mask.Overlaps("Cap Side", offMm))
                    continue;
                Vector3 capCenter = FacePoint(v.Center, axis, fn, offMm);
                result.Add("Cap Side", capCenter, axis, fn);
            }
        }
    }

    // ------------------------------------------------------------------
    // H / HT / T channels (default coverage)
    // ------------------------------------------------------------------

    static void PlanHChannels(in FrameOverlapResolver.FrameRecord h,
                              List<FrameOverlapResolver.FrameRecord> all,
                              List<PanelBox> panels, Result result,
                              Dictionary<string, PartDimensions> modelDimensions)
    {
        int intervals = h.Size + 1;
        Vector3 axis = h.LengthAxis;
        Vector3[] faceNormals = FaceNormals(h);
        var masked = MaskedRanges(h, axis, faceNormals, panels);

        float BoundaryMm(int j) => (j - intervals / 2f) * CatalogueData.ModuleMm;

        // Stacked joints: a V post standing ON this beam (post bottom on our
        // top face) or supporting it (post top under our bottom face) sits at
        // a module boundary — the covering must break there, exactly like a
        // V channel breaks at its connection levels. Without this, top and
        // bottom face veneers run straight through the posts of stacked
        // levels (the everyday case when Space pieces sit on each other).
        var joints = new HashSet<(int boundary, int face)>();
        float tol = NeospaceUnits.Mm(TolMm);
        float halfProfile = NeospaceUnits.Mm(HalfProfileMm);
        foreach (FrameOverlapResolver.FrameRecord v in all)
        {
            if (v.Root == h.Root || !BeamPartUtility.IsVertical(v.PartId))
                continue;
            float halfBody = NeospaceUnits.Mm(Skeleton.VBodyLength(v.Size)) * 0.5f;
            for (int e = 0; e < 2; e++)
            {
                Vector3 outward = e == 0 ? -Vector3.up : Vector3.up;
                Vector3 end = v.Center + outward * halfBody;
                Vector3 toEnd = end - h.Center;
                float t = Vector3.Dot(toEnd, axis);
                Vector3 off = toEnd - axis * t;

                // The post end must PRESS on the face it approaches (the face
                // opposing the end's outward direction) — a side-entry post
                // sitting flush beside the beam is a channel joint, not a
                // stacked one, and must not split this face's covering.
                int fi = DominantFace(-outward, faceNormals);
                float along = Vector3.Dot(off, faceNormals[fi]);
                Vector3 lateral = off - faceNormals[fi] * along;
                if (Mathf.Abs(along - halfProfile) > tol || lateral.magnitude > tol)
                    continue;

                // …at an interior module boundary of the span.
                int j = Mathf.RoundToInt(t / NeospaceUnits.ModuleMeters + intervals / 2f);
                if (j <= 0 || j >= intervals)
                    continue;
                if (Mathf.Abs(BoundaryMm(j) - NeospaceUnits.ToMm(t)) > TolMm)
                    continue;
                joints.Add((j, fi));
            }
        }

        // A connector's peg plugging into one of this beam's side-channel
        // holes — a regular H from either end, a twist from Peg A (Peg B only
        // ever enters a post) — presses its body on that face around the
        // ring exactly like a stacked post, so the covering must break there
        // too. Without this, the channel veneer runs straight through the
        // mounted beam. Chained connectors on the same line share no channel
        // and are excluded by the axis test.
        foreach (FrameOverlapResolver.FrameRecord o in all)
        {
            if (o.Root == h.Root || BeamPartUtility.IsVertical(o.PartId))
                continue;
            if (Mathf.Abs(Vector3.Dot(o.LengthAxis, axis)) > 0.7f)
                continue;
            int ends = BeamPartUtility.IsTwist(o.PartId) ? 1 : 2;
            for (int e = 0; e < ends; e++)
            {
                Vector3 end = e == 0 ? o.EndA : o.EndB;
                Vector3 toEnd = end - h.Center;
                float tAlong = Vector3.Dot(toEnd, axis);
                Vector3 off = toEnd - axis * tAlong;
                if (off.magnitude > halfProfile + tol)
                    continue; // peg tip must reach into this beam's profile
                int j = Mathf.RoundToInt(tAlong / NeospaceUnits.ModuleMeters + intervals / 2f);
                if (j <= 0 || j >= intervals)
                    continue;
                if (Mathf.Abs(BoundaryMm(j) - NeospaceUnits.ToMm(tAlong)) > TolMm)
                    continue;
                joints.Add((j, DominantFace(o.Center - end, faceNormals)));
            }
        }

        for (int fi = 0; fi < 4; fi++)
        {
            Vector3 fn = faceNormals[fi];
            var mask = new ChannelMask(h.Center, axis, fn, fi, masked, panels, modelDimensions);

            var dividers = new SortedSet<int> { 0, intervals };
            foreach ((int boundary, int face) in joints)
                if (face == fi)
                    dividers.Add(boundary);
            var divList = new List<int>(dividers);

            // Veneers first, caps after — a mid cap only appears where a
            // placed veneer actually meets it (see PlanVChannels: a cap whose
            // neighbours are all behind boards would float alone in the seam
            // between two panels).
            var capBoundaries = new SortedSet<int>();
            var seated = new HashSet<int>();

            var runs = new List<(int start, int size)>();
            for (int s = 0; s + 1 < divList.Count; s++)
            {
                int a = divList[s], b = divList[s + 1];
                if (!TileSection(a, b, runs, capBoundaries, (pos, size) =>
                    !mask.Overlaps($"Veneer H{size}",
                        (BoundaryMm(pos) + BoundaryMm(pos + size + 1)) * 0.5f)))
                {
                    result.Warnings.Add($"{h.PartId}: a {b - a}-module body section has no veneer combination.");
                    continue;
                }

                foreach ((int pos, int m) in runs)
                {
                    float centreMm = (BoundaryMm(pos) + BoundaryMm(pos + m + 1)) * 0.5f;
                    result.Add($"Veneer H{m}", FacePoint(h.Center, axis, fn, centreMm), axis, fn);
                    seated.Add(pos);
                    seated.Add(pos + m + 1);
                }

                // A bare 1-interval section has no veneer to seat its caps:
                // seat both boundaries so a mid cap can still close it (body
                // ends and joint boundaries stay filtered below).
                if (b - a == 1)
                {
                    seated.Add(a);
                    seated.Add(b);
                }
            }

            foreach (int pos in capBoundaries)
            {
                if (pos <= 0 || pos >= intervals)
                    continue; // beam body ends are joints, never capped
                if (joints.Contains((pos, fi)) || !seated.Contains(pos))
                    continue;
                if (mask.Overlaps("Cap Side", BoundaryMm(pos)))
                    continue;
                result.Add("Cap Side", FacePoint(h.Center, axis, fn, BoundaryMm(pos)), axis, fn);
            }
        }
    }

    /// <summary>
    /// Sorted divider holes of one channel: every mandatory level, plus each
    /// optional level that leaves at least two modules to its neighbours on
    /// both sides (a single module has no veneer, so such a divider would
    /// only add a bare strip between two caps). Optional levels are taken in
    /// ascending order, so of two adjacent ones the lower survives.
    /// </summary>
    static List<int> DividerLevels(SortedSet<int> mandatory, SortedSet<int> optional)
    {
        var kept = new SortedSet<int>(mandatory);
        foreach (int level in optional)
        {
            if (kept.Contains(level))
                continue;
            int prev = int.MinValue, next = int.MaxValue;
            foreach (int k in kept)
            {
                if (k < level) prev = k;
                else { next = k; break; }
            }
            if (level - prev >= 2 && next - level >= 2)
                kept.Add(level);
        }
        return new List<int>(kept);
    }

    // ------------------------------------------------------------------
    // Section tiling
    // ------------------------------------------------------------------

    /// <summary>
    /// Tile exposed portions of [a,b] with real catalogue parts. Test each
    /// candidate's entire plate before choosing it, so a panel in the middle
    /// splits the run instead of discarding a long veneer and its exposed
    /// remainder. Never shorten or stretch an FBX to fit a nonmodular gap.
    /// </summary>
    static bool TileSection(int a, int b, List<(int start, int size)> runs,
                            SortedSet<int> capHoles, System.Func<int, int, bool> canPlace)
    {
        runs.Clear();
        int L = b - a;

        if (L == 1)
        {
            capHoles.Add(a);
            capHoles.Add(b);
            return true;
        }

        List<int> sizes = Finishing.CoverSegment(L);
        if (sizes != null)
        {
            // Keep the catalogue's fewest/balanced-part preference when its
            // complete run is exposed (the normal unpanelled case).
            int pos = a;
            bool clear = true;
            foreach (int size in sizes)
            {
                if (!canPlace(pos, size)) { clear = false; break; }
                pos += size + 1;
            }
            if (clear)
            {
                AppendRuns(a, sizes, runs, capHoles);
                return true;
            }
        }

        if (L < 2)
            return false;

        // Dynamic programming over hole boundaries. Skipping a masked
        // interval is allowed; maximize coverage, then minimize part count
        // and prefer balanced sizes. This also retains separate exposed
        // islands between multiple panels and handles off-grid board edges.
        var covered = new int[L + 1];
        var count = new int[L + 1];
        var imbalance = new int[L + 1];
        var chosen = new int[L];
        for (int i = L - 1; i >= 0; i--)
        {
            chosen[i] = -1;
            covered[i] = covered[i + 1];
            count[i] = count[i + 1];
            imbalance[i] = imbalance[i + 1];
            foreach (int size in CatalogueData.VeneerLengths)
            {
                int span = size + 1;
                if (i + span > L || !canPlace(a + i, size)) continue;
                int c = span + covered[i + span];
                int n = 1 + count[i + span];
                int balance = span * span + imbalance[i + span];
                if (c < covered[i] || (c == covered[i] && n > count[i]) ||
                    (c == covered[i] && n == count[i] && balance >= imbalance[i])) continue;
                covered[i] = c;
                count[i] = n;
                imbalance[i] = balance;
                chosen[i] = size;
            }
        }
        for (int i = 0; i < L;)
        {
            int size = chosen[i];
            if (size < 0) { i++; continue; }
            runs.Add((a + i, size));
            capHoles.Add(a + i);
            i += size + 1;
            capHoles.Add(a + i);
        }
        return true;
    }

    static void AppendRuns(int start, List<int> sizes,
                           List<(int start, int size)> runs, SortedSet<int> capHoles)
    {
        int pos = start;
        for (int k = 0; k < sizes.Count; k++)
        {
            capHoles.Add(pos);
            runs.Add((pos, sizes[k]));
            pos += sizes[k] + 1;
            capHoles.Add(pos);
        }
    }

    /// <summary>
    /// A face's channel coverage plus physical transverse obstructions.
    /// A board beside the frame does not occupy its exposed coplanar faces:
    /// the simplified plates' wider lips must not erase an entire channel.
    /// </summary>
    sealed class ChannelMask
    {
        readonly Vector3 _center, _axis, _normal;
        readonly int _face;
        readonly Dictionary<int, List<(float lo, float hi)>> _semantic;
        readonly List<PanelBox> _panels;
        readonly Dictionary<string, PartDimensions> _dimensions;
        readonly Dictionary<string, List<(float lo, float hi)>> _physical =
            new Dictionary<string, List<(float, float)>>();

        public ChannelMask(Vector3 center, Vector3 axis, Vector3 normal, int face,
            Dictionary<int, List<(float lo, float hi)>> semantic, List<PanelBox> panels,
            Dictionary<string, PartDimensions> dimensions)
        {
            _center = center; _axis = axis; _normal = normal; _face = face;
            _semantic = semantic; _panels = panels; _dimensions = dimensions;
        }

        public bool Overlaps(string model, float offsetMm)
        {
            PartDimensions size = Dimensions(model, _dimensions);
            if (MaskedAt(_semantic, _face, offsetMm, size.HalfLengthMm)) return true;
            if (_panels == null || _panels.Count == 0) return false;
            if (!_physical.TryGetValue(model, out var ranges))
            {
                ranges = new List<(float, float)>();
                // Must match FinishController.Place's thickness offset.
                Vector3 origin = _center + _normal *
                    NeospaceUnits.Mm(HalfProfileMm + size.HalfThicknessMm + 0.2f);
                Vector3 width = Vector3.Cross(_axis, _normal).normalized;
                foreach (PanelBox panel in _panels)
                {
                    if (IsAdjacentPanelSeam(panel, width)) continue;
                    if (FinishPanelMasking.TrySweep(origin, _axis, width, _normal,
                        NeospaceUnits.Mm(size.HalfWidthMm), NeospaceUnits.Mm(size.HalfThicknessMm),
                        panel, out float lo, out float hi))
                        ranges.Add((NeospaceUnits.ToMm(lo), NeospaceUnits.ToMm(hi)));
                }
                _physical.Add(model, ranges);
            }
            foreach (var range in ranges)
                if (offsetMm + size.HalfLengthMm > range.lo + MaskTolMm &&
                    offsetMm - size.HalfLengthMm < range.hi - MaskTolMm) return true;
            return false;
        }

        bool IsAdjacentPanelSeam(PanelBox panel, Vector3 width)
        {
            // E.g. the top of an H beam beside a shelf. Only a face parallel
            // to the board can have this seam; a shelf crossing a V post is
            // perpendicular to its channel faces and still uses full SAT.
            if (Mathf.Abs(Vector3.Dot(panel.Normal, _normal)) < 0.999f) return false;

            float panelHalfWidth = Mathf.Abs(Vector3.Dot(panel.AxisU, width)) * panel.HalfU +
                                   Mathf.Abs(Vector3.Dot(panel.AxisV, width)) * panel.HalfV +
                                   Mathf.Abs(Vector3.Dot(panel.Normal, width)) * panel.HalfN;
            float nearEdge = Mathf.Abs(Vector3.Dot(panel.Center - _center, width)) - panelHalfWidth;

            // The board is beside the actual 41 mm frame footprint (within
            // the placement allowance). Its boundary can graze the 42.259 mm
            // simplified veneer lip; that does not hide the top/bottom
            // channel. A board extending over the frame footprint remains a
            // real physical obstruction.
            return nearEdge >= NeospaceUnits.Mm(HalfProfileMm - SeamToleranceMm);
        }
    }

    // ------------------------------------------------------------------
    // Geometry helpers
    // ------------------------------------------------------------------

    static float HoleOffsetMm(int i, int n) => (i - (n - 1) / 2f) * CatalogueData.ModuleMm;

    static float HoleOffset(int i, int n) => NeospaceUnits.Mm(HoleOffsetMm(i, n));

    /// <summary>World point on a channel face at a length offset (mm).</summary>
    static Vector3 FacePoint(Vector3 center, Vector3 axis, Vector3 faceNormal, float offsetMm)
    {
        return center + faceNormal * NeospaceUnits.Mm(HalfProfileMm) + axis * NeospaceUnits.Mm(offsetMm);
    }

    /// <summary>
    /// The four channel-face normals of a frame. V posts use their yaw axes;
    /// beams use local up (roll) and the horizontal perpendicular.
    /// </summary>
    static Vector3[] FaceNormals(in FrameOverlapResolver.FrameRecord frame)
    {
        Vector3 axis = frame.LengthAxis;
        Vector3 a, b;
        if (BeamPartUtility.IsVertical(frame.PartId))
        {
            a = frame.Root != null ? frame.Root.right : Vector3.right;
            a -= axis * Vector3.Dot(a, axis);
            if (a.sqrMagnitude < 1e-6f)
                a = Vector3.right;
            a.Normalize();
            b = Vector3.Cross(axis, a).normalized;
        }
        else
        {
            a = frame.LocalY;
            a -= axis * Vector3.Dot(a, axis);
            if (a.sqrMagnitude < 1e-6f)
                a = Vector3.up;
            a.Normalize();
            b = Vector3.Cross(axis, a).normalized;
        }
        return new[] { a, -a, b, -b };
    }

    static int DominantFace(Vector3 direction, Vector3[] faceNormals)
    {
        int best = 0;
        float bestDot = float.NegativeInfinity;
        for (int i = 0; i < faceNormals.Length; i++)
        {
            float d = Vector3.Dot(direction, faceNormals[i]);
            if (d > bestDot)
            {
                bestDot = d;
                best = i;
            }
        }
        return best;
    }

    // ------------------------------------------------------------------
    // Panel masking
    // ------------------------------------------------------------------

    /// <summary>
    /// Per channel face, the length-offset ranges (mm from the frame centre)
    /// hidden by a panel. A frame bordering a panelled space masks only the
    /// stretch of its inward channel spanning that panel — never the whole
    /// channel — so finishing on the rest of the same channel survives.
    /// </summary>
    static Dictionary<int, List<(float lo, float hi)>> MaskedRanges(
        in FrameOverlapResolver.FrameRecord frame, Vector3 axis,
        Vector3[] faceNormals, List<PanelBox> panels)
    {
        var ranges = new Dictionary<int, List<(float, float)>>();
        if (panels == null)
            return ranges;

        float halfSpan = (frame.EndB - frame.EndA).magnitude * 0.5f +
                         NeospaceUnits.Mm(CatalogueData.ProfileMm);

        foreach (PanelBox p in panels)
        {
            // The panel plane must run along the frame.
            if (Mathf.Abs(Vector3.Dot(p.Normal, axis)) > 0.3f)
                continue;

            // The board sits flush on (or within) the frame profile: its
            // plane may be offset up to ~profile width from the frame line.
            Vector3 toPanel = p.Center - frame.Center;
            float planeDist = Mathf.Abs(Vector3.Dot(toPanel, p.Normal));
            if (planeDist > NeospaceUnits.Mm(CatalogueData.ProfileMm + TolMm))
                continue;

            // In-plane axes: one runs with the frame, the other away from it.
            Vector3 axisU, axisV;
            float halfU, halfV;
            if (Mathf.Abs(Vector3.Dot(p.AxisU, axis)) >= Mathf.Abs(Vector3.Dot(p.AxisV, axis)))
            {
                axisU = p.AxisU; halfU = p.HalfU;
                axisV = p.AxisV; halfV = p.HalfV;
            }
            else
            {
                axisU = p.AxisV; halfU = p.HalfV;
                axisV = p.AxisU; halfV = p.HalfU;
            }
            if (Mathf.Abs(Vector3.Dot(axisU, axis)) < 0.7f)
                continue; // panel is skewed to this frame — not a border

            // The frame must border the panel: the perpendicular in-plane
            // distance from the frame line to the panel centre is about the
            // panel half-extent (the panel's edge runs along the frame).
            float perpDist = Mathf.Abs(Vector3.Dot(toPanel, axisV));
            if (Mathf.Abs(perpDist - halfV) > NeospaceUnits.Mm(60f))
                continue;

            // Length range behind the panel, in mm from the frame centre.
            float tC = Vector3.Dot(toPanel, axis);
            if (Mathf.Abs(tC) - halfU > halfSpan)
                continue; // does not touch this frame's span

            Vector3 inward = toPanel - axis * tC;
            if (inward.magnitude < NeospaceUnits.Mm(TolMm))
                continue;

            // Catalogue boards always mount flush INSIDE the channel (their
            // outer skin level with the profile face) — the only channel a
            // board occupies is the one its edge slides into: the face
            // pointing from the frame line toward the board. Top/bottom
            // faces of the bordering frames stay free.
            int fi = DominantFace(inward, faceNormals);
            float loMm = NeospaceUnits.ToMm(tC - halfU);
            float hiMm = NeospaceUnits.ToMm(tC + halfU);
            if (!ranges.TryGetValue(fi, out List<(float, float)> list))
            {
                list = new List<(float, float)>();
                ranges[fi] = list;
            }
            list.Add((loMm, hiMm));
        }
        return ranges;
    }

    /// <summary>
    /// True when a part centred at <paramref name="offsetMm"/> whose plate
    /// runs <paramref name="halfExtentMm"/> each way overlaps a masked range
    /// on this face. Span-aware: a cap centred just OUTSIDE a board still
    /// physically reaches into it, so the whole body is tested, not the
    /// centre point. Flush contact (within tolerance) is allowed.
    /// </summary>
    static bool MaskedAt(Dictionary<int, List<(float lo, float hi)>> ranges, int face,
                         float offsetMm, float halfExtentMm = 0f)
    {
        if (!ranges.TryGetValue(face, out List<(float lo, float hi)> list))
            return false;
        foreach ((float lo, float hi) in list)
            if (offsetMm + halfExtentMm > lo + MaskTolMm &&
                offsetMm - halfExtentMm < hi - MaskTolMm)
                return true;
        return false;
    }

    // ------------------------------------------------------------------
    // Panel collection
    // ------------------------------------------------------------------

    /// <summary>All placed panels reduced to oriented boxes (ghosts skipped).</summary>
    public static List<PanelBox> CollectPanels(int ghostLayerMask)
    {
        var result = new List<PanelBox>();
        foreach (PanelInstance pi in Object.FindObjectsByType<PanelInstance>(FindObjectsSortMode.None))
        {
            if (pi == null || !pi.gameObject.activeInHierarchy)
                continue;
            if ((ghostLayerMask & (1 << pi.gameObject.layer)) != 0)
                continue;

            if (TryPanelBox(pi.transform, out PanelBox box))
                result.Add(box);
        }
        return result;
    }

    /// <summary>
    /// Oriented box for one panel object: transform axes give the plate
    /// basis, mesh-local bounds give the extents. Works for live build panels
    /// and for frozen panel children inside Space piece instances alike.
    /// </summary>
    public static bool TryPanelBox(Transform t, out PanelBox box)
    {
        box = default;

        Renderer[] rends = t.GetComponentsInChildren<Renderer>(false);
        if (rends.Length == 0)
            return false;

        Vector3 nrm = t.forward.normalized;
        Vector3 u = t.right.normalized;
        Vector3 v = t.up.normalized;
        Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 max = -min;
        bool found = false;
        foreach (Renderer renderer in rends)
        {
            if (!renderer.enabled) continue;
            // Never reproject Renderer.bounds: it is already a world AABB,
            // which fattens a rotated 1 mm board into a large solid volume.
            Bounds local = renderer.localBounds;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3(sx, sy, sz));
                Vector3 d = renderer.transform.TransformPoint(corner) - t.position;
                Vector3 projected = new Vector3(Vector3.Dot(d, u), Vector3.Dot(d, v), Vector3.Dot(d, nrm));
                min = Vector3.Min(min, projected);
                max = Vector3.Max(max, projected);
                found = true;
            }
        }
        if (!found) return false;
        Vector3 centre = (min + max) * 0.5f;
        Vector3 half = (max - min) * 0.5f;

        box = new PanelBox
        {
            Center = t.position + u * centre.x + v * centre.y + nrm * centre.z,
            Normal = nrm,
            AxisU = u,
            AxisV = v,
            HalfU = half.x,
            HalfV = half.y,
            HalfN = half.z
        };
        return true;
    }

    // ------------------------------------------------------------------
    // Space Mode collection (frozen parts inside piece instances)
    // ------------------------------------------------------------------

    const string MergedBeamPrefix = "Merged_";

    /// <summary>
    /// Frame records for the CURRENT physical frame configuration of the
    /// space: every active part inside every placed piece instance, PLUS the
    /// merge-derived split segments ("Merged_H7" etc.). The merge solver
    /// deactivates originals it split or deduplicated and spawns their
    /// replacements under <see cref="SpaceMerge.DerivedRoot"/> — reading both
    /// sides is what makes the finish match the post-merge reality.
    /// </summary>
    public static List<FrameOverlapResolver.FrameRecord> CollectSpaceFrames()
    {
        var result = new List<FrameOverlapResolver.FrameRecord>();
        foreach (SpaceInstance inst in Object.FindObjectsByType<SpaceInstance>(FindObjectsSortMode.None))
        {
            if (inst == null || !inst.gameObject.activeInHierarchy)
                continue;
            foreach (Transform child in inst.transform)
            {
                if (!child.gameObject.activeSelf)
                    continue;
                if (FrameOverlapResolver.TryBuildRecord(child, out FrameOverlapResolver.FrameRecord rec))
                    result.Add(rec);
            }
        }

        Transform derived = SpaceMerge.DerivedRoot;
        if (derived != null && derived.gameObject.activeInHierarchy)
        {
            foreach (Transform child in derived)
            {
                if (!child.gameObject.activeSelf)
                    continue;
                if (!child.name.StartsWith(MergedBeamPrefix, System.StringComparison.Ordinal))
                    continue;
                string id = StructureClipboard.CleanPartId(child.name.Substring(MergedBeamPrefix.Length));
                if (id != null &&
                    FrameOverlapResolver.TryBuildRecord(child, id, out FrameOverlapResolver.FrameRecord rec))
                    result.Add(rec);
            }
        }
        return result;
    }

    /// <summary>
    /// Panel boxes for every panel physically present in the space: active
    /// panels inside piece instances (frozen clones that kept their
    /// "Panel ..." names) plus the merge-derived re-cut pieces
    /// ("MergedPanel") that replace panels the merge divided.
    /// </summary>
    public static List<PanelBox> CollectSpacePanels()
    {
        var result = new List<PanelBox>();
        foreach (SpaceInstance inst in Object.FindObjectsByType<SpaceInstance>(FindObjectsSortMode.None))
        {
            if (inst == null || !inst.gameObject.activeInHierarchy)
                continue;
            foreach (Transform child in inst.transform)
            {
                if (!child.gameObject.activeSelf || !child.name.Contains("Panel"))
                    continue;
                if (TryPanelBox(child, out PanelBox box))
                    result.Add(box);
            }
        }

        Transform derived = SpaceMerge.DerivedRoot;
        if (derived != null && derived.gameObject.activeInHierarchy)
        {
            foreach (Transform child in derived)
            {
                if (!child.gameObject.activeSelf || !child.name.Contains("Panel"))
                    continue;
                if (TryPanelBox(child, out PanelBox box))
                    result.Add(box);
            }
        }
        return result;
    }
}
