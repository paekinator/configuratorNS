using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Batch entry: pure checks followed by real-prefab Play Mode regressions.</summary>
public static class ConfiguratorValidation
{
    public static void Run()
    {
        SessionState.SetBool("ConfiguratorValidation.SpaceOnly", false);
        var failures = new List<string>();
        int count = NeospaceSelfTest.RunAll(out var core); failures.AddRange(core);
        count += ConfigurationCodeSelfTest.RunAll(out var codec); failures.AddRange(codec);
        count += SpaceCodeSelfTest.RunAll(out var space); failures.AddRange(space);
        count += PiecePersistenceSelfTest.RunAll(out var persistence); failures.AddRange(persistence);
        count += QuoteChecks(failures);
        count += SpacePlanningTargetSelfTest.RunAll(out var planning); failures.AddRange(planning);
        count += FinishGeneratorSelfTest.RunAll(out var finishing); failures.AddRange(finishing);
        count += FinishPanelMaskingSelfTest.RunAll(out var panelMasking); failures.AddRange(panelMasking);
        count += FinishChannelExposureSelfTest.RunAll(out var exposure); failures.AddRange(exposure);
        count += FinishSpaceJunctionSelfTest.RunAll(out var junctions); failures.AddRange(junctions);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/validation-pure.txt", $"{count} checks; {failures.Count} failures\n" + string.Join("\n", failures));
        Debug.Log($"Configurator pure validation: {count} checks, {failures.Count} failures");
        SessionState.SetString("ConfiguratorValidation.Failures", string.Join("\n", failures));
        SessionState.SetInt("ConfiguratorValidation.PureCount", count);
        SessionState.SetBool("ConfiguratorValidation.Pending", true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    /// <summary>Focused reproduction of Space joint, split and refresh regressions.</summary>
    public static void RunSpaceFinishing()
    {
        int count = FinishSpaceJunctionSelfTest.RunAll(out var failures);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/validation-pure.txt", $"{count} checks; {failures.Count} failures\n" + string.Join("\n", failures));
        SessionState.SetString("ConfiguratorValidation.Failures", string.Join("\n", failures));
        SessionState.SetInt("ConfiguratorValidation.PureCount", count);
        SessionState.SetBool("ConfiguratorValidation.SpaceOnly", true);
        SessionState.SetBool("ConfiguratorValidation.Pending", true);
        EditorSceneManager.OpenScene("Assets/Scenes/ConfiguratorScene.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    static int QuoteChecks(List<string> failures)
    {
        int checks = 0;
        void Check(bool pass, string text) { checks++; if (!pass) failures.Add("Quote: " + text); }
        var model = new ConfigurationModel { FinishApplied = true };
        model.Beams.Add(new BeamRecord { PartCode = 5 });
        model.Beams.Add(new BeamRecord { PartCode = 5 });
        model.Beams.Add(new BeamRecord { PartCode = 6 });
        model.Panels.Add(new PanelRecord { Axis = SlotAxis.PlusZ });
        string report = QuoteSummary.Create(model, id => id == "V9" ? 35 : (float?)null,
            "Frost", "Ivory", new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc));
        Check(report.Contains("V9 x 2 | AUD 35.00 each | AUD 70.00"), "quantities and line totals");
        Check(report.Contains("V11 x 1 | PRICE REQUIRED"), "missing price is not zero");
        Check(report.Contains("Known frame subtotal: AUD 70.00"), "known subtotal");
        Check(report.Contains("Panels: 1 | PRICE AND MATERIAL SPECIFICATION REQUIRED"), "panel omission disclosed");
        Check(report.Contains("VENEER/CAP QUANTITIES AND PRICES REQUIRED"), "finish omission disclosed");
        Check(report.Contains(ConfigurationCodec.Encode(model)), "portable code included");
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, -1f })
            Check(QuoteSummary.Create(model, _ => invalid, "", "", DateTime.UtcNow).Contains("Frames requiring pricing: 3"), "invalid catalogue amount rejected");
        bool rejected = false;
        try { QuoteSummary.Create(new ConfigurationModel(), (Func<string, float?>)null, "", "", DateTime.UtcNow); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "empty design rejected");

        var snapshot = new BuildPartSummary(new[]
        {
            new BuildPartSummary.Line("V9", PartKind.Frame, 2, 35f),
            new BuildPartSummary.Line("V11", PartKind.Frame, 1, null),
            new BuildPartSummary.Line("Panel H7xH3", PartKind.Panel, 2, 25f),
            new BuildPartSummary.Line("Veneer H7", PartKind.Finish, 4, 5f),
            new BuildPartSummary.Line("Cap Side", PartKind.Finish, 6, 2f),
            new BuildPartSummary.Line("Cap End", PartKind.Finish, 1, 2f),
            new BuildPartSummary.Line("Foot", PartKind.Finish, 1, 5f)
        });
        string complete = QuoteSummary.Create(model, snapshot, "Frost", "Ivory", DateTime.UtcNow);
        string parts = QuoteSummary.CreateParts(snapshot);
        foreach (string row in new[]
        {
            "V9 x 2 | AUD 35.00 each | AUD 70.00",
            "V11 x 1 | PRICE REQUIRED",
            "Panel H7xH3 x 2 | AUD 25.00 each | AUD 50.00",
            "Veneer H7 x 4 | AUD 5.00 each | AUD 20.00",
            "Cap Side x 6 | AUD 2.00 each | AUD 12.00",
            "Cap End x 1 | AUD 2.00 each | AUD 2.00",
            "Foot x 1 | AUD 5.00 each | AUD 5.00"
        })
            Check(complete.Contains(row) && parts.Contains(row), "shared snapshot row in quote and parts list: " + row);
        Check(complete.Contains("Total parts: 17 | Frames: 3 | Panels: 2 | Finish: 12") &&
            parts.Contains("Total parts: 17 | Frames: 3 | Panels: 2 | Finish: 12"), "all categories included in total count");
        Check(complete.Contains("Estimated total: AUD 159.00 (priced parts only)") &&
            parts.Contains("Estimated total: AUD 159.00 (priced parts only)"), "shared priced total includes panels and finish");
        Check(complete.Contains("Parts requiring pricing: 1") && parts.Contains("Parts requiring pricing: 1"), "shared snapshot discloses missing prices");
        Check(complete.Contains("Placeholder prices") && parts.Contains("Placeholder estimates"), "both documents disclose placeholder prices");
        Check(complete.Contains(ConfigurationCodec.Encode(model)) && !complete.Contains("VENEER/CAP QUANTITIES AND PRICES REQUIRED"),
            "live quote includes code and no longer excludes available finish inventory");
        return checks;
    }
}
