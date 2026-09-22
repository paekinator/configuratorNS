#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// Finishing diagnostics on real assemblies. Builds representative structures
/// through the strict placement pipeline (posts, connectors, panels), dresses
/// them with the Finish system, then writes a per-channel coverage report
/// (parts, gaps, penetrations) and renders evidence images. The same run is
/// repeated in Space Mode for a frozen piece. Output: Logs/finish-diagnostics.
/// Started by <c>FinishDiagnostics.Run</c> (Editor); exits the editor when done.
/// </summary>
public class FinishDiagnosticsHost : MonoBehaviour
{
    const string OutputDirectory = "Logs/finish-diagnostics";
    const float PollWaitSeconds = 0.5f;
    const float TimeoutSeconds = 420f;

    readonly StringBuilder _report = new StringBuilder();
    readonly List<string> _errors = new List<string>();
    float _started;
    bool _finished;

    BuildController _build;
    PanelSlotManager _slots;
    TemplateSpawner _spawner;
    FinishController _finish;
    SpaceModeController _mode;
    Dictionary<string, FinishGenerator.PartDimensions> _dims;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (!SessionState.GetBool("FinishDiagnostics.Pending", false)) return;
        SessionState.SetBool("FinishDiagnostics.Pending", false);
        var host = new GameObject("FinishDiagnosticsHost").AddComponent<FinishDiagnosticsHost>();
        DontDestroyOnLoad(host.gameObject);
        host._started = Time.realtimeSinceStartup;
        Application.logMessageReceived += host.Log;
        host.StartCoroutine(host.Run());
    }

    void Log(string condition, string stack, LogType type)
    {
        if (stack != null && stack.Contains("UnityEditor.Search.SearchDatabase") && !stack.Contains("Assets/Scripts/"))
            return;
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            _errors.Add(condition + "\n" + stack);
    }

    void Update()
    {
        if (!_finished && Time.realtimeSinceStartup - _started > TimeoutSeconds)
        {
            _errors.Add("Finish diagnostics timed out.");
            Finish();
        }
    }

    // ------------------------------------------------------------------
    // Driver
    // ------------------------------------------------------------------

    IEnumerator Run()
    {
        yield return null;
        _build = FindFirstObjectByType<BuildController>();
        _mode = FindFirstObjectByType<SpaceModeController>();
        _finish = FinishController.Ensure();
        _slots = _build != null && _build.panelSlotManager != null ? _build.panelSlotManager : FindFirstObjectByType<PanelSlotManager>();
        _spawner = FindFirstObjectByType<TemplateSpawner>();
        if (_spawner == null)
            _spawner = new GameObject("FinishDiagnosticsSpawner").AddComponent<TemplateSpawner>();
        _spawner.buildController = _build;
        _spawner.panelSlotManager = _slots;
        if (_build == null || _slots == null || _build.partDatabase == null)
        {
            _errors.Add("Scene is missing BuildController / PanelSlotManager / PartDatabase.");
            Finish();
            yield break;
        }
        _dims = (Dictionary<string, FinishGenerator.PartDimensions>)typeof(FinishController)
            .GetMethod("MeasuredModelDimensions", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_finish, null);

        Directory.CreateDirectory(OutputDirectory);
        BuildHistory.Suspended = false;
        SpacePlanningUI.Hide();
        if (SpaceModeController.Active && _mode != null) _mode.ExitSpaceMode();
        var session = FindFirstObjectByType<TemplateSession>();
        if (session != null) session.SetTool(GuidedTemplateTool.None);
        _build.SetCurrentPart(null);
        _finish.SetOn(false, announce: false);
        yield return ClearBuild();

        _report.AppendLine("Finish diagnostics — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        _report.AppendLine("Model dimensions (mm, half extents):");
        foreach (var kv in _dims)
            _report.AppendLine($"  {kv.Key,-12} L={kv.Value.HalfLengthMm * 2f:F3} W={kv.Value.HalfWidthMm * 2f:F3} T={kv.Value.HalfThicknessMm * 2f:F3}");

        string code = SessionState.GetString("FinishDiagnostics.Code", "");
        if (!string.IsNullOrEmpty(code))
        {
            yield return CodeScenario(code);
            Finish();
            yield break;
        }

        yield return Scenario("wall-bay", WallBay);
        yield return Scenario("wall-bay-single-side", WallBaySingleSide);
        yield return Scenario("shelf-ring-two-levels", ShelfRing);
        yield return Scenario("cabinet-v17", Cabinet);
        yield return Scenario("twist-branch", TwistBranch);
        yield return Scenario("wall-bay-yaw37", WallBayYaw);
        yield return SpaceScenario("space-shelf-ring", ShelfRing);
        Finish();
    }

    void Finish()
    {
        if (_finished) return;
        _finished = true;
        Application.logMessageReceived -= Log;
        _report.AppendLine();
        _report.AppendLine($"Errors/exceptions: {_errors.Count}");
        foreach (string e in _errors) _report.AppendLine(e);
        Directory.CreateDirectory(OutputDirectory);
        File.WriteAllText(Path.Combine(OutputDirectory, "report.txt"), _report.ToString());
        Debug.Log("Finish diagnostics complete: " + Path.Combine(OutputDirectory, "report.txt"));
        EditorApplication.Exit(_errors.Count == 0 ? 0 : 1);
    }

    IEnumerator Wait()
    {
        yield return new WaitForSecondsRealtime(PollWaitSeconds);
        yield return null;
    }

    IEnumerator Scenario(string name, Func<IEnumerator> builder)
    {
        _report.AppendLine();
        _report.AppendLine("==================================================================");
        _report.AppendLine("=== Scenario: " + name + " (build mode) ===");
        yield return ClearBuild();
        yield return builder();
        yield return Wait();
        _finish.SetOn(true, announce: false);
        yield return Wait();
        _finish.RefreshNow();
        yield return Wait();
        try
        {
            Report(name, spaceMode: false);
            Capture(name, spaceMode: false, extra: null);
        }
        catch (Exception e)
        {
            _errors.Add("Scenario " + name + ": " + e);
        }
        _finish.SetOn(false, announce: false);
        yield return Wait();
    }

    /// <summary>Restore a user's configuration code through the real restorer and diagnose it.</summary>
    IEnumerator CodeScenario(string code)
    {
        const string name = "user-code";
        _report.AppendLine();
        _report.AppendLine("==================================================================");
        _report.AppendLine("=== Scenario: " + name + " (restored configuration code) ===");
        _report.AppendLine("  code: " + code);
        yield return ClearBuild();

        ConfigurationCodeValidation check = ConfigurationCode.Validate(code);
        if (!check.IsValid)
        {
            _report.AppendLine("  INVALID CODE: " + check.Error);
            yield break;
        }
        _report.AppendLine($"  model: {check.Model.Beams.Count} beams, {check.Model.Panels.Count} panels, finish flag {check.Model.FinishApplied}");
        foreach (BeamRecord b in check.Model.Beams)
            _report.AppendLine($"    beam code {b.PartCode} at ({b.XMm},{b.YMm},{b.ZMm}) mm");
        foreach (PanelRecord p in check.Model.Panels)
            _report.AppendLine($"    panel axis {p.Axis} minus-side {p.SideMinus} at ({p.XMm},{p.YMm},{p.ZMm}) mm");

        bool done = false;
        string error = null;
        var restorer = ConfigurationCode.GetOrCreateRestorer(_build);
        restorer.Restore(check.Model, report =>
        {
            done = true;
            error = report.Succeeded ? null : report.Error;
        });
        float deadline = Time.realtimeSinceStartup + 60f;
        while (!done && Time.realtimeSinceStartup < deadline) yield return null;
        _report.AppendLine($"  restore: {(done ? (error ?? "succeeded") : "TIMED OUT")}");
        yield return Wait();
        yield return Wait();

        _finish.SetOn(true, announce: false);
        yield return Wait();
        _finish.RefreshNow();
        yield return Wait();
        try
        {
            Report(name, spaceMode: false);
            Capture(name, spaceMode: false, extra: null, sides: true);
        }
        catch (Exception e)
        {
            _errors.Add("Scenario " + name + ": " + e);
        }
        _finish.SetOn(false, announce: false);
        yield return Wait();
    }

    IEnumerator SpaceScenario(string name, Func<IEnumerator> builder)
    {
        _report.AppendLine();
        _report.AppendLine("==================================================================");
        _report.AppendLine("=== Scenario: " + name + " (space mode, frozen piece) ===");
        if (_mode == null || _mode.interaction == null)
        {
            _report.AppendLine("SpaceModeController unavailable; skipped.");
            yield break;
        }
        yield return ClearBuild();
        yield return builder();
        yield return Wait();

        // The real piece pipeline: save the build as a configuration code,
        // then place instances of it through the piece factory (master
        // restore + freeze) — two side by side sharing a post line, so the
        // Space merge deduplicates and splits like a user's arrangement,
        // plus one turned 90 degrees.
        string code = ConfigurationCodec.Encode(ConfigurationCapture.Capture(_build));
        _report.AppendLine("  piece code: " + code);
        yield return ClearBuild();

        _mode.EnterSpaceMode();
        for (int i = 0; i < 6; i++) yield return null;
        var interaction = _mode.interaction;
        float module = NeospaceUnits.ModuleMeters;
        var states = new List<SpaceHistory.InstanceState>
        {
            new SpaceHistory.InstanceState { PieceId = name, PieceName = name, Code = code, Position = Vector3.zero, YawDegrees = 0f },
            new SpaceHistory.InstanceState { PieceId = name, PieceName = name, Code = code, Position = new Vector3(8f * module, 0f, 0f), YawDegrees = 0f },
            new SpaceHistory.InstanceState { PieceId = name, PieceName = name, Code = code, Position = new Vector3(24f * module, 0f, 0f), YawDegrees = 90f },
        };
        bool rebuilt = false;
        interaction.RebuildFromStates(states, () => rebuilt = true);
        float deadline = Time.realtimeSinceStartup + 40f;
        while (!rebuilt && Time.realtimeSinceStartup < deadline) yield return null;
        _report.AppendLine($"  instances rebuilt: {rebuilt}, count {interaction.InstanceCount}");
        yield return Wait();
        _finish.RefreshNow();
        yield return Wait();
        _finish.RefreshNow();
        yield return Wait();
        try
        {
            Report(name, spaceMode: true);
            Capture(name, spaceMode: true, extra: interaction.gameObject);
        }
        catch (Exception e)
        {
            _errors.Add("Scenario " + name + ": " + e);
        }

        interaction.RebuildFromStates(new List<SpaceHistory.InstanceState>());
        yield return null;
        _finish.RefreshNow();
        _mode.ExitSpaceMode();
        yield return Wait();
    }

    // ------------------------------------------------------------------
    // Scene building helpers
    // ------------------------------------------------------------------

    IEnumerator ClearBuild()
    {
        foreach (Transform root in LiveFrameRoots())
        {
            root.gameObject.SetActive(false);
            Destroy(root.gameObject);
        }
        foreach (PanelInstance pi in FindObjectsByType<PanelInstance>(FindObjectsSortMode.None))
        {
            if (pi == null) continue;
            pi.gameObject.SetActive(false);
            Destroy(pi.gameObject);
        }
        yield return null;
        Physics.SyncTransforms();
        _slots.RebuildConnectionsAndRescanSlots();
        yield return null;
    }

    List<Transform> LiveFrameRoots()
    {
        var seen = new HashSet<Transform>();
        var roots = new List<Transform>();
        int ghostMask = _build.ghostLayerMask.value;
        foreach (BeamConnections conn in FindObjectsByType<BeamConnections>(FindObjectsSortMode.None))
        {
            if (conn == null) continue;
            Transform root = conn.transform.root;
            if ((ghostMask & (1 << root.gameObject.layer)) != 0) continue;
            if (seen.Add(root)) roots.Add(root);
        }
        return roots;
    }

    /// <summary>A ground point in millimetres; <see cref="Posts"/> converts to world units.</summary>
    static Vector3 Mm(float x, float y, float z) => new Vector3(x, y, z);

    Quaternion Upright(string id)
    {
        Quaternion upright = Quaternion.Euler(_build.v3RotationEuler);
        if (_build.partDatabase.TryGet(id, out var entry) && entry.overrideVPlacement)
            upright = Quaternion.Euler(entry.vRotationEuler);
        return upright;
    }

    List<GameObject> Posts(string id, float yawDegrees, params Vector3[] groundMm)
    {
        var poses = new List<TemplatePartPose>();
        Quaternion rotation = Quaternion.AngleAxis(yawDegrees, Vector3.up) * Upright(id);
        foreach (Vector3 g in groundMm)
            poses.Add(new TemplatePartPose(id, g * NeospaceUnits.Mm(1f), rotation, "finish-diagnostics"));
        var result = _build.PlacePartsBatch(poses, seatVerticalsOnFloor: true, validateOverlap: false);
        _report.AppendLine($"  posts {id}: requested {poses.Count}, placed {result.Placed} {result.Message}");
        return result.Instances ?? new List<GameObject>();
    }

    static Vector3 AxisXZ(GameObject post)
    {
        FrameOverlapResolver.TryBuildRecord(post.transform, out var rec);
        return new Vector3(rec.Center.x, 0f, rec.Center.z);
    }

    static float RowY(GameObject post, int hole)
    {
        FrameOverlapResolver.TryBuildRecord(post.transform, out var rec);
        return rec.EndA.y + hole * NeospaceUnits.ModuleMeters;
    }

    bool Beam(int size, Vector3 axisA, Vector3 axisB, float y)
    {
        bool ok = _spawner.TryPlaceConnectorAtHeight(axisA, axisB, y, size, out string message);
        _report.AppendLine($"  beam H{size} at y={NeospaceUnits.ToMm(y):F1} mm: {(ok ? "placed" : "FAILED: " + message)}");
        return ok;
    }

    bool Connector(string partId, Vector3 a, Vector3 b)
    {
        bool ok = _build.TryPlaceConnectorSpan(partId, a, b, out string message);
        _report.AppendLine($"  connector {partId}: {(ok ? "placed" : "FAILED: " + message)}");
        return ok;
    }

    int Panels(Vector3 center, Vector3 normal, bool bothSides = true)
    {
        int placed = _spawner.PlacePanelsNear(center, normal, bothSides);
        _report.AppendLine($"  panels near ({NeospaceUnits.ToMm(center.x):F0},{NeospaceUnits.ToMm(center.y):F0},{NeospaceUnits.ToMm(center.z):F0}) mm normal {normal} ({(bothSides ? "both sides" : "one side")}): {placed}");
        return placed;
    }

    void Ring(List<GameObject> posts, int hole, int size, bool panel)
    {
        float y = RowY(posts[0], hole);
        for (int i = 0; i < 4; i++)
            Beam(size, AxisXZ(posts[i]), AxisXZ(posts[(i + 1) % 4]), y);
        if (panel)
        {
            Vector3 c = (AxisXZ(posts[0]) + AxisXZ(posts[1]) + AxisXZ(posts[2]) + AxisXZ(posts[3])) * 0.25f;
            c.y = y;
            Panels(c, Vector3.up);
        }
    }

    IEnumerator WallBay()
    {
        var posts = Posts("V9", 0f, Mm(0, 0, 0), Mm(704, 0, 0));
        if (posts.Count < 2) yield break;
        yield return null;
        Vector3 a = AxisXZ(posts[0]), b = AxisXZ(posts[1]);
        float y0 = RowY(posts[0], 0), y8 = RowY(posts[0], 8);
        Beam(7, a, b, y0);
        Beam(7, a, b, y8);
        yield return null;
        Vector3 c = (a + b) * 0.5f;
        c.y = (y0 + y8) * 0.5f;
        Panels(c, Vector3.forward);
    }

    IEnumerator WallBaySingleSide()
    {
        var posts = Posts("V9", 0f, Mm(0, 0, 0), Mm(704, 0, 0));
        if (posts.Count < 2) yield break;
        yield return null;
        Vector3 a = AxisXZ(posts[0]), b = AxisXZ(posts[1]);
        float y0 = RowY(posts[0], 0), y8 = RowY(posts[0], 8);
        Beam(7, a, b, y0);
        Beam(7, a, b, y8);
        yield return null;
        Vector3 c = (a + b) * 0.5f;
        c.y = (y0 + y8) * 0.5f;
        Panels(c, Vector3.forward, bothSides: false);
    }

    IEnumerator WallBayYaw()
    {
        const float yaw = 37f;
        Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * Vector3.right;
        var posts = Posts("V9", yaw, Vector3.zero, dir * 704f);
        if (posts.Count < 2) yield break;
        yield return null;
        Vector3 a = AxisXZ(posts[0]), b = AxisXZ(posts[1]);
        float y0 = RowY(posts[0], 0), y8 = RowY(posts[0], 8);
        Beam(7, a, b, y0);
        Beam(7, a, b, y8);
        yield return null;
        Vector3 c = (a + b) * 0.5f;
        c.y = (y0 + y8) * 0.5f;
        Vector3 span = b - a;
        Panels(c, new Vector3(-span.z, 0f, span.x).normalized);
    }

    IEnumerator ShelfRing()
    {
        var posts = Posts("V9", 0f, Mm(0, 0, 0), Mm(704, 0, 0), Mm(704, 0, 704), Mm(0, 0, 704));
        if (posts.Count < 4) yield break;
        yield return null;
        Ring(posts, 8, 7, panel: true);
        yield return null;
        Ring(posts, 4, 7, panel: true);
        yield return null;
    }

    IEnumerator Cabinet()
    {
        var posts = Posts("V17", 0f, Mm(0, 0, 0), Mm(704, 0, 0), Mm(704, 0, 704), Mm(0, 0, 704));
        if (posts.Count < 4) yield break;
        yield return null;
        Ring(posts, 0, 7, panel: true);
        yield return null;
        Ring(posts, 8, 7, panel: true);
        yield return null;
        Ring(posts, 16, 7, panel: true);
        yield return null;
        // Back wall between the two rear posts (z = 704 edge), rows 8..16.
        Vector3 c = (AxisXZ(posts[2]) + AxisXZ(posts[3])) * 0.5f;
        c.y = (RowY(posts[0], 8) + RowY(posts[0], 16)) * 0.5f;
        Panels(c, Vector3.forward);
        yield return null;
    }

    IEnumerator TwistBranch()
    {
        var posts = Posts("V9", 0f, Mm(0, 0, 0), Mm(704, 0, 0), Mm(352, 0, 704));
        if (posts.Count < 3) yield break;
        yield return null;
        Vector3 a = AxisXZ(posts[0]), b = AxisXZ(posts[1]), c = AxisXZ(posts[2]);
        float y4 = RowY(posts[0], 4);
        Beam(7, a, b, y4);
        yield return null;
        Vector3 mid = (a + b) * 0.5f;
        mid.y = y4;
        Vector3 far = c;
        far.y = y4;
        Connector("T7", mid, far);
        yield return null;
    }

    // ------------------------------------------------------------------
    // Measurement / report
    // ------------------------------------------------------------------

    struct Obb
    {
        public Vector3 Center;
        public Vector3[] Axes;
        public float[] Half;
        public string Label;
    }

    void Report(string name, bool spaceMode)
    {
        int ghostMask = _build.ghostLayerMask.value;
        List<FrameOverlapResolver.FrameRecord> frames = spaceMode ? FinishGenerator.CollectSpaceFrames() : FrameOverlapResolver.CollectFrames(ghostMask);
        List<FinishGenerator.PanelBox> panels = spaceMode ? FinishGenerator.CollectSpacePanels() : FinishGenerator.CollectPanels(ghostMask);
        var seen = new HashSet<Transform>();
        frames.RemoveAll(f => f.Root == null || !f.Root.gameObject.activeInHierarchy || !seen.Add(f.Root));

        FinishGenerator.Result plan = FinishGenerator.Plan(frames, panels, _dims);
        var installed = new Dictionary<string, int>();
        var installedRoots = new List<Transform>();
        GameObject finishRoot = GameObject.Find("FinishRoot");
        if (finishRoot != null)
            foreach (Transform part in finishRoot.transform)
            {
                if (!part.gameObject.activeInHierarchy) continue;
                installedRoots.Add(part);
                installed.TryGetValue(part.name, out int n);
                installed[part.name] = n + 1;
            }

        _report.AppendLine($"Frames: {frames.Count}, panels: {panels.Count}, planned parts: {plan.Parts.Count}, installed roots: {installedRoots.Count}");
        var planned = new SortedDictionary<string, int>();
        foreach (var p in plan.Parts) { planned.TryGetValue(p.Model, out int n); planned[p.Model] = n + 1; }
        var line = new StringBuilder("  planned:");
        foreach (var kv in planned) line.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
        _report.AppendLine(line.ToString());
        line = new StringBuilder("  installed:");
        foreach (var kv in new SortedDictionary<string, int>(installed)) line.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
        _report.AppendLine(line.ToString());
        foreach (string w in plan.Warnings) _report.AppendLine("  WARNING: " + w);

        _report.AppendLine("Panels:");
        foreach (var p in panels)
            _report.AppendLine($"  centre {MmStr(p.Center)} normal {Dir(p.Normal)} halfU {NeospaceUnits.ToMm(p.HalfU):F2} halfV {NeospaceUnits.ToMm(p.HalfV):F2} halfN {NeospaceUnits.ToMm(p.HalfN):F3}");
        if (!spaceMode)
            foreach (PanelInstance pi in FindObjectsByType<PanelInstance>(FindObjectsSortMode.None))
                if (pi != null && pi.gameObject.activeInHierarchy)
                    _report.AppendLine($"  panel object '{pi.name}' slot {pi.slotId} side {pi.side} at {MmStr(pi.transform.position)} scale ({NeospaceUnits.ToMm(pi.transform.localScale.x):F1} x {NeospaceUnits.ToMm(pi.transform.localScale.y):F1} x {NeospaceUnits.ToMm(pi.transform.localScale.z):F2}) mm");

        var bodies = new List<Obb>();
        for (int fi = 0; fi < frames.Count; fi++)
        {
            FrameOverlapResolver.FrameRecord f = frames[fi];
            bool vertical = BeamPartUtility.IsVertical(f.PartId);
            float halfBodyMm = (vertical ? Skeleton.VBodyLength(f.Size) : Skeleton.HBodyLength(f.Size)) * 0.5f;
            Vector3[] normals = FaceNormals(f);
            bodies.Add(new Obb
            {
                Center = f.Center,
                Axes = new[] { f.LengthAxis, normals[0], normals[2] },
                Half = new[] { NeospaceUnits.Mm(halfBodyMm), NeospaceUnits.Mm(CatalogueData.HalfProfileMm), NeospaceUnits.Mm(CatalogueData.HalfProfileMm) },
                Label = $"{f.PartId}#{fi}"
            });

            _report.AppendLine($"{f.PartId}#{fi} centre {MmStr(f.Center)} axis {Dir(f.LengthAxis)} body ±{halfBodyMm:F1} mm ends A {MmStr(f.EndA)} B {MmStr(f.EndB)}");
            if (vertical)
            {
                bool capTop = HasPart(plan, "Cap End", f.Center + f.LengthAxis * NeospaceUnits.Mm(halfBodyMm), f.LengthAxis);
                bool capBottom = HasPart(plan, "Cap End", f.Center - f.LengthAxis * NeospaceUnits.Mm(halfBodyMm), -f.LengthAxis);
                bool foot = HasPart(plan, "Foot", f.Center - f.LengthAxis * NeospaceUnits.Mm(halfBodyMm), f.LengthAxis);
                _report.AppendLine($"  ends: top Cap End={capTop}, bottom Cap End={capBottom}, Foot={foot}");
            }

            for (int face = 0; face < 4; face++)
            {
                Vector3 n = normals[face];
                var intervals = new List<(float lo, float hi, string model)>();
                foreach (var part in plan.Parts)
                {
                    if (part.Model == "Cap End" || part.Model == "Foot") continue;
                    if (Vector3.Dot(part.Normal, n) < 0.999f || Mathf.Abs(Vector3.Dot(part.LengthDir, f.LengthAxis)) < 0.999f) continue;
                    Vector3 d = part.Center - f.Center;
                    float along = Vector3.Dot(d, f.LengthAxis);
                    Vector3 lateral = d - f.LengthAxis * along;
                    // On this face: lateral offset equals the half profile along n, nothing across.
                    if (Mathf.Abs(Vector3.Dot(lateral, n) - NeospaceUnits.Mm(CatalogueData.HalfProfileMm)) > NeospaceUnits.Mm(0.5f)) continue;
                    if ((lateral - n * Vector3.Dot(lateral, n)).magnitude > NeospaceUnits.Mm(0.5f)) continue;
                    float halfLen = HalfLengthMm(part.Model);
                    float c = NeospaceUnits.ToMm(along);
                    intervals.Add((c - halfLen, c + halfLen, part.Model));
                }
                intervals.Sort((l, r) => l.lo.CompareTo(r.lo));
                var parts = new StringBuilder();
                foreach (var iv in intervals) parts.Append($" {iv.model}[{iv.lo:F1},{iv.hi:F1}]");
                _report.AppendLine($"  face {face} {Dir(n)}:{(intervals.Count == 0 ? " (none)" : parts.ToString())}");

                // Gaps inside the body extent.
                float cursor = -halfBodyMm;
                var gaps = new List<(float lo, float hi)>();
                foreach (var iv in intervals)
                {
                    if (iv.lo > cursor + 0.6f) gaps.Add((cursor, iv.lo));
                    cursor = Mathf.Max(cursor, iv.hi);
                }
                if (halfBodyMm > cursor + 0.6f) gaps.Add((cursor, halfBodyMm));
                foreach (var g in gaps)
                {
                    float midMm = (g.lo + g.hi) * 0.5f;
                    Vector3 probe = f.Center + f.LengthAxis * NeospaceUnits.Mm(midMm) + n * NeospaceUnits.Mm(CatalogueData.HalfProfileMm + 8f);
                    string why = PanelAt(panels, probe) ? "behind panel" : JointAt(frames, f, n, midMm) ? "joint/connection" : "EXPOSED GAP";
                    if (why == "EXPOSED GAP" && g.hi - g.lo > 5f) _exposedGaps++;
                    _report.AppendLine($"    gap [{g.lo:F1},{g.hi:F1}] ({g.hi - g.lo:F1} mm): {why}");
                }
            }
        }

        // Physical penetration of installed dressing into frame bodies and panels.
        var panelObbs = new List<Obb>();
        for (int i = 0; i < panels.Count; i++)
            panelObbs.Add(new Obb
            {
                Center = panels[i].Center,
                Axes = new[] { panels[i].AxisU, panels[i].AxisV, panels[i].Normal },
                Half = new[] { panels[i].HalfU, panels[i].HalfV, panels[i].HalfN },
                Label = "panel#" + i
            });
        int penetrations = 0, seams = 0;
        foreach (Transform part in installedRoots)
        {
            foreach (MeshFilter filter in part.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                Bounds b = filter.sharedMesh.bounds;
                Transform t = filter.transform;
                Vector3 s = t.lossyScale;
                var obb = new Obb
                {
                    Center = t.TransformPoint(b.center),
                    Axes = new[] { t.right, t.up, t.forward },
                    Half = new[] { Mathf.Abs(b.extents.x * s.x), Mathf.Abs(b.extents.y * s.y), Mathf.Abs(b.extents.z * s.z) },
                    Label = part.name
                };
                foreach (Obb body in bodies)
                {
                    float depth = NeospaceUnits.ToMm(Penetration(obb, body));
                    if (depth > 0.05f)
                    {
                        penetrations++;
                        _report.AppendLine($"  PENETRATION {part.name} at {MmStr(obb.Center)} into {body.Label}: {depth:F3} mm");
                    }
                }
                if (part.name == "Foot") continue;
                foreach (Obb panel in panelObbs)
                {
                    float depth = NeospaceUnits.ToMm(Penetration(obb, panel));
                    if (depth > 0.6f)
                    {
                        penetrations++;
                        _report.AppendLine($"  PENETRATION {part.name} at {MmStr(obb.Center)} into {panel.Label}: {depth:F3} mm");
                    }
                    else if (depth > 0.05f) seams++;
                }
            }
        }
        _report.AppendLine($"Penetrations: {penetrations} (lip seams under 0.6 mm against panels: {seams})");
        _report.AppendLine($"Exposed gaps over 5 mm: {_exposedGaps}");
        _exposedGaps = 0;
    }

    int _exposedGaps;

    static bool HasPart(FinishGenerator.Result plan, string model, Vector3 at, Vector3 normal)
    {
        foreach (var p in plan.Parts)
            if (p.Model == model && (p.Center - at).magnitude < NeospaceUnits.Mm(2f) && Vector3.Dot(p.Normal, normal) > 0.99f)
                return true;
        return false;
    }

    static float HalfLengthMm(string model)
    {
        if (model == "Cap Side") return 21.15f;
        if (model.StartsWith("Veneer H", StringComparison.Ordinal) && int.TryParse(model.Substring(8), out int size))
            return (Skeleton.VeneerContactLength(size) - 2.08575f) * 0.5f;
        return 0f;
    }

    static Vector3[] FaceNormals(FrameOverlapResolver.FrameRecord frame)
    {
        Vector3 axis = frame.LengthAxis;
        Vector3 a;
        if (BeamPartUtility.IsVertical(frame.PartId))
            a = frame.Root != null ? frame.Root.right : Vector3.right;
        else
            a = frame.LocalY;
        a -= axis * Vector3.Dot(a, axis);
        if (a.sqrMagnitude < 1e-6f) a = BeamPartUtility.IsVertical(frame.PartId) ? Vector3.right : Vector3.up;
        a.Normalize();
        Vector3 b = Vector3.Cross(axis, a).normalized;
        return new[] { a, -a, b, -b };
    }

    static bool PanelAt(List<FinishGenerator.PanelBox> panels, Vector3 point)
    {
        float tol = NeospaceUnits.Mm(1.5f);
        foreach (var p in panels)
        {
            Vector3 d = point - p.Center;
            if (Mathf.Abs(Vector3.Dot(d, p.AxisU)) <= p.HalfU + tol &&
                Mathf.Abs(Vector3.Dot(d, p.AxisV)) <= p.HalfV + tol &&
                Mathf.Abs(Vector3.Dot(d, p.Normal)) <= p.HalfN + NeospaceUnits.Mm(12f))
                return true;
        }
        return false;
    }

    static bool JointAt(List<FrameOverlapResolver.FrameRecord> frames, FrameOverlapResolver.FrameRecord self, Vector3 faceNormal, float offsetMm)
    {
        Vector3 at = self.Center + self.LengthAxis * NeospaceUnits.Mm(offsetMm);
        float tol = NeospaceUnits.Mm(46f);
        foreach (var other in frames)
        {
            if (other.Root == self.Root) continue;
            foreach (Vector3 end in new[] { other.EndA, other.EndB })
            {
                Vector3 d = end - at;
                if (d.magnitude < tol && Vector3.Dot(d.normalized, faceNormal) > 0.5f) return true;
            }
            // A post standing on / under this beam, or a beam body crossing this face.
            Vector3 toOther = other.Center - at;
            float alongOther = Vector3.Dot(toOther, other.LengthAxis);
            Vector3 lateral = toOther - other.LengthAxis * alongOther;
            float halfBody = NeospaceUnits.Mm((BeamPartUtility.IsVertical(other.PartId) ? Skeleton.VBodyLength(other.Size) : Skeleton.HBodyLength(other.Size)) * 0.5f);
            if (Mathf.Abs(alongOther) <= halfBody + NeospaceUnits.Mm(2f) && lateral.magnitude < NeospaceUnits.Mm(46f) && Vector3.Dot(lateral.normalized, faceNormal) > 0.5f)
                return true;
        }
        return false;
    }

    /// <summary>Minimum overlap across the 15 separating axes; ≤ 0 when apart.</summary>
    static float Penetration(Obb a, Obb b)
    {
        float min = float.PositiveInfinity;
        Vector3 d = b.Center - a.Center;
        var axes = new List<Vector3>();
        axes.AddRange(a.Axes);
        axes.AddRange(b.Axes);
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
            {
                Vector3 c = Vector3.Cross(a.Axes[i], b.Axes[j]);
                if (c.sqrMagnitude > 1e-8f) axes.Add(c.normalized);
            }
        foreach (Vector3 axis in axes)
        {
            float ra = 0f, rb = 0f;
            for (int i = 0; i < 3; i++)
            {
                ra += a.Half[i] * Mathf.Abs(Vector3.Dot(a.Axes[i], axis));
                rb += b.Half[i] * Mathf.Abs(Vector3.Dot(b.Axes[i], axis));
            }
            float overlap = ra + rb - Mathf.Abs(Vector3.Dot(d, axis));
            if (overlap <= 0f) return overlap;
            min = Mathf.Min(min, overlap);
        }
        return min;
    }

    static string MmStr(Vector3 v) => $"({NeospaceUnits.ToMm(v.x):F1},{NeospaceUnits.ToMm(v.y):F1},{NeospaceUnits.ToMm(v.z):F1})";
    static string Dir(Vector3 v) => $"({v.x:F2},{v.y:F2},{v.z:F2})";

    // ------------------------------------------------------------------
    // Rendering evidence
    // ------------------------------------------------------------------

    void Capture(string name, bool spaceMode, GameObject extra, bool sides = false)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            _report.AppendLine("Renders skipped: null graphics device.");
            return;
        }
        var subjects = new List<GameObject>();
        if (extra != null) subjects.Add(extra);
        foreach (Transform root in LiveFrameRoots()) subjects.Add(root.gameObject);
        foreach (PanelInstance pi in FindObjectsByType<PanelInstance>(FindObjectsSortMode.None))
            if (pi != null && pi.gameObject.activeInHierarchy) subjects.Add(pi.gameObject);
        GameObject finishRoot = GameObject.Find("FinishRoot");
        if (finishRoot != null) subjects.Add(finishRoot);
        if (spaceMode && SpaceMerge.DerivedRoot != null) subjects.Add(SpaceMerge.DerivedRoot.gameObject);

        Bounds bounds = default;
        bool any = false;
        foreach (GameObject subject in subjects)
            foreach (Renderer r in subject.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            }
        if (!any) { _report.AppendLine("Renders skipped: nothing to render."); return; }

        float extent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        float wide = Mathf.Max(0.5f, extent * 1.15f);
        RenderView($"{name}-iso.png", subjects, bounds.center, wide, new Vector3(1.4f, 1.0f, 1.8f), Vector3.up);
        RenderView($"{name}-iso-under.png", subjects, bounds.center, wide, new Vector3(-1.4f, -0.8f, -1.8f), Vector3.up);
        RenderView($"{name}-front.png", subjects, bounds.center, wide, new Vector3(0f, 0f, -1f), Vector3.up);
        RenderView($"{name}-top.png", subjects, bounds.center, wide, Vector3.up, Vector3.forward);

        int ghostMask = _build.ghostLayerMask.value;
        List<FrameOverlapResolver.FrameRecord> frames = spaceMode ? FinishGenerator.CollectSpaceFrames() : FrameOverlapResolver.CollectFrames(ghostMask);
        List<FinishGenerator.PanelBox> panels = spaceMode ? FinishGenerator.CollectSpacePanels() : FinishGenerator.CollectPanels(ghostMask);

        if (sides)
        {
            // Four low elevations, plus close-ups of the lowest beam on each
            // side seen from outside (where a bare bottom channel would show).
            RenderView($"{name}-side-south.png", subjects, bounds.center, wide, new Vector3(0f, 0.35f, -1f), Vector3.up);
            RenderView($"{name}-side-north.png", subjects, bounds.center, wide, new Vector3(0f, 0.35f, 1f), Vector3.up);
            RenderView($"{name}-side-east.png", subjects, bounds.center, wide, new Vector3(1f, 0.35f, 0f), Vector3.up);
            RenderView($"{name}-side-west.png", subjects, bounds.center, wide, new Vector3(-1f, 0.35f, 0f), Vector3.up);
            int shot = 0;
            foreach (var f in frames)
            {
                if (BeamPartUtility.IsVertical(f.PartId)) continue;
                if (f.Center.y > bounds.min.y + NeospaceUnits.Mm(120f)) continue; // lowest beams only
                Vector3 outward = f.Center - bounds.center;
                outward.y = 0f;
                outward -= f.LengthAxis * Vector3.Dot(outward, f.LengthAxis);
                if (outward.sqrMagnitude < 1e-6f) continue;
                outward.Normalize();
                RenderView($"{name}-low-beam-{shot}.png", subjects, f.Center, NeospaceUnits.Mm(220f),
                    (outward * 1.5f + Vector3.up * 0.45f + f.LengthAxis * 0.4f).normalized, Vector3.up);
                shot++;
            }
        }

        // Close-ups at one post: its top corner and its mid-height bay junction,
        // seen from the panelled side and from the opposite side.
        int postIndex = -1;
        float best = float.PositiveInfinity;
        for (int i = 0; i < frames.Count; i++)
        {
            if (!BeamPartUtility.IsVertical(frames[i].PartId)) continue;
            float score = panels.Count > 0 ? (panels[0].Center - frames[i].Center).sqrMagnitude : i;
            if (score < best) { best = score; postIndex = i; }
        }
        if (postIndex < 0) return;
        FrameOverlapResolver.FrameRecord post = frames[postIndex];
        Vector3 n = new Vector3(1.4f, 0f, 1.8f).normalized, t = Vector3.zero;
        if (panels.Count > 0)
        {
            n = panels[0].Normal;
            t = panels[0].Center - post.Center;
            t -= Vector3.up * t.y;
            t -= n * Vector3.Dot(t, n);
            t.Normalize();
        }
        Vector3 front = (n * 1.6f + t * 1.0f + Vector3.up * 0.35f).normalized;
        Vector3 back = (-n * 1.6f + t * 1.0f + Vector3.up * 0.35f).normalized;
        float close = NeospaceUnits.Mm(130f);
        Vector3 top = post.Center + post.LengthAxis * NeospaceUnits.Mm(Skeleton.VBodyLength(post.Size) * 0.5f);
        RenderView($"{name}-corner-top.png", subjects, top, close, (front + Vector3.up * 0.6f).normalized, Vector3.up);
        RenderView($"{name}-corner-under.png", subjects, top, close, (front - Vector3.up * 1.0f).normalized, Vector3.up);
        RenderView($"{name}-seam-front.png", subjects, post.Center, close, front, Vector3.up);
        RenderView($"{name}-seam-back.png", subjects, post.Center, close, back, Vector3.up);
    }

    void RenderView(string fileName, List<GameObject> subjects, Vector3 focus, float orthoSize, Vector3 direction, Vector3 up)
    {
        const int width = 1400, height = 1050, captureLayer = 31;
        var changed = new Dictionary<GameObject, int>();
        GameObject cameraObject = null;
        RenderTexture target = null;
        Texture2D image = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            foreach (GameObject subject in subjects)
                foreach (Transform t in subject.GetComponentsInChildren<Transform>(true))
                    if (!changed.ContainsKey(t.gameObject))
                    {
                        changed.Add(t.gameObject, t.gameObject.layer);
                        t.gameObject.layer = captureLayer;
                    }
            cameraObject = new GameObject("FinishDiagnosticsCamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.cullingMask = 1 << captureLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.17f, 0.19f);
            camera.orthographic = true;
            camera.aspect = width / (float)height;
            camera.orthographicSize = orthoSize;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 1000f;
            camera.transform.position = focus + direction.normalized * 60f;
            camera.transform.LookAt(focus, up);
            target = new RenderTexture(width, height, 24);
            target.Create();
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(OutputDirectory, fileName), image.EncodeToPNG());
            _report.AppendLine("  render: " + fileName);
        }
        catch (Exception e)
        {
            _report.AppendLine("  render failed: " + fileName + ": " + e.Message);
        }
        finally
        {
            RenderTexture.active = previous;
            foreach (var entry in changed) if (entry.Key != null) entry.Key.layer = entry.Value;
            if (cameraObject != null)
            {
                cameraObject.GetComponent<Camera>().targetTexture = null;
                Destroy(cameraObject);
            }
            if (target != null) { target.Release(); Destroy(target); }
            if (image != null) Destroy(image);
        }
    }
}
#endif
