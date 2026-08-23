using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// <see cref="ConfigurationModel"/> → scene. Mirrors the battle-tested
/// <c>BuildHistory.RestoreRoutine</c> path: wipe everything, replay beams
/// through <c>PlacePartsBatch</c> with exact poses (no floor re-seating, no
/// overlap re-validation), then re-attach panels by geometric slot lookup,
/// then apply the finish flag. The import registers as ONE undo step.
/// </summary>
public class ConfigurationRestorer : MonoBehaviour
{
    public BuildController buildController;
    public PanelSlotManager panelSlotManager;

    public struct Report
    {
        public int BeamsPlaced, BeamsSkipped;
        public int PanelsPlaced, PanelsSkipped;
        public List<string> Notes;

        public string Summary =>
            $"Loaded {BeamsPlaced} beams and {PanelsPlaced} panels" +
            (BeamsSkipped + PanelsSkipped > 0
                ? $" ({BeamsSkipped + PanelsSkipped} parts could not be restored)."
                : ".");
    }

    bool _running;

    public bool IsRunning => _running;

    /// <summary>Replace the current build with the model's content.</summary>
    public void Restore(ConfigurationModel model, Action<Report> onDone = null)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));
        if (_running)
        {
            Debug.LogWarning("[ConfigCode] A restore is already running.");
            return;
        }
        StartCoroutine(RestoreRoutine(model, onDone));
    }

    IEnumerator RestoreRoutine(ConfigurationModel model, Action<Report> onDone)
    {
        _running = true;
        var report = new Report { Notes = new List<string>() };

        DestroyAllParts();
        yield return null;   // Destroy() completes at end of frame

        // ---- Beams: exact stored poses through the batch pipeline ----
        var poses = new List<TemplatePartPose>(model.Beams.Count);
        foreach (BeamRecord b in model.Beams)
        {
            if (!PartRegistry.TryGetPart(b.PartCode, out string partId, out bool retired))
            {
                report.BeamsSkipped++;
                report.Notes.Add($"Unknown part code {b.PartCode} · skipped.");
                continue;
            }
            if (retired)
                report.Notes.Add($"{partId} is retired · attempting placement anyway.");

            Quaternion rot = PoseQuantizer.IsValidOrientationIndex(b.OrientIndex)
                ? PoseQuantizer.FromOrientationIndex(b.OrientIndex)
                : PoseQuantizer.FromEulerDeci(b.EulerXDeci, b.EulerYDeci, b.EulerZDeci);

            poses.Add(new TemplatePartPose(
                partId,
                new Vector3(NeospaceUnits.Mm(b.XMm), NeospaceUnits.Mm(b.YMm), NeospaceUnits.Mm(b.ZMm)),
                rot,
                "config-code"));
        }

        if (buildController != null && poses.Count > 0)
        {
            var result = buildController.PlacePartsBatch(
                poses, seatVerticalsOnFloor: false, validateOverlap: false);
            report.BeamsPlaced = result.Placed;
            int failed = poses.Count - result.Placed;
            if (failed > 0)
            {
                report.BeamsSkipped += failed;
                report.Notes.Add($"{failed} beams failed to place: {result.Message}");
            }
        }
        else if (panelSlotManager != null)
        {
            panelSlotManager.RebuildConnectionsAndRescanSlots();
        }

        // ---- Panels: slot references re-resolved geometrically ----
        if (panelSlotManager != null)
        {
            foreach (PanelRecord p in model.Panels)
            {
                Vector3 center = new Vector3(
                    NeospaceUnits.Mm(p.XMm), NeospaceUnits.Mm(p.YMm), NeospaceUnits.Mm(p.ZMm));
                Vector3 normal = PoseQuantizer.FromSlotAxis(p.Axis);

                PanelSlotHandle slot = StructureClipboard.FindSlotNear(center, normal);
                if (slot == null)
                {
                    report.PanelsSkipped++;
                    report.Notes.Add($"No slot found near ({p.XMm}, {p.YMm}, {p.ZMm}) mm.");
                    continue;
                }

                int side = p.SideMinus ? -1 : 1;
                if (Vector3.Dot(slot.normal, normal) < 0f)
                    side = -side;

                if (!panelSlotManager.CanPlacePanel(slot, side) ||
                    panelSlotManager.PlacePanel(slot, side) == null)
                {
                    report.PanelsSkipped++;
                    report.Notes.Add($"Slot near ({p.XMm}, {p.YMm}, {p.ZMm}) mm rejected the panel.");
                    continue;
                }
                report.PanelsPlaced++;
            }
        }
        else if (model.Panels.Count > 0)
        {
            report.PanelsSkipped = model.Panels.Count;
            report.Notes.Add("No PanelSlotManager · panels skipped.");
        }

        // ---- Finish flag ----
        var veneers = FindObjectsByType<VeneerManager>(FindObjectsSortMode.None);
        foreach (VeneerManager vm in veneers)
        {
            if (vm == null)
                continue;
            if (model.FinishApplied) vm.ApplyVeneers();
            else vm.ClearVeneers();
        }

        yield return null;
        _running = false;

        BuildHistory.NotifyChanged();
        SelectionStatus.Set(report.Summary, 5f);
        foreach (string note in report.Notes)
            Debug.LogWarning("[ConfigCode] " + note);

        onDone?.Invoke(report);
    }

    /// <summary>Same wipe as BuildHistory: parts, panels, then slot bookkeeping.</summary>
    void DestroyAllParts()
    {
        int ghostMask = buildController != null ? buildController.ghostLayerMask.value : 0;

        foreach (BeamConnections conn in FindObjectsByType<BeamConnections>(FindObjectsSortMode.None))
        {
            if (conn == null)
                continue;
            Transform root = conn.transform.root;
            if ((ghostMask & (1 << root.gameObject.layer)) != 0)
                continue;
            if (StructureClipboard.CleanPartId(root.name) == null)
                continue;

            conn.ReleaseAll();
            Destroy(root.gameObject);
        }

        foreach (PanelInstance pi in FindObjectsByType<PanelInstance>(FindObjectsSortMode.None))
        {
            if (pi == null)
                continue;
            if ((ghostMask & (1 << pi.gameObject.layer)) != 0)
                continue;
            Destroy(pi.gameObject);
        }

        foreach (PanelSlotHandle slot in FindObjectsByType<PanelSlotHandle>(FindObjectsSortMode.None))
        {
            if (slot == null)
                continue;
            slot.panelPlus = null;
            slot.panelMinus = null;
            if (slot.blocker != null)
                slot.blocker.gameObject.SetActive(false);
        }
    }
}
