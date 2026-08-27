using System.Collections.Generic;
using UnityEngine;

/// <summary>Golden checks ported from Rhino neospace/selftest.py (frames/panels core).</summary>
public static class NeospaceSelfTest
{
    public static int RunAll(out List<string> failures)
    {
        var failureList = new List<string>();
        failures = failureList;
        int total = 0;

        void Check(bool ok, string label)
        {
            total++;
            if (!ok)
                failureList.Add(label);
        }

        Check(Near(Skeleton.VBodyLength(9), 745f), "V9 body = 745");
        Check(Near(Skeleton.HBodyLength(7), 663f), "H7 body = 663");
        Check(Near(Skeleton.VSkeletonLength(9), 704f), "V9 skeleton = 704");
        Check(Near(Skeleton.HSkeletonLength(7), 704f), "H7 skeleton = 704");
        Check(Near(Skeleton.VSkeletonLength(12), 968f), "V12 skeleton = 968");
        Check(Near(Skeleton.HSkeletonLength(10), 968f), "H10 skeleton = 968");
        Check(Near(Skeleton.VeneerContactLength(9), 839f), "Veneer H9 contact = 839");
        Check(Skeleton.CorrespondingHSize(9) == 7, "V9 corresponds to H7");
        Check(Near(Skeleton.HSkeletonLength(1), 176f), "H1 span = 176");
        Check(Near(Skeleton.HSkeletonLength(23), 2112f), "H23 span = 2112");
        Check(Near(Skeleton.VSkeletonLength(29), 2464f), "V29 height = 2464");
        Check(Near(Skeleton.VSkeletonLength(1), 0f), "V1 = point condition");

        List<float> off9 = Skeleton.VPointOffsets(9);
        Check(off9.Count == 9, "V9 point count = 9");
        Check(Near(off9[0], -352f) && Near(off9[off9.Count - 1], 352f), "V9 ends +-352");
        Check(Near(off9[4], 0f), "V9 centre point at basepoint");

        List<float> off12 = Skeleton.VPointOffsets(12);
        Check(off12.Count == 12, "V12 point count = 12");
        Check(Near(off12[5], -44f) && Near(off12[6], 44f), "V12 closest +-44");
        Check(Near(off12[0], -484f) && Near(off12[off12.Count - 1], 484f), "V12 ends +-484");
        Check(!off12.Contains(0f), "V12 basepoint is not a point");

        List<Skeleton.HPointOffset> h7 = Skeleton.HPointOffsets(7);
        Check(h7.Count == 9, "H7 skeleton points = 9");
        Check(Near(h7[0].OffsetMm, -352f) && h7[0].IsJointEnd &&
              Near(h7[h7.Count - 1].OffsetMm, 352f) && h7[h7.Count - 1].IsJointEnd,
            "H7 joint ends +-352");
        Check(Near(h7[1].OffsetMm, -264f) && !h7[1].IsJointEnd, "H7 internal at -264");

        Naming.ParsedName p = Naming.Parse("HT7");
        Check(p != null && p.Type == "HT" && p.SizeInt == 7, "HT7 parses as HT, not H");
        p = Naming.Parse("V9 Cable Hole");
        Check(p != null && p.Type == "V" && p.Variation == "Cable Hole", "V9 Cable Hole");
        p = Naming.Parse("Panel H11xH7");
        Check(p != null && p.Family == Naming.Panel && p.SizeA == 11 && p.SizeB == 7, "Panel H11xH7");
        Check(Naming.Parse("Cap Side") != null, "Cap Side");
        Check(Naming.Parse("Foot") != null, "Foot");
        Check(Naming.ToUnityPartId("HT7") == "T7", "HT7 -> T7");
        Check(Naming.FromUnityPartId("T7") == "HT7", "T7 -> HT7");

        foreach (string bad in new[]
                 {
                     "H 7", "H-7", "Panel H11 x H7", "Panel 7x7", "Outer Veneer H7",
                     "Side Cap", "CapSide", "Foot V9", "H7 Left", "Vertical 9",
                     "Cap End H7", "h7"
                 })
        {
            Check(Naming.Parse(bad) == null, "reject '" + bad + "'");
        }

        Check(!Catalogue.IsInCatalogue(Naming.Parse("V14")), "V14 not produced");
        Check(!Catalogue.IsInCatalogue(Naming.Parse("V22")), "V22 not produced");
        Check(!Catalogue.IsInCatalogue(Naming.Parse("V2")), "V2 not in catalogue");
        Check(!Catalogue.IsInCatalogue(Naming.Parse("H13")), "H13 not in catalogue");
        Check(!Catalogue.IsInCatalogue(Naming.Parse("V1 Cable Hole")), "V1 Cable Hole not in catalogue");
        Check(Catalogue.IsValidPanelPair(15, 11), "H15xH11 valid");
        Check(Catalogue.IsValidPanelPair(11, 15), "pair order-insensitive");
        Check(!Catalogue.IsValidPanelPair(15, 15), "H15xH15 invalid");
        Check(!Catalogue.IsValidPanelPair(1, 1), "H1xH1 not produced");
        Check(!Catalogue.IsValidPanelPair(9, 3), "9-edge boards retired");
        Check(!Catalogue.IsValidPanelPair(19, 7), "19-edge boards retired");
        Check(Catalogue.MaxPartnerEdge(15) == 11, "H15 partner cap = H11");
        Check(Catalogue.MaxPartnerEdge(19) == null, "H19 boards gone from catalogue");

        // --- Panel board dimensions and grid decomposition (PanelFill) ---
        Check(Near(PanelFill.EdgeMm(1), 134.717f), "H1 edge = 134.717mm");
        Check(Near(PanelFill.EdgeMm(7), 662.717f), "H7 edge = 662.717mm");
        Check(Near(PanelFill.EdgeMm(15), 1366.717f), "H15 edge = 1366.717mm");
        Check(PanelFill.TryModulesFromOpeningMm(663f, out int pfMod) && pfMod == 8,
            "663mm opening = 8 modules");
        Check(!PanelFill.TryModulesFromOpeningMm(700f, out _), "700mm opening not modular");

        var pfRuns = PanelFill.Decompositions(10);
        Check(pfRuns.Count > 0 && ListEq(pfRuns[0], 5, 3), "10 modules -> H5+H3 (balanced beats H7+H1)");
        Check(PanelFill.Decompositions(3).Count == 0, "odd module span cannot be summed");

        PanelFill.GridPlan pfPlan = PanelFill.PlanGrid(8, 8);
        Check(pfPlan.Ok && pfPlan.BoardCount == 1 &&
              pfPlan.Columns[0] == 7 && pfPlan.Rows[0] == 7, "8x8 modules = one H7xH7");
        pfPlan = PanelFill.PlanGrid(16, 16);
        Check(pfPlan.Ok && pfPlan.BoardCount == 2 && pfPlan.Columns.Count == 1 &&
              pfPlan.Columns[0] == 15 && ListEq(pfPlan.Rows, 7, 7),
            "16x16 (H15xH15 not produced) -> two H15xH7 with a beam divider");
        pfPlan = PanelFill.PlanGrid(20, 8);
        Check(pfPlan.Ok && ListEq(pfPlan.Columns, 11, 7) && ListEq(pfPlan.Rows, 7),
            "20x8 -> H11+H7 columns with a V9 post divider");
        pfPlan = PanelFill.PlanGrid(2, 2);
        Check(!pfPlan.Ok, "2x2 (H1xH1 board not produced) fails");
        pfPlan = PanelFill.PlanGrid(8, 3);
        Check(!pfPlan.Ok, "odd height opening fails");

        Check(ListEq(Division.SegmentSizes(7, new[] { 4 }), 3, 3), "H7 cut@4 -> H3+H3");
        Check(ListEq(Division.SegmentSizes(7, new[] { 2 }), 1, 5), "H7 cut@2 -> H1+H5");
        Check(ListEq(Division.SegmentSizes(7, new[] { 6 }), 5, 1), "H7 cut@6 -> H5+H1");
        Check(Division.SegmentSizes(7, new[] { 3 }) == null, "H7 cut@3 invalid (even)");
        Check(Division.SegmentSizes(7, new[] { 1 }) == null, "H7 cut@1 invalid (H0)");
        Check(ListEq(Division.DivisiblePoints(7), 2, 4, 6), "H7 divisible pts");
        Check(ListEq(Division.DivisiblePoints(15), 4, 6, 8, 10, 12), "H15 divisible pts (skips H13)");
        Check(ListEq(Division.SegmentSizes(15, new[] { 4, 8 }), 3, 3, 7), "H15 double cut -> H3+H3+H7");
        Check(ListEq(Division.SegmentSizes(7, new[] { 2, 4 }), 1, 1, 3), "H7 double cut -> H1+H1+H3");
        Check(ListEq(Division.SegmentSizes(11, new[] { 8 }), 7, 3), "H11 cut@8 -> H7+H3");
        Check(Division.DivisiblePoints(1).Count == 0, "H1 not divisible");

        Catalogue.SnapEntry? snap = Catalogue.SnapToTable(700f, Catalogue.HSpanSnapTable(), 20f);
        Check(snap.HasValue && Near(snap.Value.DistanceMm, 704f) && snap.Value.Size == 7,
            "700mm snaps to H7 span");
        Check(Catalogue.SnapToTable(800f, Catalogue.HSpanSnapTable(), 20f) == null,
            "800mm snaps to nothing at 20mm tol");

        // --- Coaxial overlap, longer wins (ported from Rhino overlap.py) ---

        // Vertical: V9 skeleton = 704 (half 352), V13 skeleton = 1056 (half 528).
        var v9At352 = new[] { new Overlap.VFrameRecord(0f, 0f, 352f, 9) }; // span [0, 704]

        Overlap.Resolution r = Overlap.ResolveVertical(9, 0f, 0f, 352f, v9At352);
        Check(r.Mode == Overlap.Mode.Skip && r.DeleteIndices.Count == 0, "V equal span -> Skip");

        r = Overlap.ResolveVertical(9, 0f, 0f, 352f,
            new[] { new Overlap.VFrameRecord(0f, 0f, 528f, 13) }); // existing [0, 1056]
        Check(r.Mode == Overlap.Mode.Skip, "V shorter inside longer -> Skip");

        r = Overlap.ResolveVertical(13, 0f, 0f, 528f, v9At352);
        Check(r.Mode == Overlap.Mode.Insert && ListEq(r.DeleteIndices, 0),
            "V longer covers shorter -> Insert + delete");

        r = Overlap.ResolveVertical(9, 0f, 0f, 528f, v9At352); // new span [176, 880]
        Check(r.Mode == Overlap.Mode.Block, "V partial overlap -> Block");

        r = Overlap.ResolveVertical(9, 100f, 0f, 352f, v9At352);
        Check(r.Mode == Overlap.Mode.Insert && r.DeleteIndices.Count == 0,
            "V different axis -> Insert");

        r = Overlap.ResolveVertical(9, 0f, 0f, 1144f, v9At352); // new span [792, 1496], 88 gap
        Check(r.Mode == Overlap.Mode.Insert && r.DeleteIndices.Count == 0,
            "V disjoint stacked spans -> Insert");

        r = Overlap.ResolveVertical(29, 0f, 0f, 1232f, new[]
        {
            new Overlap.VFrameRecord(0f, 0f, 352f, 9),
            new Overlap.VFrameRecord(0f, 0f, 2112f, 9),
        }); // V29 span [0, 2464] covers both V9s [0,704] + [1760,2464]
        Check(r.Mode == Overlap.Mode.Insert && ListEq(r.DeleteIndices, 0, 1),
            "V covers two shorter -> Insert + delete both");

        // --- Merge splits: piece sizes AND positions (geometry preserved) ---

        // The owner's worked example: H15 replaced by a middle split must read
        // 663 + 41 + 663 = 1367 (two H7 bodies around one post profile).
        Check(Near(Skeleton.HBodyLength(7) * 2f + CatalogueData.ProfileMm, Skeleton.HBodyLength(15)),
            "H15 body = 663 + 41 + 663");

        List<BeamSplit.Segment> segs = BeamSplit.Plan(15, new[] { 8 });
        Check(segs != null && segs.Count == 2 &&
              segs[0].Size == 7 && Near(segs[0].CenterOffsetMm, -352f) &&
              segs[1].Size == 7 && Near(segs[1].CenterOffsetMm, 352f),
            "H15 mid split -> H7@-352 + H7@+352");

        segs = BeamSplit.Plan(7, new[] { 2 });
        Check(segs != null && segs.Count == 2 &&
              segs[0].Size == 1 && Near(segs[0].CenterOffsetMm, -264f) &&
              segs[1].Size == 5 && Near(segs[1].CenterOffsetMm, 88f),
            "H7 cut@2 -> H1@-264 + H5@+88");

        segs = BeamSplit.Plan(15, new[] { 4, 8 });
        Check(segs != null && segs.Count == 3 &&
              segs[0].Size == 3 && Near(segs[0].CenterOffsetMm, -528f) &&
              segs[1].Size == 3 && Near(segs[1].CenterOffsetMm, -176f) &&
              segs[2].Size == 7 && Near(segs[2].CenterOffsetMm, 352f),
            "H15 double cut -> H3+H3+H7 positioned");

        Check(BeamSplit.Plan(7, new[] { 3 }) == null, "H7 cut@3 refused (even piece)");
        Check(BeamSplit.Plan(15, new[] { 14 }) == null, "H15 cut@14 refused (H13 not produced)");

        Check(BeamSplit.TryCutIndexFromOffset(15, 0f, 2f, out int cut) && cut == 8,
            "offset 0 on H15 -> point 8");
        Check(BeamSplit.TryCutIndexFromOffset(15, -352f, 2f, out cut) && cut == 4,
            "offset -352 on H15 -> point 4");
        Check(!BeamSplit.TryCutIndexFromOffset(15, 44f, 2f, out _),
            "off-point offset refused");
        Check(!BeamSplit.TryCutIndexFromOffset(15, 704f, 2f, out _),
            "joint-end offset refused");

        // --- Line rebuild: partially overlapping coaxial runs ---

        // Two H7s offset by 4 modules (352 mm), posts at every junction:
        // union 0..1056 with posts at 352 and 704 -> H3 + H3 + H3.
        var lineSpans = new List<(float lo, float hi)>
        {
            (0f, 704f), (352f, 1056f)
        };
        var linePosts = new List<float> { 0f, 352f, 704f, 1056f };
        segs = BeamSplit.PlanLine(lineSpans, linePosts, 2f, out float center, out string lineError);
        Check(segs != null && segs.Count == 3 &&
              segs[0].Size == 3 && segs[1].Size == 3 && segs[2].Size == 3 &&
              Near(center, 528f) && Near(segs[0].CenterOffsetMm, -352f) &&
              Near(segs[1].CenterOffsetMm, 0f) && Near(segs[2].CenterOffsetMm, 352f),
            "line rebuild: H7+H7 offset 4 -> H3+H3+H3");

        // The user's exact blocked pose: two H7s offset by 3 modules (264 mm).
        // Post gaps of 3 modules need an H2, which is not produced — refused.
        lineSpans = new List<(float lo, float hi)> { (0f, 704f), (264f, 968f) };
        linePosts = new List<float> { 0f, 264f, 704f, 968f };
        Check(BeamSplit.PlanLine(lineSpans, linePosts, 2f, out _, out lineError) == null &&
              lineError != null && lineError.Contains("H2"),
            "line rebuild: odd 3-module offset refused with H2 reason");

        // Two H15s offset by 8 modules: union 24 modules, posts at thirds
        // -> H7 + H7 + H7 (the classic shelf-row merge).
        lineSpans = new List<(float lo, float hi)> { (0f, 1408f), (704f, 2112f) };
        linePosts = new List<float> { 0f, 704f, 1408f, 2112f };
        segs = BeamSplit.PlanLine(lineSpans, linePosts, 2f, out center, out lineError);
        Check(segs != null && segs.Count == 3 &&
              segs[0].Size == 7 && segs[1].Size == 7 && segs[2].Size == 7,
            "line rebuild: H15+H15 offset 8 -> H7+H7+H7");

        // A junction inside the union without a post cannot be built.
        lineSpans = new List<(float lo, float hi)> { (0f, 704f), (352f, 1056f) };
        linePosts = new List<float> { 0f, 1056f };
        Check(BeamSplit.PlanLine(lineSpans, linePosts, 2f, out _, out lineError) == null,
            "line rebuild: junction without post refused");

        // Disjoint runs (a coverage gap) are not one line.
        lineSpans = new List<(float lo, float hi)> { (0f, 704f), (792f, 1496f) };
        linePosts = new List<float> { 0f, 704f, 792f, 1496f };
        Check(BeamSplit.PlanLine(lineSpans, linePosts, 2f, out _, out lineError) == null,
            "line rebuild: gapped spans refused");

        // Connector span classification: planned H7 span (0,0,0)->(704,0,0).
        Vector3 pa = new Vector3(0f, 0f, 0f), pb = new Vector3(704f, 0f, 0f);
        Check(Overlap.ClassifySpanOverlap(pa, pb, pa, pb) == Overlap.SpanClass.Equal,
            "span equal");
        Check(Overlap.ClassifySpanOverlap(pa, pb,
                new Vector3(176f, 0f, 0f), new Vector3(528f, 0f, 0f)) == Overlap.SpanClass.InsideNew,
            "span inside_new");
        Check(Overlap.ClassifySpanOverlap(pa, pb,
                new Vector3(-88f, 0f, 0f), new Vector3(792f, 0f, 0f)) == Overlap.SpanClass.CoversNew,
            "span covers_new");
        Check(Overlap.ClassifySpanOverlap(pa, pb,
                new Vector3(352f, 0f, 0f), new Vector3(1056f, 0f, 0f)) == Overlap.SpanClass.Partial,
            "span partial");
        Check(Overlap.ClassifySpanOverlap(pa, pb,
                new Vector3(704f, 0f, 0f), new Vector3(1408f, 0f, 0f)) == Overlap.SpanClass.None,
            "chained endpoint-touch -> None");
        Check(Overlap.ClassifySpanOverlap(pa, pb,
                new Vector3(176f, 10f, 0f), new Vector3(528f, 10f, 0f)) == Overlap.SpanClass.None,
            "off-line span -> None");

        // Connector longer-wins resolution.
        Vector3 up = new Vector3(0f, 1f, 0f);
        var h7Same = new[] { new Overlap.ConnectorRecord(pa, pb, "H7", up) };

        r = Overlap.ResolveConnectorSpan(pa, pb, "H7", h7Same);
        Check(r.Mode == Overlap.Mode.Skip, "H equal same name -> Skip");

        r = Overlap.ResolveConnectorSpan(pa, pb, "HT7", h7Same);
        Check(r.Mode == Overlap.Mode.Block, "H equal different name -> Block");

        r = Overlap.ResolveConnectorSpan(pa, pb, "H7", new[]
        {
            new Overlap.ConnectorRecord(new Vector3(176f, 0f, 0f), new Vector3(528f, 0f, 0f), "H3", up),
        });
        Check(r.Mode == Overlap.Mode.Insert && ListEq(r.DeleteIndices, 0),
            "H covers shorter -> Insert + delete");

        r = Overlap.ResolveConnectorSpan(pa, pb, "H7", new[]
        {
            new Overlap.ConnectorRecord(new Vector3(-88f, 0f, 0f), new Vector3(792f, 0f, 0f), "H9", up),
        });
        Check(r.Mode == Overlap.Mode.Skip, "H covered by longer -> Skip");

        r = Overlap.ResolveConnectorSpan(pa, pb, "H7", new[]
        {
            new Overlap.ConnectorRecord(new Vector3(352f, 0f, 0f), new Vector3(1056f, 0f, 0f), "H7", up),
        });
        Check(r.Mode == Overlap.Mode.Block, "H partial overlap -> Block");

        r = Overlap.ResolveConnectorSpan(pa, pb, "H7", new[]
        {
            new Overlap.ConnectorRecord(new Vector3(704f, 0f, 0f), new Vector3(1408f, 0f, 0f), "H7", up),
        });
        Check(r.Mode == Overlap.Mode.Insert && r.DeleteIndices.Count == 0,
            "H chained connectors -> Insert");

        r = Overlap.ResolveConnectorSpan(pa, pb, "H7", h7Same, new[] { up });
        Check(r.Mode == Overlap.Mode.Skip, "H equal + matching roll -> Skip");

        r = Overlap.ResolveConnectorSpan(pa, pb, "H7", h7Same, new[] { new Vector3(0f, -1f, 0f) });
        Check(r.Mode == Overlap.Mode.Skip, "H equal + 180-degree roll -> Skip");

        r = Overlap.ResolveConnectorSpan(pa, pb, "H7", h7Same, new[] { new Vector3(0f, 0f, 1f) });
        Check(r.Mode == Overlap.Mode.Block, "H equal + wrong roll -> Block");

        // --- Finishing coverability (ported from Rhino finishing.py) ---

        Check(ListEq(Finishing.VeneerSpans(),
                2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16),
            "veneer spans = every span 2..16 (full H1-H15 catalogue)");
        Check(!Finishing.Coverable(1) && Finishing.Coverable(2) &&
              Finishing.Coverable(3) && Finishing.Coverable(4),
            "coverable: 1 no, 2 yes, 3 yes, 4 yes");
        Check(Finishing.Coverable(11) && Finishing.Coverable(13) && Finishing.Coverable(15),
            "coverable: 11, 13, 15 all yes");
        Check(ListEq(Finishing.CoverSegment(3), 2), "3 intervals -> one Veneer H2");
        Check(ListEq(Finishing.CoverSegment(7), 6), "7 intervals -> one Veneer H6");
        Check(ListEq(Finishing.CoverSegment(8), 7), "8 intervals -> one Veneer H7");
        Check(ListEq(Finishing.CoverSegment(16), 15), "16 intervals -> one Veneer H15");
        Check(ListEq(Finishing.CoverSegment(20), 9, 9), "20 intervals -> balanced H9+H9");
        Check(ListEq(Finishing.CoverSegment(24), 11, 11), "24 intervals -> H11+H11");
        Check(ListEq(Finishing.CoverSegment(13), 12), "13 intervals -> Veneer H12");

        // V9 with a connector at hole 4: dividers 0/4/8, both halves coverable.
        List<Finishing.Segment> segs9 = Finishing.ChannelSegments(8, new[] { 4 });
        Check(segs9.Count == 2 && segs9[0].Intervals == 4 && segs9[1].Intervals == 4,
            "V9 channel split at hole 4 -> 4 + 4");
        Check(Finishing.ChannelCoverable(8, new[] { 4 }), "V9 split at 4 coverable");
        Check(Finishing.ChannelCoverable(8, new[] { 3 }),
            "V9 split at 3 coverable (H2 + H4 fill 3 and 5)");

        List<string> names = Catalogue.AllBlockNames();
        Check(names.Count == new HashSet<string>(names).Count, "no duplicate catalogue names");
        Check(names.Contains("Panel H15xH11") && names.Contains("HT23 Cable Hole"),
            "expected names present");

        foreach (string n in names)
        {
            Naming.ParsedName parsed = Naming.Parse(n);
            if (parsed == null || !Catalogue.IsInCatalogue(parsed))
                failures.Add("catalogue name round-trip failed: " + n);
            total++;
        }

        return total;
    }

    static bool Near(float a, float b) => System.Math.Abs(a - b) < 1e-3f;

    static bool ListEq(List<int> actual, params int[] expected)
    {
        if (actual == null || actual.Count != expected.Length)
            return false;
        for (int i = 0; i < expected.Length; i++)
        {
            if (actual[i] != expected[i])
                return false;
        }
        return true;
    }
}

