using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// All mouse/keyboard work inside Space Mode.
///
/// Placing: pick a piece in the left panel → a translucent ghost follows the
/// cursor on the floor (88 mm grid snap, R rotates 90°) → every click stamps
/// one instance; Esc or right-click puts the tool down.
///
/// Arranging: click a piece to select it (Shift+click adds/removes, clicking
/// empty floor clears), drag a selected piece to move the whole selection on
/// the grid. A small action card near the selection offers Duplicate,
/// Rotate, Remove. Delete/Backspace removes, R rotates around the
/// selection's centre. Every completed action records one undo step.
/// </summary>
public class SpaceInteractionController : MonoBehaviour
{
    public BuildController buildController;
    public PieceInstanceFactory factory;
    public SpaceHistory history;
    public SpaceEditSession editSession;

    // Theme tokens, read live so Space Mode chrome follows the palette.
    static Color Accent => UIThemeController.AccentColor;
    static Color Ink => UIThemeController.InkColor;
    static Color Surface => UIThemeController.CardColor;
    static Color Danger => UIThemeController.DangerColor;

    readonly List<SpaceInstance> _instances = new List<SpaceInstance>();
    readonly List<SpaceInstance> _selected = new List<SpaceInstance>();

    public IReadOnlyList<SpaceInstance> Instances => _instances;
    public IReadOnlyList<SpaceInstance> Selected => _selected;

    /// <summary>
    /// True while pieces are physically mid-move (a drag in progress) —
    /// consumers like the Finish auto-regeneration hold their work until the
    /// merge has landed. An ARMED placement ghost does not count: the tool
    /// stays armed between placements ("click to place another"), and each
    /// committed placement must be dressed immediately.
    /// </summary>
    public bool Busy => _dragging;
    public int InstanceCount => _instances.Count;
    public string ArmedPieceId => _armedRecord != null ? _armedRecord.id : null;
    public event System.Action ArmedChanged;

    // Placement
    PieceLibrary.PieceRecord _armedRecord;
    GameObject _armedMaster;
    GameObject _ghost;
    float _ghostYaw;
    Vector3 _armedSize;
    Vector3 _armedCenter;
    bool _ghostBlocked;
    string _ghostBlockReason;
    Vector3 _lastGhostPos;
    float _lastGhostYaw;
    bool _ghostChecked;

    // Group highlight: pieces merged into one structure light up together —
    // persistently while one of them is selected, briefly after a snap.
    readonly HashSet<SpaceInstance> _hintSet = new HashSet<SpaceInstance>();
    readonly List<SpaceInstance> _flashGroup = new List<SpaceInstance>();
    float _flashUntil;

    // Drag-move
    SpaceInstance _pressInstance;
    bool _pressWithShift;
    bool _dragging;
    Vector3 _grabPoint;
    readonly List<Vector3> _dragStartPositions = new List<Vector3>();
    readonly List<int> _dragStartGroups = new List<int>();
    Vector3 _lastDragDelta;

    /// <summary>
    /// Order-independent fingerprint of the merged group an instance belongs
    /// to (0 = alone). Compared before/after an action so the snap flash only
    /// fires when group membership actually CHANGES — not when an existing
    /// group is merely moved around.
    /// </summary>
    static int GroupFingerprint(SpaceInstance inst)
    {
        var group = SpaceMerge.GroupOf(inst);
        if (group == null)
            return 0;
        int h = 0;
        foreach (SpaceInstance member in group)
            if (member != null)
                h ^= member.GetHashCode();
        return h;
    }

    // Action card
    RectTransform _card;
    TextMeshProUGUI _cardLabel;
    GameObject _editButton;
    Canvas _canvas;
    Sprite _cardSprite;
    float _cardPpu = 1f;
    TMP_FontAsset _font;

    Camera Cam => buildController != null && buildController.cam != null
        ? buildController.cam : Camera.main;

    void Update()
    {
        if (!SpaceModeController.Active)
        {
            HideCard();
            return;
        }

        HandleKeys();

        if (_flashUntil > 0f && Time.unscaledTime >= _flashUntil)
        {
            _flashUntil = 0f;
            _flashGroup.Clear();
            RefreshGroupHints();
        }

        if (_ghost != null)
            UpdatePlacement();
        else
            UpdateSelection();

        UpdateCard();
    }

    // ------------------------------------------------------------------
    // Group highlight
    // ------------------------------------------------------------------

    /// <summary>
    /// Recompute which pads glow as "grouped": every member of a merged
    /// group containing a selected piece, plus any group still flashing
    /// after a snap. Selected pieces keep their stronger selection pad.
    /// </summary>
    void RefreshGroupHints()
    {
        _hintSet.Clear();
        foreach (SpaceInstance sel in _selected)
        {
            var group = SpaceMerge.GroupOf(sel);
            if (group == null)
                continue;
            foreach (SpaceInstance member in group)
                _hintSet.Add(member);
        }

        bool flashing = Time.unscaledTime < _flashUntil;
        if (flashing)
            foreach (SpaceInstance member in _flashGroup)
                if (member != null)
                    _hintSet.Add(member);

        foreach (SpaceInstance inst in _instances)
        {
            if (inst == null)
                continue;
            bool hint = _hintSet.Contains(inst) && !inst.IsSelected;
            bool strong = flashing && _flashGroup.Contains(inst);
            inst.SetGroupHint(hint, strong);
        }
    }

    /// <summary>
    /// After a placement/move/rotate/duplicate, make the snap unmistakable:
    /// every member of the merged group flashes its full-strength pad, and a
    /// bright glow runs along the exact frame members the pieces fused on.
    /// Nothing flashes when the pieces merely sit next to each other.
    /// </summary>
    void FlashMergedGroups(IEnumerable<SpaceInstance> touched)
    {
        _flashGroup.Clear();
        var joints = new List<(Vector3 a, Vector3 b)>();
        foreach (SpaceInstance inst in touched)
        {
            var group = SpaceMerge.GroupOf(inst);
            if (group == null)
                continue;
            foreach (SpaceInstance member in group)
                if (!_flashGroup.Contains(member))
                    _flashGroup.Add(member);

            var groupJoints = SpaceMerge.JointsOf(inst);
            if (groupJoints != null)
                foreach ((Vector3 a, Vector3 b) seg in groupJoints)
                    if (!joints.Contains(seg))
                        joints.Add(seg);
        }

        _flashUntil = _flashGroup.Count > 0 ? Time.unscaledTime + 1.6f : 0f;
        RefreshGroupHints();

        if (joints.Count > 0)
            StartCoroutine(SnapGlow(joints));
    }

    /// <summary>
    /// The snap seam itself: bright lines along the fused frame members,
    /// swelling briefly and fading out.
    /// </summary>
    System.Collections.IEnumerator SnapGlow(List<(Vector3 a, Vector3 b)> joints)
    {
        var root = new GameObject("SnapGlow");
        root.transform.SetParent(transform, false);

        var glowMaterial = new Material(Shader.Find("Sprites/Default"));
        var lines = new List<LineRenderer>(joints.Count);
        foreach ((Vector3 a, Vector3 b) in joints)
        {
            var go = new GameObject("Seam", typeof(LineRenderer));
            go.transform.SetParent(root.transform, false);
            var line = go.GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, a);
            line.SetPosition(1, b);
            line.sharedMaterial = glowMaterial;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            lines.Add(line);
        }

        const float duration = 1.1f;
        // Snap flash in a lightened theme accent — loud enough to read as
        // "these fused", but in the palette's own voice.
        Color bright = Color.Lerp(UIThemeController.AccentColor, Color.white, 0.25f);
        bright.a = 0.95f;
        float wide = NeospaceUnits.Mm(70f);
        float slim = NeospaceUnits.Mm(18f);

        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = t / duration;
            // Two quick pulses, then fade out.
            float pulse = Mathf.Abs(Mathf.Sin(k * Mathf.PI * 2f));
            float fade = 1f - k * k;
            float width = Mathf.Lerp(slim, wide, pulse) * fade;
            Color c = bright;
            c.a = bright.a * fade;
            foreach (LineRenderer line in lines)
            {
                if (line == null)
                    continue;
                line.startWidth = line.endWidth = width;
                line.startColor = line.endColor = c;
            }
            yield return null;
        }

        Destroy(root);
        Destroy(glowMaterial);
    }

    // ------------------------------------------------------------------
    // Keys
    // ------------------------------------------------------------------

    void HandleKeys()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_ghost != null) DisarmPlacement();
            else if (_selected.Count > 0) DeselectAll();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            if (_ghost != null)
                _ghostYaw = Mathf.Repeat(_ghostYaw + 90f, 360f);
            else if (_selected.Count > 0)
                RotateSelection();
        }

        if ((Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace)) &&
            _selected.Count > 0)
            DeleteSelection();

        bool mod = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                   Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
        if (mod && Input.GetKeyDown(KeyCode.D) && _selected.Count > 0)
            DuplicateSelection();
    }

    // ------------------------------------------------------------------
    // Placement
    // ------------------------------------------------------------------

    public void ArmPlacement(PieceLibrary.PieceRecord record)
    {
        DisarmPlacement();
        DeselectAll();

        SelectionStatus.Set($"Preparing \"{record.name}\"…", 0f);
        factory.GetMaster(record.id, record.code, master =>
        {
            if (master == null || !SpaceModeController.Active)
            {
                ArmedChanged?.Invoke();
                return;
            }

            _armedRecord = record;
            _armedMaster = master;
            _ghost = factory.CreateGhost(master);
            _ghostYaw = 0f;
            var masterBox = master.GetComponent<BoxCollider>();
            _armedSize = masterBox.size;
            _armedCenter = masterBox.center;
            _ghostBlocked = false;
            _ghostChecked = false;
            SelectionStatus.Set(
                $"Placing \"{record.name}\" · click the floor to place it. R rotates, Esc puts it down.", 0f);
            ArmedChanged?.Invoke();
        });
    }

    public void DisarmPlacement()
    {
        if (_ghost != null)
            Destroy(_ghost);
        _ghost = null;
        _armedRecord = null;
        _armedMaster = null;
        if (SpaceModeController.Active)
            SelectionStatus.Set(SpaceModeController.IdleStatus, 0f);
        ArmedChanged?.Invoke();
    }

    void UpdatePlacement()
    {
        if (Input.GetMouseButtonDown(1))
        {
            DisarmPlacement();
            return;
        }

        if (!TryFloorPoint(Input.mousePosition, out Vector3 point))
            return;

        // The piece's BODY follows the cursor, not its pivot: the pivot is
        // lattice-quantized and can sit up to half a module off the visual
        // centre, which made pieces hang beside the cursor and swing when
        // rotated. Aim the footprint centre at the cursor and derive the
        // (still lattice-snapped) pivot from it.
        Quaternion ghostRot = Quaternion.Euler(0f, _ghostYaw, 0f);
        Vector3 fc = ghostRot * new Vector3(_armedCenter.x, 0f, _armedCenter.z);
        Vector3 snapped = SnapToGrid(point - fc);
        _ghost.transform.SetPositionAndRotation(snapped, ghostRot);

        // Pieces merge like build-mode structures: shared frames are reused,
        // beams split into catalogue pieces around new posts, panels re-seat
        // per sub-bay. A pose the solver can't build (no catalogue piece for
        // a split, beams crossing mid-span) shows a red ghost and refuses
        // the click. Re-analyzed only when the snapped pose changes.
        if (!_ghostChecked || snapped != _lastGhostPos || !Mathf.Approximately(_ghostYaw, _lastGhostYaw))
        {
            _ghostChecked = true;
            _lastGhostPos = snapped;
            _lastGhostYaw = _ghostYaw;
            bool nowBlocked = SpaceMerge.PoseBlocked(
                _instances, null, SpaceMerge.RecordsFrom(_ghost.transform, -1), out _ghostBlockReason);
            if (nowBlocked != _ghostBlocked)
            {
                _ghostBlocked = nowBlocked;
                factory.TintGhost(_ghost, !nowBlocked);
            }
            // Tell the user WHY the ghost is red while they hover, not only
            // after a refused click.
            if (nowBlocked && !string.IsNullOrEmpty(_ghostBlockReason))
                SelectionStatus.Set(_ghostBlockReason, 1.5f);
        }

        if (LeftClickGesture.ClickReleased)
        {
            if (_ghostBlocked)
            {
                SelectionStatus.Set(string.IsNullOrEmpty(_ghostBlockReason)
                    ? "The pieces can't merge here · move it so the frames line up."
                    : _ghostBlockReason, 4f);
                return;
            }

            SpaceInstance instance = factory.CreateInstance(
                _armedMaster, _armedRecord.id, _armedRecord.name, _armedRecord.code,
                _armedRecord.price, snapped, _ghostYaw);
            instance.transform.SetParent(transform, true);
            _instances.Add(instance);

            ApplyMerge();
            _ghostChecked = false;   // the space changed under the ghost
            history.Record(CurrentStates());

            var group = SpaceMerge.GroupOf(instance);
            if (group != null)
            {
                FlashMergedGroups(new[] { instance });
                SelectionStatus.Set(
                    $"Placed \"{_armedRecord.name}\" · snapped together with " +
                    $"{group.Count - 1} piece{(group.Count == 2 ? "" : "s")}. They now share their frames.", 4f);
            }
            else
            {
                SelectionStatus.Set(
                    $"Placed \"{_armedRecord.name}\" · click to place another, Esc to stop.", 0f);
            }
        }
    }

    // ------------------------------------------------------------------
    // Selection & move
    // ------------------------------------------------------------------

    void UpdateSelection()
    {
        // Press: remember what was hit; plain press on an unselected piece
        // selects it right away so the same gesture can turn into a drag.
        if (LeftClickGesture.PressedThisFrame && !LeftClickGesture.PressStartedOverUI)
        {
            _pressInstance = InstanceUnderPointer();
            _pressWithShift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (_pressInstance != null && !_pressWithShift && !_pressInstance.IsSelected)
                SelectOnly(_pressInstance);

            if (_pressInstance != null && !_pressWithShift &&
                TryFloorPoint(Input.mousePosition, out Vector3 grab))
            {
                _grabPoint = grab;
                _dragStartPositions.Clear();
                _dragStartGroups.Clear();
                foreach (SpaceInstance inst in _selected)
                {
                    _dragStartPositions.Add(inst.transform.position);
                    _dragStartGroups.Add(GroupFingerprint(inst));
                }
            }
        }

        // Drag turns into a group move.
        if (!_dragging && _pressInstance != null && !_pressWithShift &&
            LeftClickGesture.IsDragging && _dragStartPositions.Count == _selected.Count)
        {
            _dragging = true;
            _lastDragDelta = Vector3.zero;
            LeftClickGesture.PressClaim = this;
        }

        if (_dragging)
        {
            if (TryFloorPoint(Input.mousePosition, out Vector3 point))
            {
                Vector3 delta = SnapDelta(point - _grabPoint);
                if (delta != _lastDragDelta && !MoveBlocked(delta))
                {
                    _lastDragDelta = delta;
                    for (int i = 0; i < _selected.Count; i++)
                        _selected[i].transform.position = _dragStartPositions[i] + delta;
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                _dragging = false;
                _pressInstance = null;
                if (_lastDragDelta != Vector3.zero)
                {
                    ApplyMerge();
                    for (int i = 0; i < _selected.Count && i < _dragStartGroups.Count; i++)
                    {
                        if (_dragStartGroups[i] != GroupFingerprint(_selected[i]))
                        {
                            FlashMergedGroups(_selected);
                            break;
                        }
                    }
                    history.Record(CurrentStates());
                    UpdateSelectionStatus();
                }
            }
            return;
        }

        // Clean click: select / toggle / clear.
        if (LeftClickGesture.ClickReleased)
        {
            SpaceInstance hit = _pressInstance ?? InstanceUnderPointer();
            _pressInstance = null;

            if (hit == null)
            {
                DeselectAll();
            }
            else if (_pressWithShift)
            {
                if (hit.IsSelected) Deselect(hit);
                else Select(hit);
            }
            else
            {
                SelectOnly(hit);
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            _pressInstance = null;
        }
    }

    readonly HashSet<SpaceInstance> _ignoreSet = new HashSet<SpaceInstance>();

    /// <summary>
    /// Would moving the whole selection by this delta leave an arrangement
    /// the merge solver can't build (unsplittable beam, mid-span crossing)?
    /// </summary>
    bool MoveBlocked(Vector3 delta)
    {
        _ignoreSet.Clear();
        foreach (SpaceInstance inst in _selected)
            _ignoreSet.Add(inst);

        var candidate = new List<SpaceMerge.PartRecord>();
        for (int i = 0; i < _selected.Count; i++)
        {
            SpaceInstance inst = _selected[i];
            candidate.AddRange(SpaceMerge.RecordsFrom(
                inst, _dragStartPositions[i] + delta, inst.transform.eulerAngles.y,
                _instances.IndexOf(inst)));
        }
        return SpaceMerge.PoseBlocked(_instances, _ignoreSet, candidate, out _);
    }

    SpaceInstance InstanceUnderPointer()
    {
        Ray ray = Cam.ScreenPointToRay(Input.mousePosition);
        var hits = Physics.RaycastAll(ray, 500f);
        SpaceInstance best = null;
        float bestDist = float.MaxValue;
        foreach (RaycastHit hit in hits)
        {
            var inst = hit.collider.GetComponentInParent<SpaceInstance>();
            if (inst != null && hit.distance < bestDist)
            {
                best = inst;
                bestDist = hit.distance;
            }
        }
        return best;
    }

    void Select(SpaceInstance inst)
    {
        if (!_selected.Contains(inst))
        {
            _selected.Add(inst);
            inst.SetSelected(true);
        }
        UpdateSelectionStatus();
    }

    void Deselect(SpaceInstance inst)
    {
        _selected.Remove(inst);
        inst.SetSelected(false);
        UpdateSelectionStatus();
    }

    void SelectOnly(SpaceInstance inst)
    {
        foreach (SpaceInstance other in _selected)
            if (other != null && other != inst)
                other.SetSelected(false);
        _selected.Clear();
        _selected.Add(inst);
        inst.SetSelected(true);
        UpdateSelectionStatus();
    }

    public void DeselectAll()
    {
        foreach (SpaceInstance inst in _selected)
            if (inst != null)
                inst.SetSelected(false);
        _selected.Clear();
        UpdateSelectionStatus();
    }

    void UpdateSelectionStatus()
    {
        RefreshGroupHints();

        if (!SpaceModeController.Active)
            return;

        if (_selected.Count == 0)
        {
            SelectionStatus.Set(SpaceModeController.IdleStatus, 0f);
        }
        else
        {
            string what = _selected.Count == 1
                ? $"\"{_selected[0].pieceName}\""
                : $"{_selected.Count} pieces";
            SelectionStatus.Set(
                $"{what} selected · drag to move, R rotates, Delete removes. " +
                "Shift+click adds more, click empty floor to deselect.", 0f);
        }
    }

    // ------------------------------------------------------------------
    // Selection actions
    // ------------------------------------------------------------------

    public void DuplicateSelection()
    {
        if (_selected.Count == 0)
            return;

        if (!TryFindDuplicateOffset(out Vector3 offset))
        {
            SelectionStatus.Set("No free spot for a copy nearby · move some pieces first.", 3f);
            return;
        }

        var copies = new List<SpaceInstance>();

        foreach (SpaceInstance src in _selected)
        {
            // The instance itself is a frozen clone — clone it directly.
            GameObject go = Instantiate(src.gameObject, transform, true);
            go.name = src.gameObject.name;
            go.transform.position = src.transform.position + offset;

            var copy = go.GetComponent<SpaceInstance>();
            copy.SetSelected(false);
            copies.Add(copy);
            _instances.Add(copy);
        }

        DeselectAll();
        foreach (SpaceInstance copy in copies)
            Select(copy);

        ApplyMerge();
        FlashMergedGroups(copies);
        history.Record(CurrentStates());
        SelectionStatus.Set(
            $"Duplicated {copies.Count} piece{(copies.Count == 1 ? "" : "s")} · drag the copy where you want it.", 0f);
    }

    public void RotateSelection()
    {
        if (_selected.Count == 0)
            return;

        // Turn around the selection's VISUAL centre (footprint centres, not
        // pivots) so a single piece spins in place instead of swinging around
        // its off-centre pivot.
        Vector3 centroid = Vector3.zero;
        foreach (SpaceInstance inst in _selected)
            centroid += inst.FootprintCenterWorld;
        centroid /= _selected.Count;
        centroid = SnapToGrid(centroid);

        // Dry run first: the turned arrangement must still merge legally.
        _ignoreSet.Clear();
        foreach (SpaceInstance inst in _selected)
            _ignoreSet.Add(inst);

        var candidate = new List<SpaceMerge.PartRecord>();
        foreach (SpaceInstance inst in _selected)
        {
            (Vector3 newPos, float newYaw) = RotatedPose(inst, centroid);
            candidate.AddRange(SpaceMerge.RecordsFrom(inst, newPos, newYaw,
                _instances.IndexOf(inst)));
        }

        if (SpaceMerge.PoseBlocked(_instances, _ignoreSet, candidate, out string why))
        {
            SelectionStatus.Set(string.IsNullOrEmpty(why)
                ? "Can't rotate here · the turned pieces wouldn't merge. Move them clear first."
                : why, 4f);
            return;
        }

        var beforeGroups = new List<int>(_selected.Count);
        foreach (SpaceInstance inst in _selected)
            beforeGroups.Add(GroupFingerprint(inst));

        foreach (SpaceInstance inst in _selected)
        {
            (Vector3 newPos, float newYaw) = RotatedPose(inst, centroid);
            inst.transform.SetPositionAndRotation(newPos, Quaternion.Euler(0f, newYaw, 0f));
        }

        ApplyMerge();
        for (int i = 0; i < _selected.Count; i++)
        {
            if (beforeGroups[i] != GroupFingerprint(_selected[i]))
            {
                FlashMergedGroups(_selected);
                break;
            }
        }
        history.Record(CurrentStates());
    }

    /// <summary>
    /// Pose of one instance after a +90° yaw of the selection around
    /// <paramref name="centroid"/>: its footprint centre orbits the centroid
    /// and the lattice-snapped pivot is re-derived from the turned centre.
    /// </summary>
    static (Vector3 pos, float yaw) RotatedPose(SpaceInstance inst, Vector3 centroid)
    {
        Vector3 c = inst.FootprintCenterWorld - centroid;
        Vector3 turned = centroid + new Vector3(c.z, 0f, -c.x);   // +90° yaw
        float newYaw = inst.transform.eulerAngles.y + 90f;
        Vector3 fc = Quaternion.Euler(0f, newYaw, 0f) *
            new Vector3(inst.footprintCenter.x, 0f, inst.footprintCenter.z);
        return (SnapToGrid(turned - fc), newYaw);
    }

    /// <summary>
    /// Find the nearest sideways offset (88 mm steps in +X) where a copy of
    /// the whole selection fits. Edge-deep contact is allowed, so a copy of
    /// a shelf usually lands exactly flush with the original — sharing its
    /// boundary frames, ready to read as one connected row.
    /// </summary>
    bool TryFindDuplicateOffset(out Vector3 offset)
    {
        float module = NeospaceUnits.ModuleMeters;

        var records = new List<SpaceMerge.PartRecord>();
        for (int step = 1; step <= 100; step++)
        {
            Vector3 candidate = new Vector3(module * step, 0f, 0f);

            records.Clear();
            foreach (SpaceInstance src in _selected)
                records.AddRange(SpaceMerge.RecordsFrom(
                    src, src.transform.position + candidate, src.transform.eulerAngles.y, -1));

            if (!SpaceMerge.PoseBlocked(_instances, null, records, out _))
            {
                offset = candidate;
                return true;
            }
        }

        offset = Vector3.zero;
        return false;
    }

    public void DeleteSelection()
    {
        if (_selected.Count == 0)
            return;

        int n = _selected.Count;
        foreach (SpaceInstance inst in _selected)
        {
            _instances.Remove(inst);
            Destroy(inst.gameObject);
        }
        _selected.Clear();

        // Parts hidden or split against a removed piece come back whole.
        ApplyMerge();
        history.Record(CurrentStates());
        SelectionStatus.Set($"Removed {n} piece{(n == 1 ? "" : "s")} · Ctrl+Z (Cmd+Z) to undo.", 4f);
        UpdateSelectionStatus();
    }

    // ------------------------------------------------------------------
    // State (history / mode switching)
    // ------------------------------------------------------------------

    public List<SpaceHistory.InstanceState> CurrentStates()
    {
        var states = new List<SpaceHistory.InstanceState>(_instances.Count);
        foreach (SpaceInstance inst in _instances)
        {
            if (inst == null)
                continue;
            states.Add(new SpaceHistory.InstanceState
            {
                PieceId = inst.pieceId,
                PieceName = inst.pieceName,
                Code = inst.code,
                Price = inst.price,
                Position = inst.transform.position,
                YawDegrees = inst.transform.eulerAngles.y
            });
        }
        return states;
    }

    /// <summary>
    /// Destroy everything and re-clone the given state (undo/redo, loading a
    /// space code). Masters already in the cache clone synchronously; unknown
    /// ones (a pasted code from someone else) build one by one first.
    /// <paramref name="onComplete"/> fires once every instance is in place.
    /// </summary>
    public void RebuildFromStates(List<SpaceHistory.InstanceState> states,
        System.Action onComplete = null)
    {
        DeselectAll();
        foreach (SpaceInstance inst in _instances)
            if (inst != null)
                Destroy(inst.gameObject);
        _instances.Clear();

        int pending = states.Count;
        if (pending == 0)
        {
            ApplyMerge();
            onComplete?.Invoke();
            return;
        }

        foreach (SpaceHistory.InstanceState state in states)
        {
            SpaceHistory.InstanceState s = state;
            factory.GetMaster(s.PieceId, s.Code, master =>
            {
                if (master == null)
                {
                    Debug.LogWarning($"[Space] Piece \"{s.PieceName}\" could not be rebuilt.");
                }
                else
                {
                    SpaceInstance inst = factory.CreateInstance(
                        master, s.PieceId, s.PieceName, s.Code, s.Price, s.Position, s.YawDegrees);
                    inst.transform.SetParent(transform, true);
                    _instances.Add(inst);
                }

                if (--pending == 0)
                {
                    ApplyMerge();
                    onComplete?.Invoke();
                }
            });
        }
    }

    /// <summary>Show / hide all placed pieces (mode switching).</summary>
    public void ShowInstances(bool show)
    {
        foreach (SpaceInstance inst in _instances)
            if (inst != null)
                inst.gameObject.SetActive(show);
        SpaceMerge.ShowDerived(show);
    }

    /// <summary>
    /// Recompute the merged view (shared frames deduplicated, beams split
    /// into catalogue pieces, panels re-seated per sub-bay).
    /// </summary>
    void ApplyMerge()
    {
        SpaceMerge.Apply(_instances,
            buildController != null ? buildController.partDatabase : null, transform);
        RefreshGroupHints();   // groups may have changed (deletes, undo, code load)
    }

    // ------------------------------------------------------------------
    // Geometry helpers
    // ------------------------------------------------------------------

    bool TryFloorPoint(Vector3 screenPos, out Vector3 point)
    {
        Ray ray = Cam.ScreenPointToRay(screenPos);
        var floor = new Plane(Vector3.up, Vector3.zero);
        if (floor.Raycast(ray, out float enter))
        {
            point = ray.GetPoint(enter);
            return true;
        }
        point = Vector3.zero;
        return false;
    }

    static Vector3 SnapToGrid(Vector3 world)
    {
        float module = NeospaceUnits.ModuleMeters;
        return new Vector3(
            Mathf.Round(world.x / module) * module,
            0f,
            Mathf.Round(world.z / module) * module);
    }

    static Vector3 SnapDelta(Vector3 delta)
    {
        float module = NeospaceUnits.ModuleMeters;
        return new Vector3(
            Mathf.Round(delta.x / module) * module,
            0f,
            Mathf.Round(delta.z / module) * module);
    }

    // ------------------------------------------------------------------
    // Action card
    // ------------------------------------------------------------------

    void OnEnable() => UIThemeController.ThemeChanged += HandleThemeChanged;
    void OnDisable() => UIThemeController.ThemeChanged -= HandleThemeChanged;

    void HandleThemeChanged()
    {
        if (_card == null)
            return;
        Vector2 pos = _card.anchoredPosition;
        bool show = _card.gameObject.activeSelf;
        Destroy(_card.gameObject);
        _card = null;
        _cardLabel = null;
        _editButton = null;
        if (!show)
            return;
        BuildCard();
        if (_card == null)
            return;
        _card.gameObject.SetActive(true);
        _card.anchoredPosition = pos;
        _card.SetAsLastSibling();
    }

    void UpdateCard()
    {
        if (_selected.Count == 0 || _dragging || _ghost != null)
        {
            HideCard();
            return;
        }

        if (_card == null)
            BuildCard();
        if (_card == null)
            return;

        Vector3 centroid = Vector3.zero;
        float top = 0f;
        foreach (SpaceInstance inst in _selected)
        {
            centroid += inst.FootprintCenterWorld;
            top = Mathf.Max(top, inst.size.y);
        }
        centroid /= _selected.Count;
        centroid.y = top;

        Vector3 screen = Cam.WorldToScreenPoint(centroid);
        if (screen.z <= 0f)
        {
            HideCard();
            return;
        }

        _card.gameObject.SetActive(true);
        _cardLabel.text = _selected.Count == 1
            ? _selected[0].pieceName
            : $"{_selected.Count} pieces";

        // Edit is offered when the whole selection is copies of one piece.
        if (_editButton != null)
            _editButton.SetActive(editSession != null && SelectionSharesOneCode());

        var canvasRect = (RectTransform)_canvas.transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, new Vector2(screen.x + 16f, screen.y + 16f), null, out Vector2 local);

        Vector2 half = canvasRect.rect.size * 0.5f;
        local.x = Mathf.Clamp(local.x, -half.x + 8f, half.x - _card.sizeDelta.x - 8f);
        local.y = Mathf.Clamp(local.y, -half.y + 8f, half.y - _card.sizeDelta.y - 8f);

        _card.SetAsLastSibling();
        _card.anchoredPosition = local;
    }

    void HideCard()
    {
        if (_card != null)
            _card.gameObject.SetActive(false);
    }

    void BuildCard()
    {
        _canvas = FindFirstObjectByType<Canvas>();
        if (_canvas == null)
            return;

        AdoptCardStyle();

        var go = new GameObject("SpaceActions", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_canvas.transform, false);
        _card = (RectTransform)go.transform;
        _card.anchorMin = _card.anchorMax = new Vector2(0.5f, 0.5f);
        _card.pivot = Vector2.zero;
        _card.sizeDelta = new Vector2(372f, 96f);

        var img = go.GetComponent<Image>();
        img.color = Surface;
        StyleCard(img, 1.4f);

        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
        shadow.effectDistance = new Vector2(0f, -3f);

        _cardLabel = CreateText(_card, "Label", "", 13.5f, Ink, true);
        var labelRt = _cardLabel.rectTransform;
        labelRt.anchorMin = new Vector2(0f, 1f);
        labelRt.anchorMax = new Vector2(1f, 1f);
        labelRt.pivot = new Vector2(0.5f, 1f);
        labelRt.offsetMin = new Vector2(14f, -34f);
        labelRt.offsetMax = new Vector2(-14f, -10f);
        _cardLabel.overflowMode = TextOverflowModes.Ellipsis;

        _editButton = CardButton("Btn_EditPiece", "Edit", Ink, Surface, 12f, 54f, OnEditClicked);
        CardButton("Btn_Duplicate", "Duplicate", Accent, Color.white, 72f, 86f, DuplicateSelection);
        CardButton("Btn_Rotate", "Rotate", Surface, Ink, 164f, 64f, RotateSelection);
        CardButton("Btn_Remove", "Remove", Danger, Color.white, 234f, 72f, DeleteSelection);
        // "×" not "✕": the dingbat is missing from the UI font.
        CardButton("Btn_Deselect", "×", Ink, Surface, 328f, 32f, DeselectAll);

        _card.gameObject.SetActive(false);
    }

    bool SelectionSharesOneCode()
    {
        if (_selected.Count == 0)
            return false;
        string code = _selected[0].code;
        foreach (SpaceInstance inst in _selected)
            if (inst.code != code)
                return false;
        return true;
    }

    void OnEditClicked()
    {
        if (editSession == null || !SelectionSharesOneCode())
            return;
        editSession.BeginEdit(new List<SpaceInstance>(_selected));
    }

    GameObject CardButton(string name, string label, Color bg, Color fg,
        float x, float width, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(_card, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(x, 12f);
        rt.sizeDelta = new Vector2(width, 40f);

        var img = go.GetComponent<Image>();
        img.color = bg;
        StyleCard(img, 1.8f);

        go.GetComponent<Button>().onClick.AddListener(onClick);

        TextMeshProUGUI text = CreateText(rt, "Text", label, 12.5f, fg, true);
        var textRt = text.rectTransform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        text.alignment = TextAlignmentOptions.Center;
        return go;
    }

    TextMeshProUGUI CreateText(RectTransform parent, string name, string value,
        float size, Color color, bool bold)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = value;
        if (_font != null)
            tmp.font = _font;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.raycastTarget = false;
        if (bold)
            tmp.fontStyle = FontStyles.Bold;
        return tmp;
    }

    void StyleCard(Image img, float ppu)
    {
        if (_cardSprite == null)
            return;
        img.sprite = _cardSprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = _cardPpu * ppu;
    }

    void AdoptCardStyle()
    {
        Transform partsPanel = _canvas.transform.Find("PartsPanel");
        if (partsPanel == null)
            return;

        var img = partsPanel.GetComponent<Image>();
        if (img != null && img.sprite != null)
        {
            _cardSprite = img.sprite;
            _cardPpu = img.pixelsPerUnitMultiplier;
        }

        var text = partsPanel.GetComponentInChildren<TMP_Text>(true);
        if (text != null && text.font != null)
            _font = text.font;
    }
}
