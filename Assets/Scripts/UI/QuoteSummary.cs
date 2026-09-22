using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>A reviewable request for supplier pricing, with a portable design code.</summary>
public static class QuoteSummary
{
    /// <summary>The same active-part snapshot used by the count and price pill.</summary>
    public static string Create(ConfigurationModel model, BuildPartSummary summary,
        string panelFinish, string frameFinish, DateTime utcNow)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        if (summary == null) throw new ArgumentNullException(nameof(summary));
        if (model.Beams.Count + model.Panels.Count == 0)
            throw new InvalidOperationException("Build something before preparing a quote.");

        var text = new StringBuilder();
        text.AppendLine("NEOSPACE - QUOTE REQUEST");
        text.AppendLine("Prepared " + utcNow.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture));
        text.AppendLine("Estimate only. Placeholder prices require supplier confirmation before ordering.");
        text.AppendLine("Currency: AUD. Includes the active frames, panels and finish parts listed below.");
        text.AppendLine();
        AppendParts(text, summary);
        text.AppendLine();
        text.AppendLine("Panel colour preview: " + panelFinish);
        text.AppendLine("Veneer/cap colour preview: " + frameFinish);
        text.AppendLine("Colour previews require physical sample confirmation. Palette selections are listed separately from the design code.");
        text.AppendLine("Not included: delivery, installation, unlisted accessories and any tax adjustments.");
        text.AppendLine("Twist-frame estimates may use the matching H-frame price; supplier must verify them.");
        text.AppendLine("This is a design review list, not a manufacturing approval or an order. No payment has been taken.");
        text.AppendLine();
        text.AppendLine("DESIGN CODE - open with Load code to inspect dimensions and placement");
        text.AppendLine(ConfigurationCodec.Encode(model));
        return text.ToString();
    }

    public static string CreateParts(BuildPartSummary summary, string priceNote = null)
    {
        if (summary == null) throw new ArgumentNullException(nameof(summary));
        var text = new StringBuilder();
        text.AppendLine("NEOSPACE - PARTS & PRICES");
        text.AppendLine("Placeholder estimates in AUD. Confirm final prices with your supplier.");
        if (!string.IsNullOrWhiteSpace(priceNote)) text.AppendLine(priceNote);
        text.AppendLine();
        AppendParts(text, summary);
        text.AppendLine();
        text.AppendLine("Counts reflect the active design, including panels and the finish parts currently applied.");
        text.AppendLine("Not included: delivery, installation, unlisted accessories and any tax adjustments.");
        return text.ToString();
    }

    static void AppendParts(StringBuilder text, BuildPartSummary summary)
    {
        foreach (PartKind kind in new[] { PartKind.Frame, PartKind.Panel, PartKind.Finish })
        {
            text.AppendLine(kind == PartKind.Frame ? "FRAME PARTS" : kind == PartKind.Panel ? "PANELS" : "FINISH PARTS");
            bool any = false;
            foreach (var line in summary.Lines)
            {
                if (line.Kind != kind) continue;
                any = true;
                text.Append(line.PartId).Append(" x ").Append(line.Quantity);
                if (line.UnitPrice.HasValue && line.TotalPrice.HasValue)
                    text.Append(" | AUD ").Append(Money(line.UnitPrice.Value)).Append(" each | AUD ").Append(Money(line.TotalPrice.Value));
                else
                    text.Append(" | PRICE REQUIRED");
                text.AppendLine();
            }
            if (!any) text.AppendLine("None");
            text.AppendLine();
        }
        text.AppendLine($"Total parts: {summary.PartCount} | Frames: {summary.FrameCount} | Panels: {summary.PanelCount} | Finish: {summary.FinishCount}");
        text.AppendLine("Estimated total: AUD " + Money(summary.TotalPrice) +
            (summary.UnpricedPartCount > 0 ? " (priced parts only)" : ""));
        text.AppendLine("Parts requiring pricing: " + summary.UnpricedPartCount);
    }

    /// <summary>
    /// Legacy code-only export. A design code has panel references but no
    /// resolved panel sizes or generated finish inventory; disclose that limit.
    /// Live UI exports use the complete snapshot overload above.
    /// </summary>
    public static string Create(ConfigurationModel model, Func<string, float?> priceForPart,
        string panelFinish, string frameFinish, DateTime utcNow)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        if (model.Beams.Count + model.Panels.Count == 0)
            throw new InvalidOperationException("Build something before preparing a quote.");

        var quantities = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (BeamRecord beam in model.Beams)
        {
            string id = PartRegistry.TryGetPart(beam.PartCode, out string partId, out _)
                ? partId : "Unknown part " + beam.PartCode;
            quantities.TryGetValue(id, out int count);
            quantities[id] = count + 1;
        }

        var text = new StringBuilder();
        text.AppendLine("NEOSPACE - QUOTE REQUEST");
        text.AppendLine("Prepared " + utcNow.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture));
        text.AppendLine("Estimate only. Supplier confirmation is required before ordering.");
        text.AppendLine("Currency: AUD. Source: configurator's local frame price placeholders (unverified).");
        text.AppendLine();
        text.AppendLine("FRAME PARTS");
        decimal subtotal = 0;
        int unpriced = 0;
        foreach (var pair in quantities)
        {
            float? value = priceForPart?.Invoke(pair.Key);
            bool valid = value.HasValue && value.Value >= 0 &&
                !float.IsNaN(value.Value) && !float.IsInfinity(value.Value) && value.Value <= 1000000000f;
            if (valid)
            {
                decimal unit = decimal.Round((decimal)value.Value, 2, MidpointRounding.AwayFromZero);
                decimal line = unit * pair.Value;
                subtotal += line;
                text.AppendLine($"{pair.Key} x {pair.Value} | AUD {Money(unit)} each | AUD {Money(line)}");
            }
            else
            {
                unpriced += pair.Value;
                text.AppendLine($"{pair.Key} x {pair.Value} | PRICE REQUIRED");
            }
        }
        text.AppendLine();
        text.AppendLine("Known frame subtotal: AUD " + Money(subtotal));
        text.AppendLine($"Frames requiring pricing: {unpriced}");
        text.AppendLine($"Panels: {model.Panels.Count}" + (model.Panels.Count > 0 ? " | PRICE AND MATERIAL SPECIFICATION REQUIRED" : ""));
        text.AppendLine("Frame dressing: " + (model.FinishApplied ? "On | VENEER/CAP QUANTITIES AND PRICES REQUIRED" : "Off"));
        text.AppendLine("Panel colour preview: " + panelFinish);
        text.AppendLine("Veneer/cap colour preview: " + frameFinish);
        text.AppendLine("Colour previews require physical sample confirmation. Palette selections are listed separately from the design code.");
        text.AppendLine("Panel sizes and applied finish quantities are unavailable in this code-only export. Open the design to include them in Parts & prices and its quote.");
        text.AppendLine("Excluded from this known frame subtotal: unresolved panels and finish, other accessories, delivery and any tax adjustments.");
        text.AppendLine("Twist-frame estimates use the matching H-frame price; supplier must verify them.");
        text.AppendLine("This is not a complete manufacturing BOM or an order. No payment has been taken.");
        text.AppendLine();
        text.AppendLine("DESIGN CODE - open with Load code to inspect dimensions and placement");
        text.AppendLine(ConfigurationCodec.Encode(model));
        return text.ToString();
    }

    static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
