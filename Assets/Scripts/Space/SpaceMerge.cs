using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The merge solver for Space Mode. Placed pieces are rigid, frozen clones,
/// but physically a space is ONE structure: where pieces meet, shared frames
/// must be reused (longer wins), a post landing inside another piece's beam
/// must split that beam into catalogue pieces (H15 → H7 + post + H7), and
/// panels whose bay is divided must re-seat per sub-bay. When no catalogue
/// split exists the pose is refused — the ghost turns red.
///
/// Everything here is DERIVED state: instance
/// data (piece id + pose) never changes, so codes, undo snapshots and
/// Edit-Piece stay exact. <see cref="Apply"/> recomputes the merged view
/// (hide originals, spawn derived split visuals) after every change;
/// <see cref="PoseBlocked"/> answers "would this pose merge legally?" for
/// ghosts, drags, rotations and duplicates.
///
/// The maths reuses the pure core (<see cref="BeamSplit"/>, catalogue data)
/// and mirrors Build Mode's merge rules. Pieces only ever sit on the 88 mm
/// lattice in 90° yaw steps, so all spans stay world-axis aligned.
/// </summary>
public static class SpaceMerge
{
    // Tolerances in millimetres, converted through NeospaceUnits so they are
    // correct at the project's world scale (1 unit = 100 mm). Lattice-snapped
    // geometry either coincides or is a full module apart, so these only
    // absorb float noise.
    static float LateralTol => NeospaceUnits.Mm(6f);
    static float SpanTol => NeospaceUnits.Mm(6f);
    static float YTol => NeospaceUnits.Mm(30f);
    /// <summary>Closer to a skeleton end than this = the shared joint post, not an internal crossing.</summary>
    static float EndMargin => NeospaceUnits.Mm(44f);
    /// <summary>
    /// A wall sheet mounts flush on the frame faces: outset + gap + half
    /// thickness ≈ 20 mm off its frame plane (measured; see
    /// PanelSlotManager.GetPanelPlacement). A frame member DIVIDING the
    /// sheet's bay therefore measures ~20 mm from the sheet, while members
    /// of the NEXT parallel frame line measure 88 − 20 ≈ 68 mm. The band
    /// must bracket the former and exclude the latter — the previous 45–75
    /// band did the opposite, so posts crossed wall panels without
    /// splitting them and next-line posts cut panels they never touched.
    /// </summary>
    static float PanelPlaneMin => NeospaceUnits.Mm(4f);
    static float PanelPlaneMax => NeospaceUnits.Mm(44f);
    /// <summary>Interior margin for "is this point really inside the sheet?" tests.</summary>
    static float SheetEdgeTol => NeospaceUnits.Mm(5f);

    /// <summary>One frozen part reduced to identity + span/sheet geometry.</summary>
    public struct PartRecord
    {
        public Transform Tr;        // scene child; null for pose candidates
        public string Id;           // beam id ("V13", "H7", "T7"); null for panels
        public bool IsPanel;
        public bool IsVertical;
        public int Size;
        public Vector3 Center;      // beams: body centre; panels: sheet centre
        public Vector3 Axis;        // beams: unit span axis (world X/Z, or up)
        public float HalfSpan;      // beams: half skeleton length (m)
        public float MinY, MaxY;    // V posts: vertical body extent
        public Quaternion Rot;      // original child rotation (derived spawns)
        public Vector3 Normal, Up, Right;   // panels
        public float Width, Height, Thick;  // panels
        public float Price;
    }

    /// <summary>Signed price correction: derived pieces added minus originals hidden.</summary>
    public static float PriceDelta { get; private set; }

    /// <summary>
    /// Groups of instances whose frames merged into one structure (shared or
    /// rebuilt beams, split panels…), recomputed by <see cref="Apply"/>.
    /// Only groups of two or more are listed.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<SpaceInstance>> Groups => _groups;
    static readonly List<List<SpaceInstance>> _groups = new List<List<SpaceInstance>>();
    static readonly List<List<(Vector3 a, Vector3 b)>> _groupJoints =
        new List<List<(Vector3 a, Vector3 b)>>();

    /// <summary>All instances merged with this one (itself included), or null when it stands alone.</summary>
    public static IReadOnlyList<SpaceInstance> GroupOf(SpaceInstance inst)
    {
        foreach (List<SpaceInstance> group in _groups)
            if (group.Contains(inst))
                return group;
        return null;
    }

    /// <summary>
    /// World-space segments along the frame members this instance's group
    /// fused on (shared posts, merged beam lines) — where the snap physically
    /// happened. Null when the instance stands alone.
    /// </summary>
    public static IReadOnlyList<(Vector3 a, Vector3 b)> JointsOf(SpaceInstance inst)
    {
        for (int g = 0; g < _groups.Count; g++)
            if (_groups[g].Contains(inst))
                return _groupJoints[g];
        return null;
    }

    static Transform _derivedRoot;
    static UIBuildStats _stats;

    // ------------------------------------------------------------------
    // Record extraction
    // ------------------------------------------------------------------

    /// <summary>Part records of every child under a root, in current world pose.</summary>
    public static List<PartRecord> RecordsFrom(Transform root)
    {
        var list = new List<PartRecord>();
        if (root == null)
            return list;
        foreach (Transform child in root)
            if (TryBuildRecord(child, out PartRecord rec))
                list.Add(rec);
        return list;
    }

    /// <summary>
    /// Part records of an instance as they WOULD be at another pose (drag,
    /// rotate, duplicate previews). Yaw-only rigid remap of the live records.
    /// </summary>
    public static List<PartRecord> RecordsFrom(SpaceInstance inst,
        Vector3 newPosition, float newYawDegrees)
    {
        var list = RecordsFrom(inst.transform);
        Transform t = inst.transform;
        Quaternion dq = Quaternion.Euler(0f, newYawDegrees, 0f) * Quaternion.Inverse(t.rotation);
        Vector3 oldPos = t.position;

        for (int i = 0; i < list.Count; i++)
        {
            PartRecord r = list[i];
            Vector3 c = newPosition + dq * (r.Center - oldPos);
            float dy = c.y - r.Center.y;
            r.Center = c;
            r.Axis = SnapAxis(dq * r.Axis);
            r.Rot = dq * r.Rot;
            r.Normal = dq * r.Normal;
            r.Up = dq * r.Up;
            r.Right = dq * r.Right;
            r.MinY += dy;
            r.MaxY += dy;
            list[i] = r;
        }
        return list;
    }

    static Vector3 SnapAxis(Vector3 axis)
    {
        // Yaw steps are exact 90°, but float noise creeps in — keep spans
        // exactly world-axis aligned.
        if (Mathf.Abs(axis.y) > 0.9f)
            return Vector3.up;
        return Mathf.Abs(axis.x) >= Mathf.Abs(axis.z) ? Vector3.right : Vector3.forward;
    }

    static bool TryBuildRecord(Transform child, out PartRecord rec)
    {
        rec = default;
        if (child == null || child.name == "SelectionPad")
            return false;

        string id = StructureClipboard.CleanPartId(child.name);
        if (id != null)
        {
            Naming.ParsedName parsed = Naming.Parse(Naming.FromUnityPartId(id));
            if (parsed == null || parsed.Family != Naming.Frame)
                return false;
            if (!TryMeshBounds(child, out Bounds bounds))
                return false;

            rec.Tr = child;
            rec.Id = id;
            rec.Size = parsed.SizeInt;
            rec.Center = bounds.center;
            rec.Rot = child.rotation;
            rec.IsVertical = BeamPartUtility.IsVertical(id);
            rec.Price = PriceOfBeam(id);
            if (rec.IsVertical)
            {
                rec.Axis = Vector3.up;
                rec.MinY = bounds.min.y;
                rec.MaxY = bounds.max.y;
                rec.HalfSpan = NeospaceUnits.Mm(Skeleton.VSkeletonLength(rec.Size)) * 0.5f;
            }
            else
            {
                rec.Axis = bounds.size.x >= bounds.size.z ? Vector3.right : Vector3.forward;
                rec.HalfSpan = NeospaceUnits.Mm(Skeleton.HSkeletonLength(rec.Size)) * 0.5f;
            }
            return true;
        }

        if (child.name.Contains("Panel"))
        {
            rec.Tr = child;
            rec.IsPanel = true;
            rec.Center = child.position;
            rec.Rot = child.rotation;
            rec.Normal = child.forward;
            rec.Up = child.up;
            rec.Right = child.right;
            Vector3 s = child.lossyScale;
            rec.Width = s.x;
            rec.Height = s.y;
            rec.Thick = s.z;
            rec.Price = PanelPrice();
            return true;
        }

        return false;
    }

    /// <summary>
    /// World AABB from mesh bounds × matrices — valid even while the child
    /// is deactivated (dedup-hidden parts must still count as real).
    /// While the part is active, only active meshes count — identical to the
    /// renderer bounds Build Mode's merge measures, so cut offsets match the
    /// 88 mm lattice exactly. Hidden parts fall back to including inactive
    /// meshes (everything under a deactivated root reports inactive).
    /// </summary>
    static bool TryMeshBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        bool rootActive = root.gameObject.activeInHierarchy;
        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf == null || mf.sharedMesh == null)
                continue;
            if (rootActive && !mf.gameObject.activeInHierarchy)
                continue;   // prefab-internal hidden helper meshes don't count
            Bounds local = mf.sharedMesh.bounds;
            Matrix4x4 m = mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3(
                    (i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 world = m.MultiplyPoint3x4(corner);
                if (!any)
                {
                    bounds = new Bounds(world, Vector3.zero);
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(world);
                }
            }
        }
        return any;
    }

    // ------------------------------------------------------------------
    // Analysis
    // ------------------------------------------------------------------

    /// <summary>A division line across a sheet, in sheet-local coordinates.</summary>
    struct PanelCut
    {
        public float Pos;    // offset from the sheet centre along Right (W) or Up (H)
        public float Inset;  // clearance each side (profile cuts) or ~0 (flush seams)
    }

    /// <summary>One coaxial run of beams replaced by catalogue pieces.</summary>
    struct LineRebuild
    {
        public int TemplateIdx;                     // record whose family/rotation/layer the pieces copy
        public Vector3 CenterWorld;                 // union centre on the line
        public Vector3 Axis;
        public List<BeamSplit.Segment> Segments;
        /// <summary>
        /// Synthetic beam spanning the whole union. The original beams are
        /// hidden before the crossing pass runs, so panels crossed by the
        /// rebuilt line must be divided against this record instead.
        /// </summary>
        public PartRecord Union;
    }

    sealed class Outcome
    {
        public bool Blocked;
        public string Reason;
        public bool[] Hidden;
        public readonly Dictionary<int, SortedSet<int>> BeamCuts = new Dictionary<int, SortedSet<int>>();
        public readonly Dictionary<int, List<BeamSplit.Segment>> BeamPlans = new Dictionary<int, List<BeamSplit.Segment>>();
        public readonly Dictionary<int, List<PanelCut>> PanelCutsW = new Dictionary<int, List<PanelCut>>();
        public readonly Dictionary<int, List<PanelCut>> PanelCutsH = new Dictionary<int, List<PanelCut>>();
        /// <summary>Sheet-local (w0, w1, h0, h1) regions ceded to an earlier overlapping sheet.</summary>
        public readonly Dictionary<int, List<Vector4>> PanelSubtract = new Dictionary<int, List<Vector4>>();
        public readonly HashSet<int> PanelRemoved = new HashSet<int>();
        /// <summary>
        /// Sheet-local (w, h) points where a post punches through a floor/roof
        /// sheet. Adjudicated after all cuts are known: a post standing on a
        /// seam splits the sheet (build mode's divided bay); a post with no
        /// seam removes it.
        /// </summary>
        public readonly Dictionary<int, List<Vector2>> PanelPunch = new Dictionary<int, List<Vector2>>();
        /// <summary>Partially-overlapping coaxial runs rebuilt as one row of catalogue pieces.</summary>
        public readonly List<LineRebuild> LineRebuilds = new List<LineRebuild>();

        /// <summary>
        /// Record index pairs that structurally interacted (dedup, rebuild,
        /// split, panel division). Mapped to instances afterwards, these
        /// define the merged GROUPS — pieces whose frames fused into one.
        /// </summary>
        public readonly List<(int a, int b)> Links = new List<(int a, int b)>();

        public void Link(int a, int b) => Links.Add((a, b));

        public void Block(string reason)
        {
            if (!Blocked)
            {
                Blocked = true;
                Reason = reason;
            }
        }
    }

    /// <summary>
    /// Resolve the merged view of all records (earlier index = higher
    /// priority, keeps its parts). Never touches the scene.
    /// </summary>
    static Outcome Analyze(List<PartRecord> recs)
    {
        var o = new Outcome { Hidden = new bool[recs.Count] };

        // Pass 1: coaxial duplicates and containment — longer wins.
        for (int i = 0; i < recs.Count; i++)
        {
            if (o.Hidden[i])
                continue;
            for (int j = i + 1; j < recs.Count; j++)
            {
                if (o.Hidden[i])
                    break;
                if (o.Hidden[j])
                    continue;
                ResolveCoaxial(recs, i, j, o);
            }
        }

        // Pass 2: crossings (posts into beam interiors) and panel dividers.
        for (int i = 0; i < recs.Count; i++)
        {
            if (o.Hidden[i])
                continue;
            for (int j = i + 1; j < recs.Count; j++)
            {
                if (o.Hidden[j])
                    continue;
                ResolveCrossing(recs, i, j, o);
            }
        }

        // Pass 2.5: rebuilt coaxial runs divide the panels they cross. The
        // beams that formed each run were hidden in pass 1, so pass 2 never
        // saw them — the synthetic union record stands in for the whole line.
        foreach (LineRebuild lr in o.LineRebuilds)
            for (int p = 0; p < recs.Count; p++)
                if (recs[p].IsPanel && !o.Hidden[p])
                    ResolvePanelBeam(recs, p, lr.Union, lr.TemplateIdx, o);

        // Punch adjudication: a post through a floor sheet only removes it
        // when NO seam runs where the post stands. When the same merge cuts
        // the sheet along that post's line (its beams divide the bay), the
        // sub-sheets already part around the post — exactly build mode's
        // divided-bay refill.
        float seamTol = NeospaceUnits.Mm(23f);
        foreach (KeyValuePair<int, List<Vector2>> kv in o.PanelPunch)
        {
            int p = kv.Key;
            if (o.Hidden[p] || o.PanelRemoved.Contains(p))
                continue;
            o.PanelCutsW.TryGetValue(p, out List<PanelCut> cutsW);
            o.PanelCutsH.TryGetValue(p, out List<PanelCut> cutsH);
            o.PanelSubtract.TryGetValue(p, out List<Vector4> subtract);

            foreach (Vector2 punch in kv.Value)
            {
                bool onSeam = false;
                if (cutsW != null)
                    foreach (PanelCut c in cutsW)
                        if (Mathf.Abs(punch.x - c.Pos) <= seamTol) { onSeam = true; break; }
                if (!onSeam && cutsH != null)
                    foreach (PanelCut c in cutsH)
                        if (Mathf.Abs(punch.y - c.Pos) <= seamTol) { onSeam = true; break; }
                if (!onSeam && subtract != null)
                    foreach (Vector4 rect in subtract)
                        if (punch.x > rect.x && punch.x < rect.y &&
                            punch.y > rect.z && punch.y < rect.w) { onSeam = true; break; }
                if (!onSeam)
                {
                    o.PanelRemoved.Add(p);
                    break;
                }
            }
        }

        // Pass 3: every cut beam needs a catalogue split plan.
        foreach (KeyValuePair<int, SortedSet<int>> kv in o.BeamCuts)
        {
            PartRecord beam = recs[kv.Key];
            List<BeamSplit.Segment> plan = BeamSplit.Plan(beam.Size, kv.Value);
            if (plan == null)
            {
                o.Block($"{beam.Id} can't be split there · no catalogue pieces make up that division.");
                continue;
            }
            o.BeamPlans[kv.Key] = plan;
        }

        return o;
    }

    static void ResolveCoaxial(List<PartRecord> recs, int i, int j, Outcome o)
    {
        PartRecord a = recs[i], b = recs[j];

        if (a.IsPanel || b.IsPanel)
        {
            if (a.IsPanel && b.IsPanel)
                ResolvePanelPanel(recs, i, j, o);
            return;
        }

        if (a.IsVertical && b.IsVertical)
        {
            if (PlanDistance(a.Center, b.Center) > LateralTol)
                return;

            float overlap = Mathf.Min(a.MaxY, b.MaxY) - Mathf.Max(a.MinY, b.MinY);
            if (overlap <= SpanTol)
                return;   // stacked / touching — fine

            bool sameLow = Mathf.Abs(a.MinY - b.MinY) <= SpanTol;
            bool sameHigh = Mathf.Abs(a.MaxY - b.MaxY) <= SpanTol;
            if (sameLow && sameHigh)
            {
                o.Hidden[j] = true;                       // equal — first one stays
                o.Link(i, j);
            }
            else if (b.MinY >= a.MinY - SpanTol && b.MaxY <= a.MaxY + SpanTol)
            {
                o.Hidden[j] = true;                       // b inside a
                o.Link(i, j);
            }
            else if (a.MinY >= b.MinY - SpanTol && a.MaxY <= b.MaxY + SpanTol)
            {
                o.Hidden[i] = true;                       // a inside b — longer wins
                o.Link(i, j);
            }
            else
                o.Block($"{a.Id} and {b.Id} overlap partially · line the pieces up so posts either match or contain each other.");
            return;
        }

        if (!a.IsVertical && !b.IsVertical)
        {
            if (Mathf.Abs(a.Center.y - b.Center.y) > YTol)
                return;

            if (Vector3.Dot(a.Axis, b.Axis) > 0.9f)
            {
                // Same direction: collinear?
                Vector3 d = b.Center - a.Center;
                Vector3 lateral = d - a.Axis * Vector3.Dot(d, a.Axis);
                lateral.y = 0f;
                if (lateral.magnitude > LateralTol)
                    return;

                float sA = Vector3.Dot(a.Center, a.Axis);
                float sB = Vector3.Dot(b.Center, a.Axis);
                float loA = sA - a.HalfSpan, hiA = sA + a.HalfSpan;
                float loB = sB - b.HalfSpan, hiB = sB + b.HalfSpan;
                float overlap = Mathf.Min(hiA, hiB) - Mathf.Max(loA, loB);
                if (overlap <= SpanTol)
                    return;   // end-to-end on the same line

                bool sameLo = Mathf.Abs(loA - loB) <= SpanTol;
                bool sameHi = Mathf.Abs(hiA - hiB) <= SpanTol;
                if (sameLo && sameHi)
                {
                    if (a.Id == b.Id)
                    {
                        o.Hidden[j] = true;
                        o.Link(i, j);
                    }
                    else
                        o.Block($"{a.Id} and {b.Id} want the same span · the beams don't match.");
                }
                else if (loB >= loA - SpanTol && hiB <= hiA + SpanTol)
                {
                    o.Hidden[j] = true;
                    o.Link(i, j);
                }
                else if (loA >= loB - SpanTol && hiA <= hiB + SpanTol)
                {
                    o.Hidden[i] = true;
                    o.Link(i, j);
                }
                else
                    ResolveLineRebuild(recs, i, j, o);
            }
        }
    }

    /// <summary>
    /// Two coaxial beams overlapping PARTIALLY — pieces pushed together with an
    /// offset. Physically that line is one continuous run of material with
    /// posts standing on it, so rebuild it the way a fitter would: replace all
    /// the beams on the run with catalogue pieces meeting the posts. Blocks
    /// only when the run genuinely can't be built (a needed piece size isn't
    /// produced, a junction has no post, off-module geometry).
    /// </summary>
    static void ResolveLineRebuild(List<PartRecord> recs, int i, int j, Outcome o)
    {
        PartRecord a = recs[i];
        Vector3 axis = a.Axis;

        // Every beam on this line and level (same lateral position).
        var members = new List<int>();
        for (int k = 0; k < recs.Count; k++)
        {
            PartRecord r = recs[k];
            if (o.Hidden[k] || r.IsPanel || r.IsVertical)
                continue;
            if (Vector3.Dot(r.Axis, axis) < 0.9f)
                continue;
            if (Mathf.Abs(r.Center.y - a.Center.y) > YTol)
                continue;
            Vector3 d = r.Center - a.Center;
            Vector3 lateral = d - axis * Vector3.Dot(d, axis);
            lateral.y = 0f;
            if (lateral.magnitude > LateralTol)
                continue;
            members.Add(k);
        }

        // Keep only the connected overlap component containing i and j.
        members.Sort((x, y) =>
            Vector3.Dot(recs[x].Center, axis).CompareTo(Vector3.Dot(recs[y].Center, axis)));
        var component = new List<int>();
        float coverHi = float.NegativeInfinity;
        foreach (int k in members)
        {
            float lo = Vector3.Dot(recs[k].Center, axis) - recs[k].HalfSpan;
            float hi = Vector3.Dot(recs[k].Center, axis) + recs[k].HalfSpan;
            if (component.Count > 0 && lo > coverHi + SpanTol)
            {
                if (component.Contains(i) || component.Contains(j))
                    break;      // i/j's component is complete
                component.Clear();
            }
            component.Add(k);
            coverHi = Mathf.Max(coverHi, hi);
        }
        if (!component.Contains(i) || !component.Contains(j))
        {
            o.Block($"{a.Id} and {recs[j].Id} overlap partially along their span.");
            return;
        }

        // All pieces on one run must be the same family (H with H, HT with HT).
        string family = FamilyPrefix(a.Id);
        foreach (int k in component)
        {
            if (FamilyPrefix(recs[k].Id) != family)
            {
                o.Block($"{a.Id} and {recs[k].Id} can't merge · different beam types on one line.");
                return;
            }
        }

        // Posts standing on this line at this level.
        var spans = new List<(float lo, float hi)>(component.Count);
        float unionLo = float.PositiveInfinity, unionHi = float.NegativeInfinity;
        foreach (int k in component)
        {
            float s = Vector3.Dot(recs[k].Center, axis);
            unionLo = Mathf.Min(unionLo, s - recs[k].HalfSpan);
            unionHi = Mathf.Max(unionHi, s + recs[k].HalfSpan);
            spans.Add((NeospaceUnits.ToMm(s - recs[k].HalfSpan), NeospaceUnits.ToMm(s + recs[k].HalfSpan)));
        }

        float postSlack = NeospaceUnits.Mm(25f);
        var posts = new List<float>();
        for (int k = 0; k < recs.Count; k++)
        {
            PartRecord p = recs[k];
            if (p.IsPanel || !p.IsVertical)
                continue;
            if (a.Center.y < p.MinY - postSlack || a.Center.y > p.MaxY + postSlack)
                continue;
            Vector3 d = p.Center - a.Center;
            Vector3 lateral = d - axis * Vector3.Dot(d, axis);
            lateral.y = 0f;
            if (lateral.magnitude > LateralTol)
                continue;
            posts.Add(NeospaceUnits.ToMm(Vector3.Dot(p.Center, axis)));
        }

        List<BeamSplit.Segment> plan = BeamSplit.PlanLine(spans, posts,
            NeospaceUnits.ToMm(SpanTol), out float centerMm, out string error);
        if (plan == null)
        {
            o.Block($"{a.Id} and {recs[j].Id} can't merge here: {error}");
            return;
        }

        foreach (int k in component)
        {
            o.Hidden[k] = true;
            if (k != component[0])
                o.Link(component[0], k);
        }

        float centerS = NeospaceUnits.Mm(centerMm);
        Vector3 centerWorld = a.Center + axis * (centerS - Vector3.Dot(a.Center, axis));

        PartRecord union = a;
        union.Tr = null;
        union.Center = a.Center + axis * ((unionLo + unionHi) * 0.5f - Vector3.Dot(a.Center, axis));
        union.HalfSpan = (unionHi - unionLo) * 0.5f;

        o.LineRebuilds.Add(new LineRebuild
        {
            TemplateIdx = i,
            CenterWorld = centerWorld,
            Axis = axis,
            Segments = plan,
            Union = union
        });
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

    /// <summary>
    /// Two sheets in the same plane. Panels never block a merge: identical or
    /// contained sheets dedup, partial overlaps cut the later sheet flush
    /// along the earlier sheet's edges (build mode's "panels adjust to the
    /// slots that exist" · the earlier piece keeps its board, the later piece
    /// keeps only the region it alone covers).
    /// </summary>
    static void ResolvePanelPanel(List<PartRecord> recs, int i, int j, Outcome o)
    {
        PartRecord a = recs[i], b = recs[j];

        if (Mathf.Abs(Vector3.Dot(a.Normal, b.Normal)) < 0.95f)
            return;   // perpendicular sheets never truly meet (60 mm outsets)

        Vector3 d = a.Center - b.Center;
        if (Mathf.Abs(Vector3.Dot(d, b.Normal)) > NeospaceUnits.Mm(3f))
            return;   // parallel but different planes (e.g. both sides of a wall)

        // Project a's rectangle into b's in-plane frame (all sheets are
        // lattice-aligned, so axes coincide or swap).
        float cw = Vector3.Dot(d, b.Right);
        float ch = Vector3.Dot(d, b.Up);
        float aHw = Mathf.Abs(Vector3.Dot(a.Right, b.Right)) * a.Width * 0.5f +
                    Mathf.Abs(Vector3.Dot(a.Up, b.Right)) * a.Height * 0.5f;
        float aHh = Mathf.Abs(Vector3.Dot(a.Right, b.Up)) * a.Width * 0.5f +
                    Mathf.Abs(Vector3.Dot(a.Up, b.Up)) * a.Height * 0.5f;

        float bHw = b.Width * 0.5f, bHh = b.Height * 0.5f;
        float w0 = Mathf.Max(-bHw, cw - aHw), w1 = Mathf.Min(bHw, cw + aHw);
        float h0 = Mathf.Max(-bHh, ch - aHh), h1 = Mathf.Min(bHh, ch + aHh);
        float touchTol = NeospaceUnits.Mm(15f);
        if (w1 - w0 <= touchTol || h1 - h0 <= touchTol)
            return;   // side by side / merely abutting

        bool coversB = w0 <= -bHw + SheetEdgeTol && w1 >= bHw - SheetEdgeTol &&
                       h0 <= -bHh + SheetEdgeTol && h1 >= bHh - SheetEdgeTol;
        if (coversB)
        {
            o.Hidden[j] = true;      // b fully covered — a stays
            o.Link(i, j);
            return;
        }
        bool coversA = w1 - w0 >= aHw * 2f - SheetEdgeTol &&
                       h1 - h0 >= aHh * 2f - SheetEdgeTol;
        if (coversA)
        {
            o.Hidden[i] = true;      // a sits fully inside b — keep the bigger sheet
            o.Link(i, j);
            return;
        }

        // Partial: cut b flush along a's edges and cede the overlap to a.
        o.Link(i, j);
        float seamInset = NeospaceUnits.Mm(1f);
        if (w0 > -bHw + SheetEdgeTol) AddCut(o.PanelCutsW, j, w0, seamInset);
        if (w1 < bHw - SheetEdgeTol) AddCut(o.PanelCutsW, j, w1, seamInset);
        if (h0 > -bHh + SheetEdgeTol) AddCut(o.PanelCutsH, j, h0, seamInset);
        if (h1 < bHh - SheetEdgeTol) AddCut(o.PanelCutsH, j, h1, seamInset);

        if (!o.PanelSubtract.TryGetValue(j, out List<Vector4> rects))
            o.PanelSubtract[j] = rects = new List<Vector4>();
        rects.Add(new Vector4(w0 - seamInset, w1 + seamInset, h0 - seamInset, h1 + seamInset));
    }

    static void ResolveCrossing(List<PartRecord> recs, int i, int j, Outcome o)
    {
        PartRecord a = recs[i], b = recs[j];

        if (a.IsPanel || b.IsPanel)
        {
            if (a.IsPanel != b.IsPanel)
            {
                // Panel index first, beam second.
                if (a.IsPanel)
                    ResolvePanelBeam(recs, i, j, o);
                else
                    ResolvePanelBeam(recs, j, i, o);
            }
            return;
        }

        if (a.IsVertical != b.IsVertical)
        {
            if (a.IsVertical)
                ResolvePostBeam(recs, i, j, o);
            else
                ResolvePostBeam(recs, j, i, o);
            return;
        }

        if (!a.IsVertical && !b.IsVertical)
            ResolveBeamBeamCross(recs, i, j, o);
    }

    /// <summary>Post crossing an H/HT beam: shared joint, catalogue cut, or block.</summary>
    static void ResolvePostBeam(List<PartRecord> recs, int postIdx, int beamIdx, Outcome o)
    {
        PartRecord post = recs[postIdx], beam = recs[beamIdx];

        if (beam.Center.y < post.MinY - NeospaceUnits.Mm(25f) ||
            beam.Center.y > post.MaxY + NeospaceUnits.Mm(25f))
            return;   // beam level not on this post

        Vector3 d = post.Center - beam.Center;
        Vector3 lateral = d - beam.Axis * Vector3.Dot(d, beam.Axis);
        lateral.y = 0f;
        if (lateral.magnitude > LateralTol)
            return;   // not on the beam's line

        float t = Vector3.Dot(d, beam.Axis);
        if (Mathf.Abs(t) >= beam.HalfSpan - EndMargin)
            return;   // at (or beyond) the joint end — the normal shared column

        if (!BeamSplit.TryCutIndexFromOffset(beam.Size, NeospaceUnits.ToMm(t), 6f, out int cut))
        {
            o.Block($"A post lands off the 88 mm points inside {beam.Id} · that split can't be built.");
            return;
        }

        if (!o.BeamCuts.TryGetValue(beamIdx, out SortedSet<int> cuts))
            o.BeamCuts[beamIdx] = cuts = new SortedSet<int>();
        cuts.Add(cut);
        o.Link(postIdx, beamIdx);
    }

    /// <summary>Two horizontal beams crossing in plan at the same level.</summary>
    static void ResolveBeamBeamCross(List<PartRecord> recs, int i, int j, Outcome o)
    {
        PartRecord a = recs[i], b = recs[j];
        if (Mathf.Abs(a.Center.y - b.Center.y) > YTol)
            return;
        if (Mathf.Abs(Vector3.Dot(a.Axis, b.Axis)) > 0.1f)
            return;   // parallel handled by the coaxial pass

        // Perpendicular: intersection parameters along each span.
        float tA = Vector3.Dot(b.Center - a.Center, a.Axis);
        float tB = Vector3.Dot(a.Center - b.Center, b.Axis);
        if (Mathf.Abs(tA) > a.HalfSpan + LateralTol || Mathf.Abs(tB) > b.HalfSpan + LateralTol)
            return;   // lines cross outside the spans

        bool aInterior = Mathf.Abs(tA) < a.HalfSpan - EndMargin;
        bool bInterior = Mathf.Abs(tB) < b.HalfSpan - EndMargin;
        if (!aInterior && !bInterior)
            return;   // shared corner joint
        if (aInterior && bInterior)
        {
            o.Block($"{a.Id} and {b.Id} would cross mid-span · beams can't pass through each other.");
            return;
        }

        // One beam ENDS against the other's interior: legal only when a post
        // stands at that point (it will split the crossed beam).
        Vector3 cross = a.Center + a.Axis * tA;
        for (int k = 0; k < recs.Count; k++)
        {
            if (o.Hidden[k] || !IsPost(recs[k]))
                continue;
            PartRecord p = recs[k];
            if (PlanDistance(p.Center, cross) <= LateralTol &&
                cross.y >= p.MinY - NeospaceUnits.Mm(25f) &&
                cross.y <= p.MaxY + NeospaceUnits.Mm(25f))
                return;
        }
        o.Block($"{(aInterior ? b.Id : a.Id)} ends against the side of {(aInterior ? a.Id : b.Id)} with no post there.");
    }

    static bool IsPost(in PartRecord r) => !r.IsPanel && r.IsVertical;

    /// <summary>
    /// A beam or post meeting a panel sheet. Panels never block a merge:
    /// they divide along the crossing member when the geometry gives clean
    /// sub-bays, and are removed when it doesn't (build mode: no slot →
    /// no panel).
    /// </summary>
    static void ResolvePanelBeam(List<PartRecord> recs, int panelIdx, int beamIdx, Outcome o)
        => ResolvePanelBeam(recs, panelIdx, recs[beamIdx], beamIdx, o);

    /// <param name="linkIdx">Record index representing the dividing member
    /// for grouping (a real beam's index, or a rebuilt run's template).</param>
    static void ResolvePanelBeam(List<PartRecord> recs, int panelIdx, in PartRecord beam,
        int linkIdx, Outcome o)
    {
        PartRecord panel = recs[panelIdx];
        bool floorLike = Mathf.Abs(panel.Normal.y) > 0.7f;
        float cutInset = NeospaceUnits.Mm(CatalogueData.HalfProfileMm + 5f);

        if (beam.IsVertical)
        {
            Vector3 q = new Vector3(beam.Center.x, panel.Center.y, beam.Center.z);
            Vector3 d = q - panel.Center;

            if (floorLike)
            {
                // A post PASSING THROUGH a floor/roof sheet punches it. A post
                // that merely ends flush at the sheet's face is fine — boards
                // sit on top of post columns everywhere. Whether a punch
                // removes the sheet or splits it (post standing on a seam the
                // same merge cuts) is decided after all cuts are known.
                if (panel.Center.y < beam.MinY + SheetEdgeTol ||
                    panel.Center.y > beam.MaxY - SheetEdgeTol)
                    return;
                float pw = Vector3.Dot(d, panel.Right);
                float ph = Vector3.Dot(d, panel.Up);
                if (Mathf.Abs(pw) < panel.Width * 0.5f - SheetEdgeTol &&
                    Mathf.Abs(ph) < panel.Height * 0.5f - SheetEdgeTol)
                {
                    if (!o.PanelPunch.TryGetValue(panelIdx, out List<Vector2> punches))
                        o.PanelPunch[panelIdx] = punches = new List<Vector2>();
                    punches.Add(new Vector2(pw, ph));
                    o.Link(panelIdx, linkIdx);
                }
                return;
            }

            // Wall sheet: a post standing in the bay divides it.
            float dn = Mathf.Abs(Vector3.Dot(d, panel.Normal));
            if (dn < PanelPlaneMin || dn > PanelPlaneMax)
                return;
            float dw = Vector3.Dot(d, panel.Right);
            if (Mathf.Abs(dw) >= panel.Width * 0.5f - SheetEdgeTol)
                return;   // at/outside the sheet's own edge posts

            float bottom = panel.Center.y - panel.Height * 0.5f;
            float top = panel.Center.y + panel.Height * 0.5f;
            if (beam.MaxY <= bottom + SheetEdgeTol || beam.MinY >= top - SheetEdgeTol)
                return;   // post entirely below/above the sheet — never touches it
            if (beam.MinY <= bottom + YTol && beam.MaxY >= top - YTol)
                AddCut(o.PanelCutsW, panelIdx, dw, cutInset);
            else
                o.PanelRemoved.Add(panelIdx);   // post ends mid-sheet: no clean sub-bay
            o.Link(panelIdx, linkIdx);
            return;
        }

        // Horizontal beam vs panel.
        float along = Vector3.Dot(beam.Axis, panel.Normal);
        if (Mathf.Abs(along) > 0.9f)
        {
            // Beam runs INTO the sheet: does its body actually reach it?
            float s = Vector3.Dot(panel.Center - beam.Center, beam.Axis);
            if (Mathf.Abs(s) >= beam.HalfSpan - NeospaceUnits.Mm(10f))
                return;   // sheet plane beyond the beam's ends
            Vector3 hit = beam.Center + beam.Axis * s;
            Vector3 dh2 = hit - panel.Center;
            if (Mathf.Abs(Vector3.Dot(dh2, panel.Right)) < panel.Width * 0.5f - NeospaceUnits.Mm(10f) &&
                Mathf.Abs(Vector3.Dot(dh2, panel.Up)) < panel.Height * 0.5f - NeospaceUnits.Mm(10f))
            {
                o.PanelRemoved.Add(panelIdx);   // beam pokes through the sheet
                o.Link(panelIdx, linkIdx);
            }
            return;
        }

        if (floorLike)
        {
            // A floor/roof board with another piece's beam lying in its slab
            // divides along that beam (the classic tabletop-over-tabletop
            // merge). Edge beams of the sheet's own bay sit ~20 mm outside
            // the board, past the edge test below.
            float dyF = beam.Center.y - panel.Center.y;
            if (Mathf.Abs(dyF) > YTol)
                return;   // not at this board's level

            bool alongUp = Mathf.Abs(Vector3.Dot(beam.Axis, panel.Up)) > 0.9f;
            Vector3 dF = beam.Center - panel.Center;
            float cutPos = alongUp ? Vector3.Dot(dF, panel.Right) : Vector3.Dot(dF, panel.Up);
            float halfCutExtent = alongUp ? panel.Width * 0.5f : panel.Height * 0.5f;
            if (Mathf.Abs(cutPos) >= halfCutExtent - SheetEdgeTol)
                return;   // at/outside the board's edge

            // Along the beam: does its span actually overlap the board at all?
            // A beam that stops SHORT of the board (another bay of the same
            // merged piece, on the same line and level) must be ignored, not
            // treated as "ends mid-board".
            float halfAlong = alongUp ? panel.Height * 0.5f : panel.Width * 0.5f;
            float boardT = Vector3.Dot(panel.Center - beam.Center, beam.Axis);
            float reach = Mathf.Min(beam.HalfSpan, boardT + halfAlong) -
                          Mathf.Max(-beam.HalfSpan, boardT - halfAlong);
            if (reach <= SheetEdgeTol)
                return;   // disjoint along the beam's axis

            // Must cover the board fully for a clean split.
            if (boardT - halfAlong >= -beam.HalfSpan - EndMargin &&
                boardT + halfAlong <= beam.HalfSpan + EndMargin)
                AddCut(alongUp ? o.PanelCutsW : o.PanelCutsH, panelIdx, cutPos, cutInset);
            else
                o.PanelRemoved.Add(panelIdx);   // beam ends mid-board: no clean sub-bay
            o.Link(panelIdx, linkIdx);
            return;
        }

        // Wall sheet with an in-plane horizontal divider (a merged piece's
        // beam crossing the bay): divide when it spans the whole sheet.
        Vector3 dd = beam.Center - panel.Center;
        float ddn = Mathf.Abs(Vector3.Dot(dd, panel.Normal));
        if (ddn < PanelPlaneMin || ddn > PanelPlaneMax)
            return;
        float dy = Vector3.Dot(dd, panel.Up);
        if (Mathf.Abs(dy) >= panel.Height * 0.5f - SheetEdgeTol)
            return;   // at/outside the sheet's own edge beams

        // Does the beam's span actually overlap the sheet at all? A beam that
        // stops short of the sheet on the same line never touches it.
        float sheetCenterT = Vector3.Dot(panel.Center - beam.Center, beam.Axis);
        float halfSheet = panel.Width * 0.5f;
        float wallReach = Mathf.Min(beam.HalfSpan, sheetCenterT + halfSheet) -
                          Mathf.Max(-beam.HalfSpan, sheetCenterT - halfSheet);
        if (wallReach <= SheetEdgeTol)
            return;   // disjoint along the beam's axis

        // Divide only when the beam covers the sheet's full width.
        if (sheetCenterT - halfSheet >= -beam.HalfSpan - EndMargin &&
            sheetCenterT + halfSheet <= beam.HalfSpan + EndMargin)
            AddCut(o.PanelCutsH, panelIdx, dy, cutInset);
        else
            o.PanelRemoved.Add(panelIdx);       // beam ends mid-sheet: no clean sub-bay
        o.Link(panelIdx, linkIdx);
    }

    static void AddCut(Dictionary<int, List<PanelCut>> map, int panelIdx, float offset, float inset)
    {
        if (!map.TryGetValue(panelIdx, out List<PanelCut> cuts))
            map[panelIdx] = cuts = new List<PanelCut>();
        foreach (PanelCut c in cuts)
            if (Mathf.Abs(c.Pos - offset) < NeospaceUnits.Mm(2f))
                return;
        cuts.Add(new PanelCut { Pos = offset, Inset = inset });
    }

    static float PlanDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // ------------------------------------------------------------------
    // Pose feasibility (ghost / drag / rotate / duplicate)
    // ------------------------------------------------------------------

    /// <summary>
    /// Would the candidate parts merge legally with every placed instance?
    /// Instances in <paramref name="ignore"/> (the selection being moved)
    /// are excluded. When blocked, <paramref name="reason"/> explains why
    /// in user language.
    /// </summary>
    public static bool PoseBlocked(IReadOnlyList<SpaceInstance> instances,
        ICollection<SpaceInstance> ignore, List<PartRecord> candidate, out string reason)
    {
        var recs = new List<PartRecord>();
        for (int i = 0; i < instances.Count; i++)
        {
            SpaceInstance inst = instances[i];
            if (inst == null || (ignore != null && ignore.Contains(inst)))
                continue;
            recs.AddRange(RecordsFrom(inst.transform));
        }
        recs.AddRange(candidate);

        Outcome o = Analyze(recs);
        reason = o.Reason;
        if (o.Blocked)
            LogBlockedPose(o.Reason, recs);
        return o.Blocked;
    }

    // ------------------------------------------------------------------
    // Diagnostics
    // ------------------------------------------------------------------

    static string _lastLoggedReason;
    static float _lastLogTime;

    /// <summary>
    /// One console line per distinct blocked pose (throttled): the rule that
    /// fired plus every record's geometry in millimetres. Lands in Editor.log,
    /// so a refused merge can be diagnosed exactly instead of guessed at.
    /// </summary>
    static void LogBlockedPose(string reason, List<PartRecord> recs)
    {
        if (reason == _lastLoggedReason && Time.realtimeSinceStartup - _lastLogTime < 2f)
            return;
        _lastLoggedReason = reason;
        _lastLogTime = Time.realtimeSinceStartup;

        var sb = new System.Text.StringBuilder();
        sb.Append("[SpaceMerge] Pose blocked: ").Append(reason).Append('\n');
        for (int i = 0; i < recs.Count; i++)
        {
            PartRecord r = recs[i];
            if (r.IsPanel)
            {
                sb.Append($"  #{i} Panel c=({DumpMm(r.Center.x)},{DumpMm(r.Center.y)},{DumpMm(r.Center.z)}) " +
                          $"n={DumpAxis(r.Normal)} w={DumpMm(r.Width)} h={DumpMm(r.Height)}\n");
            }
            else
            {
                sb.Append($"  #{i} {r.Id} c=({DumpMm(r.Center.x)},{DumpMm(r.Center.y)},{DumpMm(r.Center.z)}) " +
                          $"axis={DumpAxis(r.Axis)} half={DumpMm(r.HalfSpan)} " +
                          $"y=[{DumpMm(r.MinY)},{DumpMm(r.MaxY)}]\n");
            }
        }
        Debug.Log(sb.ToString());
    }

    static float DumpMm(float units) => Mathf.Round(NeospaceUnits.ToMm(units) * 10f) / 10f;

    static string DumpAxis(Vector3 v)
    {
        float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);
        if (ax >= ay && ax >= az) return "X";
        return ay >= az ? "Y" : "Z";
    }

    // ------------------------------------------------------------------
    // Apply — derived merged view
    // ------------------------------------------------------------------

    /// <summary>
    /// Recompute the merged view of the whole space: restore everything,
    /// hide duplicates/covered/split/divided originals, spawn derived split
    /// visuals, and update <see cref="PriceDelta"/>. Call after every
    /// placement, move, rotation, duplicate, delete, undo and code load.
    /// </summary>
    public static void Apply(IReadOnlyList<SpaceInstance> instances,
        PartDatabase partDatabase, Transform derivedParent)
    {
        PriceDelta = 0f;
        _groups.Clear();
        _groupJoints.Clear();
        ClearDerived();

        var recs = new List<PartRecord>();
        var owner = new List<int>();   // record index → instance index
        for (int i = 0; i < instances.Count; i++)
        {
            if (instances[i] == null)
                continue;
            int before = recs.Count;
            recs.AddRange(RecordsFrom(instances[i].transform));
            for (int r = before; r < recs.Count; r++)
                owner.Add(i);
        }

        Outcome o = Analyze(recs);
        if (o.Blocked)
        {
            // Defensive: an illegal arrangement slipped in (old save/undo
            // state). Show raw parts rather than guessing at a merge.
            Debug.LogWarning($"[SpaceMerge] Unresolvable arrangement: {o.Reason}");
            foreach (PartRecord r in recs)
                if (r.Tr != null)
                    r.Tr.gameObject.SetActive(true);
            return;
        }

        ComputeGroups(instances, owner, recs, o);
        EnsureDerivedRoot(derivedParent);

        for (int i = 0; i < recs.Count; i++)
        {
            PartRecord r = recs[i];
            if (r.Tr == null)
                continue;

            bool hide = o.Hidden[i] || o.BeamPlans.ContainsKey(i) || o.PanelRemoved.Contains(i) ||
                        o.PanelCutsW.ContainsKey(i) || o.PanelCutsH.ContainsKey(i) ||
                        o.PanelSubtract.ContainsKey(i);
            if (r.Tr.gameObject.activeSelf == hide)
                r.Tr.gameObject.SetActive(!hide);
            if (hide)
                PriceDelta -= r.Price;
        }

        foreach (KeyValuePair<int, List<BeamSplit.Segment>> kv in o.BeamPlans)
            SpawnBeamSegments(recs[kv.Key], kv.Value, partDatabase,
                recs[kv.Key].Center, recs[kv.Key].Axis);

        foreach (LineRebuild lr in o.LineRebuilds)
            SpawnBeamSegments(recs[lr.TemplateIdx], lr.Segments, partDatabase,
                lr.CenterWorld, lr.Axis);

        var panelIdx = new HashSet<int>();
        foreach (int k in o.PanelCutsW.Keys) panelIdx.Add(k);
        foreach (int k in o.PanelCutsH.Keys) panelIdx.Add(k);
        foreach (int k in o.PanelSubtract.Keys) panelIdx.Add(k);
        foreach (int k in panelIdx)
        {
            if (o.Hidden[k] || o.PanelRemoved.Contains(k))
                continue;
            o.PanelCutsW.TryGetValue(k, out List<PanelCut> cutsW);
            o.PanelCutsH.TryGetValue(k, out List<PanelCut> cutsH);
            o.PanelSubtract.TryGetValue(k, out List<Vector4> subtract);
            SpawnPanelPieces(recs[k], cutsW, cutsH, subtract);
        }

        // The physical frame configuration just changed (merge resolved,
        // splits spawned, panels re-cut) — the finish dressing re-reads it
        // immediately: merge / frame change first, finish second. The panel
        // restyler gets the same ping (derived visuals are new objects).
        FinishController.NotifyStructureChanged();
        FinishStyleController.RequestSweep();
    }

    /// <summary>
    /// Union-find over the interaction links: every pair of records that
    /// merged, split or divided each other joins their owning instances into
    /// one group. Pieces merely sitting near each other never link.
    /// </summary>
    static void ComputeGroups(IReadOnlyList<SpaceInstance> instances, List<int> owner,
        List<PartRecord> recs, Outcome o)
    {
        _groups.Clear();
        _groupJoints.Clear();
        int n = instances.Count;
        if (n == 0 || o.Links.Count == 0)
            return;

        var parent = new int[n];
        for (int i = 0; i < n; i++)
            parent[i] = i;

        int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }
            return x;
        }

        foreach ((int ra, int rb) in o.Links)
        {
            int a = owner[ra], b = owner[rb];
            if (a == b)
                continue;
            a = Find(a);
            b = Find(b);
            if (a != b)
                parent[b] = a;
        }

        var byRoot = new Dictionary<int, List<SpaceInstance>>();
        var rootOf = new Dictionary<int, int>();   // group list index → root
        for (int i = 0; i < n; i++)
        {
            if (instances[i] == null)
                continue;
            int root = Find(i);
            if (!byRoot.TryGetValue(root, out List<SpaceInstance> list))
                byRoot[root] = list = new List<SpaceInstance>();
            list.Add(instances[i]);
        }
        foreach (KeyValuePair<int, List<SpaceInstance>> kv in byRoot)
        {
            if (kv.Value.Count <= 1)
                continue;
            rootOf[_groups.Count] = kv.Key;
            _groups.Add(kv.Value);
            _groupJoints.Add(new List<(Vector3, Vector3)>());
        }

        // Joint segments: the skeleton lines of every cross-instance linked
        // member (skipping panels), grouped with their merged group — these
        // mark WHERE two pieces fused, for the snap flash.
        var groupByRoot = new Dictionary<int, int>();
        foreach (KeyValuePair<int, int> kv in rootOf)
            groupByRoot[kv.Value] = kv.Key;

        foreach ((int ra, int rb) in o.Links)
        {
            if (owner[ra] == owner[rb])
                continue;
            if (!groupByRoot.TryGetValue(Find(owner[ra]), out int g))
                continue;
            List<(Vector3, Vector3)> joints = _groupJoints[g];
            if (joints.Count >= 64)
                continue;
            AddJointSegment(joints, recs[ra]);
            AddJointSegment(joints, recs[rb]);
        }
    }

    static void AddJointSegment(List<(Vector3, Vector3)> joints, in PartRecord r)
    {
        if (r.IsPanel)
            return;
        Vector3 a, b;
        if (r.IsVertical)
        {
            a = new Vector3(r.Center.x, r.MinY, r.Center.z);
            b = new Vector3(r.Center.x, r.MaxY, r.Center.z);
        }
        else
        {
            a = r.Center - r.Axis * r.HalfSpan;
            b = r.Center + r.Axis * r.HalfSpan;
        }
        foreach ((Vector3 ea, Vector3 eb) in joints)
            if ((ea - a).sqrMagnitude < 1e-4f && (eb - b).sqrMagnitude < 1e-4f)
                return;
        joints.Add((a, b));
    }

    static void SpawnBeamSegments(in PartRecord beam, List<BeamSplit.Segment> plan, PartDatabase db,
        Vector3 runCenter, Vector3 runAxis)
    {
        string prefix = beam.Id.Substring(0, beam.Id.Length - beam.Size.ToString().Length);
        foreach (BeamSplit.Segment seg in plan)
        {
            string segId = prefix + seg.Size;
            GameObject prefab = db != null ? db.GetRealPrefab(segId) : null;
            if (prefab == null)
            {
                // Should not happen (Plan only returns catalogue sizes) —
                // fall back to showing the original rather than a hole.
                Debug.LogWarning($"[SpaceMerge] No prefab for {segId}; keeping {beam.Id} whole.");
                if (beam.Tr != null)
                    beam.Tr.gameObject.SetActive(true);
                return;
            }

            GameObject go = Object.Instantiate(prefab, _derivedRoot);
            go.name = "Merged_" + segId;
            StripToVisual(go);
            SetLayer(go, beam.Tr != null ? beam.Tr.gameObject.layer : 0);
            go.transform.rotation = beam.Rot;
            go.transform.position = Vector3.zero;
            if (TryMeshBounds(go.transform, out Bounds b))
            {
                Vector3 desired = runCenter + runAxis * NeospaceUnits.Mm(seg.CenterOffsetMm);
                go.transform.position = desired - b.center;
            }
            PriceDelta += PriceOfBeam(segId);
        }
    }

    static void SpawnPanelPieces(in PartRecord panel, List<PanelCut> cutsW, List<PanelCut> cutsH,
        List<Vector4> subtract)
    {
        if (panel.Tr == null)
            return;

        List<(float lo, float hi)> spansW = SubSpans(panel.Width, cutsW);
        List<(float lo, float hi)> spansH = SubSpans(panel.Height, cutsH);
        float sliver = NeospaceUnits.Mm(30f);

        foreach ((float loW, float hiW) in spansW)
        {
            foreach ((float loH, float hiH) in spansH)
            {
                float w = hiW - loW, h = hiH - loH;
                if (w < sliver || h < sliver)
                    continue;

                // Regions ceded to an earlier overlapping sheet stay empty.
                if (subtract != null)
                {
                    float cw = (loW + hiW) * 0.5f, ch = (loH + hiH) * 0.5f;
                    bool gone = false;
                    foreach (Vector4 rect in subtract)
                        if (cw > rect.x && cw < rect.y && ch > rect.z && ch < rect.w)
                        {
                            gone = true;
                            break;
                        }
                    if (gone)
                        continue;
                }

                GameObject go = Object.Instantiate(panel.Tr.gameObject, _derivedRoot);
                go.name = "MergedPanel";
                go.SetActive(true);
                StripToVisual(go);
                go.transform.rotation = panel.Rot;
                go.transform.localScale = new Vector3(w, h, panel.Thick);
                go.transform.position = panel.Center +
                    panel.Right * ((loW + hiW) * 0.5f) +
                    panel.Up * ((loH + hiH) * 0.5f);
                PriceDelta += PanelPrice();
            }
        }
    }

    /// <summary>Sub-intervals of [-extent/2, extent/2] between cut planes, edges kept, per-cut inset.</summary>
    static List<(float, float)> SubSpans(float extent, List<PanelCut> cuts)
    {
        var result = new List<(float, float)>();
        float half = extent * 0.5f;
        if (cuts == null || cuts.Count == 0)
        {
            result.Add((-half, half));
            return result;
        }

        var sorted = new List<PanelCut>(cuts);
        sorted.Sort((x, y) => x.Pos.CompareTo(y.Pos));

        float lo = -half;
        foreach (PanelCut cut in sorted)
        {
            result.Add((lo, cut.Pos - cut.Inset));
            lo = cut.Pos + cut.Inset;
        }
        result.Add((lo, half));
        return result;
    }

    // ------------------------------------------------------------------
    // Derived visuals bookkeeping
    // ------------------------------------------------------------------

    /// <summary>
    /// Container of the merge-derived visuals (split beam segments named
    /// "Merged_&lt;id&gt;", panel pieces named "MergedPanel"). Together with the
    /// still-active instance children these ARE the current physical frame
    /// configuration — consumers like the Finish dressing must read both.
    /// </summary>
    public static Transform DerivedRoot => _derivedRoot;

    static void EnsureDerivedRoot(Transform parent)
    {
        if (_derivedRoot == null)
        {
            var go = new GameObject("SpaceMergeDerived");
            _derivedRoot = go.transform;
        }
        if (parent != null && _derivedRoot.parent != parent)
            _derivedRoot.SetParent(parent, true);
    }

    static void ClearDerived()
    {
        if (_derivedRoot == null)
            return;
        for (int i = _derivedRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = _derivedRoot.GetChild(i).gameObject;
            child.SetActive(false);   // invisible immediately; Destroy is deferred
            Object.Destroy(child);
        }
    }

    /// <summary>Show / hide the derived split visuals (mode switching).</summary>
    public static void ShowDerived(bool show)
    {
        if (_derivedRoot != null)
            _derivedRoot.gameObject.SetActive(show);
    }

    static void StripToVisual(GameObject go)
    {
        for (int pass = 0; pass < 4; pass++)
        {
            MonoBehaviour[] scripts = go.GetComponentsInChildren<MonoBehaviour>(true);
            if (scripts.Length == 0)
                break;
            foreach (MonoBehaviour mb in scripts)
                if (mb != null)
                    Object.DestroyImmediate(mb);
        }
        foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>(true))
            if (rb != null)
                Object.DestroyImmediate(rb);
        foreach (Collider col in go.GetComponentsInChildren<Collider>(true))
            if (col != null)
                Object.DestroyImmediate(col);
    }

    static void SetLayer(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = layer;
    }

    // ------------------------------------------------------------------
    // Prices
    // ------------------------------------------------------------------

    static UIBuildStats Stats()
    {
        if (_stats == null)
            _stats = Object.FindFirstObjectByType<UIBuildStats>();
        return _stats;
    }

    static float PriceOfBeam(string id)
    {
        UIBuildStats stats = Stats();
        return stats != null ? stats.PriceForPart(id) : 0f;
    }

    static float PanelPrice()
    {
        UIBuildStats stats = Stats();
        return stats != null ? stats.panelPrice : 0f;
    }
}
