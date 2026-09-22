#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Play Mode integration checks in an otherwise empty scene.</summary>
public static class ConfigurationRestoreRegression
{
    public static IEnumerator RunAll(Action<string> failure)
    {
        if (Object.FindFirstObjectByType<BuildHistory>() != null ||
            Object.FindFirstObjectByType<BeamConnections>() != null)
        {
            failure("Configuration restore regression requires an empty Play Mode scene.");
            yield break;
        }

        int checks = 0;
        void Check(bool passed, string label) { checks++; if (!passed) failure(label); }
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Vertical/V9.prefab");
        GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PanelMesh.prefab");
        if (prefab == null || panelPrefab == null)
        {
            failure("Restore regression requires the V9 and PanelMesh prefabs.");
            yield break;
        }

        bool previousSuspended = BuildHistory.Suspended;
        BuildHistory.Suspended = false;
        bool hadFinish = FinishController.Instance != null;
        bool hadConfigurationTools = GameObject.Find("ConfigurationTools") != null;
        var host = new GameObject("ConfigurationRestoreRegression");
        var database = ScriptableObject.CreateInstance<PartDatabase>();
        database.parts.Add(new PartDatabase.PartEntry { partId = "V9", realPrefab = prefab });
        var build = host.AddComponent<BuildController>();
        build.partDatabase = database;
        build.debugLogs = false;
        var panels = host.AddComponent<PanelSlotManager>();
        panels.panelPrefab = panelPrefab;
        panels.debug = false;
        build.panelSlotManager = panels;
        var history = host.AddComponent<BuildHistory>();
        history.buildController = build;
        history.panelSlotManager = panels;
        var restorer = host.AddComponent<ConfigurationRestorer>();
        restorer.buildController = build;
        restorer.panelSlotManager = panels;

        try
        {
            yield return Frames(2);
            var placed = build.PlacePartsBatch(new List<TemplatePartPose>
            {
                new TemplatePartPose("V9", new Vector3(1.76f, 0f, 0.88f), Quaternion.identity, "save-regression")
            }, seatVerticalsOnFloor: false, validateOverlap: false);
            Check(placed.Placed == 1, "Restore baseline beam placed.");
            FinishController.Ensure().SetOn(true, announce: false);
            BuildHistory.NotifyChanged();
            yield return Frames(3);
            string originalCode = ConfigurationCode.Encode(build);
            var originalBeam = Object.FindFirstObjectByType<BeamConnections>();

            // A known registry id whose prefab is absent must be rejected
            // before the current scene is hidden or destroyed.
            var unavailable = ConfigurationCode.Decode(ConfigurationCodeSelfTest.GoldenCode);
            ConfigurationRestorer.Report report = default;
            bool done = false;
            restorer.Restore(unavailable, result => { report = result; done = true; });
            Check(done && !report.Succeeded && !restorer.IsRunning,
                "Missing prefab is rejected synchronously before scene mutation.");
            Check(originalBeam != null && ConfigurationCode.Encode(build) == originalCode,
                "Missing-prefab rejection retains the original object, pose, and finish.");

            // Syntax-valid geometry with an impossible panel reference fails
            // after replay has begun. The exact original object must survive.
            var impossible = ConfigurationCode.Decode(originalCode);
            impossible.Panels.Add(new PanelRecord
            {
                XMm = 999999, YMm = 999999, ZMm = 999999, Axis = SlotAxis.PlusY
            });
            done = false;
            restorer.Restore(impossible, result => { report = result; done = true; });
            yield return Frames(8);
            Check(done && !report.Succeeded && report.PanelsSkipped == 1,
                "Unattachable panel rejects the whole replacement.");
            Check(originalBeam != null && originalBeam.gameObject.activeInHierarchy,
                "Rollback restores the exact original GameObject.");
            Check(ConfigurationCode.Encode(build) == originalCode,
                "Rollback preserves the original configuration including finish.");
            Check(!restorer.IsRunning && !history.IsRestoring, "Rollback releases history and input guards.");

            var replacement = ConfigurationCode.Decode(originalCode);
            var beam = replacement.Beams[0];
            beam.XMm += 880;
            replacement.Beams[0] = beam;
            replacement.FinishApplied = false;
            string replacementCode = ConfigurationCode.Encode(replacement);
            done = false;
            restorer.Restore(replacement, result => { report = result; done = true; });
            yield return Frames(8);
            Check(done && report.Succeeded && report.BeamsPlaced == 1, "Valid code loads completely.");
            Check(originalBeam == null && ConfigurationCode.Encode(build) == replacementCode,
                "Successful load commits the saved pose and finish, then removes the old object.");
            history.Undo();
            yield return Frames(6);
            Check(ConfigurationCode.Encode(build) == originalCode, "One Undo restores the complete pre-load configuration.");
            history.Redo();
            yield return Frames(6);
            Check(ConfigurationCode.Encode(build) == replacementCode, "One Redo restores the completed imported configuration.");

            // Invalid text must never reach the restorer at all.
            bool threw = false;
            try { ConfigurationCode.Load("NS1-corrupt", build); }
            catch (ConfigurationCodeException) { threw = true; }
            Check(threw && ConfigurationCode.Encode(build) == replacementCode,
                "Corrupt input is rejected before touching the scene.");

            var unsupported = new GameObject("V99");
            unsupported.AddComponent<BeamConnections>();
            bool captureRejected = false;
            try { ConfigurationCode.Encode(build); }
            catch (InvalidOperationException) { captureRejected = true; }
            Check(captureRejected, "A part missing from the save registry prevents a silently incomplete save.");
            Object.Destroy(unsupported);
            yield return null;

            // Space Mode hides the current build and suspends piece history
            // while this same restorer prepares render-only master groups.
            var hiddenOriginal = Object.FindFirstObjectByType<BeamConnections>();
            hiddenOriginal.gameObject.SetActive(false);
            BuildHistory.Suspended = true;
            var factory = host.AddComponent<PieceInstanceFactory>();
            factory.buildController = build;
            GameObject master = null;
            done = false;
            factory.GetMaster("regression-piece", replacementCode, result => { master = result; done = true; });
            yield return Frames(10);
            Check(done && master != null, "Space master staging succeeds while piece history is suspended.");
            Check(hiddenOriginal != null && !hiddenOriginal.gameObject.activeSelf,
                "Preparing a space master preserves the hidden original piece.");
            hiddenOriginal.gameObject.SetActive(true);
            BuildHistory.Suspended = false;
        }
        finally
        {
            foreach (BeamConnections beam in Object.FindObjectsByType<BeamConnections>(FindObjectsSortMode.None))
                Object.Destroy(beam.transform.root.gameObject);
            foreach (PanelInstance panel in Object.FindObjectsByType<PanelInstance>(FindObjectsSortMode.None))
                Object.Destroy(panel.gameObject);
            if (panels.slotsRoot != null) Object.Destroy(panels.slotsRoot.gameObject);
            if (panels.panelsRoot != null) Object.Destroy(panels.panelsRoot.gameObject);
            if (!hadFinish && FinishController.Instance != null) Object.Destroy(FinishController.Instance.gameObject);
            if (!hadConfigurationTools && GameObject.Find("ConfigurationTools") != null)
                Object.Destroy(GameObject.Find("ConfigurationTools"));
            Object.Destroy(host);
            Object.Destroy(database);
            BuildHistory.Suspended = previousSuspended;
        }
        yield return null;
        Debug.Log($"Configuration restore regression: {checks} checks executed.");
    }

    static IEnumerator Frames(int count)
    {
        for (int i = 0; i < count; i++) yield return null;
    }
}
#endif
