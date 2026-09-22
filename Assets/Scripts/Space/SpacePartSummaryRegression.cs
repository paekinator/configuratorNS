#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Real Space merge/remove paths in the isolated validation scene; no saved-library writes.</summary>
public static class SpacePartSummaryRegression
{
    public static IEnumerator RunAll(Action<string> failure)
    {
        int checks = 0, failed = 0;
        void Check(bool passed, string label)
        {
            checks++;
            if (passed) return;
            failed++;
            failure("Space part summary: " + label);
        }

        var mode = Object.FindFirstObjectByType<SpaceModeController>();
        var stats = Object.FindFirstObjectByType<UIBuildStats>();
        Check(mode != null && mode.interaction != null && stats != null,
            "validation scene has Space Mode and stats");
        if (mode == null || mode.interaction == null || stats == null) yield break;
        var interaction = mode.interaction;
        Check(!SpaceModeController.Active && interaction.InstanceCount == 0,
            "fixture starts outside Space Mode with no placed space instances");
        if (SpaceModeController.Active || interaction.InstanceCount != 0) yield break;

        var owned = new List<GameObject>();
        var finish = FinishController.Ensure();
        bool wasFinished = finish.IsOn;
        try
        {
            mode.EnterSpaceMode();
            for (int i = 0; i < 5; i++) yield return null;
            Check(SpaceModeController.Active && stats.enabled,
                "Space Mode keeps the clickable summary component enabled");
            if (!SpaceModeController.Active) yield break;

            SpaceInstance first = Piece("First", out _, out _);
            SpaceInstance second = Piece("Second", out _, out GameObject secondPanel);
            // Use the actual controller backing collections only to register
            // renderer-only fixtures; deletion/rebuild run the production paths.
            var instances = (List<SpaceInstance>)interaction.Instances;
            instances.Add(first);
            instances.Add(second);
            Merge();
            stats.RefreshNow();
            Check(stats.Summary.FrameCount == 1 && stats.Summary.PanelCount == 1,
                "coincident pieces deduplicate one frame and one board through SpaceMerge.Apply");
            Check(stats.Summary.FinishCount > 0,
                "merged physical frame receives installed finishing before its summary");
            int mergedPartCount = stats.PartCount;
            decimal mergedPrice = stats.Summary.TotalPrice;

            // A real placement preview has renderers but no SpaceInstance.
            var ghost = new GameObject("PieceGhost");
            owned.Add(ghost);
            Box(ghost.transform, "V9", new Vector3(2000, 372.5f, 0), new Vector3(41, 745, 41));
            Box(ghost.transform, "Panel preview", new Vector3(3000, 1000, 0), new Vector3(600, 400, 1));
            stats.RefreshNow();
            Check(stats.PartCount == mergedPartCount && stats.Summary.TotalPrice == mergedPrice,
                "renderer-only placement preview contributes no parts or price");

            // Two 600 x 400 sheets offset 300 mm overlap by half. The first
            // stays whole and the second contributes one strip (300 mm
            // minus the merge solver's existing 1 mm seam allowance).
            secondPanel.transform.localPosition += Vector3.right * NeospaceUnits.Mm(300);
            Merge();
            stats.RefreshNow();
            int activeDerivedPanels = 0;
            if (SpaceMerge.DerivedRoot != null)
                foreach (Transform part in SpaceMerge.DerivedRoot)
                    if (part.gameObject.activeInHierarchy && part.name.Contains("Panel")) activeDerivedPanels++;
            Check(!secondPanel.activeSelf && activeDerivedPanels == 1 &&
                stats.Summary.FrameCount == 1 && stats.Summary.PanelCount == 2,
                "overlap replaces the hidden original board with exactly one counted derived strip");

            ((List<SpaceInstance>)interaction.Selected).Add(second);
            interaction.DeleteSelection();
            stats.RefreshNow();
            Check(!second.gameObject.activeInHierarchy && stats.Summary.FrameCount == 1 && stats.Summary.PanelCount == 1,
                "same-frame delete excludes removed instance and obsolete derived boards");

            interaction.RebuildFromStates(new List<SpaceHistory.InstanceState>());
            stats.RefreshNow();
            Check(!first.gameObject.activeInHierarchy && stats.PartCount == 0 && stats.Summary.TotalPrice == 0m,
                "same-frame clear/rebuild excludes old frames, boards and generated finish");
            yield return null;
            stats.RefreshNow();
            Check(stats.PartCount == 0, "empty summary remains empty after deferred destruction settles");
        }
        finally
        {
            interaction.RebuildFromStates(new List<SpaceHistory.InstanceState>());
            foreach (GameObject go in owned)
                if (go != null) { go.SetActive(false); Object.Destroy(go); }
            if (SpaceModeController.Active) mode.ExitSpaceMode();
            finish.SetOn(wasFinished, announce: false);
            stats.RefreshNow();
            Debug.Log($"Space part summary regression: {checks} checks, {failed} failures.");
        }
        yield return null;

        SpaceInstance Piece(string name, out GameObject frame, out GameObject panel)
        {
            var root = new GameObject("SummaryMerge" + name);
            root.transform.SetParent(interaction.transform, false);
            owned.Add(root);
            frame = Box(root.transform, "V9", new Vector3(0, 372.5f, 0), new Vector3(41, 745, 41));
            panel = Box(root.transform, "Panel fixture", new Vector3(1000, 1000, 0), new Vector3(600, 400, 1));
            var inst = root.AddComponent<SpaceInstance>();
            inst.pieceId = name;
            inst.pieceName = name;
            return inst;
        }

        void Merge() => SpaceMerge.Apply(interaction.Instances,
            mode.buildController != null ? mode.buildController.partDatabase : null, interaction.transform);
    }

    static GameObject Box(Transform parent, string name, Vector3 centerMm, Vector3 sizeMm)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = centerMm * NeospaceUnits.Mm(1);
        go.transform.localScale = sizeMm * NeospaceUnits.Mm(1);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
}
#endif
