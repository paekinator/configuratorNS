using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Snapshot-based undo / redo / clear for the whole build (beams AND panels).
///
/// Every mutating tool calls <see cref="NotifyChanged"/> after it finishes; the
/// capture itself is deferred to the NEXT frame so that (a) several changes made
/// by one action (a template batch plus its panels, a layer move, a clipboard
/// stamp) collapse into a single history step, and (b) objects removed with
/// Destroy() — which only vanish at end of frame — are really gone before the
/// scene is read.
///
/// Undo / redo restore a snapshot by wiping all parts and replaying beams
/// through the regular batch-placement pipeline, then re-placing panels onto
/// the slots the rebuilt beams form. Shortcuts: Ctrl/Cmd+Z undo,
/// Ctrl/Cmd+Shift+Z or Ctrl/Cmd+Y redo.
/// </summary>
public class BuildHistory : MonoBehaviour
{
    public BuildController buildController;
    public PanelSlotManager panelSlotManager;

    [Tooltip("Maximum stored history steps; the oldest are dropped beyond this.")]
    public int maxSteps = 100;

    struct BeamState
    {
        public string PartId;
        public Vector3 Pos;
        public Quaternion Rot;
    }

    struct PanelState
    {
        public Vector3 Center;
        public Vector3 Normal;
        public int Side;
    }

    class Snapshot
    {
        public readonly List<BeamState> Beams = new List<BeamState>();
        public readonly List<PanelState> Panels = new List<PanelState>();
        public bool FinishApplied;
        public string Signature;
        public bool IsEmpty => Beams.Count == 0 && Panels.Count == 0;
    }

    readonly List<Snapshot> _timeline = new List<Snapshot>();
    int _index = -1;
    int _captureAtFrame = -1;
    bool _restoring;
    bool _externalRestore;

    public static BuildHistory Instance { get; private set; }
    public bool IsRestoring => _restoring;

    /// <summary>
    /// While true (Space Mode), the piece-build history is fully dormant: no
    /// captures, no keyboard shortcuts, and the rail's buttons no-op — Space
    /// Mode routes those to its own history instead.
    /// </summary>
    public static bool Suspended;

    void OnEnable() { Instance = this; }

    void OnDisable()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        // Baseline so the very first placement can be undone.
        CaptureNow();
    }

    /// <summary>
    /// Raised whenever a mutating tool reports a change — the cheap signal
    /// change-driven systems (slot rescans, price readout, dimensions,
    /// finish) listen to instead of polling the scene. Fires even while a
    /// restore or Space Mode suppresses history capture: the STRUCTURE still
    /// changed, only the undo step is skipped.
    /// </summary>
    public static event System.Action Changed;

    /// <summary>Call after any change to placed beams or panels.</summary>
    public static void NotifyChanged()
    {
        Changed?.Invoke();
        if (Instance == null || Instance._restoring || Suspended)
            return;
        Instance._captureAtFrame = Time.frameCount + 1;
    }

    void LateUpdate()
    {
        if (Suspended)
            return;

        if (_captureAtFrame >= 0 && Time.frameCount >= _captureAtFrame && !_restoring)
        {
            _captureAtFrame = -1;
            CaptureNow();
        }

        HandleShortcuts();
    }

    void HandleShortcuts()
    {
        // Text editing owns Ctrl/Cmd+Z while a name, code or dimension field
        // is focused; typing must never undo the structure behind a dialog.
        GameObject selected = UnityEngine.EventSystems.EventSystem.current != null
            ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
        if (selected != null && selected.activeInHierarchy && (selected.GetComponentInParent<TMPro.TMP_InputField>() != null ||
            selected.GetComponentInParent<UnityEngine.UI.InputField>() != null))
            return;
        bool mod = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                   Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
        if (!mod)
            return;

        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (Input.GetKeyDown(KeyCode.Z))
        {
            if (shift) Redo();
            else Undo();
        }
        else if (Input.GetKeyDown(KeyCode.Y))
        {
            Redo();
        }
    }

    // ------------------------------------------------------------------
    // Capture
    // ------------------------------------------------------------------

    void CaptureNow()
    {
        Snapshot snap = ReadScene();

        // Skip no-op captures (failed placements, redundant notifications).
        if (_index >= 0 && _index < _timeline.Count &&
            _timeline[_index].Signature == snap.Signature)
            return;

        // A new action invalidates any redo tail.
        if (_index < _timeline.Count - 1)
            _timeline.RemoveRange(_index + 1, _timeline.Count - _index - 1);

        _timeline.Add(snap);
        _index = _timeline.Count - 1;

        while (_timeline.Count > Mathf.Max(2, maxSteps))
        {
            _timeline.RemoveAt(0);
            _index--;
        }
    }

    Snapshot ReadScene()
    {
        var snap = new Snapshot();
        snap.FinishApplied = FinishController.Instance != null && FinishController.Instance.IsOn;
        int ghostMask = buildController != null ? buildController.ghostLayerMask.value : 0;

        foreach (BeamConnections conn in FindObjectsByType<BeamConnections>(FindObjectsSortMode.None))
        {
            if (conn == null)
                continue;
            Transform root = conn.transform.root;
            if ((ghostMask & (1 << root.gameObject.layer)) != 0)
                continue;
            string id = StructureClipboard.CleanPartId(root.name);
            if (id == null)
                continue;

            snap.Beams.Add(new BeamState { PartId = id, Pos = root.position, Rot = root.rotation });
        }

        foreach (PanelInstance pi in FindObjectsByType<PanelInstance>(FindObjectsSortMode.None))
        {
            if (pi == null)
                continue;
            if ((ghostMask & (1 << pi.gameObject.layer)) != 0)
                continue;

            snap.Panels.Add(new PanelState
            {
                Center = pi.transform.position,
                Normal = pi.transform.forward,
                Side = pi.side >= 0 ? 1 : -1
            });
        }

        snap.Signature = BuildSignature(snap);
        return snap;
    }

    static string BuildSignature(Snapshot snap)
    {
        var lines = new List<string>(snap.Beams.Count + snap.Panels.Count);

        foreach (BeamState b in snap.Beams)
        {
            lines.Add($"B|{b.PartId}|{Key(b.Pos)}|" +
                      $"{Mathf.RoundToInt(b.Rot.eulerAngles.x)},{Mathf.RoundToInt(b.Rot.eulerAngles.y)},{Mathf.RoundToInt(b.Rot.eulerAngles.z)}");
        }
        foreach (PanelState p in snap.Panels)
            lines.Add($"P|{Key(p.Center)}|{Key(p.Normal * 10f)}|{p.Side}");

        lines.Sort(System.StringComparer.Ordinal);

        var sb = new StringBuilder(lines.Count * 32);
        sb.AppendLine(snap.FinishApplied ? "F|1" : "F|0");
        foreach (string line in lines)
            sb.AppendLine(line);
        return sb.ToString();

        static string Key(Vector3 v) =>
            $"{Mathf.RoundToInt(v.x * 500f)},{Mathf.RoundToInt(v.y * 500f)},{Mathf.RoundToInt(v.z * 500f)}";
    }

    // ------------------------------------------------------------------
    // Undo / redo / clear
    // ------------------------------------------------------------------

    public bool CanUndo => _index > 0;
    public bool CanRedo => _index >= 0 && _index < _timeline.Count - 1;

    public void Undo()
    {
        if (_restoring || Suspended)
            return;
        StartCoroutine(StepRoutine(undo: true));
    }

    public void Redo()
    {
        if (_restoring || Suspended)
            return;
        StartCoroutine(StepRoutine(undo: false));
    }

    IEnumerator StepRoutine(bool undo)
    {
        _restoring = true;
        // A button/shortcut may run before the next-frame capture of the
        // previous action. Settle that action (including deferred Destroy)
        // before selecting a history step, otherwise Undo can skip it.
        if (_captureAtFrame >= 0)
        {
            yield return null;
            CaptureNow();
            _captureAtFrame = -1;
        }

        if (undo ? !CanUndo : !CanRedo)
        {
            _restoring = false;
            SelectionStatus.Set(undo ? "Nothing to undo." : "Nothing to redo.", 2f);
            yield break;
        }

        _index += undo ? -1 : 1;
        string message = undo
            ? $"Undo · {_timeline.Count - 1 - _index} redo step{(_timeline.Count - 1 - _index == 1 ? "" : "s")} available."
            : "Redo.";
        yield return RestoreRoutine(_timeline[_index], message);
    }

    /// <summary>Remove every placed beam and panel. Undoable like any action.</summary>
    public void ClearAll()
    {
        if (_restoring || Suspended)
            return;

        StartCoroutine(ClearRoutine());
    }

    IEnumerator ClearRoutine()
    {
        _restoring = true;
        if (_captureAtFrame >= 0)
        {
            yield return null;
            CaptureNow();
            _captureAtFrame = -1;
        }

        Snapshot current = ReadScene();
        if (current.IsEmpty)
        {
            _restoring = false;
            SelectionStatus.Set("The grid is already empty.", 2f);
            yield break;
        }

        int total = current.Beams.Count + current.Panels.Count;

        // Finish dressing follows the structure automatically: clearing all
        // parts triggers FinishController's rebuild, which dresses nothing.
        DestroyAllParts();
        yield return null;

        if (panelSlotManager != null)
            panelSlotManager.RebuildConnectionsAndRescanSlots();

        // Empty restores do not pass through placement code. Announce their
        // completed scene too, while still suppressing a new history step.
        NotifyChanged();
        _restoring = false;
        _captureAtFrame = -1;
        CaptureNow();
        SelectionStatus.Set($"Cleared {total} parts · Ctrl+Z (Cmd+Z) to undo.", 5f);
    }

    IEnumerator RestoreRoutine(Snapshot snap, string doneMessage)
    {
        _restoring = true;

        DestroyAllParts();
        yield return null;

        if (buildController != null && snap.Beams.Count > 0)
        {
            var poses = new List<TemplatePartPose>(snap.Beams.Count);
            foreach (BeamState b in snap.Beams)
                poses.Add(new TemplatePartPose(b.PartId, b.Pos, b.Rot, "history"));

            // Restore exact stored poses: no floor re-seating, and no overlap
            // re-validation — the snapshot is a scene state that already passed
            // validation when it was built (re-checking would wrongly reject
            // beams plugged into their hosts).
            buildController.PlacePartsBatch(poses, seatVerticalsOnFloor: false, validateOverlap: false);
        }
        else if (panelSlotManager != null)
        {
            panelSlotManager.RebuildConnectionsAndRescanSlots();
        }

        int panelsBack = 0;
        if (panelSlotManager != null)
        {
            foreach (PanelState p in snap.Panels)
            {
                PanelSlotHandle slot = StructureClipboard.FindSlotNear(p.Center, p.Normal);
                if (slot == null)
                    continue;

                int side = Vector3.Dot(slot.normal, p.Normal) >= 0f ? p.Side : -p.Side;
                if (!panelSlotManager.CanPlacePanel(slot, side))
                    continue;
                if (panelSlotManager.PlacePanel(slot, side) != null)
                    panelsBack++;
            }
        }

        yield return null;
        if (FinishController.Instance != null || snap.FinishApplied)
            FinishController.Ensure().SetOn(snap.FinishApplied, announce: false);
        NotifyChanged();
        _restoring = false;
        _captureAtFrame = -1;

        SelectionStatus.Set(doneMessage, 3f);
    }

    /// <summary>Reserve history while an external load replays several frames.</summary>
    public bool TryBeginExternalRestore()
    {
        if (_restoring || Suspended)
            return false;
        _restoring = true;
        _externalRestore = true;
        return true;
    }

    /// <summary>
    /// Call after yielding one frame, before the external loader mutates parts.
    /// This preserves an action whose deferred capture was still pending.
    /// </summary>
    public void CaptureBeforeExternalRestore()
    {
        if (!_externalRestore)
            return;
        CaptureNow();
        _captureAtFrame = -1;
    }

    /// <summary>Call once the complete loaded (or rolled-back) scene is stable.</summary>
    public void EndExternalRestore(bool recordChange)
    {
        if (!_externalRestore)
            return;
        NotifyChanged();
        _captureAtFrame = -1;
        if (recordChange)
            CaptureNow();
        _externalRestore = false;
        _restoring = false;
    }

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

        // The panels were destroyed directly (not via RemovePanel), so clear
        // the slot bookkeeping and switch bay blockers off — a stale active
        // blocker would collide with every beam replayed around its bay.
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
