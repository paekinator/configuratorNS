#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Analytic SAT fixtures; no scene objects or assets are required.</summary>
public static class FinishPanelMaskingSelfTest
{
    public static int RunAll(out List<string> failures)
    {
        var issues = new List<string>();
        failures = issues;
        int total = 0;
        void Check(bool ok, string label) { total++; if (!ok) issues.Add(label); }
        float Mm(float value) => NeospaceUnits.Mm(value);
        Vector3 Point(float x, float y, float z) => new Vector3(Mm(x), Mm(y), Mm(z));

        try
        {
            Vector3 origin = Vector3.zero;
            Vector3 length = Vector3.up;
            Vector3 width = Vector3.right;
            Vector3 outward = Vector3.forward;
            float halfWidth = Mm(20.5f);
            float halfThickness = Mm(2f);

            // A 200 x 200 x 1 mm horizontal sheet occupies y=[20,21] mm.
            // Its large footprint fully contains this channel cross-section.
            var panel = new FinishGenerator.PanelBox
            {
                Center = Point(0f, 20.5f, 0f),
                AxisU = width,
                AxisV = outward,
                Normal = length,
                HalfU = Mm(100f),
                HalfV = Mm(100f),
                HalfN = Mm(0.5f)
            };

            Check(FinishPanelMasking.TrySweep(origin, length, width, outward,
                halfWidth, halfThickness, panel, out float lo, out float hi),
                "A transverse 1mm sheet masks the channel that passes through it.");
            Check(Mathf.Abs(NeospaceUnits.ToMm(lo) - 20.001f) < 0.00025f &&
                  Mathf.Abs(NeospaceUnits.ToMm(hi) - 20.999f) < 0.00025f,
                "The mask is the physical [20,21]mm slab with only 0.001mm contact tolerance.");
            Check(FinishPanelMasking.Intersects(origin, length, width, outward,
                Mm(100f), halfWidth, halfThickness, panel),
                "A long veneer penetrating the shelf is rejected.");
            Check(!FinishPanelMasking.Intersects(origin, length, width, outward,
                Mm(19f), halfWidth, halfThickness, panel),
                "A veneer ending at 19mm stays clear of a shelf starting at 20mm.");

            // The sheet's near edge equals the cross-section's +20.5mm edge.
            panel.Center = Point(120.5f, 20.5f, 0f);
            Check(!FinishPanelMasking.TrySweep(origin, length, width, outward,
                halfWidth, halfThickness, panel, out _, out _),
                "Flush stationary edge contact does not mask an exposed channel.");
            panel.Center = Point(120.4f, 20.5f, 0f);
            Check(FinishPanelMasking.TrySweep(origin, length, width, outward,
                halfWidth, halfThickness, panel, out _, out _),
                "A real 0.1mm penetration is detected despite the contact tolerance.");
            panel.Center = Point(200f, 20.5f, 0f);
            Check(!FinishPanelMasking.TrySweep(origin, length, width, outward,
                halfWidth, halfThickness, panel, out _, out _),
                "A distant panel does not mask the channel on the outside face.");

            // Rotating the square makes a diamond in XZ. Its WORLD AABB
            // overlaps the origin's cross-section, but the actual board does
            // not: its diagonal near edge lies beyond the plate's footprint.
            float diagonal = Mathf.Sqrt(0.5f);
            panel.Center = Point(125f, 20.5f, 125f);
            panel.AxisU = new Vector3(diagonal, 0f, diagonal);
            panel.AxisV = new Vector3(-diagonal, 0f, diagonal);
            Check(!FinishPanelMasking.TrySweep(origin, length, width, outward,
                halfWidth, halfThickness, panel, out _, out _),
                "A rotated board's inflated world AABB cannot produce a false mask.");

            panel.Center = Point(0f, 20.5f, 0f);
            panel.AxisU = width;
            panel.AxisV = new Vector3(0f, diagonal, -diagonal);
            panel.Normal = new Vector3(0f, diagonal, diagonal);
            Check(FinishPanelMasking.TrySweep(origin, length, width, outward,
                halfWidth, halfThickness, panel, out lo, out hi),
                "A tilted board crossing the channel has a finite masked interval.");
            Check(FinishPanelMasking.TrySweep(origin, -length, width, outward,
                halfWidth, halfThickness, panel, out float reverseLo, out float reverseHi) &&
                Mathf.Abs(NeospaceUnits.ToMm(reverseLo + hi)) < 0.00025f &&
                Mathf.Abs(NeospaceUnits.ToMm(reverseHi + lo)) < 0.00025f,
                "Reversing the sweep direction reverses interval endpoints without changing physical coverage.");

            Check(!FinishPanelMasking.TrySweep(new Vector3(float.NaN, 0f, 0f),
                length, width, outward, halfWidth, halfThickness, panel, out _, out _),
                "Nonfinite positions are rejected deterministically.");
            Check(!FinishPanelMasking.TrySweep(origin, length, width, outward,
                -Mm(1f), halfThickness, panel, out _, out _),
                "Negative physical dimensions are rejected.");
            Check(!FinishPanelMasking.TrySweep(origin, length, length, outward,
                halfWidth, halfThickness, panel, out _, out _),
                "A nonorthogonal plate basis is rejected.");
        }
        catch (Exception e)
        {
            issues.Add("Unexpected panel-mask self-test exception: " + e);
        }
        return total;
    }
}
#endif
