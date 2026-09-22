using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "My Projects" — save the whole scene under a name, and open it again.
///
/// A project is not a block. A block is an item you place, over and over; a
/// project is the arrangement you are working on, so it opens INSTEAD of what
/// is on screen and is never placed into anything. <see cref="ProjectLibrary"/>
/// holds the store; this is only the panel.
///
/// Both modes can be saved, and the two save genuinely different documents —
/// Pro saves every frame and panel, Lite saves which block sits where — so
/// every record carries the mode that made it and opening one switches to
/// that mode first. That switch is not this class's business: the code goes
/// to <see cref="ConfigurationCodeUI.OpenCode"/>, which already knows how to
/// leave Space Mode for a piece code and enter it (and wait) for a space code.
///
/// The panel itself is BAKED by ConfiguratorUIBuilder — frame, fields,
/// buttons and the hidden row template. Nothing here builds UI; it clones the
/// template once per saved project and fills it in. That is deliberate: this
/// panel arrived after the cleanup that made the builder the single owner of
/// the interface, so it never had a second owner to inherit.
/// </summary>
public class ProjectPanelUI : MonoBehaviour
{
    public BuildController buildController;

    [Header("Baked by ConfiguratorUIBuilder")]
    public GameObject panel;
    public RectTransform listContent;
    public TMP_InputField nameInput;
    public TextMeshProUGUI emptyLabel;

    [Tooltip("Hidden prototype row, cloned once per saved project.")]
    public GameObject rowTemplate;

    readonly List<Texture2D> _thumbnails = new List<Texture2D>();
    readonly List<Behaviour> _suppressedCameraControls = new List<Behaviour>();

    SpaceInteractionController _space;
    SpaceInteractionController Space =>
        _space != null ? _space : _space = FindFirstObjectByType<SpaceInteractionController>();

    ConfigurationCodeUI _codeUI;
    ConfigurationCodeUI CodeUI =>
        _codeUI != null ? _codeUI : _codeUI = FindFirstObjectByType<ConfigurationCodeUI>();

    static ProjectLibrary.Mode CurrentMode =>
        SpaceModeController.Active ? ProjectLibrary.Mode.Lite : ProjectLibrary.Mode.Pro;

    void OnEnable() => UIThemeController.ThemeChanged += RestyleRows;

    void OnDisable() => UIThemeController.ThemeChanged -= RestyleRows;

    void OnDestroy() => ReleaseThumbnails();

    void Update()
    {
        if (panel != null && panel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            ClosePanel();

        SuppressCameraWhileTyping();
    }

    // ------------------------------------------------------------------
    // Panel
    // ------------------------------------------------------------------

    /// <summary>Wired to the rail's Projects button by the builder.</summary>
    public void TogglePanel()
    {
        if (panel == null)
            return;

        if (panel.activeSelf)
        {
            ClosePanel();
            return;
        }

        // Only one rail panel at a time — they share the slot beside the rail.
        UIChrome.CloseOtherRailPanels(panel.transform.parent, panel.name);
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        Refresh();
    }

    public void ClosePanel()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    /// <summary>
    /// Wired to "Open from code". The paste dialog already accepts both kinds
    /// of code and switches modes on its own, so a project someone sent you
    /// arrives through the same door as one of your own.
    /// </summary>
    public void OpenFromCode()
    {
        if (CodeUI != null)
            CodeUI.ShowLoadDialog();
    }

    /// <summary>
    /// Put the current work down and start empty.
    ///
    /// The first of the four guarded moments: this is the one place so far
    /// that can throw away a scene, and it asks first when there is anything
    /// to lose. An empty scene, or one that matches what is saved, goes
    /// without a question — there is nothing to warn about.
    /// </summary>
    public void StartNewProject()
    {
        CurrentProject.GuardThen(buildController, "Start a new project?",
            "Starting a new one clears the scene and cannot be undone.",
            "Discard and start", NewProject);
    }

    void NewProject()
    {
        // Mode-aware: the two modes hold different work in different places,
        // and each already knows how to empty itself.
        if (SpaceModeController.Active)
        {
            var space = FindFirstObjectByType<SpaceModeController>();
            if (space != null)
                space.SpaceClear();
        }
        else
        {
            var history = FindFirstObjectByType<BuildHistory>();
            if (history != null)
                history.ClearAll();
        }

        CurrentProject.Forget();
        ClosePanel();
        SelectionStatus.Set("New project · nothing saved yet.", 4f);
    }

    /// <summary>
    /// The camera controllers read raw keys (WASD/QE), so typing a project
    /// name would fly the camera around. Disable them while the field has
    /// focus and put them back afterwards.
    /// </summary>
    void SuppressCameraWhileTyping()
    {
        bool typing = nameInput != null && nameInput.isFocused;

        if (typing && _suppressedCameraControls.Count == 0)
        {
            var controllers = new Behaviour[]
            {
                FindFirstObjectByType<FlyCameraController>(),
                FindFirstObjectByType<CadCameraController>()
            };
            foreach (Behaviour b in controllers)
            {
                if (b != null && b.enabled)
                {
                    b.enabled = false;
                    _suppressedCameraControls.Add(b);
                }
            }
        }
        else if (!typing && _suppressedCameraControls.Count > 0)
        {
            foreach (Behaviour b in _suppressedCameraControls)
                if (b != null)
                    b.enabled = true;
            _suppressedCameraControls.Clear();
        }
    }

    // ------------------------------------------------------------------
    // Saving
    // ------------------------------------------------------------------

    /// <summary>Wired to "Save project".</summary>
    public void SaveNewProject()
    {
        var record = new ProjectLibrary.ProjectRecord
        {
            id = System.Guid.NewGuid().ToString("N"),
            createdUtc = ProjectLibrary.NowUtc()
        };

        string typed = nameInput != null ? nameInput.text.Trim() : string.Empty;
        record.name = string.IsNullOrEmpty(typed)
            ? $"Project {ProjectLibrary.LoadAll().Count + 1}"
            : typed;

        if (!CaptureInto(record))
            return;

        ProjectLibrary.Save(record);

        // Saving makes this the open project. Until now the app forgot which
        // project you were in the moment this panel closed, so there was
        // nothing to put a name to and nothing to warn about losing.
        CurrentProject.Set(record.id, record.name, record.code);

        // The name went into the saved project; leaving it in the box would
        // silently mislabel the next save.
        if (nameInput != null)
            nameInput.text = string.Empty;

        Refresh();
        SelectionStatus.Set($"Saved project \"{record.name}\".", 4f);
    }

    void OverwriteProject(ProjectLibrary.ProjectRecord record)
    {
        ProjectLibrary.Mode was = ProjectLibrary.ModeOf(record);
        if (was != CurrentMode)
        {
            SelectionStatus.Set(
                $"\"{record.name}\" is a {ProjectLibrary.NameOf(was)} project · " +
                $"switch back to {ProjectLibrary.NameOf(was)} before updating it.", 6f);
            return;
        }

        if (!CaptureInto(record))
            return;

        ProjectLibrary.Save(record);
        CurrentProject.Set(record.id, record.name, record.code);
        Refresh();
        SelectionStatus.Set($"Updated \"{record.name}\" to match the current scene.", 4f);
    }

    /// <summary>
    /// Fill a record from whatever is on screen right now: code, counts, size,
    /// price and a fresh thumbnail. False (with a status line) when there is
    /// nothing to save — checked BEFORE anything is written, so a mis-click
    /// never replaces a good project with an empty one.
    /// </summary>
    bool CaptureInto(ProjectLibrary.ProjectRecord record)
    {
        record.mode = CurrentMode.ToString();

        bool ok = CurrentMode == ProjectLibrary.Mode.Lite
            ? CaptureLite(record)
            : CapturePro(record);
        if (!ok)
            return false;

        record.modifiedUtc = ProjectLibrary.NowUtc();
        return true;
    }

    bool CapturePro(ProjectLibrary.ProjectRecord record)
    {
        ConfigurationModel model = ConfigurationCapture.Capture(buildController);
        if (model.Beams.Count == 0 && model.Panels.Count == 0)
        {
            SelectionStatus.Set("The grid is empty · build something before saving a project.", 4f);
            return false;
        }

        record.code = ConfigurationCode.Encode(model);
        record.beamCount = model.Beams.Count;
        record.panelCount = model.Panels.Count;
        record.blockCount = 0;

        var stats = FindFirstObjectByType<UIBuildStats>();
        record.price = stats != null ? stats.TotalPrice : 0f;

        bool hasBounds = StructureBounds.TryCompute(buildController, out StructureBounds.Info info);
        Bounds bounds = hasBounds ? info.WorldBounds : default;
        if (hasBounds)
        {
            record.widthMm = Mathf.RoundToInt(info.WidthMm);
            record.depthMm = Mathf.RoundToInt(info.DepthMm);
            record.heightMm = Mathf.RoundToInt(info.HeightMm);
        }

        StoreThumbnail(record, hasBounds, bounds);
        return true;
    }

    bool CaptureLite(ProjectLibrary.ProjectRecord record)
    {
        SpaceInteractionController space = Space;
        if (space == null || space.InstanceCount == 0)
        {
            SelectionStatus.Set("The scene is empty · place a block before saving a project.", 4f);
            return false;
        }

        List<SpaceHistory.InstanceState> states = space.CurrentStates();
        try
        {
            record.code = SpaceCodec.Encode(states);
        }
        catch (ConfigurationCodeException e)
        {
            SelectionStatus.Set(e.Message, 6f);
            return false;
        }

        record.blockCount = states.Count;
        record.beamCount = 0;
        record.panelCount = 0;

        // The same figure the price pill shows, merge correction included.
        record.price = SpaceModeController.TotalPrice(space);

        bool hasBounds = TryComputeSpaceBounds(space, out Bounds bounds);
        if (hasBounds)
        {
            float toMm = NeospaceUnits.MetersToMm;
            record.widthMm = Mathf.RoundToInt(bounds.size.x * toMm);
            record.depthMm = Mathf.RoundToInt(bounds.size.z * toMm);
            record.heightMm = Mathf.RoundToInt(bounds.size.y * toMm);
        }

        StoreThumbnail(record, hasBounds, bounds);
        return true;
    }

    /// <summary>
    /// Bounds of everything placed in Lite mode. StructureBounds cannot do
    /// this: placed blocks are frozen, renderer-only clones with their beam
    /// scripts stripped, so there is nothing for it to find.
    /// </summary>
    static bool TryComputeSpaceBounds(SpaceInteractionController space, out Bounds bounds)
    {
        bounds = default;
        bool any = false;

        foreach (SpaceInstance inst in space.Instances)
        {
            if (inst == null)
                continue;

            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>())
            {
                if (r == null || !r.enabled)
                    continue;

                if (!any)
                {
                    bounds = r.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }
        }

        return any;
    }

    void StoreThumbnail(ProjectLibrary.ProjectRecord record, bool hasBounds, Bounds bounds)
    {
        Camera cam = buildController != null && buildController.cam != null
            ? buildController.cam
            : Camera.main;

        Texture2D thumb = hasBounds
            ? ThumbnailCapture.Framed(cam, bounds)
            : ThumbnailCapture.Plain(cam);
        if (thumb == null)
            return;

        ProjectLibrary.SaveThumbnail(record.id, thumb);
        record.hasThumbnail = true;
        Destroy(thumb);
    }

    // ------------------------------------------------------------------
    // Opening and deleting
    // ------------------------------------------------------------------

    void OpenProject(ProjectLibrary.ProjectRecord record)
    {
        if (CodeUI == null)
        {
            SelectionStatus.Set("Project loading is not available in this scene.", 5f);
            return;
        }

        // The second guarded moment. Opening replaces what is on screen, so
        // it asks when that would lose something — and reopening the project
        // you are already in, unchanged, asks nothing.
        CurrentProject.GuardThen(buildController, $"Open \"{record.name}\"?",
            "Opening it replaces what is on screen.",
            "Discard and open",
            () =>
            {
                if (!CodeUI.OpenCode(record.code, record.name))
                    return;

                CurrentProject.Set(record.id, record.name, record.code);
                ClosePanel();
            });
    }

    void DeleteProject(ProjectLibrary.ProjectRecord record)
    {
        ProjectLibrary.Delete(record.id);

        // What is on screen is still there and still yours; it just has no
        // project behind it any more. Forgetting rather than clearing is the
        // difference between "this is unsaved" and "this is gone".
        if (CurrentProject.Id == record.id)
            CurrentProject.Forget();

        Refresh();
        SelectionStatus.Set($"Deleted project \"{record.name}\".", 4f);
    }

    void ShowProjectCode(ProjectLibrary.ProjectRecord record)
    {
        // A centred window holding the code in a selectable box: silent
        // clipboard writes are blocked in WebGL builds.
        if (CodeUI != null)
            CodeUI.ShowShareDialog($"Code for \"{record.name}\"", record.code);
    }

    // ------------------------------------------------------------------
    // Rows
    // ------------------------------------------------------------------

    public void Refresh()
    {
        if (listContent == null || rowTemplate == null)
            return;

        ReleaseThumbnails();
        for (int i = listContent.childCount - 1; i >= 0; i--)
            Destroy(listContent.GetChild(i).gameObject);

        List<ProjectLibrary.ProjectRecord> records = ProjectLibrary.LoadAll();
        if (emptyLabel != null)
            emptyLabel.gameObject.SetActive(records.Count == 0);

        foreach (ProjectLibrary.ProjectRecord record in records)
            BuildRow(record);
    }

    void BuildRow(ProjectLibrary.ProjectRecord record)
    {
        GameObject go = Instantiate(rowTemplate, listContent);
        go.name = "Project_" + record.id;

        // The template is baked inactive; a clone's Awake — which is where
        // UIConfirmingButton attaches its own click handler — does not run
        // until it is switched on, so activate before wiring anything.
        go.SetActive(true);

        Transform row = go.transform;

        var name = Find<TextMeshProUGUI>(row, "Name");
        if (name != null)
            name.text = record.name;

        // Two badges: which kind of project, and what it costs. The part
        // count and dimensions used to sit here too — four facts where
        // choosing between projects needs one or two.
        var mode = Find<TextMeshProUGUI>(row, "Badge_Mode/Text");
        if (mode != null)
            mode.text = ProjectLibrary.NameOf(ProjectLibrary.ModeOf(record));

        var cost = Find<TextMeshProUGUI>(row, "Badge_Cost/Text");
        if (cost != null)
            cost.text = record.price > 0f ? $"A${record.price:N0}" : "—";

        // The mode is a badge below the name now, not a line of prose.
        var thumb = Find<RawImage>(row, "Thumb");
        if (thumb != null)
        {
            Texture2D tex = record.hasThumbnail ? ProjectLibrary.LoadThumbnail(record.id) : null;
            if (tex != null)
            {
                thumb.texture = tex;
                thumb.color = Color.white;
                _thumbnails.Add(tex);
            }
            else
            {
                Color ink = UIThemeController.InkColor;
                thumb.color = new Color(ink.r, ink.g, ink.b, 0.08f);
            }
        }

        Wire(row, "Btn_Code", () => ShowProjectCode(record));
        Wire(row, "Btn_Open", () => OpenProject(record));
        Confirm(row, "Btn_Update", () => OverwriteProject(record));
        Confirm(row, "Btn_Delete", () => DeleteProject(record));
    }

    static void Wire(Transform row, string path, UnityEngine.Events.UnityAction action)
    {
        Transform t = row.Find(path);
        if (t != null && t.TryGetComponent(out Button button))
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
    }

    /// <summary>
    /// A row button whose action is destructive enough to want a second
    /// click. The baked template carries the UIConfirmingButton; this only
    /// says what to do once it is confirmed.
    /// </summary>
    static void Confirm(Transform row, string path, UnityEngine.Events.UnityAction action)
    {
        Transform t = row.Find(path);
        if (t != null && t.TryGetComponent(out UIConfirmingButton confirming))
        {
            confirming.onConfirmed.RemoveAllListeners();
            confirming.onConfirmed.AddListener(action);
        }
    }

    static T Find<T>(Transform row, string path) where T : Component
    {
        Transform t = row.Find(path);
        return t != null ? t.GetComponent<T>() : null;
    }

    /// <summary>
    /// Rows are clones, so the theme controller has never heard of them.
    /// Repaint the parts whose colour it owns when the palette changes.
    /// </summary>
    void RestyleRows()
    {
        if (listContent == null)
            return;

        foreach (Transform row in listContent)
        {
            if (row.TryGetComponent(out Image bg))
                bg.color = UIThemeController.SurfaceColor;

            var name = Find<TextMeshProUGUI>(row, "Name");
            if (name != null)
                name.color = UIThemeController.InkColor;

            foreach (string badge in new[] { "Badge_Mode/Text", "Badge_Cost/Text" })
            {
                var text = Find<TextMeshProUGUI>(row, badge);
                if (text != null)
                    text.color = UIThemeController.InkColor;
            }

            Tint(row, "Btn_Code", UIThemeController.SurfaceColor, UIThemeController.InkColor);
            Tint(row, "Btn_Open", UIThemeController.AccentColor, Color.white);
            Tint(row, "Btn_Update", UIThemeController.SurfaceColor, UIThemeController.InkColor);
            Tint(row, "Btn_Delete", UIThemeController.SurfaceColor, UIThemeController.DangerColor);
        }
    }

    static void Tint(Transform row, string path, Color background, Color foreground)
    {
        Transform t = row.Find(path);
        if (t == null)
            return;

        if (t.TryGetComponent(out Image img))
            img.color = background;

        var label = t.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
            label.color = foreground;
    }

    void ReleaseThumbnails()
    {
        foreach (Texture2D tex in _thumbnails)
            if (tex != null)
                Destroy(tex);
        _thumbnails.Clear();
    }
}
