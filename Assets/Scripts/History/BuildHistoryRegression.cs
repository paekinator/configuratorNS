#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Frame-based integration regression. Run as a coroutine in an empty Play
/// Mode scene; uses the real V9 prefab and placement/history/summary code.
/// </summary>
public static class BuildHistoryRegression
{
    public static IEnumerator RunAll(Action<string> failure)
    {
        if (Object.FindFirstObjectByType<BuildHistory>() != null ||
            Object.FindFirstObjectByType<SelectableBeam>() != null)
        {
            failure("History regression requires an empty Play Mode scene.");
            yield break;
        }

        int checks = 0;
        void Check(bool passed, string message)
        {
            checks++;
            if (!passed) failure(message);
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Vertical/V9.prefab");
        GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PanelMesh.prefab");
        if (prefab == null || panelPrefab == null)
        {
            failure("V9 or PanelMesh prefab was not found.");
            yield break;
        }

        bool previousSuspended = BuildHistory.Suspended;
        BuildHistory.Suspended = false;
        var host = new GameObject("HistoryRegression");
        var database = ScriptableObject.CreateInstance<PartDatabase>();
        database.parts.Add(new PartDatabase.PartEntry { partId = "V9", realPrefab = prefab });
        var build = host.AddComponent<BuildController>();
        build.partDatabase = database;
        build.debugLogs = false;
        var history = host.AddComponent<BuildHistory>();
        history.buildController = build;
        var stats = host.AddComponent<UIBuildStats>();
        var countLabel = new GameObject("PartCount", typeof(RectTransform), typeof(TextMeshProUGUI));
        countLabel.transform.SetParent(host.transform);
        stats.partCountText = countLabel.GetComponent<TextMeshProUGUI>();
        var priceLabel = new GameObject("Price", typeof(RectTransform), typeof(TextMeshProUGUI));
        priceLabel.transform.SetParent(host.transform);
        stats.priceText = priceLabel.GetComponent<TextMeshProUGUI>();
        int notifications = 0;
        void Changed() { notifications++; }
        BuildHistory.Changed += Changed;

        void ExpectSummary(int count, float price, string action)
        {
            Check(stats.PartCount == count && Mathf.Approximately(stats.TotalPrice, price),
                $"{action}: expected {count} parts / ${price}, got {stats.PartCount} / ${stats.TotalPrice}.");
            Check(stats.partCountText.text == (count == 1 ? "1 part" : $"{count} parts"),
                action + ": part-count label does not match the scene.");
            Check(stats.priceText.text == "Est. $" + price.ToString("N0") +
                (stats.HasIncompletePricing ? "*" : ""), action + ": price label does not match the scene.");
        }

        void Place()
        {
            var result = build.PlacePartsBatch(new List<TemplatePartPose>
            {
                new TemplatePartPose("V9", Vector3.zero, Quaternion.identity, "regression")
            }, seatVerticalsOnFloor: false, validateOverlap: false);
            Check(result.Placed == 1, "The V9 batch should place one beam.");
        }

        try
        {
            Check(stats.TryPriceForPart("V9", out float initialPrice) && initialPrice == 35f,
                "Price lookup must work before Start.");
            Check(!stats.TryPriceForPart("V11", out _), "Missing V11 pricing must be explicit.");
            Check(stats.TryPriceForPart("T9", out float twistPrice) && twistPrice == 75f,
                "Twist pricing should use its matching H size.");
            yield return Frames(2);
            ExpectSummary(0, 0f, "empty baseline");

            Place();
            yield return Frames(3);
            ExpectSummary(1, 35f, "place V9");
            int beforeUndo = notifications;
            history.Undo();
            yield return Frames(5);
            ExpectSummary(0, 0f, "undo last V9");
            Check(notifications > beforeUndo, "Undo-to-empty must announce the completed change.");
            Check(history.CanRedo, "Undo should retain a redo step.");

            history.Redo();
            yield return Frames(5);
            ExpectSummary(1, 35f, "redo V9");

            int beforeClear = notifications;
            history.ClearAll();
            yield return Frames(5);
            ExpectSummary(0, 0f, "clear V9");
            Check(notifications > beforeClear, "Clear-to-empty must announce the completed change.");
            history.Undo();
            yield return Frames(5);
            ExpectSummary(1, 35f, "undo clear");
            history.ClearAll();
            yield return Frames(5);

            Place();
            history.Undo(); // Deliberately before the deferred capture.
            yield return Frames(6);
            ExpectSummary(0, 0f, "same-frame placement and Undo");
            Check(history.CanRedo, "Rapid Undo must preserve the just-placed beam for Redo.");
            history.Redo();
            yield return Frames(5);
            ExpectSummary(1, 35f, "redo rapid placement");

            // Deletion happens at the end of this frame, after notification.
            var beam = Object.FindFirstObjectByType<BeamConnections>();
            Object.Destroy(beam.gameObject);
            BuildHistory.NotifyChanged();
            yield return Frames(3);
            ExpectSummary(0, 0f, "deferred delete");
            history.Undo();
            yield return Frames(5);
            ExpectSummary(1, 35f, "undo delete");

            // Unknown priced parts without active connectors exercise the
            // event path independently of AttachmentPoint.StructureVersion.
            var unknown = new GameObject("V11");
            unknown.AddComponent<SelectableBeam>();
            BuildHistory.NotifyChanged();
            yield return Frames(3);
            ExpectSummary(2, 35f, "part without attachment points");
            Check(stats.UnpricedPartCount == 1, "Unknown beam price must be disclosed.");
            Object.Destroy(unknown);
            BuildHistory.NotifyChanged();
            yield return Frames(3);
            ExpectSummary(1, 35f, "remove part without attachment points");

            var panel = Object.Instantiate(panelPrefab);
            panel.name = "DefaultPricedPanel";
            panel.transform.position = Vector3.one * NeospaceUnits.Mm(3000f);
            panel.transform.localScale = NeospaceUnits.Mm(1f) * new Vector3(PanelFill.EdgeMm(1), PanelFill.EdgeMm(3), 1f);
            if (panel.GetComponent<PanelInstance>() == null) panel.AddComponent<PanelInstance>();
            yield return Frames(2);
            ExpectSummary(2, 60f, "panel placeholder estimate");
            Check(stats.UnpricedPartCount == 0, "A panel should use the configured $25 placeholder estimate.");
            Object.Destroy(panel);
            yield return Frames(2);

            stats.partPrices.Find(p => p.partId == "V9").price = 40f;
            stats.RefreshNow();
            ExpectSummary(1, 40f, "same-count catalogue price edit");

            stats.partPrices.Add(new UIBuildStats.PartPrice("H0", 0f));
            stats.RefreshNow();
            Check(stats.TryPriceForPart("H0", out float freePrice) && freePrice == 0f,
                "An explicitly configured zero price must differ from a missing price.");
        }
        finally
        {
            BuildHistory.Changed -= Changed;
            foreach (BeamConnections beam in Object.FindObjectsByType<BeamConnections>(FindObjectsSortMode.None))
                Object.Destroy(beam.transform.root.gameObject);
            foreach (PanelInstance panel in Object.FindObjectsByType<PanelInstance>(FindObjectsSortMode.None))
                Object.Destroy(panel.gameObject);
            Object.Destroy(host);
            Object.Destroy(database);
            BuildHistory.Suspended = previousSuspended;
        }
        yield return null;
        Debug.Log($"History/summary regression: {checks} checks executed.");
    }

    static IEnumerator Frames(int count)
    {
        for (int i = 0; i < count; i++) yield return null;
    }
}
#endif
