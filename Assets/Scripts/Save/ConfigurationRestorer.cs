using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// <see cref="ConfigurationModel"/> → scene. Parks the previous build, replays beams
/// through <c>PlacePartsBatch</c> with exact poses (no floor re-seating, no
/// overlap re-validation), then re-attach panels by geometric slot lookup,
/// then applies the finish flag. A complete import commits ONE undo step;
/// a failed import restores the original scene objects.
/// </summary>
public class ConfigurationRestorer : MonoBehaviour
{
    public BuildController buildController;
    public PanelSlotManager panelSlotManager;

    public struct Report
    {
        public bool Succeeded;
        public string Error;
        public int BeamsPlaced, BeamsSkipped;
        public int PanelsPlaced, PanelsSkipped;
        public List<string> Notes;

        public string Summary =>
            !Succeeded ? "Could not load this piece. Your previous build was kept. " + Error :
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
            onDone?.Invoke(new Report { Error = "A load is already running.", Notes = new List<string>() });
            return;
        }
        string preflight = ValidateForRestore(model);
        if (preflight != null)
        {
            var rejected = new Report { Error = preflight, Notes = new List<string>() };
            SelectionStatus.Set(rejected.Summary, 8f);
            onDone?.Invoke(rejected);
            return;
        }
        StartCoroutine(RestoreRoutine(model, onDone));
    }

    /// <summary>Checks dependencies before anything in the current scene is changed.</summary>
    public string ValidateForRestore(ConfigurationModel model)
    {
        if (model == null) return "The configuration is empty.";
        if (buildController == null) return "Build controls are unavailable.";
        if (model.Beams.Count > 0 && buildController.partDatabase == null)
            return "The part catalogue is unavailable.";
        foreach (BeamRecord beam in model.Beams)
        {
            if (!PartRegistry.TryGetPart(beam.PartCode, out string partId, out _))
                return $"Unknown part code {beam.PartCode}.";
            if (buildController.partDatabase.GetRealPrefab(partId) == null)
                return $"Part {partId} is unavailable in this build.";
        }
        if (model.Panels.Count > 0 && (panelSlotManager == null || panelSlotManager.panelPrefab == null))
            return "Panels are unavailable in this build.";
        return null;
    }

    struct ParkedPart
    {
        public Transform Transform;
        public Transform Parent;
    }

    IEnumerator RestoreRoutine(ConfigurationModel model, Action<Report> onDone)
    {
        _running = true;
        var report = new Report { Notes = new List<string>() };
        // Space's PieceInstanceFactory deliberately uses this same replay
        // pipeline to stage render-only masters while piece history sleeps.
        BuildHistory history = BuildHistory.Suspended ? null : BuildHistory.Instance;
        if (history != null && !history.TryBeginExternalRestore())
        {
            _running = false;
            report.Error = "Finish the current history operation first.";
            SelectionStatus.Set(report.Summary, 5f);
            onDone?.Invoke(report);
            yield break;
        }

        var parked = new List<ParkedPart>();
        var inputControls = new List<Behaviour>();
        GameObject parking = null;
        bool replayStarted = false;
        bool oldFinish = FinishController.Instance != null && FinishController.Instance.IsOn;
        try
        {
            SuspendMutatingInputs(inputControls);
            // Settle a placement/deletion from the same frame before taking
            // the undo checkpoint and preserving the actual original objects.
            yield return null;
            history?.CaptureBeforeExternalRestore();
            parking = new GameObject("PreviousBuildDuringLoad");
            parking.SetActive(false);
            try
            {
                FinishController.Ensure().SetOn(false, announce: false);
                ParkCurrentParts(parking.transform, parked);
            }
            catch (Exception e) { report.Error = e.Message; }

            yield return null;
            if (report.Error == null)
            {
                try
                {
                    replayStarted = true;
                    ApplyModel(model, ref report);
                    if (report.BeamsSkipped + report.PanelsSkipped > 0)
                        report.Error = "Some saved parts could not be attached in this version.";
                    else
                        report.Succeeded = true;
                }
                catch (Exception e) { report.Error = e.Message; }
            }

            if (!report.Succeeded)
            {
                // Original objects remain untouched in the inactive parking
                // hierarchy, so even a corrupt panel reference cannot erase
                // the current build. Remove only the attempted replacement.
                if (replayStarted) DestroyAllParts();
                yield return null;
                foreach (ParkedPart part in parked)
                    if (part.Transform != null) part.Transform.SetParent(part.Parent, true);
                panelSlotManager?.RebuildConnectionsAndRescanSlots();
                FinishController.Ensure().SetOn(oldFinish, announce: false);
            }
            // On success Destroy(parking) commits the replacement. On failure
            // it is empty: all original objects have been restored above.
            Destroy(parking);
            parking = null;
            yield return null;
        }
        finally
        {
            // A disabled host / unexpected exception must still release the
            // history guard and retain any parked originals.
            if (parking != null)
            {
                // If an unexpected exception interrupts replay, remove its
                // active objects before bringing originals back. A failure
                // while parking has not created a replacement to remove.
                if (replayStarted) DestroyAllParts();
                foreach (ParkedPart part in parked)
                    if (part.Transform != null) part.Transform.SetParent(part.Parent, true);
                Destroy(parking);
            }
            foreach (Behaviour control in inputControls)
                if (control != null) control.enabled = true;
            _running = false;
            if (history != null) history.EndExternalRestore(report.Succeeded);
            else BuildHistory.NotifyChanged();
        }

        SelectionStatus.Set(report.Summary, 7f);
        foreach (string note in report.Notes) Debug.LogWarning("[ConfigCode] " + note);
        onDone?.Invoke(report);
    }

    static void SuspendMutatingInputs(List<Behaviour> suspended)
    {
        // Component.enabled does not block public UI callbacks, so stop the
        // EventSystem as well as world/keyboard tools for this short transaction.
        // Managers used by the replay (slots, finish, history) remain enabled.
        foreach (Behaviour control in FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
        {
            if (!control.enabled) continue;
            if (!(control is BuildController || control is GhostController ||
                control is PanelGhostController || control is TemplateSession ||
                control is GuidedModeController || control is FreePartSession ||
                control is MarqueeSelectionController || control is MoveGizmoController ||
                control is BeamResizeSession || control is PanelLayerMover ||
                control is StructureClipboard || control is SpaceModeController ||
                control is SpaceInteractionController || control is UnityEngine.EventSystems.EventSystem))
                continue;
            suspended.Add(control);
            control.enabled = false;
        }
    }

    void ApplyModel(ConfigurationModel model, ref Report report)
    {

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
        // The finish is the FinishController dressing; setting the toggle
        // regenerates (or clears) it for the freshly restored structure.
        FinishController.Ensure().SetOn(model.FinishApplied, announce: false);

    }

    void ParkCurrentParts(Transform parking, List<ParkedPart> parked)
    {
        int ghostMask = buildController.ghostLayerMask.value;
        var roots = new HashSet<Transform>();
        foreach (BeamConnections conn in FindObjectsByType<BeamConnections>(FindObjectsSortMode.None))
        {
            if (conn == null) continue;
            Transform root = conn.transform.root;
            if ((ghostMask & (1 << root.gameObject.layer)) != 0 ||
                StructureClipboard.CleanPartId(root.name) == null || !roots.Add(root)) continue;
            conn.ReleaseAll();
        }
        foreach (PanelInstance panel in FindObjectsByType<PanelInstance>(FindObjectsSortMode.None))
            if ((ghostMask & (1 << panel.gameObject.layer)) == 0) roots.Add(panel.transform);

        foreach (Transform root in roots)
        {
            parked.Add(new ParkedPart { Transform = root, Parent = root.parent });
            root.SetParent(parking, true);
        }
        ClearPanelSlots();
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
            root.gameObject.SetActive(false);
            Destroy(root.gameObject);
        }

        foreach (PanelInstance pi in FindObjectsByType<PanelInstance>(FindObjectsSortMode.None))
        {
            if (pi == null)
                continue;
            if ((ghostMask & (1 << pi.gameObject.layer)) != 0)
                continue;
            pi.gameObject.SetActive(false);
            Destroy(pi.gameObject);
        }

        ClearPanelSlots();
    }

    void ClearPanelSlots()
    {
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
