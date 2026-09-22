#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A board alongside an H/T beam hides the inward channel, while the exposed
/// top and bottom channels retain their full runs. Uses the exact live board
/// edge, gap and thickness: its seam overlaps the supplied veneer's width by
/// about 0.49 mm, which must not be treated as a transverse obstruction.
/// </summary>
public static class FinishChannelExposureSelfTest
{
    public static int RunAll(out List<string> failures)
    {
        failures = new List<string>();
        var failed = failures;
        int checks = 0;
        void Check(bool passed, string label)
        {
            checks++;
            if (!passed) failed.Add("Channel exposure: " + label);
        }

        foreach (string id in new[] { "H7", "H23", "T7" })
        foreach (float yaw in new[] { 0f, 37f })
        {
            int size = int.Parse(id.Substring(1));
            var frame = Frame(id, size, yaw);
            Vector3 inward = Vector3.Cross(frame.LengthAxis, Vector3.up).normalized;
            var baseline = Plan(frame);
            string prefix = id + " at " + yaw + " degrees: ";
            Check(FullCoverage(baseline, frame, Vector3.up) &&
                  FullCoverage(baseline, frame, Vector3.down) &&
                  FullCoverage(baseline, frame, inward) &&
                  FullCoverage(baseline, frame, -inward),
                prefix + "without panels all four channels have full modular veneer coverage");

            foreach (int sides in new[] { 1, -1, 0 })
            {
                var panels = new List<FinishGenerator.PanelBox>();
                if (sides >= 0) panels.Add(Panel(frame, inward, 1));
                if (sides <= 0) panels.Add(Panel(frame, inward, -1));
                var result = FinishGenerator.Plan(new List<FrameOverlapResolver.FrameRecord> { frame }, panels);
                string state = sides > 0 ? "upper board" : sides < 0 ? "lower board" : "both boards";
                Check(Signature(result, frame, Vector3.up) == Signature(baseline, frame, Vector3.up),
                    prefix + state + " preserves the exact full top channel run");
                Check(Signature(result, frame, Vector3.down) == Signature(baseline, frame, Vector3.down),
                    prefix + state + " preserves the exact full bottom channel run");
                Check(Signature(result, frame, -inward) == Signature(baseline, frame, -inward),
                    prefix + state + " preserves the outward channel run");
                Check(Signature(result, frame, inward).Length == 0,
                    prefix + state + " hides the inward panel-facing channel");
            }

            foreach (int side in new[] { 1, -1 })
            {
                Vector3 face = Vector3.up * side;
                // A board spanning the actual frame face is an obstruction,
                // not the narrow seam beside it. Keep the opposite face free.
                var covering = Panel(frame, inward, side);
                covering.Center -= inward * NeospaceUnits.Mm(352f);
                var covered = FinishGenerator.Plan(new List<FrameOverlapResolver.FrameRecord> { frame },
                    new List<FinishGenerator.PanelBox> { covering });
                Check(Signature(covered, frame, face).Length == 0,
                    prefix + "board covering the " + (side > 0 ? "top" : "bottom") + " frame face suppresses its veneer");
                Check(Signature(covered, frame, -face) == Signature(baseline, frame, -face),
                    prefix + "a face-covering board preserves the opposite exposed face");

                // The seam allowance absorbs code quantization (restored and
                // Space pieces sit up to 0.5 mm off per part): an edge at
                // 20.1 mm is still a seam; well inside the footprint it covers.
                float limit = CatalogueData.HalfProfileMm - FinishGenerator.SeamToleranceMm;
                foreach (float nearEdgeMm in new[] { 20.51f, 20.1f, limit + 0.01f, limit - 0.01f })
                {
                    var edge = Panel(frame, inward, side);
                    edge.Center = frame.Center + inward * (edge.HalfV + NeospaceUnits.Mm(nearEdgeMm)) +
                        face * NeospaceUnits.Mm(20.5f);
                    var atEdge = FinishGenerator.Plan(new List<FrameOverlapResolver.FrameRecord> { frame },
                        new List<FinishGenerator.PanelBox> { edge });
                    bool exposed = nearEdgeMm >= limit;
                    Check(exposed
                            ? Signature(atEdge, frame, face) == Signature(baseline, frame, face)
                            : Signature(atEdge, frame, face).Length == 0,
                        prefix + "board edge at " + nearEdgeMm + " mm " +
                        (exposed ? "preserves the adjacent seam" : "masks its intrusion into the frame footprint") +
                        " on the " + (side > 0 ? "top" : "bottom") + " face");
                }
            }
        }
        return checks;
    }

    static FrameOverlapResolver.FrameRecord Frame(string id, int size, float yaw)
    {
        Vector3 axis = Quaternion.AngleAxis(yaw, Vector3.up) * Vector3.right;
        Vector3 center = Vector3.up * NeospaceUnits.Mm(800f);
        Vector3 halfLength = axis * NeospaceUnits.Mm(Skeleton.HSkeletonLength(size) * 0.5f);
        return new FrameOverlapResolver.FrameRecord
        {
            PartId = id, Size = size, Center = center, LengthAxis = axis,
            LocalY = Vector3.up, EndA = center - halfLength, EndB = center + halfLength
        };
    }

    static FinishGenerator.PanelBox Panel(FrameOverlapResolver.FrameRecord frame, Vector3 inward, int side)
    {
        // Same geometry as PanelSlotManager.GetPanelPlacement: 704 mm bay
        // depth, the PanelFill fitting allowance, and a 1 mm sheet at +/-20.5.
        return new FinishGenerator.PanelBox
        {
            Center = frame.Center + inward * NeospaceUnits.Mm(352f) + Vector3.up * NeospaceUnits.Mm(side * 20.5f),
            Normal = Vector3.up, AxisU = frame.LengthAxis, AxisV = inward,
            HalfU = NeospaceUnits.Mm(PanelFill.EdgeMm(frame.Size) * 0.5f),
            HalfV = NeospaceUnits.Mm(PanelFill.EdgeMm(7) * 0.5f), HalfN = NeospaceUnits.Mm(0.5f)
        };
    }

    static FinishGenerator.Result Plan(FrameOverlapResolver.FrameRecord frame)
        => FinishGenerator.Plan(new List<FrameOverlapResolver.FrameRecord> { frame }, new List<FinishGenerator.PanelBox>());

    static bool FullCoverage(FinishGenerator.Result result, FrameOverlapResolver.FrameRecord frame, Vector3 normal)
    {
        int modules = 0;
        foreach (var part in result.Parts)
            if (Vector3.Dot(part.Normal, normal) > 0.999f && part.Model.StartsWith("Veneer H", StringComparison.Ordinal))
                modules += int.Parse(part.Model.Substring(8)) + 1;
        return modules == frame.Size + 1;
    }

    static string Signature(FinishGenerator.Result result, FrameOverlapResolver.FrameRecord frame, Vector3 normal)
    {
        var rows = new List<string>();
        foreach (var part in result.Parts)
        {
            if (Vector3.Dot(part.Normal, normal) < 0.999f) continue;
            if (part.Model != "Cap Side" && !part.Model.StartsWith("Veneer H", StringComparison.Ordinal)) continue;
            int offsetMicrons = Mathf.RoundToInt(1000f * NeospaceUnits.ToMm(Vector3.Dot(part.Center - frame.Center, frame.LengthAxis)));
            rows.Add(part.Model + "@" + offsetMicrons);
        }
        rows.Sort(StringComparer.Ordinal);
        return string.Join(";", rows);
    }
}
#endif
