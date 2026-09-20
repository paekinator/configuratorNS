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
///    suspended (the rail's Undo/Redo route to the space history instead),
///  - the dock's tabs switch to the block library,
///  - the price total counts the placed piece instances.
///
/// Camera, floor and chrome stay — same room, different work.
/// </summary>
public class SpaceModeController : MonoBehaviour
{
    public BuildController buildController;
    public SpaceInteractionController interaction;
    public SpaceHistory history;
    public SpacePanelUI spacePanel;
    public SpaceEditSession editSession;

    public static bool Active { get; private set; }

    /// <summary>
    /// Raised whenever the mode changes, with the new value of
    /// <see cref="Active"/> — true for Lite (Space), false for Pro (Piece).
    ///
    /// Added so the dock can swap its tab set with the mode. Mode used to be
    /// readable only by polling the static, which meant anything that cared
    /// had to check it every frame or be told by hand.
    /// </summary>
    public static event System.Action<bool> ModeChanged;

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
        // GroundGridController stays awake: the grid adapts to placed piece
        // instances in Space Mode too (StructureBounds counts them), and its
        // hover highlight follows the block ghost the same way it follows a
        // part ghost.
    };

    readonly List<Behaviour> _slept = new List<Behaviour>();
    readonly List<GameObject> _hiddenBuildRoots = new List<GameObject>();
    // (There is no _hiddenUi list any more: SwapUi hides nothing, so there is
    // nothing to restore. See the note there.)

    // (The lone "Space mode" pill's fields are gone with it — see
    // UpdateModeButtonVisual, which now paints only the Pro | Lite segments.)

    UIBuildStats _stats;
    float _statsTimer;

    // No _clearAction: Clear all was a ⋯-menu button, and My Projects calls
    // SpaceClear directly when the mode is Lite. Nothing is re-pointed on the
    // way in and out of the mode any more.
    UnityEngine.Events.UnityAction _undoAction, _redoAction;

    /// <summary>
    /// Names neither the mode nor a panel that has moved.
    ///
    /// It said "Space Mode · pick a piece on the left" — two things wrong at
    /// once. The mode is a switch the reader can see, so saying it spends the
    /// line on something already on screen; and there is nothing on the left
    /// any more, because blocks live in the dock's Blocks tab now. A hint
    /// pointing at a panel that is not there is worse than no hint.
    /// </summary>
    public const string IdleStatus =
        "Pick a block below, then click the floor to place it. " +
        "Drag blocks to move them, R rotates.";

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

    /// <summary>
    /// Said once per session, when you first switch modes carrying work that
    /// is not in a project.
    ///
    /// A MODAL here would be wrong, and this is the one of the four guarded
    /// moments that is not really a guard. Switching modes loses nothing:
    /// EnterSpaceMode hides the build and ExitSpaceMode gives it straight
    /// back. Asking "are you sure?" every time you press Pro | Lite would be
    /// a false alarm on a control people use constantly, and the fastest way
    /// to teach someone to dismiss the dialog that DOES matter.
    ///
    /// What is true, and worth saying once, is that the safety net is memory
    /// only: it does not survive closing the tab.
    /// </summary>
    static bool _saidSwitchingKeepsWork;

    public void ToggleMode()
    {
        // During "Edit Piece" the mode button means "back to my space".
        if (!Active && editSession != null && editSession.IsEditing)
        {
            editSession.CancelEdit();
            return;
        }

        if (!_saidSwitchingKeepsWork &&
            CurrentProject.HasUnsavedWork(buildController, out _))
        {
            _saidSwitchingKeepsWork = true;
            SelectionStatus.Set(
                "Your work is kept while you switch modes — but it is not saved. "
                + "Save it as a project to keep it for good.", 7f);
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

        // This method used to hide a list of things on the way into Space mode
        // and restore them on the way out. It hides nothing now, and the
        // hide/restore machinery is gone with it:
        //
        //  - PartsPanel and GuidedToolsPanel live in the dock body, and
        //    DockTabs owns what that body shows — it swaps its whole tab set
        //    with the mode. Hiding them from here as well made two owners of
        //    one piece of state, and whichever ran last won.
        //
        //  - Btn_Pieces and PiecesPanel were hidden because "Pieces" meant the
        //    piece library, a Piece-mode idea with nothing to offer in Space
        //    mode. So the rail silently lost a button whenever you switched to
        //    Lite. Nothing is hidden now: a rail that changes length on a mode
        //    switch is worse than a button that explains itself, and My Blocks
        //    says plainly that blocks are built in Pro when you press Save
        //    there. My Projects (Btn_Projects) is genuinely mode-independent —
        //    a project is a SCENE, and both modes have one to save.
        //
        //  - Load code always stayed: it is mode-aware.
        //
        // The "My Pieces" panel is not shown in either mode. Lite's dock tabs
        // own the body now, and its Blocks tab is waiting for the real block
        // catalogue rather than borrowing that panel.
        spacePanel.Hide();

        UpdateModeButtonVisual();
        ModeChanged?.Invoke(toSpace);
    }

    /// <summary>
    /// While in Space Mode the Undo / Redo buttons work on the space (the
    /// builder history is suspended, so the original listeners no-op — these
    /// extra listeners do the space work).
    ///
    /// Resolved from the CANVAS, not from a top bar. This used to bail out
    /// entirely when it could not find a "TopBar", which meant dissolving that
    /// bar would silently unwire undo and redo in Lite mode: both buttons
    /// still there, still lit, doing nothing. The buttons live in the utility
    /// rail and have for a long time.
    /// </summary>
    void RouteHistoryButtons(bool toSpace)
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;
        Transform root = canvas.transform;

        _undoAction ??= () => { if (Active) SpaceUndo(); };
        _redoAction ??= () => { if (Active) SpaceRedo(); };

        Wire("Btn_Undo", _undoAction, toSpace);
        Wire("Btn_Redo", _redoAction, toSpace);

        // No Clear all to route. It was a button in the ⋯ menu, which is
        // retired; My Projects calls SpaceClear directly when the mode is
        // Lite, so nothing has to be re-pointed on the way in and out.

        void Wire(string name, UnityEngine.Events.UnityAction action, bool add)
        {
            Transform t = name.Contains("/") ? UIChrome.FindPanel(root, name)
                                             : UIChrome.FindButton(root, name);
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

    // RegisterModeButton (singular) is gone with the last caller. It wired a
    // lone "Space mode" pill from a builder two revisions back; nothing has
    // produced one for a long time.

    // The Pro | Lite switch. The active segment gets the dark pill, matching
    // the dock's tabs.
    Button _segBuild, _segSpace;

    public void RegisterModeButtons(Button buildSegment, Button spaceSegment)
    {
        _segBuild = buildSegment;
        _segSpace = spaceSegment;
        var motion = buildSegment.transform.parent.GetComponent<UIModeSwitchMotion>();
        if (motion == null) motion = buildSegment.transform.parent.gameObject.AddComponent<UIModeSwitchMotion>();
        motion.Initialize(buildSegment, spaceSegment);

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

    public void SpaceClear()
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
        float total = TotalPrice(interaction);

        if (_stats.partCountText != null)
            _stats.partCountText.text = count == 1 ? "1 piece" : $"{count} pieces";
        if (_stats.priceText != null)
            _stats.priceText.text = "$" + total.ToString("N0");
    }

    /// <summary>
    /// What the space currently costs: every placed piece, plus the merge
    /// correction. Merged pieces are priced as ONE structure — hidden shared
    /// or covered parts are deducted, and catalogue pieces spawned by beam
    /// splits and panel divisions are added.
    ///
    /// Public and static because a saved Lite project stores this figure, and
    /// a second copy of the formula in the projects panel would disagree with
    /// the price pill the moment two blocks touched.
    /// </summary>
    public static float TotalPrice(SpaceInteractionController interaction)
    {
        if (interaction == null)
            return 0f;

        float total = 0f;
        foreach (SpaceInstance inst in interaction.Instances)
            total += inst.price;

        return Mathf.Max(0f, total + SpaceMerge.PriceDelta);
    }
}
