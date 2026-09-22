#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Geometry regressions for the owner's finishing rules: panels hide only
/// their occupied length, exposed channel spans remain dressed, and connector
/// ends never get extrusion caps. The updated supplied veneer catalogue
/// remains authoritative (all H1-H15 sizes, without chamfer variants).
/// </summary>
public static class FinishGeneratorSelfTest
{
    const float EpsilonMm = 1.1f;

    public static int RunAll(out List<string> failures)
    {
        var failed = new List<string>();
        failures = failed;
        int checks = 0;
        var fixtures = new List<GameObject>();
        void Check(bool passed, string label)
        {
            checks++;
            if (!passed) failed.Add("Finishing: " + label);
        }

        try
        {
            var v9 = Frame("V9", 9, Vector3.up, fixtures);
            var bare = Plan(v9);
            Check(HasVeneerAt(bare, v9, Vector3.right, 0f), "bare V9 has veneer at the middle of its +X channel");
            Check(HasCapAt(bare, v9, Vector3.right, -352f) && HasCapAt(bare, v9, Vector3.right, 352f),
                "bare V9 has Cap Sides at its two end holes");
            Check(CountModel(bare, "Cap End") == 1 && CountModel(bare, "Foot") == 1,
                "grounded V9 receives one exposed-end cap and one foot");

            float halfBodyMm = Skeleton.VBodyLength(v9.Size) * 0.5f;
            var topShelf = CrossShelf(v9, halfBodyMm + 0.7f);
            var coveredTop = Plan(v9, topShelf);
            Check(CountModel(coveredTop, "Cap End") == 0,
                "a shelf crossing the actual top cap body suppresses that Cap End");
            Check(CountModel(coveredTop, "Foot") == 1,
                "top-end panel masking preserves the grounded foot");
            var touchingTop = CrossShelf(v9, halfBodyMm + 5.2f);
            Check(CountModel(Plan(v9, touchingTop), "Cap End") == 1,
                "a shelf merely touching the cap's outer surface preserves the end cap");

            // A board beside the end leaves the extrusion face exposed.
            var besideTop = topShelf;
            besideTop.Center += Vector3.forward * NeospaceUnits.Mm(34f);
            Check(CountModel(Plan(v9, besideTop), "Cap End") == 1,
                "a shelf beside the cap leaves its exposed top intact");

            // Real top-hole shelf: its 1 mm board is centered 20.5 mm above
            // the top hole (the body end), and its nearest X/Z edges are
            // 20.6415 mm outside the post center. The cap lip's envelope may
            // overlap this corner seam; the nominal 41 mm end is uncovered.
            var adjacentTop = CrossShelf(v9, halfBodyMm);
            adjacentTop.HalfU = adjacentTop.HalfV = NeospaceUnits.Mm(PanelFill.EdgeMm(7) * 0.5f);
            adjacentTop.HalfN = NeospaceUnits.Mm(0.5f);
            Check(CountModel(Plan(v9, adjacentTop), "Cap End") == 1,
                "a top-hole shelf beside the 41mm post corner preserves its exposed Cap End");

            // Verify measured thickness rather than a fictional oversized
            // cap reaching beyond the post into an adjacent panel. This board
            // covers the nominal end footprint and occupies [3,4] mm beyond
            // the body end: a normal cap ends at 1.2 mm, a thicker one at 4.2.
            var raisedCover = CrossShelf(v9, halfBodyMm + 3.5f);
            raisedCover.Center -= (Vector3.right + Vector3.forward) * NeospaceUnits.Mm(352f);
            raisedCover.HalfN = NeospaceUnits.Mm(0.5f);
            Check(CountModel(Plan(v9, raisedCover), "Cap End") == 1,
                "a panel above the normal cap's thickness leaves that cap exposed");
            var thickerEndCap = new Dictionary<string, FinishGenerator.PartDimensions>
            {
                ["Cap End"] = new FinishGenerator.PartDimensions
                {
                    HalfLengthMm = 21.1504f, HalfWidthMm = 21.15f, HalfThicknessMm = 2f
                }
            };
            Check(CountModel(FinishGenerator.Plan(
                new List<FrameOverlapResolver.FrameRecord> { v9 },
                new List<FinishGenerator.PanelBox> { raisedCover }, thickerEndCap), "Cap End") == 0,
                "end-cap masking uses measured thickness at the nominal extrusion footprint");

            var floatingV = Frame("V9", 9, Vector3.up, fixtures);
            Vector3 raise = Vector3.up * NeospaceUnits.Mm(1000f);
            floatingV.Center += raise;
            floatingV.EndA += raise;
            floatingV.EndB += raise;
            floatingV.Root.position += raise;
            var underShelf = CrossShelf(floatingV, -halfBodyMm - 0.7f);
            var coveredBottom = Plan(floatingV, underShelf);
            Check(CountModel(coveredBottom, "Cap End") == 1 && CountModel(coveredBottom, "Foot") == 0,
                "a shelf under a floating post masks its bottom cap and preserves its exposed top");
            var adjacentBottom = CrossShelf(floatingV, -halfBodyMm);
            adjacentBottom.HalfU = adjacentBottom.HalfV = adjacentTop.HalfU;
            adjacentBottom.HalfN = adjacentTop.HalfN;
            adjacentBottom.Normal = Vector3.down;
            Check(CountModel(Plan(floatingV, adjacentBottom), "Cap End") == 2,
                "a mirrored shelf beside a floating post's bottom corner preserves both exposed Cap Ends");

            var middlePanel = BorderPanel(v9, Vector3.right, -176f, 176f);
            var partial = Plan(v9, middlePanel);
            Check(ClearOfMask(partial, v9, Vector3.right, -176f, 176f),
                "partial side panel has no overlapping +X veneer or Cap Side");
            Check(HasVeneerAt(partial, v9, Vector3.right, -264f),
                "partial side panel preserves the exposed lower +X channel");
            Check(HasVeneerAt(partial, v9, Vector3.right, 264f),
                "partial side panel preserves the exposed upper +X channel");
            Check(HasVeneerAt(partial, v9, Vector3.left, 0f) &&
                  HasVeneerAt(partial, v9, Vector3.forward, 0f) && HasVeneerAt(partial, v9, Vector3.back, 0f),
                "partial side panel does not hide the other three channel faces");

            var full = Plan(v9, BorderPanel(v9, Vector3.right, -400f, 400f));
            Check(CountFaceParts(full, v9, Vector3.right) == 0,
                "full-length side panel removes inward veneers and Cap Sides");
            Check(HasVeneerAt(full, v9, Vector3.left, 0f), "full side panel leaves the outward channel dressed");

            var v17 = Frame("V17", 17, Vector3.up, fixtures);
            var opposite = Plan(v17,
                BorderPanel(v17, Vector3.right, -352f, 0f),
                BorderPanel(v17, Vector3.left, 0f, 352f));
            Check(ClearOfMask(opposite, v17, Vector3.right, -352f, 0f), "+X panel range is clear with panels on both sides");
            Check(ClearOfMask(opposite, v17, Vector3.left, 0f, 352f), "-X panel range is clear with panels on both sides");
            Check(HasVeneerAt(opposite, v17, Vector3.right, 176f), "opposite panel does not extend the +X mask above its own range");
            Check(HasVeneerAt(opposite, v17, Vector3.left, -176f), "opposite panel does not extend the -X mask below its own range");
            Check(HasVeneerAt(opposite, v17, Vector3.right, -528f) && HasVeneerAt(opposite, v17, Vector3.right, 528f),
                "both exposed ends survive on the +X channel");
            Check(HasVeneerAt(opposite, v17, Vector3.left, -528f) && HasVeneerAt(opposite, v17, Vector3.left, 528f),
                "both exposed ends survive on the -X channel");

            var overlapping = Plan(v17,
                BorderPanel(v17, Vector3.right, -352f, 0f),
                BorderPanel(v17, Vector3.right, -176f, 176f));
            Check(ClearOfMask(overlapping, v17, Vector3.right, -352f, 176f), "overlapping board ranges hide their entire union");
            Check(HasVeneerAt(overlapping, v17, Vector3.right, -528f) && HasVeneerAt(overlapping, v17, Vector3.right, 440f),
                "overlapping board ranges preserve both exposed ends");
            Check(Signature(overlapping) == Signature(Plan(v17,
                    BorderPanel(v17, Vector3.right, -176f, 176f), BorderPanel(v17, Vector3.right, -352f, 0f))),
                "overlapping masks produce the same finishing regardless of panel enumeration order");

            var separated = Plan(v17,
                BorderPanel(v17, Vector3.right, -352f, -176f),
                BorderPanel(v17, Vector3.right, 176f, 352f));
            Check(ClearOfMask(separated, v17, Vector3.right, -352f, -176f) &&
                  ClearOfMask(separated, v17, Vector3.right, 176f, 352f), "separate board ranges are both clear");
            Check(HasVeneerAt(separated, v17, Vector3.right, 0f), "an exposed bay between two panels keeps its veneer");
            Check(HasVeneerAt(separated, v17, Vector3.right, -528f) && HasVeneerAt(separated, v17, Vector3.right, 528f),
                "separate board ranges preserve the external exposed bays too");

            // The cap centre is outside this board, but its 41 mm body would
            // extend into it. Checking only the centre would wrongly keep it.
            var nearCap = Plan(v9, BorderPanel(v9, Vector3.right, 360f, 440f));
            Check(!HasCapAt(nearCap, v9, Vector3.right, 352f), "a cap whose body reaches into the panel is removed even when its centre is outside");
            Check(HasCapAt(nearCap, v9, Vector3.right, -352f), "a distant exposed cap survives near-end masking");
            Check(HasVeneerAt(nearCap, v9, Vector3.right, 0f), "cap-only masking retains the non-overlapping veneer");

            // The panel is across the post rather than parallel to it: this
            // shelf still intersects the two channels facing into its corner.
            var shelf = CrossShelf(v9, 0f);
            var crossed = Plan(v9, shelf);
            Check(ClearOfMask(crossed, v9, Vector3.right, -4f, 4f), "horizontal shelf leaves no +X veneer/cap through the board thickness");
            Check(ClearOfMask(crossed, v9, Vector3.forward, -4f, 4f), "horizontal shelf leaves no +Z veneer/cap through the board thickness");
            Check(HasVeneerAt(crossed, v9, Vector3.right, -176f) && HasVeneerAt(crossed, v9, Vector3.right, 176f),
                "horizontal shelf preserves the +X veneer above and below it");
            Check(HasVeneerAt(crossed, v9, Vector3.forward, -176f) && HasVeneerAt(crossed, v9, Vector3.forward, 176f),
                "horizontal shelf preserves the +Z veneer above and below it");
            Check(HasVeneerAt(crossed, v9, Vector3.left, 0f) && HasVeneerAt(crossed, v9, Vector3.back, 0f),
                "a corner shelf does not hide outward post channels");
            var farShelf = CrossShelf(v9, 0f);
            farShelf.Center += Vector3.right * NeospaceUnits.Mm(1000f);
            Check(HasVeneerAt(Plan(v9, farShelf), v9, Vector3.right, 0f), "a distant horizontal panel does not mask the post");

            foreach (string id in new[] { "H7", "T7" })
            {
                var h = Frame(id, 7, Vector3.right, fixtures);
                var uncovered = Plan(h);
                Check(CountModel(uncovered, "Cap End") == 0 && CountModel(uncovered, "Foot") == 0,
                    id + " joint ends never receive Cap End or Foot");
                var masked = Plan(h, BorderPanel(h, Vector3.forward, -176f, 176f));
                Check(ClearOfMask(masked, h, Vector3.forward, -176f, 176f), id + " partial panel has no inward veneer/cap intersection");
                Check(HasVeneerAt(masked, h, Vector3.forward, -264f) && HasVeneerAt(masked, h, Vector3.forward, 264f),
                    id + " partial panel preserves both exposed channel lengths");
                Check(HasVeneerAt(masked, h, Vector3.back, 0f), id + " partial panel leaves the outward face dressed");
                Check(CountModel(masked, "Cap End") == 0 && CountModel(masked, "Foot") == 0,
                    id + " panel masking does not introduce extrusion end caps");
            }

            // Exercise the real renderer-to-OBB adapter, not a preconstructed
            // PanelBox. Projecting a rotated world AABB onto panel axes inflates
            // both span and thickness; mesh-local bounds must stay exact.
            var root = NewFixture("RotatedPanelRoot", fixtures);
            root.transform.position = new Vector3(32f, 18f, -25f);
            root.transform.rotation = Quaternion.Euler(23f, 37f, 11f);
            root.AddComponent<PanelInstance>();
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.name = "ExactPanelMesh";
            mesh.hideFlags = HideFlags.HideInHierarchy;
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localPosition = NeospaceUnits.Mm(1f) * new Vector3(10f, -20f, 18f);
            mesh.transform.localScale = NeospaceUnits.Mm(1f) * new Vector3(704f, 352f, 8f);
            Object.DestroyImmediate(mesh.GetComponent<Collider>());
            Vector3 expectedCentre = mesh.transform.position;
            List<FinishGenerator.PanelBox> collected = FinishGenerator.CollectPanels(0);
            int match = collected.FindIndex(p => (p.Center - expectedCentre).sqrMagnitude < 0.0001f);
            Check(match >= 0, "rotated panel collection preserves the actual mesh centre");
            if (match >= 0)
            {
                var box = collected[match];
                Check(NearMm(box.HalfU, 352f) && NearMm(box.HalfV, 176f), "rotated panel OBB preserves actual in-plane half extents");
                Check(NearMm(box.HalfN, 4f), "rotated panel OBB preserves its 8 mm thickness");
                Check(Vector3.Dot(box.AxisU, root.transform.right) > 0.999f &&
                      Vector3.Dot(box.AxisV, root.transform.up) > 0.999f && Vector3.Dot(box.Normal, root.transform.forward) > 0.999f,
                    "rotated panel OBB retains the panel's own axes");
            }
        }
        finally
        {
            foreach (GameObject fixture in fixtures)
                if (fixture != null) Object.DestroyImmediate(fixture);
        }
        return checks;
    }

    static FrameOverlapResolver.FrameRecord Frame(string id, int size, Vector3 axis, List<GameObject> fixtures)
    {
        bool vertical = id.StartsWith("V", StringComparison.Ordinal);
        float spanMm = vertical ? Skeleton.VSkeletonLength(size) : Skeleton.HSkeletonLength(size);
        Vector3 centre = vertical
            ? Vector3.up * NeospaceUnits.Mm(Skeleton.VBodyLength(size) * 0.5f)
            : Vector3.up * NeospaceUnits.Mm(800f);
        var root = NewFixture(id + "_FinishFixture", fixtures);
        root.transform.position = centre;
        return new FrameOverlapResolver.FrameRecord
        {
            Root = root.transform, PartId = id, Size = size, Center = centre,
            LengthAxis = axis, LocalY = Vector3.up,
            EndA = centre - axis * NeospaceUnits.Mm(spanMm * 0.5f),
            EndB = centre + axis * NeospaceUnits.Mm(spanMm * 0.5f)
        };
    }

    static GameObject NewFixture(string name, List<GameObject> fixtures)
    {
        // DontSave objects are excluded by Unity scene queries; hide these
        // transient fixtures in the hierarchy while still exercising collection.
        var root = new GameObject(name) { hideFlags = HideFlags.HideInHierarchy };
        fixtures.Add(root);
        return root;
    }

    static FinishGenerator.Result Plan(FrameOverlapResolver.FrameRecord frame, params FinishGenerator.PanelBox[] panels)
        => FinishGenerator.Plan(new List<FrameOverlapResolver.FrameRecord> { frame }, new List<FinishGenerator.PanelBox>(panels));

    static FinishGenerator.PanelBox BorderPanel(FrameOverlapResolver.FrameRecord frame, Vector3 face, float loMm, float hiMm)
    {
        return new FinishGenerator.PanelBox
        {
            Center = frame.Center + frame.LengthAxis * NeospaceUnits.Mm((loMm + hiMm) * 0.5f) + face * NeospaceUnits.Mm(352f),
            AxisU = frame.LengthAxis, AxisV = face, Normal = Vector3.Cross(frame.LengthAxis, face).normalized,
            HalfU = NeospaceUnits.Mm((hiMm - loMm) * 0.5f), HalfV = NeospaceUnits.Mm(352f), HalfN = NeospaceUnits.Mm(4f)
        };
    }

    static FinishGenerator.PanelBox CrossShelf(FrameOverlapResolver.FrameRecord frame, float offsetMm)
    {
        return new FinishGenerator.PanelBox
        {
            Center = frame.Center + Vector3.up * NeospaceUnits.Mm(offsetMm) +
                (Vector3.right + Vector3.forward) * NeospaceUnits.Mm(352f),
            AxisU = Vector3.right, AxisV = Vector3.forward, Normal = Vector3.up,
            HalfU = NeospaceUnits.Mm(352f), HalfV = NeospaceUnits.Mm(352f), HalfN = NeospaceUnits.Mm(4f)
        };
    }

    static bool OnFace(FinishGenerator.Placement part, FrameOverlapResolver.FrameRecord frame, Vector3 face)
        => Vector3.Dot(part.Normal, face) > 0.999f && Mathf.Abs(Vector3.Dot(part.LengthDir, frame.LengthAxis)) > 0.999f;

    static bool HasVeneerAt(FinishGenerator.Result result, FrameOverlapResolver.FrameRecord frame, Vector3 face, float offsetMm)
    {
        foreach (var part in result.Parts)
        {
            if (!OnFace(part, frame, face) || !TryHalfLengthMm(part.Model, out float half) || part.Model == "Cap Side") continue;
            float centre = NeospaceUnits.ToMm(Vector3.Dot(part.Center - frame.Center, frame.LengthAxis));
            if (offsetMm >= centre - half - EpsilonMm && offsetMm <= centre + half + EpsilonMm) return true;
        }
        return false;
    }

    static bool HasCapAt(FinishGenerator.Result result, FrameOverlapResolver.FrameRecord frame, Vector3 face, float offsetMm)
    {
        foreach (var part in result.Parts)
            if (part.Model == "Cap Side" && OnFace(part, frame, face) &&
                Mathf.Abs(NeospaceUnits.ToMm(Vector3.Dot(part.Center - frame.Center, frame.LengthAxis)) - offsetMm) < EpsilonMm)
                return true;
        return false;
    }

    static bool ClearOfMask(FinishGenerator.Result result, FrameOverlapResolver.FrameRecord frame, Vector3 face, float loMm, float hiMm)
    {
        foreach (var part in result.Parts)
        {
            if (!OnFace(part, frame, face) || !TryHalfLengthMm(part.Model, out float half)) continue;
            float centre = NeospaceUnits.ToMm(Vector3.Dot(part.Center - frame.Center, frame.LengthAxis));
            if (centre + half > loMm + EpsilonMm && centre - half < hiMm - EpsilonMm) return false;
        }
        return true;
    }

    static bool TryHalfLengthMm(string model, out float half)
    {
        half = 0f;
        if (model == "Cap Side") { half = CatalogueData.HalfProfileMm; return true; }
        if (!model.StartsWith("Veneer ", StringComparison.Ordinal)) return false;
        int h = model.LastIndexOf('H');
        if (h < 0 || !int.TryParse(model.Substring(h + 1), out int size)) return false;
        half = Skeleton.VeneerContactLength(size) * 0.5f;
        return true;
    }

    static int CountModel(FinishGenerator.Result result, string model) => result.Parts.FindAll(p => p.Model == model).Count;
    static int CountFaceParts(FinishGenerator.Result result, FrameOverlapResolver.FrameRecord frame, Vector3 face)
        => result.Parts.FindAll(p => OnFace(p, frame, face) && TryHalfLengthMm(p.Model, out _)).Count;
    static bool NearMm(float world, float mm) => Mathf.Abs(NeospaceUnits.ToMm(world) - mm) < 0.1f;

    static string Signature(FinishGenerator.Result result)
    {
        var parts = new List<string>();
        foreach (var part in result.Parts)
            parts.Add(part.Model + "|" + Key(part.Center) + "|" + Key(part.Normal) + "|" + Key(part.LengthDir));
        parts.Sort(StringComparer.Ordinal);
        return string.Join("\n", parts);
        static string Key(Vector3 value) =>
            $"{Mathf.RoundToInt(value.x * 10000f)},{Mathf.RoundToInt(value.y * 10000f)},{Mathf.RoundToInt(value.z * 10000f)}";
    }
}
#endif
