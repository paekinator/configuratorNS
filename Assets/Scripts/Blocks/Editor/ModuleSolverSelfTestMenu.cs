#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ModuleSolverSelfTestMenu
{
    /// <summary>
    /// Where both commands leave their output, as well as the Console.
    ///
    /// The Console is not a reliable record: Unity clears it on entering Play
    /// mode, and these results are only interesting for a scene that has to
    /// be built in Play mode — so the run and the wipe are one keystroke
    /// apart, and this output was lost to that twice before the file existed.
    /// Logs/ is already in .gitignore, so nothing here reaches a commit.
    /// </summary>
    const string LogFolder = "Logs";

    /// <summary>
    /// Separate files per command, so running the selftest does not wipe the
    /// scene report that was the reason for running anything.
    /// </summary>
    static void Write(string fileName, string text)
    {
        Debug.Log(text);
        string path = System.IO.Path.Combine(LogFolder, fileName);
        try
        {
            System.IO.Directory.CreateDirectory(LogFolder);
            System.IO.File.WriteAllText(path,
                System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n\n" + text);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Could not write {path}: {e.Message}");
        }
    }

    [MenuItem("Tools/Configurator/Run ModuleSolver Selftest")]
    public static void Run()
    {
        int total = ModuleSolverSelfTest.RunAll(out List<string> failures);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"ModuleSolver selftest: {total} checks, {failures.Count} failures");
        foreach (string f in failures)
            sb.AppendLine("  FAIL: " + f);
        Write("ModuleSolverSelftest.txt", sb.ToString());

        EditorUtility.DisplayDialog("ModuleSolver Selftest",
            failures.Count == 0
                ? $"{total} checks passed."
                : $"{failures.Count} / {total} checks failed. See Console.",
            "OK");
    }

    /// <summary>
    /// Report the modules in the open scene. Not a test — a way to see what
    /// the solver thinks before wiring it to a button that captures blocks.
    ///
    /// It names every part and where it stands, because the only question
    /// worth asking of this is "why is that one separate?", and a count
    /// cannot answer it. Each module also reports how many live pairings its
    /// parts hold: a part with zero is joined to nothing, which is what an
    /// unexpected extra module always turns out to be.
    /// </summary>
    [MenuItem("Tools/Configurator/Report Modules In Scene")]
    public static void Report()
    {
        var build = Object.FindFirstObjectByType<BuildController>();
        List<ModuleSolver.Module> modules = ModuleSolver.FindAll(build);

        if (modules.Count == 0)
        {
            // Not a failure, and worth saying why: the configurator builds
            // entirely at runtime. No beam, panel or attachment point is
            // saved into ConfiguratorScene.unity, so in edit mode there is
            // nothing to find — and AttachmentPoint.Live, which holds the
            // connections, is filled by OnEnable and is empty too.
            Write("ModuleReport.txt", EditorApplication.isPlaying
                ? "Modules: none — nothing is built yet."
                : "Modules: none — parts only exist in Play mode. "
                  + "Enter Play mode, build something, then run this again. "
                  + "(The selftest needs no scene and runs in either mode.)");
            return;
        }

        // How many live pairings each root holds, so a part joined to nothing
        // is visible as such rather than inferred.
        var pairings = new Dictionary<Transform, int>();
        foreach (AttachmentPoint ap in AttachmentPoint.Live)
        {
            if (ap == null || ap.pairedWith == null)
                continue;
            Transform root = ap.transform.root;
            pairings.TryGetValue(root, out int n);
            pairings[root] = n + 1;
        }

        ModuleSolver.Reconciliation check = ModuleSolver.Reconcile(build, modules);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"MODULES: {modules.Count}");
        sb.AppendLine($"  frames {check.SolvedFrames} of {check.SceneFrames} in the scene"
                      + (check.FramesAgree ? " (all accounted for)" : "   <-- FRAMES MISSING"));
        sb.AppendLine($"  panels {check.SolvedPanels} of {check.ScenePanels} in the scene"
                      + (check.PanelsAgree ? " (all accounted for)" : "   <-- PANELS MISSING"));
        sb.AppendLine($"  parts {check.SolvedFrames + check.SolvedPanels}"
                      + $"   (the price readout counts {check.SceneFrames + check.ScenePanels})");

        if (check.Rejected.Count > 0)
        {
            sb.AppendLine($"  REJECTED BY THE SOLVER: {check.Rejected.Count}");
            foreach (string line in check.Rejected)
                sb.AppendLine("       " + line);
        }

        for (int i = 0; i < modules.Count; i++)
        {
            ModuleSolver.Module m = modules[i];

            // Tripwires. The first can no longer fire: panels are assigned to
            // a module rather than being nodes, which is what used to produce
            // one phantom module holding every panel in the scene.
            string flag = m.Frames.Count == 0 ? "   <-- NO FRAMES: panels on their own"
                        : m.PartCount == 1 ? "   <-- a single part, joined to nothing"
                        : string.Empty;

            sb.AppendLine($"  {i + 1}. {m.Frames.Count} frames, {m.Panels.Count} panels"
                          + $" · {m.WidthMm}x{m.DepthMm}x{m.HeightMm} mm{flag}");

            foreach (Transform root in m.Frames)
            {
                pairings.TryGetValue(root, out int n);
                Vector3 p = root.position;
                sb.AppendLine($"       {root.name}"
                              + $"  at ({p.x:F2}, {p.y:F2}, {p.z:F2})"
                              + $"  pairings={n}"
                              + (n == 0 ? "   <-- joined to nothing" : string.Empty));
            }
        }

        Write("ModuleReport.txt", sb.ToString());
    }
}
#endif
