using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plans the finishing parts (veneers, Cap Sides, Cap Ends, Feet) a placed
/// build needs, ported from Rhino neospace_rhino/finishing_gen.py with the
/// owner's current rules: one veneer model per size (no chamfer variants),
/// and a Foot wrapping every grounded post bottom.
///
/// Planning is pure — it reads frame/panel records and emits placements.
/// <see cref="FinishController"/> turns placements into scene objects.
///
/// Per frame:
///  - V posts: Cap End on the top body end (and on a floating bottom).
///    Each of the four side channels is divided at the two end holes plus
///    every hole a connector plugs into; a Cap Side sits at each divider
///    (unless the joint itself occupies that face, or a panel hides it) and
///    veneers fill the sections between, split by a mid Cap Side where one
///    veneer cannot reach.
///  - H/HT/T beams: each of the four channels gets default coverage across
///    the body — one veneer, or two with a mid Cap Side. Beam ends are
///    joints, never capped.
///  - Panel masking: a channel facing into a panelled space is skipped over
///    the length range actually behind the panel, never the whole channel.
/// </summary>
public static class FinishGenerator
{
    /// <summary>Half the frame profile: channel faces sit here (mm).</summary>
    const float HalfProfileMm = 20.5f;

    /// <summary>Position tolerance for joints/holes, generous for meshes.</summary>
    const float TolMm = 6f;

    /// <summary>Masking tolerance along a channel (Rhino used 0.5 mm).</summary>
    const float MaskTolMm = 1f;

    /// <summary>
    /// Physical plate half-lengths along the channel, for the span-aware
    /// board mask. A veneer's plate is its catalogue contact length
    /// ((m+1) modules minus one profile), leaving a profile-wide seat at
    /// each end hole — which is exactly the Cap Side's width.
    /// </summary>
    const float CapHalfMm = CatalogueData.HalfProfileMm;
    static float VeneerHalfMm(int m) => Skeleton.VeneerContactLength(m) * 0.5f;

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

    public static Result Plan(List<FrameOverlapResolver.FrameRecord> frames, List<PanelBox> panels)
    {
        var result = new Result();
        if (frames == null || frames.Count == 0)
            return result;

        foreach (FrameOverlapResolver.FrameRecord frame in frames)
        {
            if (BeamPartUtility.IsVertical(frame.PartId))
            {
                PlanVEnds(frame, frames, result);
                PlanVChannels(frame, frames, panels, result);
            }
            else
            {
                PlanHChannels(frame, frames, panels, result);
            }
        }
        return result;
    }

    // ------------------------------------------------------------------
    // V frame ends (Cap End; Foot reserved for later)
    // ------------------------------------------------------------------

    static void PlanVEnds(in FrameOverlapResolver.FrameRecord v,
                          List<FrameOverlapResolver.FrameRecord> all, Result result)
    {
        float halfBody = NeospaceUnits.Mm(Skeleton.VBodyLength(v.Size)) * 0.5f;
        Vector3 axis = v.LengthAxis;
        Vector3 topEnd = v.Center + axis * halfBody;
        Vector3 bottomEnd = v.Center - axis * halfBody;
        float tol = NeospaceUnits.Mm(TolMm);

        if (!ContinuedBeyond(v, all, topEnd, axis, tol) && !RestsOnBeamFace(v, all, topEnd, axis))
            result.Add("Cap End", topEnd, PerpendicularOf(axis), axis);

        bool grounded = bottomEnd.y < tol;
        if (grounded)
        {
            // Normal points up: the foot wraps the post's lowest stretch
            // (Unity posts sit on the floor; nothing lifts them 10 mm).
            result.Add("Foot", bottomEnd, PerpendicularOf(axis), axis);
        }
        else if (!ContinuedBeyond(v, all, bottomEnd, -axis, tol) &&
                 !RestsOnBeamFace(v, all, bottomEnd, -axis))
        {
            result.Add("Cap End", bottomEnd, PerpendicularOf(axis), -axis);
        }
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
                              List<PanelBox> panels, Result result)
    {
        int n = v.Size;
        Vector3 axis = v.LengthAxis;
        Vector3[] faceNormals = FaceNormals(v);

        // Connection levels and the (level, face) each joint occupies.
        var levels = new SortedSet<int>();
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
                if ((hole - end).magnitude > tol)
                    continue;
                levels.Add(i);
                joints.Add((i, DominantFace(h.Center - end, faceNormals)));
            }
        }

        List<int> dividers = Finishing.DividerLevels(n - 1, levels);
        var masked = MaskedRanges(v, axis, faceNormals, panels);

        for (int fi = 0; fi < 4; fi++)
        {
            Vector3 fn = faceNormals[fi];

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
                if (!TileSection(a, b, runs, capHoles))
                {
                    result.Warnings.Add($"{v.PartId}: a {b - a}-module channel section has no veneer combination.");
                    continue;
                }

                foreach ((int pos, int m) in runs)
                {
                    float centreMm = (HoleOffsetMm(pos, n) + HoleOffsetMm(pos + m + 1, n)) * 0.5f;
                    if (!MaskedAt(masked, fi, centreMm, VeneerHalfMm(m)))
                    {
                        result.Add($"Veneer H{m}", FacePoint(v.Center, axis, fn, centreMm), axis, fn);
                        seated.Add(pos);
                        seated.Add(pos + m + 1);
                    }
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
                if (MaskedAt(masked, fi, offMm, CapHalfMm))
                    continue;
                Vector3 capCenter = FacePoint(v.Center, axis, fn, offMm);
                if (InPanelDressedBand(capCenter, fn, v, panels))
                    continue;
                result.Add("Cap Side", capCenter, axis, fn);
            }
        }
    }

    // ------------------------------------------------------------------
    // H / HT / T channels (default coverage)
    // ------------------------------------------------------------------

    static void PlanHChannels(in FrameOverlapResolver.FrameRecord h,
                              List<FrameOverlapResolver.FrameRecord> all,
                              List<PanelBox> panels, Result result)
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

        for (int fi = 0; fi < 4; fi++)
        {
            Vector3 fn = faceNormals[fi];

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
                if (!TileSection(a, b, runs, capBoundaries))
                {
                    result.Warnings.Add($"{h.PartId}: a {b - a}-module body section has no veneer combination.");
                    continue;
                }

                foreach ((int pos, int m) in runs)
                {
                    float centreMm = (BoundaryMm(pos) + BoundaryMm(pos + m + 1)) * 0.5f;
                    if (!MaskedAt(masked, fi, centreMm, VeneerHalfMm(m)))
                    {
                        result.Add($"Veneer H{m}", FacePoint(h.Center, axis, fn, centreMm), axis, fn);
                        seated.Add(pos);
                        seated.Add(pos + m + 1);
                    }
                }
            }

            foreach (int pos in capBoundaries)
            {
                if (pos <= 0 || pos >= intervals)
                    continue; // beam body ends are joints, never capped
                if (joints.Contains((pos, fi)) || !seated.Contains(pos))
                    continue;
                if (MaskedAt(masked, fi, BoundaryMm(pos), CapHalfMm))
                    continue;
                result.Add("Cap Side", FacePoint(h.Center, axis, fn, BoundaryMm(pos)), axis, fn);
            }
        }
    }

    // ------------------------------------------------------------------
    // Section tiling
    // ------------------------------------------------------------------

    /// <summary>
    /// Veneer runs (start hole, size) covering the section [a, b], plus the
    /// cap holes the tiling introduces. Uses the exact catalogue combination
    /// when one exists — with the full H1-H15 veneer range that is every
    /// section of 2+ intervals. Sections with no combination (1 interval,
    /// or any hole left by a slimmer future catalogue) degrade gracefully
    /// instead of leaving the whole channel bare: every interval but one is
    /// tiled and the single bare module near the middle is closed off with
    /// a Cap Side at each of its holes. Returns false only when nothing
    /// fits at all.
    /// </summary>
    static bool TileSection(int a, int b, List<(int start, int size)> runs,
                            SortedSet<int> capHoles)
    {
        runs.Clear();
        int L = b - a;

        List<int> sizes = Finishing.CoverSegment(L);
        if (sizes != null)
        {
            int pos = a;
            for (int k = 0; k < sizes.Count; k++)
            {
                runs.Add((pos, sizes[k]));
                pos += sizes[k] + 1;
                if (k < sizes.Count - 1)
                    capHoles.Add(pos);
            }
            return true;
        }

        if (L < 3)
            return false;

        // Best-effort split: even stretches below and above a 1-interval
        // gap, the gap sitting at (or just above) the section's middle.
        int lowLen = ((L - 1) / 2 + 1) & ~1;
        int hiLen = L - 1 - lowLen;

        AppendRuns(a, Finishing.CoverSegment(lowLen), runs, capHoles);
        capHoles.Add(a + lowLen);
        capHoles.Add(a + lowLen + 1);
        if (hiLen > 0)
            AppendRuns(a + lowLen + 1, Finishing.CoverSegment(hiLen), runs, capHoles);
        return true;
    }

    static void AppendRuns(int start, List<int> sizes,
                           List<(int start, int size)> runs, SortedSet<int> capHoles)
    {
        int pos = start;
        for (int k = 0; k < sizes.Count; k++)
        {
            runs.Add((pos, sizes[k]));
            pos += sizes[k] + 1;
            if (k < sizes.Count - 1)
                capHoles.Add(pos);
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

    static Vector3 PerpendicularOf(Vector3 axis)
    {
        Vector3 p = Vector3.Cross(axis, Vector3.up);
        if (p.sqrMagnitude < 1e-4f)
            p = Vector3.Cross(axis, Vector3.right);
        return p.normalized;
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
    /// True when a post divider cap would sit on the MASKED side of the ring
    /// band a resting board dresses. A board mounted over a ring of beams
    /// slides its edge into their inward channels: those go bare, and the
    /// coplanar post strips between the beam joints must stay bare too — a
    /// lone white cap there breaks the band (the classic table-edge junction
    /// post seen from below). The band is the profile-deep stretch just
    /// behind the board's mounting face, near the board's footprint.
    ///
    /// Direction matters: only faces looking INTO the boarded area sit on
    /// that masked side. Outward faces of edge and corner posts are coplanar
    /// with the ring's VENEERED outer channels — their caps complete the
    /// white band and must stay. Boards parallel to the post (wall panels)
    /// never trigger this — their flanking post faces keep normal covering.
    /// </summary>
    static bool InPanelDressedBand(Vector3 capCenter, Vector3 faceNormal,
                                   in FrameOverlapResolver.FrameRecord v,
                                   List<PanelBox> panels)
    {
        if (panels == null)
            return false;
        Vector3 axis = v.LengthAxis;
        foreach (PanelBox p in panels)
        {
            if (Mathf.Abs(Vector3.Dot(p.Normal, axis)) < 0.7f)
                continue; // board must lie across the post

            // Depth behind the board's plane, measured toward the post body
            // (the side the structure is on). The board mid-plane rides
            // 18 mm off the ring face and the ring runs one profile deep:
            // the dressed band is ~[18, 59] mm behind the plane.
            float side = Mathf.Sign(Vector3.Dot(v.Center - p.Center, p.Normal));
            float depthMm = NeospaceUnits.ToMm(Vector3.Dot(capCenter - p.Center, p.Normal)) * side;
            if (depthMm < 16f || depthMm > 61f)
                continue;

            Vector3 q = capCenter - p.Center;
            float margin = NeospaceUnits.Mm(45f);
            if (Mathf.Abs(Vector3.Dot(q, p.AxisU)) > p.HalfU + margin ||
                Mathf.Abs(Vector3.Dot(q, p.AxisV)) > p.HalfV + margin)
                continue; // board is nowhere near this post

            // The cap's face must look toward the board's interior (in the
            // board's plane) to sit on the masked side of the band.
            Vector3 toBoard = p.Center - capCenter;
            toBoard -= p.Normal * Vector3.Dot(toBoard, p.Normal);
            if (Vector3.Dot(toBoard, faceNormal) > 0f)
                return true;
        }
        return false;
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
    /// basis, rendered bounds give the extents. Works for live build panels
    /// and for frozen panel children inside Space piece instances alike.
    /// </summary>
    static bool TryPanelBox(Transform t, out PanelBox box)
    {
        box = default;

        Renderer[] rends = t.GetComponentsInChildren<Renderer>(false);
        if (rends.Length == 0)
            return false;

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
            b.Encapsulate(rends[i].bounds);

        Vector3 nrm = t.forward.normalized;
        Vector3 u = t.right.normalized;
        Vector3 v = t.up.normalized;

        float halfU = 0f, halfV = 0f, halfN = 0f;
        Vector3 e = b.extents;
        for (int sx = -1; sx <= 1; sx += 2)
        for (int sy = -1; sy <= 1; sy += 2)
        for (int sz = -1; sz <= 1; sz += 2)
        {
            Vector3 d = Vector3.Scale(e, new Vector3(sx, sy, sz));
            halfU = Mathf.Max(halfU, Mathf.Abs(Vector3.Dot(d, u)));
            halfV = Mathf.Max(halfV, Mathf.Abs(Vector3.Dot(d, v)));
            halfN = Mathf.Max(halfN, Mathf.Abs(Vector3.Dot(d, nrm)));
        }

        box = new PanelBox
        {
            Center = b.center,
            Normal = nrm,
            AxisU = u,
            AxisV = v,
            HalfU = halfU,
            HalfV = halfV,
            HalfN = halfN
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
