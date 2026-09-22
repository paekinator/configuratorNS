using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public enum PartKind { Frame, Panel, Finish }

/// <summary>One immutable, priced snapshot shared by the header, list and quote.</summary>
public sealed class BuildPartSummary
{
    public sealed class Line
    {
        public string PartId { get; }
        public PartKind Kind { get; }
        public int Quantity { get; }
        public decimal? UnitPrice { get; }
        public decimal? TotalPrice => UnitPrice * Quantity;

        public Line(string partId, PartKind kind, int quantity, float? unitPrice)
        {
            if (string.IsNullOrWhiteSpace(partId)) throw new ArgumentException("Part identity is required.", nameof(partId));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            PartId = partId;
            Kind = kind;
            Quantity = quantity;
            if (unitPrice.HasValue && IsValidPrice(unitPrice.Value))
                UnitPrice = decimal.Round((decimal)unitPrice.Value, 2, MidpointRounding.AwayFromZero);
        }
    }

    public IReadOnlyList<Line> Lines { get; }
    public int PartCount => FrameCount + PanelCount + FinishCount;
    public int FrameCount { get; }
    public int PanelCount { get; }
    public int FinishCount { get; }
    public int UnpricedPartCount { get; }
    public decimal TotalPrice { get; }

    public BuildPartSummary(IEnumerable<Line> lines)
    {
        var copy = new List<Line>(lines ?? throw new ArgumentNullException(nameof(lines)));
        copy.Sort((a, b) => a.Kind != b.Kind ? a.Kind.CompareTo(b.Kind) : StringComparer.Ordinal.Compare(a.PartId, b.PartId));
        Lines = copy.AsReadOnly();
        foreach (Line line in copy)
        {
            switch (line.Kind)
            {
                case PartKind.Frame: FrameCount += line.Quantity; break;
                case PartKind.Panel: PanelCount += line.Quantity; break;
                case PartKind.Finish: FinishCount += line.Quantity; break;
            }
            if (line.TotalPrice.HasValue) TotalPrice += line.TotalPrice.Value;
            else UnpricedPartCount += line.Quantity;
        }
    }

    public static bool IsValidPrice(float value) => value >= 0f && value <= 1000000000f &&
        !float.IsNaN(value) && !float.IsInfinity(value);
}

/// <summary>Counts installed objects, including active results of Space merges.</summary>
public static class BuildPartSummaryCollector
{
    public static BuildPartSummary Capture(UIBuildStats pricing)
    {
        var frames = new Dictionary<string, int>(StringComparer.Ordinal);
        var panels = new Dictionary<string, int>(StringComparer.Ordinal);
        var finish = new Dictionary<string, int>(StringComparer.Ordinal);
        var seen = new HashSet<Transform>();
        if (SpaceModeController.Active)
        {
            foreach (var frame in FinishGenerator.CollectSpaceFrames())
                if (frame.Root != null && frame.Root.gameObject.activeInHierarchy && seen.Add(frame.Root))
                    Add(frames, frame.PartId);
            foreach (var panel in FinishGenerator.CollectSpacePanels())
                Add(panels, PanelPartId(NeospaceUnits.ToMm(panel.HalfU * 2), NeospaceUnits.ToMm(panel.HalfV * 2)));
        }
        else
        {
            var build = BuildHistory.Instance != null ? BuildHistory.Instance.buildController :
                UnityEngine.Object.FindFirstObjectByType<BuildController>();
            int ghostMask = build != null ? build.ghostLayerMask.value : 0;
            foreach (var beam in UnityEngine.Object.FindObjectsByType<SelectableBeam>(FindObjectsSortMode.None))
            {
                Transform root = beam.transform.root;
                if (!root.gameObject.activeInHierarchy || IsGhost(root, ghostMask) ||
                    !BeamPartUtility.IsBeam(root.name) || !seen.Add(root)) continue;
                Add(frames, StructureClipboard.CleanPartId(root.name) ?? root.name);
            }
            foreach (var panel in UnityEngine.Object.FindObjectsByType<PanelInstance>(FindObjectsSortMode.None))
            {
                if (!panel.gameObject.activeInHierarchy || IsGhost(panel.transform, ghostMask) ||
                    !seen.Add(panel.transform)) continue;
                string id;
                if (FinishGenerator.TryPanelBox(panel.transform, out var box))
                    id = PanelPartId(NeospaceUnits.ToMm(box.HalfU * 2), NeospaceUnits.ToMm(box.HalfV * 2));
                else if (panel.sizeA > 0 && panel.sizeB > 0)
                    id = PanelPartId(PanelFill.EdgeMm(panel.sizeA), PanelFill.EdgeMm(panel.sizeB));
                else id = "Panel (unspecified size)";
                Add(panels, id);
            }
        }
        if (FinishController.Instance != null)
            FinishController.Instance.CollectUsedParts(finish);
        var lines = new List<BuildPartSummary.Line>();
        Append(frames, PartKind.Frame);
        Append(panels, PartKind.Panel);
        Append(finish, PartKind.Finish);
        return new BuildPartSummary(lines);

        void Append(Dictionary<string, int> quantities, PartKind kind)
        {
            foreach (var pair in quantities)
                lines.Add(new BuildPartSummary.Line(pair.Key, kind, pair.Value,
                    pricing.TryPriceForPart(pair.Key, out float price) ? price : (float?)null));
        }
    }

    static bool IsGhost(Transform part, int mask)
    {
        for (Transform t = part; t != null; t = t.parent)
            if ((mask & (1 << t.gameObject.layer)) != 0 || t.name.Contains("_GhostInstance")) return true;
        return false;
    }

    static void Add(Dictionary<string, int> quantities, string id)
    {
        quantities.TryGetValue(id, out int count);
        quantities[id] = count + 1;
    }

    public static string PanelPartId(float widthMm, float heightMm)
    {
        float large = Mathf.Max(widthMm, heightMm), small = Mathf.Min(widthMm, heightMm);
        int a = EdgeSize(large), b = EdgeSize(small);
        return a > 0 && b > 0 ? $"Panel H{a}xH{b}" :
            "Panel " + large.ToString("0.##", CultureInfo.InvariantCulture) + " x " +
            small.ToString("0.##", CultureInfo.InvariantCulture) + " mm";
    }

    static int EdgeSize(float mm)
    {
        foreach (int n in PanelFill.EdgeSizes)
            if (Mathf.Abs(PanelFill.EdgeMm(n) - mm) < 0.5f) return n;
        return 0;
    }
}
