using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The switch between PIECE MODE (the existing builder) and SPACE MODE
/// (arranging saved pieces into a room).
///
/// Entering Space Mode:
///  - the current piece build is hidden (kept intact, restored on exit),
///  - every builder tool/controller is put to sleep and its history is
///    suspended (top-bar Undo/Redo/Clear route to the space history instead),
///  - the left panel switches to the "My Pieces" list,
///  - the price readout shows the sum of the placed piece instances.
///
/// Camera, floor and top bar stay — same room, different work.
/// </summary>
public class SpaceModeController : MonoBehaviour
{
    public BuildController buildController;
    public SpaceInteractionController interaction;
    public SpaceHistory history;
    public SpacePanelUI spacePanel;
    public SpaceEditSession editSession;

    public static bool Active { get; private set; }

    // Builder components put to sleep while Space Mode runs.
    static readonly System.Type[] SleepTypes =
    {
        typeof(BuildController), typeof(FreePartSession),
        typeof(MarqueeSelectionController), typeof(PanelLayerMover),
        typeof(MoveGizmoController), typeof(AttachmentMarkerController),
        typeof(StructureDimensionsController), typeof(UIBuildStats),
        typeof(TemplateSession), typeof(GuidedModeController),
        typeof(TemplateGhostPreview), typeof(TemplatePreviewGuide),
        typeof(StructureClipboard), typeof(GhostController)
        // AdaptiveGridController stays awake: the grid adapts to placed
        // piece instances in Space Mode too (StructureBounds counts them).
    };

    readonly List<Behaviour> _slept = new List<Behaviour>();
    readonly List<GameObject> _hiddenBuildRoots = new List<GameObject>();
    readonly List<GameObject> _hiddenUi = new List<GameObject>();

    Button _modeButton;
    TextMeshProUGUI _modeLabel;
    Image _modeImage;

    UIBuildStats _stats;
    float _statsTimer;

    UnityEngine.Events.UnityAction _undoAction, _redoAction, _clearAction;

    public const string IdleStatus =
        "Space Mode · pick a piece on the left, then click the floor to place it. " +
        "Drag pieces to move them, R rotates.";

    void OnEnable()
    {
        UIThemeController.ThemeChanged += UpdateModeButtonVisual;
        UpdateModeButtonVisual();
    }

    void OnDisable()
    {
        UIThemeController.ThemeChanged -= UpdateModeButtonVisual;
    }

    void Update()
    {
        if (!Active)
            return;

        HandleUndoKeys();
        UpdatePriceReadout();

        // Timed messages (errors etc.) expire — fall back to the space hint,
        // never to the builder's idle wording.
        if (!SelectionStatus.TryGet(out _))
            SelectionStatus.Set(IdleStatus, 0f);
    }

    // ------------------------------------------------------------------
    // Mode switch
    // ------------------------------------------------------------------

    public void ToggleMode()
    {
        // During "Edit Piece" the mode button means "back to my space".
        if (!Active && editSession != null && editSession.IsEditing)
        {
            editSession.CancelEdit();
            return;
        }

        if (Active) ExitSpaceMode();
        else EnterSpaceMode();
    }

    public void EnterSpaceMode()
    {
        if (Active || _entering)
            return;
        StartCoroutine(EnterRoutine());
    }

    bool _entering;

    System.Collections.IEnumerator EnterRoutine()
    {
        _entering = true;

        // Put down whatever the builder was doing, then give the builder
        // tools one frame while still enabled so their visuals (gizmo,
        // action card, ghosts) hide themselves before going to sleep.
        var marquee = FindFirstObjectByType<MarqueeSelectionController>();
        if (marquee != null)
        {
            marquee.ClearSelection();
            marquee.HideActionCard();
        }
        var freeSession = FindFirstObjectByType<FreePartSession>();
        if (freeSession != null)
            freeSession.SetKind(FreePartKind.None);
        if (buildController != null)
            buildController.currentPartId = null;

        yield return null;

        _entering = false;
        Active = true;

        // Hide the piece build (kept exactly as it is).
        _hiddenBuildRoots.Clear();
        foreach (Transform root in CollectBuildRoots())
        {
            root.gameObject.SetActive(false);
            _hiddenBuildRoots.Add(root.gameObject);
        }

        // Sleep the builder systems.
        _slept.Clear();
        foreach (System.Type type in SleepTypes)
        {
            foreach (Object obj in FindObjectsByType(type, FindObjectsSortMode.None))
            {
                if (obj is Behaviour behaviour && behaviour.enabled)
                {
                    behaviour.enabled = false;
                    _slept.Add(behaviour);
                }
            }
        }

        BuildHistory.Suspended = true;
        SwapUi(toSpace: true);
        RouteHistoryButtons(toSpace: true);

        interaction.ShowInstances(true);
        history.ResetBaseline(interaction.CurrentStates());

        _stats = FindFirstObjectByType<UIBuildStats>();
        UpdatePriceReadout(force: true);

        SelectionStatus.Set(IdleStatus, 0f);
    }

    public void ExitSpaceMode()
    {
        if (!Active)
            return;
        Active = false;

        interaction.DisarmPlacement();
        interaction.DeselectAll();
        interaction.ShowInstances(false);

        foreach (GameObject go in _hiddenBuildRoots)
            if (go != null)
                go.SetActive(true);
        _hiddenBuildRoots.Clear();

        foreach (Behaviour behaviour in _slept)
            if (behaviour != null)
                behaviour.enabled = true;
        _slept.Clear();

        BuildHistory.Suspended = false;
        SwapUi(toSpace: false);
        RouteHistoryButtons(toSpace: false);

        SelectionStatus.Clear();
    }

    /// <summary>Roots of every placed beam and panel (ghosts excluded).</summary>
    List<Transform> CollectBuildRoots()
    {
        var roots = new HashSet<Transform>();
        int ghostMask = buildController != null ? buildController.ghostLayerMask.value : 0;

        foreach (SelectableBeam beam in FindObjectsByType<SelectableBeam>(FindObjectsSortMode.None))
        {
            Transform root = beam.transform.root;
            if ((ghostMask & (1 << root.gameObject.layer)) == 0 &&
                StructureClipboard.CleanPartId(root.name) != null)
                roots.Add(root);
        }
        // Hide each panel object itself, NOT its transform.root: panels are
        // parented under the shared "PanelsRoot" container, and deactivating
        // that container would also hide (and orphan) any panel the piece
        // restorer creates while Space Mode is active.
        foreach (PanelInstance pi in FindObjectsByType<PanelInstance>(FindObjectsSortMode.None))
        {
            if ((ghostMask & (1 << pi.gameObject.layer)) == 0)
                roots.Add(pi.transform);
        }
        return new List<Transform>(roots);
    }

    // ------------------------------------------------------------------
    // UI
    // ------------------------------------------------------------------

    void SwapUi(bool toSpace)
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        if (toSpace)
        {
            _hiddenUi.Clear();
            Hide(canvas.transform, "PartsPanel");
            Hide(canvas.transform, "GuidedToolsPanel");
            Hide(canvas.transform, "PiecesPanel");
            Hide(canvas.transform, "TopBar/Btn_Pieces");
            // Load code stays: it is mode-aware. (Space codes are copied
            // from the Space panel's own button.)

            spacePanel.Show();
        }
        else
        {
            spacePanel.Hide();

            foreach (GameObject go in _hiddenUi)
                if (go != null)
                    go.SetActive(true);
            _hiddenUi.Clear();
        }

        UpdateModeButtonVisual();

        void Hide(Transform parent, string path)
        {
            Transform t = parent.Find(path);
            if (t != null && t.gameObject.activeSelf)
            {
                t.gameObject.SetActive(false);
                _hiddenUi.Add(t.gameObject);
            }
        }
    }

    /// <summary>
    /// While in Space Mode the top-bar Undo / Redo / Clear all buttons work
    /// on the space (the builder history is suspended, so the original
    /// listeners no-op — these extra listeners do the space work).
    /// </summary>
    void RouteHistoryButtons(bool toSpace)
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        Transform bar = canvas != null ? canvas.transform.Find("TopBar") : null;
        if (bar == null)
            return;

        _undoAction ??= () => { if (Active) SpaceUndo(); };
        _redoAction ??= () => { if (Active) SpaceRedo(); };
        _clearAction ??= () => { if (Active) SpaceClear(); };

        Wire("Btn_Undo", _undoAction, toSpace);
        Wire("Btn_Redo", _redoAction, toSpace);
        // Clear all lives in the ⋯ overflow menu now (old scenes: top bar).
        Wire("MoreMenu/Btn_ClearAll", _clearAction, toSpace);
        Wire("Btn_ClearAll", _clearAction, toSpace);

        void Wire(string name, UnityEngine.Events.UnityAction action, bool add)
        {
            Transform t = bar.Find(name);
            if (t == null || !t.TryGetComponent(out Button btn))
                return;

            // Confirming buttons fire actions from onConfirmed, not onClick
            // (onClick is the arm/"Sure?" step).
            if (t.TryGetComponent(out UIConfirmingButton confirming))
            {
                confirming.onConfirmed.RemoveListener(action);
                if (add)
                    confirming.onConfirmed.AddListener(action);
                return;
            }

            btn.onClick.RemoveListener(action);
            if (add)
                btn.onClick.AddListener(action);
        }
    }

    public void RegisterModeButton(Button button)
    {
        _modeButton = button;
        _modeLabel = button.GetComponentInChildren<TextMeshProUGUI>(true);
        _modeImage = button.GetComponent<Image>();
        button.onClick.AddListener(ToggleMode);
        UpdateModeButtonVisual();
    }

    // Segmented Build | Space switch (current top bar). The active segment
    // gets the dark pill, matching the Tools/Parts tabs.
    Button _segBuild, _segSpace;

    public void RegisterModeButtons(Button buildSegment, Button spaceSegment)
    {
        _segBuild = buildSegment;
        _segSpace = spaceSegment;

        buildSegment.onClick.AddListener(() =>
        {
            if (Active)
                ExitSpaceMode();
        });
        spaceSegment.onClick.AddListener(() =>
        {
            if (Active)
                return;
            // During "Edit Piece" the Space segment means "back to my space".
            if (editSession != null && editSession.IsEditing)
                editSession.CancelEdit();
            else
                EnterSpaceMode();
        });

        UpdateModeButtonVisual();
    }

    UIThemeController _segTheme;

    void UpdateModeButtonVisual()
    {
        // Segmented control (current scenes).
        if (_segBuild != null && _segSpace != null)
        {
            if (_segTheme == null)
                _segTheme = FindFirstObjectByType<UIThemeController>();
            UIThemeController theme = _segTheme;
            var palette = theme != null ? (theme.IsDark ? theme.dark : theme.light) : null;
            Color ink = palette != null ? palette.ink : UIThemeController.InkColor;
            Color muted = palette != null ? palette.muted : UIThemeController.MutedColor;
            Color activeText = palette != null ? palette.card : UIThemeController.CardColor;

            Paint(_segBuild, !Active);
            Paint(_segSpace, Active);

            void Paint(Button segment, bool on)
            {
                if (segment.TryGetComponent(out Image bg))
                    bg.color = on ? ink : Color.clear;
                var label = segment.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                    label.color = on ? activeText : muted;
            }
        }

        // Legacy single pill (older scenes).
        if (_modeLabel != null)
            _modeLabel.text = Active ? "Piece mode" : "Space mode";
        if (_modeImage != null)
            _modeImage.color = Active ? UIThemeController.AccentColor : UIThemeController.SurfaceColor;
        if (_modeLabel != null)
            _modeLabel.color = Active ? Color.white : UIThemeController.InkColor;
    }

    // ------------------------------------------------------------------
    // History routing
    // ------------------------------------------------------------------

    void HandleUndoKeys()
    {
        bool mod = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                   Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
        if (!mod)
            return;

        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (Input.GetKeyDown(KeyCode.Z))
        {
            if (shift) SpaceRedo();
            else SpaceUndo();
        }
        else if (Input.GetKeyDown(KeyCode.Y))
        {
            SpaceRedo();
        }
    }

    void SpaceUndo()
    {
        List<SpaceHistory.InstanceState> states = history.Undo();
        if (states != null)
        {
            interaction.RebuildFromStates(states);
            SelectionStatus.Set("Undone.", 2f);
        }
    }

    void SpaceRedo()
    {
        List<SpaceHistory.InstanceState> states = history.Redo();
        if (states != null)
        {
            interaction.RebuildFromStates(states);
            SelectionStatus.Set("Redone.", 2f);
        }
    }

    void SpaceClear()
    {
        if (interaction.InstanceCount == 0)
        {
            SelectionStatus.Set("The space is already empty.", 2f);
            return;
        }
        int n = interaction.InstanceCount;
        interaction.RebuildFromStates(new List<SpaceHistory.InstanceState>());
        history.Record(interaction.CurrentStates());
        SelectionStatus.Set($"Removed {n} piece{(n == 1 ? "" : "s")} · Ctrl+Z (Cmd+Z) to undo.", 4f);
    }

    // ------------------------------------------------------------------
    // Price readout
    // ------------------------------------------------------------------

    void UpdatePriceReadout(bool force = false)
    {
        _statsTimer += Time.unscaledDeltaTime;
        if (!force && _statsTimer < 0.25f)
            return;
        _statsTimer = 0f;

        if (_stats == null)
            return;

        int count = interaction.InstanceCount;
        float total = 0f;
        foreach (SpaceInstance inst in interaction.Instances)
            total += inst.price;

        // Merged pieces are priced as ONE structure: hidden shared/covered
        // parts are deducted, catalogue pieces spawned by beam splits and
        // panel divisions are added.
        total = Mathf.Max(0f, total + SpaceMerge.PriceDelta);

        if (_stats.partCountText != null)
            _stats.partCountText.text = count == 1 ? "1 piece" : $"{count} pieces";
        if (_stats.priceText != null)
            _stats.priceText.text = "$" + total.ToString("N0");
    }
}
