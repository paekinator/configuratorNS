#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Run after the finishing suites in the isolated ConfiguratorScene. Expected
/// quantities come from actual active finish roots and the independently placed
/// frame/panel fixtures. Expected money uses explicit test prices, not the price
/// lookup or summary implementation under test. No preferences/library writes.
/// </summary>
public static class BuildPartSummaryRegression
{
    public static IEnumerator RunAll(Action<string> failure)
    {
        int checks = 0, failed = 0;
        void Check(bool passed, string label)
        {
            checks++;
            if (passed) return;
            failed++;
            failure("Build part summary: " + label);
        }

        var build = Object.FindFirstObjectByType<BuildController>();
        var history = Object.FindFirstObjectByType<BuildHistory>();
        var stats = Object.FindFirstObjectByType<UIBuildStats>();
        var finish = FinishController.Ensure();
        var framePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Vertical/V9.prefab");
        var panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PanelMesh.prefab");
        Check(build != null && history != null && stats != null && framePrefab != null && panelPrefab != null,
            "real scene, header, V9 and PanelMesh prefabs must be available");
        if (build == null || history == null || stats == null || framePrefab == null || panelPrefab == null) yield break;

        bool previousSuspended = BuildHistory.Suspended;
        int previousGhostMask = build.ghostLayerMask.value;
        var originalPrices = stats.partPrices;
        float originalPanel = stats.panelPrice, originalVeneer = stats.veneerPrice;
        float originalCapSide = stats.capSidePrice, originalCapEnd = stats.capEndPrice, originalFoot = stats.footPrice;
        var owned = new List<GameObject>();
        var expectedOverrides = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
        decimal? panelUnit = 25m;
        decimal veneerUnit = 5m, capSideUnit = 2m, capEndUnit = 2m, footUnit = 5m;

        decimal? Unit(string id, PartKind kind)
        {
            if (expectedOverrides.TryGetValue(id, out decimal? value)) return value;
            if (kind == PartKind.Frame) return id == "V9" ? 35m : (decimal?)null;
            if (kind == PartKind.Panel) return panelUnit;
            if (id.StartsWith("Veneer H", StringComparison.Ordinal)) return veneerUnit;
            if (id == "Cap Side") return capSideUnit;
            if (id == "Cap End") return capEndUnit;
            if (id == "Foot") return footUnit;
            return null;
        }

        void Override(string id, float configured, decimal? expected)
        {
            stats.partPrices.RemoveAll(p => p != null && string.Equals(p.partId, id, StringComparison.OrdinalIgnoreCase));
            stats.partPrices.Add(new UIBuildStats.PartPrice(id, configured));
            expectedOverrides[id] = expected;
        }

        void Expect(int frames, int panels, string action)
        {
            var summary = stats.Summary;
            Check(summary != null, action + ": summary exists");
            if (summary == null) return;
            Dictionary<string, int> actualFinish = ActiveFinishQuantities();
            int finishCount = 0, quantity = 0, unpriced = 0;
            decimal total = 0m;
            foreach (int count in actualFinish.Values) finishCount += count;
            var identities = new HashSet<string>(StringComparer.Ordinal);
            int lineFrames = 0, linePanels = 0, lineFinish = 0;
            bool correctLines = true;
            foreach (var line in summary.Lines)
            {
                correctLines &= identities.Add(line.Kind + ":" + line.PartId) && line.Quantity > 0;
                quantity += line.Quantity;
                decimal? expectedPrice = Unit(line.PartId, line.Kind);
                correctLines &= line.UnitPrice == expectedPrice &&
                                line.TotalPrice == (expectedPrice.HasValue ? expectedPrice.Value * line.Quantity : (decimal?)null);
                if (expectedPrice.HasValue) total += expectedPrice.Value * line.Quantity;
                else unpriced += line.Quantity;
                if (line.Kind == PartKind.Frame)
                {
                    lineFrames += line.Quantity;
                    correctLines &= line.PartId == "V9";
                }
                else if (line.Kind == PartKind.Panel) linePanels += line.Quantity;
                else if (line.Kind == PartKind.Finish)
                {
                    lineFinish += line.Quantity;
                    correctLines &= actualFinish.TryGetValue(line.PartId, out int actual) && actual == line.Quantity;
                }
            }
            Check(summary.FrameCount == frames && summary.PanelCount == panels && summary.FinishCount == finishCount &&
                  lineFrames == frames && linePanels == panels && lineFinish == finishCount,
                action + ": grouped quantities match the real frame, panel and active generated finish parts");
            Check(correctLines, action + ": unique lines use expected quantities, unit prices and exact decimal line totals");
            Check(summary.PartCount == frames + panels + finishCount && quantity == summary.PartCount && stats.PartCount == summary.PartCount,
                action + ": overall count includes each frame, panel and generated finish exactly once");
            Check(summary.TotalPrice == total && Mathf.Approximately(stats.TotalPrice, (float)total) &&
                  summary.UnpricedPartCount == unpriced && stats.UnpricedPartCount == unpriced,
                action + ": summary and header totals agree with independently priced lines");
            // The redesign retired the header pill (the total moves to the
            // Checkout tab), so UIBuildStats may run with no labels at all;
            // when labels exist they must read the same snapshot.
            bool hasLabels = stats.partCountText != null && stats.priceText != null;
            Check(!hasLabels ||
                  (stats.partCountText.text == (summary.PartCount == 1 ? "1 part" : summary.PartCount + " parts") &&
                   stats.priceText.text == "Est. $" + total.ToString("N0") + (unpriced > 0 ? "*" : "")),
                action + ": visible count and estimated-price labels match the current summary");
        }

        try
        {
            BuildHistory.Suspended = false;
            build.ghostLayerMask = 1 << 30;
            stats.partPrices = new List<UIBuildStats.PartPrice>();
            // A controlled runtime catalogue makes these checks independent
            // of the owner's Inspector overrides; restore its exact list later.
            Override("V9", 35f, 35m);
            stats.panelPrice = 25f;
            stats.veneerPrice = 5f;
            stats.capSidePrice = 2f;
            stats.capEndPrice = 2f;
            stats.footPrice = 5f;
            SpacePlanningUI.Hide();
            if (SpaceModeController.Active) Object.FindFirstObjectByType<SpaceModeController>()?.ExitSpaceMode();
            Object.FindFirstObjectByType<TemplateSession>()?.SetTool(GuidedTemplateTool.None);
            build.SetCurrentPart(null);
            finish.SetOn(false, announce: false);
            history.ClearAll();
            yield return Frames(5);
            stats.RefreshNow();
            Expect(0, 0, "empty baseline");

            bool allVeneerDefaults = true;
            for (int size = 1; size <= 15; size++)
                allVeneerDefaults &= stats.TryPriceForPart("Veneer H" + size, out float price) && price == 5f;
            Check(allVeneerDefaults, "every supplied veneer size receives the $5 placeholder price");
            Check(stats.TryPriceForPart("Panel H1xH3", out float cataloguePanelPrice) && cataloguePanelPrice == 25f &&
                  stats.TryPriceForPart("Panel 300 x 500 mm", out float customPanelPrice) && customPanelPrice == 25f,
                "catalogue and measured panel ids use the configured panel price");

            var placed = build.PlacePartsBatch(new List<TemplatePartPose>
            {
                new TemplatePartPose("V9", Vector3.zero, Quaternion.Euler(build.v3RotationEuler), "summary-regression")
            }, seatVerticalsOnFloor: true, validateOverlap: false);
            Check(placed.Placed == 1 && placed.Instances != null && placed.Instances.Count == 1,
                "actual V9 fixture places once");
            if (placed.Instances == null || placed.Instances.Count != 1) yield break;
            owned.Add(placed.Instances[0]);
            yield return Frames(5);
            Expect(1, 0, "placed frame with finish off");

            finish.SetOn(true);
            yield return Frames(5);
            Check(ActiveFinishQuantities().Count > 0, "finish on generates real veneer/cap/foot FBX instances");
            Expect(1, 0, "finish on");
            finish.SetOn(false);
            yield return Frames(5);
            Expect(1, 0, "finish off");
            history.Undo();
            yield return Frames(7);
            Check(finish.IsOn && ActiveFinishQuantities().Count > 0, "Undo restores enabled finish and actual generated parts");
            Expect(1, 0, "undo finish off");
            history.Redo();
            yield return Frames(7);
            Check(!finish.IsOn && ActiveFinishQuantities().Count == 0, "Redo removes the disabled finish completely");
            Expect(1, 0, "redo finish off");
            history.Undo();
            yield return Frames(7);

            var current = FindFixtureFrame();
            Check(current != null, "restored V9 exists before deletion");
            if (current == null) yield break;
            Object.Destroy(current.gameObject);
            BuildHistory.NotifyChanged();
            yield return Frames(7);
            Expect(0, 0, "deferred frame deletion removes dressing too");
            history.Undo();
            yield return Frames(7);
            Expect(1, 0, "undo frame deletion");
            history.Redo();
            yield return Frames(7);
            Expect(0, 0, "redo frame deletion");
            history.Undo();
            yield return Frames(7);
            Expect(1, 0, "restore frame for panel/pricing checks");

            // These independent, far-away boards exercise real mesh grouping
            // without introducing incidental frame masking into the tally.
            GameObject first = MakePanel(panelPrefab, PanelFill.EdgeMm(1), PanelFill.EdgeMm(3), 0f, 0);
            GameObject rotated = MakePanel(panelPrefab, PanelFill.EdgeMm(3), PanelFill.EdgeMm(1), 37f, 1);
            GameObject custom = MakePanel(panelPrefab, 300f, 500f, 23f, 2);
            owned.Add(first); owned.Add(rotated); owned.Add(custom);
            var ghostPanel = MakePanel(panelPrefab, 600f, 800f, 0f, 3);
            ghostPanel.layer = 30;
            owned.Add(ghostPanel);
            var hiddenPanel = MakePanel(panelPrefab, 400f, 700f, 0f, 4);
            hiddenPanel.SetActive(false);
            owned.Add(hiddenPanel);
            var ghostFrame = Object.Instantiate(framePrefab);
            ghostFrame.name = "V9_GhostInstance";
            ghostFrame.layer = 30;
            ghostFrame.transform.position = Vector3.one * NeospaceUnits.Mm(10000f);
            owned.Add(ghostFrame);
            yield return Frames(5);
            Expect(1, 3, "three real panels, with ghost and inactive parts excluded");
            string groupedPanelId = null, measuredPanelId = null;
            int panelLines = 0;
            foreach (var line in stats.Summary.Lines)
                if (line.Kind == PartKind.Panel)
                {
                    panelLines++;
                    if (line.Quantity == 2) groupedPanelId = line.PartId;
                    if (line.Quantity == 1) measuredPanelId = line.PartId;
                }
            Check(panelLines == 2 && groupedPanelId != null && measuredPanelId != null &&
                  groupedPanelId.Contains("H1") && groupedPanelId.Contains("H3") &&
                  measuredPanelId.Contains("300") && measuredPanelId.Contains("500"),
                "actual panel dimensions group equal rotated/swapped boards and keep the custom size distinct");
            if (groupedPanelId == null || measuredPanelId == null) yield break;

            // Space pieces are renderer-only clones. Hidden source children
            // (as retained during a merge) and the hidden current piece must
            // not contribute alongside the visible installed children.
            var space = Object.FindFirstObjectByType<SpaceModeController>();
            Check(space != null, "Space Mode controller exists for frozen-piece tally checks");
            if (space != null)
            {
                Transform pieceFrame = FindFixtureFrame();
                space.EnterSpaceMode();
                yield return Frames(5);
                Check(SpaceModeController.Active && pieceFrame != null && !pieceFrame.gameObject.activeInHierarchy,
                    "entering Space Mode hides the original builder frame");
                var frozenRoot = new GameObject("SummaryFrozenSpacePiece");
                frozenRoot.AddComponent<SpaceInstance>();
                owned.Add(frozenRoot);
                if (pieceFrame != null)
                {
                    var visibleFrame = FrozenClone(pieceFrame.gameObject, frozenRoot.transform, "V9", true);
                    var visiblePanel = FrozenClone(first, frozenRoot.transform, "Panel installed", true);
                    FrozenClone(pieceFrame.gameObject, frozenRoot.transform, "V9", false);
                    FrozenClone(first, frozenRoot.transform, "Panel hidden source", false);
                    yield return Frames(5);
                    stats.RefreshNow();
                    Expect(1, 1, "Space frozen pieces exclude hidden source and builder parts");
                    visibleFrame.SetActive(false);
                    visiblePanel.SetActive(false);
                    stats.RefreshNow();
                    Expect(0, 0, "Space with only hidden source geometry is empty");
                }
                Object.Destroy(frozenRoot);
                yield return Frames(2);
                space.ExitSpaceMode();
                yield return Frames(5);
                Expect(1, 3, "returning from Space restores the original piece summary");
            }

            Vector3 fixedPanelPosition = first.transform.position;
            Vector3 fixedPanelScale = first.transform.localScale;
            var existingFinish = ActiveFinishRoots();
            stats.panelPrice = 30f; panelUnit = 30m;
            stats.veneerPrice = 6f; veneerUnit = 6m;
            stats.capSidePrice = 3f; capSideUnit = 3m;
            stats.capEndPrice = 4f; capEndUnit = 4m;
            stats.footPrice = 7f; footUnit = 7m;
            stats.RefreshNow();
            Expect(1, 3, "editable placeholder prices without geometry changes");
            Override("V9", 45.5f, 45.50m);
            Override(groupedPanelId, 31.25f, 31.25m);
            Override("Veneer H7", 8.75f, 8.75m);
            Override("Cap Side", 3.125f, 3.13m);
            stats.RefreshNow();
            Expect(1, 3, "exact catalogue overrides and decimal rounding");
            Check(first.transform.position == fixedPanelPosition && first.transform.localScale == fixedPanelScale &&
                  existingFinish.TrueForAll(part => part != null && part.activeInHierarchy),
                "price customization updates totals without moving panels or rebuilding the finish geometry");

            foreach (float invalid in new[] { -1f, float.NaN, float.PositiveInfinity })
            {
                Override("V9", invalid, null);
                stats.RefreshNow();
                Check(!stats.TryPriceForPart("V9", out _), "invalid explicit frame price is unpriced: " + invalid);
                Expect(1, 3, "invalid frame price " + invalid);
            }
            Override("Veneer H7", float.NaN, null);
            Override("Cap Side", float.PositiveInfinity, null);
            Override(groupedPanelId, -1f, null);
            stats.RefreshNow();
            Check(!stats.TryPriceForPart("Veneer H7", out _) && !stats.TryPriceForPart("Cap Side", out _) &&
                  !stats.TryPriceForPart(groupedPanelId, out _),
                "invalid exact finish/panel overrides do not silently fall back to placeholder prices");
            Expect(1, 3, "invalid grouped panel and finish prices");

            Override("V9", 0f, 0m);
            stats.panelPrice = 0f; panelUnit = 0m;
            stats.RefreshNow();
            Check(stats.TryPriceForPart("V9", out float freeFrame) && freeFrame == 0f &&
                  stats.TryPriceForPart(measuredPanelId, out float freePanel) && freePanel == 0f,
                "explicit and placeholder zero prices are valid free parts, distinct from missing prices");
            Expect(1, 3, "zero prices remain priced");
            stats.panelPrice = float.NaN; panelUnit = null;
            stats.RefreshNow();
            Expect(1, 3, "invalid panel fallback is disclosed");

            Object.Destroy(first);
            Object.Destroy(rotated);
            Object.Destroy(custom);
            yield return Frames(5);
            Expect(1, 0, "panel removal clears panel lines and count without stale totals");
        }
        finally
        {
            if (SpaceModeController.Active) Object.FindFirstObjectByType<SpaceModeController>()?.ExitSpaceMode();
            finish.SetOn(false, announce: false);
            foreach (GameObject item in owned) if (item != null) Object.Destroy(item);
            Transform current = FindFixtureFrame();
            if (current != null) Object.Destroy(current.gameObject);
            stats.partPrices = originalPrices;
            stats.panelPrice = originalPanel;
            stats.veneerPrice = originalVeneer;
            stats.capSidePrice = originalCapSide;
            stats.capEndPrice = originalCapEnd;
            stats.footPrice = originalFoot;
            build.ghostLayerMask = previousGhostMask;
            BuildHistory.Suspended = previousSuspended;
            Debug.Log($"Build part summary regression: {checks} checks, {failed} failures.");
        }
        yield return null;
        stats.RefreshNow();
    }

    static GameObject MakePanel(GameObject prefab, float widthMm, float heightMm, float yaw, int index)
    {
        GameObject panel = Object.Instantiate(prefab);
        panel.name = "Panel 999 x 999 mm"; // Deliberately unrelated to its real dimensions.
        panel.transform.SetPositionAndRotation(NeospaceUnits.Mm(1f) * new Vector3(5000f + index * 1200f, 1000f, 5000f),
            Quaternion.Euler(12f, yaw, 8f));
        panel.transform.localScale = NeospaceUnits.Mm(1f) * new Vector3(widthMm, heightMm, 1f);
        if (panel.GetComponent<PanelInstance>() == null) panel.AddComponent<PanelInstance>();
        foreach (Collider collider in panel.GetComponentsInChildren<Collider>()) collider.enabled = false;
        return panel;
    }

    static Transform FindFixtureFrame()
    {
        foreach (BeamConnections beam in Object.FindObjectsByType<BeamConnections>(FindObjectsSortMode.None))
        {
            Transform root = beam.transform.root;
            if (root.gameObject.layer != 30 && StructureClipboard.CleanPartId(root.name) == "V9") return root;
        }
        return null;
    }

    static GameObject FrozenClone(GameObject source, Transform parent, string name, bool active)
    {
        var clone = Object.Instantiate(source, parent);
        clone.name = name;
        clone.transform.position += Vector3.right * NeospaceUnits.Mm(3000f);
        foreach (MonoBehaviour behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true)) Object.Destroy(behaviour);
        foreach (Collider collider in clone.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        clone.SetActive(active);
        return clone;
    }

    static List<GameObject> ActiveFinishRoots()
    {
        var parts = new List<GameObject>();
        GameObject root = GameObject.Find("FinishRoot");
        if (root == null) return parts;
        foreach (Transform child in root.transform)
            if (child.gameObject.activeInHierarchy && child.GetComponentsInChildren<MeshRenderer>().Length > 0)
                parts.Add(child.gameObject);
        return parts;
    }

    static Dictionary<string, int> ActiveFinishQuantities()
    {
        var quantities = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (GameObject child in ActiveFinishRoots())
        {
            quantities.TryGetValue(child.name, out int count);
            quantities[child.name] = count + 1;
        }
        return quantities;
    }

    static IEnumerator Frames(int count)
    {
        for (int i = 0; i < count; i++) yield return null;
    }
}
#endif
