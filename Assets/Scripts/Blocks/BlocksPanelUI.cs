using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The Blocks tab: collections down the left, the blocks in the selected one
/// as cards on the right.
///
/// One page serves both modes. What differs is only what you may DO here —
/// capturing a block from the scene is a Pro action, because Lite has no
/// editable modules to pick from — so the Add card is offered in Pro and not
/// in Lite. Two copies of the layout to express that would be two things to
/// keep in step.
///
/// The layout is baked by ConfiguratorUIBuilder, templates included; this
/// clones them and fills them in. It builds no UI.
///
/// Capturing, renaming and deleting are not wired yet — they are the steps
/// after this one. The Add card is present and inert on purpose: the layout
/// is worth seeing and judging before behaviour hangs off it.
/// </summary>
public class BlocksPanelUI : MonoBehaviour
{
    [Header("Baked by ConfiguratorUIBuilder")]
    public RectTransform collectionList;
    public RectTransform gallery;
    public TextMeshProUGUI emptyLabel;

    /// <summary>
    /// Every control this resolves BY PATH on a cloned block card.
    ///
    /// Declared here, and read by UIWiringCheck, because the two spelled them
    /// separately once and disagreed: the builder put the pencil inside the
    /// thumbnail, the check asserted "Thumb/Btn_EditBlock" and passed, and
    /// this class looked for "Btn_EditBlock" and silently found nothing. The
    /// card rendered perfectly and the pencil did nothing at all.
    ///
    /// A check that spells the path itself is only testing the builder. It
    /// has to test the path the RUNTIME uses, which means one list.
    /// </summary>
    public static readonly string[] CardControls =
    {
        "Thumb", "Thumb/Image",
        "Actions", "Actions/Btn_EditBlock",
        "Actions/Btn_EditBlock/Icon_Edit", "Actions/Btn_EditBlock/Icon_Done",
        "Actions/Btn_Replace", "Actions/Btn_Share", "Actions/Btn_Delete",
        // The label, not just the button that holds it — asserting only
        // "Name" passed while the runtime looked up a path that did not
        // exist, and every card showed the template's baked text forever.
        "Name", "Name/Label", "Price", "Size", "NameEdit",
    };

    [Header("Templates")]
    public GameObject parentHeaderTemplate;
    public GameObject collectionRowTemplate;
    public GameObject addCollectionTemplate;
    public GameObject blockCardTemplate;
    public GameObject addCardTemplate;

    readonly List<Texture2D> _thumbnails = new List<Texture2D>();
    readonly Dictionary<string, Image> _rowBackgrounds = new Dictionary<string, Image>();

    /// <summary>Which collection row sits where, for drag-and-drop to hit.</summary>
    readonly Dictionary<string, RectTransform> _rowRects = new Dictionary<string, RectTransform>();

    string _selectedId;

    /// <summary>
    /// The parent group currently being organised, if any. Adding, renaming
    /// and deleting all live inside this one mode, because they are one
    /// activity; outside it a collection row does exactly one thing, which is
    /// open its gallery.
    /// </summary>
    BlockCollections.Parent? _editingParent;

    /// <summary>The collection whose name is being typed, if any.</summary>
    string _renamingId;

    /// <summary>
    /// The card whose name field should take the keyboard on the next
    /// rebuild — the one just opened, or just captured. Only then: grabbing
    /// focus on every rebuild would steal it back from wherever the user had
    /// moved on to.
    /// </summary>
    string _focusBlockId = string.Empty;

    /// <summary>
    /// The block currently being edited, if any. Per card rather than per
    /// gallery: you organise one block at a time, and a mode covering the
    /// whole shelf would stop you placing any of them.
    /// </summary>
    string _editingBlockId;

    /// <summary>
    /// The card whose action column should ANIMATE on the next rebuild —
    /// the one whose toggle was just pressed. Every other card snaps, so a
    /// gallery refresh does not replay an open card's opening.
    /// </summary>
    string _animateBlockId = string.Empty;

    string _draggingBlockId;

    /// <summary>Lite has no editable modules, so a block cannot be captured there.</summary>
    static bool CanCapture => !SpaceModeController.Active;

    /// <summary>
    /// Whether this person may reorganise NEOSPACE's Collection as well as
    /// their own. There are no accounts in this project yet, so it is true —
    /// but it is ONE named gate rather than the check being absent, so wiring
    /// it to a real sign-in later is a single line here instead of a hunt
    /// through the panel for every place the distinction should have been.
    /// It is not security and does not pretend to be: nothing server-side
    /// enforces it.
    /// </summary>
    static bool IsAdmin => true;

    static bool MayEdit(BlockCollections.Parent parent) =>
        parent == BlockCollections.Parent.Mine || IsAdmin;

    void OnEnable()
    {
        SpaceModeController.ModeChanged += OnModeChanged;
        Refresh();
    }

    void OnDisable()
    {
        SpaceModeController.ModeChanged -= OnModeChanged;
        ReleaseThumbnails();
    }

    void OnModeChanged(bool _) => Refresh();

    // ------------------------------------------------------------------
    // Collections
    // ------------------------------------------------------------------

    public void Refresh()
    {
        if (collectionList == null || gallery == null)
            return;

        RebuildCollections();
        RebuildGallery();
    }

    void RebuildCollections()
    {
        Clear(collectionList);
        _rowBackgrounds.Clear();
        _rowRects.Clear();

        AddParent(BlockCollections.Parent.Neospace);
        AddParent(BlockCollections.Parent.Mine);

        // Land somewhere sensible on first open rather than showing a chosen
        // collection's worth of nothing with no collection chosen.
        if (string.IsNullOrEmpty(_selectedId) || !_rowBackgrounds.ContainsKey(_selectedId))
        {
            _selectedId = BlockCollections.DefaultMineId;
            HighlightSelection();
        }
    }

    void AddParent(BlockCollections.Parent parent)
    {
        if (parentHeaderTemplate == null || collectionRowTemplate == null)
            return;

        GameObject header = Clone(parentHeaderTemplate, collectionList);
        var label = Find<TextMeshProUGUI>(header.transform, "Label");
        if (label != null)
            label.text = BlockCollections.LabelOf(parent).ToUpperInvariant();

        BlockCollections.Parent captured = parent;
        bool mayEdit = MayEdit(parent);
        bool editing = _editingParent == parent;
        string status = EditingStatus(parent);

        Wire(header.transform, "Btn_EditCollections", () => ToggleEdit(captured), mayEdit);

        // The toggle shows what pressing it does NEXT: a pencil to start
        // organising, a cross to stop.
        Show(header.transform, "Btn_EditCollections/Icon_Edit", !editing);
        Show(header.transform, "Btn_EditCollections/Icon_Done", editing);
        Hover(header.transform, "Btn_EditCollections",
              editing ? "Finish editing?" : "Edit collections?",
              editing ? status : null);

        foreach (BlockCollections.Collection collection in BlockCollections.In(parent))
        {
            GameObject row = Clone(collectionRowTemplate, collectionList);
            row.name = "Collection_" + collection.id;
            string id = collection.id;

            var rowLabel = Find<TextMeshProUGUI>(row.transform, "Label");
            if (rowLabel != null)
                rowLabel.text = collection.name;

            int held = BlockCollections.BlocksIn(id).Count;
            var count = Find<TextMeshProUGUI>(row.transform, "Count");
            if (count != null)
                count.text = held.ToString("00");

            if (row.TryGetComponent(out Image background))
                _rowBackgrounds[id] = background;
            _rowRects[id] = (RectTransform)row.transform;

            // In edit mode the row's red bin appears and the label slides
            // clear of it, so the shift itself says the mode is on.
            Transform remove = row.transform.Find("Btn_Remove");
            bool removable = editing && id != BlockCollections.DefaultMineId;
            if (remove != null)
            {
                remove.gameObject.SetActive(removable);
                if (removable && remove.TryGetComponent(out Button removeButton))
                {
                    removeButton.onClick.RemoveAllListeners();
                    removeButton.onClick.AddListener(() => AskRemoveCollection(collection, held));
                    Hover(row.transform, "Btn_Remove", "Remove a collection?", status);
                }
            }
            if (rowLabel != null)
                rowLabel.rectTransform.offsetMin = new Vector2(editing ? 34f : 12f, 0f);

            // OUTSIDE edit mode a row opens its gallery and does nothing
            // else. Rename used to hang off this same click, which meant
            // going to look at a collection dropped you into a text field you
            // never asked for. Inside the mode the click renames instead,
            // because that is the only reason to be in it.
            if (row.TryGetComponent(out Button button))
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(editing
                    ? (UnityEngine.Events.UnityAction)(() => StartRename(id))
                    : () => Select(id));
            }

            var rename = Find<TMP_InputField>(row.transform, "NameEdit");
            if (rename != null)
            {
                bool typing = _renamingId == id;
                rename.gameObject.SetActive(typing);
                if (rowLabel != null)
                    rowLabel.gameObject.SetActive(!typing);
                if (typing)
                    BeginRename(rename, collection);
            }
        }

        // The empty slot at the end of the group: the place a collection
        // appears is the place you made it.
        if (editing && addCollectionTemplate != null)
        {
            GameObject add = Clone(addCollectionTemplate, collectionList);
            if (add.TryGetComponent(out Button addButton))
            {
                addButton.onClick.RemoveAllListeners();
                addButton.onClick.AddListener(() => AddCollection(captured));
            }
            if (add.TryGetComponent(out UIHoverStatus addHover))
                addHover.baseStatus = status;
        }
    }

    /// <summary>
    /// The one line the pill shows while a group is being organised. Short on
    /// purpose: it is a state, not an instruction. What each control does is
    /// answered by hovering it, where the question is actually being asked,
    /// rather than by a sentence listing every control up front.
    /// </summary>
    static string EditingStatus(BlockCollections.Parent parent) =>
        "Editing " + BlockCollections.LabelOf(parent);

    static void Show(Transform parent, string path, bool shown)
    {
        Transform t = parent.Find(path);
        if (t != null && t.gameObject.activeSelf != shown)
            t.gameObject.SetActive(shown);
    }

    static void Hover(Transform parent, string path, string hint, string baseStatus)
    {
        Transform t = parent.Find(path);
        if (t != null && t.TryGetComponent(out UIHoverStatus hover))
        {
            hover.hint = hint;
            hover.baseStatus = baseStatus;
        }
    }

    static void Wire(Transform parent, string path, UnityEngine.Events.UnityAction action, bool enabled)
    {
        Transform t = parent.Find(path);
        if (t == null || !t.TryGetComponent(out Button button))
            return;

        button.gameObject.SetActive(enabled);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    // ------------------------------------------------------------------
    // Edit mode
    // ------------------------------------------------------------------

    void ToggleEdit(BlockCollections.Parent parent)
    {
        _editingParent = _editingParent == parent ? (BlockCollections.Parent?)null : parent;
        _renamingId = null;
        RebuildCollections();
        HighlightSelection();

        // Sticky while the mode is on — it is a state, not an announcement —
        // and cleared outright when it ends rather than replaced with a
        // message about something no longer happening.
        if (_editingParent == parent)
            SelectionStatus.Set(EditingStatus(parent), 0f);
        else
            SelectionStatus.Clear();
    }

    void AskRemoveCollection(BlockCollections.Collection collection, int held)
    {
        var dialog = FindFirstObjectByType<UIConfirmDialog>();

        // Empty is not a question worth asking — there is nothing to lose and
        // adding one back is a single click.
        if (held == 0)
        {
            RemoveCollection(collection);
            return;
        }

        if (dialog == null)
        {
            SelectionStatus.Set($"\"{collection.name}\" still holds {held} block"
                                + $"{(held == 1 ? "" : "s")} · empty it first.", 6f);
            return;
        }

        dialog.Ask(
            $"Delete \"{collection.name}\"?",
            $"It holds {held} block{(held == 1 ? "" : "s")}, and "
            + (held == 1 ? "it will be deleted with it." : "they will be deleted with it.")
            + " This cannot be undone.",
            "Delete both",
            () => RemoveCollection(collection));
    }

    void RemoveCollection(BlockCollections.Collection collection)
    {
        // The blocks go too. The dialog said so; leaving them behind filed
        // under a collection that no longer exists would strand them where
        // nothing can show them.
        List<PieceLibrary.PieceRecord> held = BlockCollections.BlocksIn(collection.id);
        foreach (PieceLibrary.PieceRecord record in held)
            PieceLibrary.Delete(record.id);

        if (!BlockCollections.Delete(collection.id, out string refusal, force: true))
        {
            SelectionStatus.Set(refusal, 6f);
            return;
        }

        if (_selectedId == collection.id)
            _selectedId = BlockCollections.DefaultMineId;

        Refresh();
        SelectionStatus.Set(held.Count == 0
            ? $"Deleted \"{collection.name}\"."
            : $"Deleted \"{collection.name}\" and {held.Count} block{(held.Count == 1 ? "" : "s")}.", 5f);
    }

    // ------------------------------------------------------------------
    // Renaming
    // ------------------------------------------------------------------

    void StartRename(string collectionId)
    {
        _renamingId = collectionId;
        RebuildCollections();
        HighlightSelection();
    }

    /// <summary>
    /// Hand the field the current name, selected, and let Enter or clicking
    /// away commit it. Selected rather than empty so the old name is there to
    /// keep if you change your mind, and gone the moment you type.
    /// </summary>
    void BeginRename(TMP_InputField field, BlockCollections.Collection collection)
    {
        string id = collection.id;

        field.onSubmit.RemoveAllListeners();
        field.onDeselect.RemoveAllListeners();
        field.text = collection.name;

        field.onSubmit.AddListener(value => CommitRename(id, value));
        field.onDeselect.AddListener(value => CommitRename(id, value));

        field.Select();
        field.ActivateInputField();
        field.selectionAnchorPosition = 0;
        field.selectionFocusPosition = field.text.Length;
    }

    void ToggleBlockEdit(string blockId)
    {
        bool leaving = _editingBlockId == blockId;

        // Leaving takes whatever is in the field with it. The field is open
        // for the whole of edit mode now, so it may well hold something typed
        // and never submitted — discarding that because Enter was not pressed
        // would be losing work the user can see on screen.
        if (leaving)
            CommitOpenRename(blockId);

        _editingBlockId = leaving ? null : blockId;
        _focusBlockId = leaving ? string.Empty : blockId;
        SelectionStatus.Clear();

        // Only this card animates; the rest of the gallery is being rebuilt
        // for bookkeeping and should just be in the right state already.
        _animateBlockId = blockId;
        RebuildGallery();
        _animateBlockId = string.Empty;
        _focusBlockId = string.Empty;
    }

    /// <summary>Save whatever the open field holds for this card.</summary>
    void CommitOpenRename(string blockId)
    {
        if (gallery == null)
            return;

        Transform card = gallery.Find("Block_" + blockId);
        var field = card != null ? Find<TMP_InputField>(card, "NameEdit") : null;
        if (field != null)
            SaveBlockName(blockId, field.text);
    }

    /// <summary>
    /// Point at another module and make this block that instead. Keeps the
    /// name, the collection and the id — this is the same block, redrawn, so
    /// anything already placed from it and anyone holding its code still
    /// refers to something that exists.
    /// </summary>
    void ReplaceBlock(PieceLibrary.PieceRecord record)
    {
        if (!CanCapture)
        {
            SelectionStatus.Set(
                $"Modules are only in {UIChrome.ProLabel} · switch over to replace a block.", 6f);
            return;
        }

        var build = FindFirstObjectByType<BuildController>();
        ModulePickSession.GetOrCreate(build).Arm(module =>
        {
            ConfigurationModel model = ConfigurationCapture.Capture(build, module);
            if (model.Beams.Count == 0 && model.Panels.Count == 0)
            {
                SelectionStatus.Set("That module could not be read · nothing changed.", 6f);
                return;
            }

            record.code = ConfigurationCode.Encode(model);
            record.beamCount = model.Beams.Count;
            record.panelCount = model.Panels.Count;
            record.widthMm = module.WidthMm;
            record.depthMm = module.DepthMm;
            record.heightMm = module.HeightMm;
            record.price = PriceOf(module);
            record.modifiedUtc = PieceLibrary.NowUtc();

            Camera cam = build != null && build.cam != null ? build.cam : Camera.main;
            Texture2D shot = module.HasBounds
                ? ThumbnailCapture.FramedIsolated(cam, module.WorldBounds, module.Roots)
                : ThumbnailCapture.Plain(cam);
            if (shot != null)
            {
                PieceLibrary.SaveThumbnail(record.id, shot);
                record.hasThumbnail = true;
                Destroy(shot);
            }

            PieceLibrary.Save(record);
            _editingBlockId = null;
            Refresh();
            SelectionStatus.Set($"\"{record.name}\" now matches that module.", 5f);
        });
    }

    /// <summary>
    /// Show the block's code in a window you can copy from. Not a silent
    /// clipboard write: WebGL browsers refuse those, and a button that
    /// claims to have copied something and has not is worse than no button.
    /// </summary>
    void ShareBlock(PieceLibrary.PieceRecord record)
    {
        var codeUI = FindFirstObjectByType<ConfigurationCodeUI>();
        if (codeUI == null)
        {
            SelectionStatus.Set("Code sharing is not available in this scene.", 5f);
            return;
        }

        codeUI.ShowShareDialog($"Code for \"{record.name}\"", record.code);
    }


    /// <summary>
    /// Place the block into the scene. The two modes place genuinely
    /// different things, which is the owner's rule, not an implementation
    /// detail:
    ///
    ///   Lite  a sealed clone. Frames and panels are not selectable; the
    ///         block moves and prices as one object.
    ///   Pro   real parts, merged into the build. Every frame and panel is
    ///         then editable, and tools can connect more to them.
    /// </summary>
    void PlaceBlock(PieceLibrary.PieceRecord record)
    {
        ActiveInteraction.Exit();

        if (SpaceModeController.Active)
        {
            var interaction = FindFirstObjectByType<SpaceInteractionController>();
            if (interaction == null)
            {
                SelectionStatus.Set("Block placement is not available in this scene.", 5f);
                return;
            }

            interaction.ArmPlacement(record);
            return;
        }

        StampBlockIntoBuild(record);
    }

    /// <summary>
    /// Pro placement: hand the block's parts to the clipboard and let it
    /// stamp them.
    ///
    /// Pro needed no placement code of its own. What it wants from a block —
    /// real parts, merged into the build, editable afterwards — is precisely
    /// what a clipboard stamp already does: a stamped post landing inside an
    /// existing beam splits it into catalogue pieces, a part with no valid
    /// split there is refused rather than forced, and panels retry while the
    /// slots settle. Reimplementing that for blocks would have been a second
    /// set of merge rules to disagree with the first.
    /// </summary>
    void StampBlockIntoBuild(PieceLibrary.PieceRecord record)
    {
        ConfigurationCodeValidation check = ConfigurationCode.Validate(record.code);
        if (!check.IsValid)
        {
            SelectionStatus.Set($"\"{record.name}\" cannot be placed: {check.Error}", 7f);
            return;
        }

        var clipboard = FindFirstObjectByType<StructureClipboard>();
        if (clipboard == null)
        {
            SelectionStatus.Set("Block placement is not available in this scene.", 5f);
            return;
        }

        int loaded = clipboard.LoadFromModel(check.Model, record.name, out int skipped);
        if (loaded == 0)
        {
            SelectionStatus.Set($"\"{record.name}\" has nothing this version can place.", 6f);
            return;
        }

        clipboard.ShowBlockGhost(record.id, record.code);

        string note = skipped > 0
            ? $" {skipped} part{(skipped == 1 ? "" : "s")} this version does not know were left out."
            : string.Empty;
        SelectionStatus.Set(
            $"Placing \"{record.name}\" · click the floor to place it. "
            + $"R rotates, Esc puts it down.{note}", 0f);
    }

    void AskRemoveBlock(PieceLibrary.PieceRecord record)
    {
        var dialog = FindFirstObjectByType<UIConfirmDialog>();
        if (dialog == null)
        {
            RemoveBlock(record);
            return;
        }

        dialog.Ask(
            $"Delete \"{record.name}\"?",
            "The block is removed from your library. Anything already placed "
            + "in a scene stays where it is. This cannot be undone.",
            "Delete",
            () => RemoveBlock(record));
    }

    void RemoveBlock(PieceLibrary.PieceRecord record)
    {
        PieceLibrary.Delete(record.id);
        if (_editingBlockId == record.id)
            _editingBlockId = null;
        Refresh();
        SelectionStatus.Set($"Deleted \"{record.name}\".", 4f);
    }

    /// <summary>
    /// Hand the field the block's name and keep it there for the whole of
    /// edit mode. Enter saves without closing anything: the field is the
    /// card's name while editing, not a dialog that has to be dismissed.
    ///
    /// <paramref name="focus"/> only on the rebuild that opened the card. The
    /// field survives every later rebuild, and re-selecting it each time
    /// would snatch the keyboard back from wherever the user had gone.
    /// </summary>
    void BeginBlockRename(TMP_InputField field, PieceLibrary.PieceRecord record, bool focus)
    {
        string id = record.id;

        field.onSubmit.RemoveAllListeners();
        field.onDeselect.RemoveAllListeners();

        // Only while it is not being typed in — assigning text to a focused
        // field would undo the keystroke that triggered the rebuild.
        if (!field.isFocused)
            field.text = record.name;

        field.onSubmit.AddListener(value => SaveBlockName(id, value));
        field.onDeselect.AddListener(value => SaveBlockName(id, value));

        if (!focus)
            return;

        field.Select();
        field.ActivateInputField();
        field.selectionAnchorPosition = 0;
        field.selectionFocusPosition = field.text.Length;
    }

    /// <summary>
    /// Write a new name, if it is one. Blank keeps the old name rather than
    /// leaving a card nobody can identify — the same rule as a collection.
    /// No rebuild: the label it would refresh is hidden behind the field
    /// anyway, and rebuilding mid-edit would destroy the field being typed in.
    /// </summary>
    void SaveBlockName(string blockId, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        foreach (PieceLibrary.PieceRecord record in PieceLibrary.LoadAll())
        {
            if (record.id != blockId)
                continue;
            if (record.name == value.Trim())
                return;

            record.name = value.Trim();
            record.modifiedUtc = PieceLibrary.NowUtc();
            PieceLibrary.Save(record);
            return;
        }
    }

    void CommitRename(string collectionId, string value)
    {
        if (_renamingId != collectionId)
            return;

        _renamingId = null;

        // A blank name is not a name; keep the old one rather than leaving a
        // row nobody can identify.
        if (!string.IsNullOrWhiteSpace(value))
            BlockCollections.Rename(collectionId, value);

        RebuildCollections();
        HighlightSelection();
    }

    void Select(string collectionId)
    {
        _selectedId = collectionId;
        HighlightSelection();
        RebuildGallery();
    }

    void AddCollection(BlockCollections.Parent parent)
    {
        BlockCollections.Collection created = BlockCollections.Add(parent, null);
        _selectedId = created.id;
        // Stay in edit mode: you have just made one, you may well make another.

        // Opens straight into its name, selected. Naming the thing you just
        // made is the next thing you were going to do, and "New collection"
        // is nobody's idea of a name.
        _renamingId = created.id;

        Refresh();
        SelectionStatus.Set(EditingStatus(parent) + " · name it, Enter to keep", 0f);
    }

    /// <summary>
    /// Mockup: the chosen collection is a filled rounded block, the rest are
    /// transparent. The hover outline is suppressed on the chosen one — its
    /// fill already says it is chosen, the same rule the dock tabs follow.
    /// </summary>
    void HighlightSelection()
    {
        foreach (KeyValuePair<string, Image> entry in _rowBackgrounds)
        {
            if (entry.Value == null)
                continue;

            bool active = entry.Key == _selectedId;
            entry.Value.color = active
                ? new Color(1f, 1f, 1f, 0.10f)
                : Color.clear;

            var label = Find<TextMeshProUGUI>(entry.Value.transform, "Label");
            if (label != null)
                label.color = active ? Color.white : UIChrome.DockText;

            if (entry.Value.TryGetComponent(out UIHoverReveal hover))
                hover.Suppressed = active;
        }
    }

    // ------------------------------------------------------------------
    // Blocks
    // ------------------------------------------------------------------

    void RebuildGallery()
    {
        ReleaseThumbnails();
        Clear(gallery);

        List<PieceLibrary.PieceRecord> blocks = BlockCollections.BlocksIn(_selectedId);

        // The Add card leads the row, so the way to make a block is where the
        // eye already is rather than past everything already made.
        if (CanCapture && addCardTemplate != null)
        {
            GameObject add = Clone(addCardTemplate, gallery);
            if (add.TryGetComponent(out Button addButton))
            {
                addButton.onClick.RemoveAllListeners();
                addButton.onClick.AddListener(BeginCapture);
            }
        }

        foreach (PieceLibrary.PieceRecord record in blocks)
            BuildCard(record);

        if (emptyLabel != null)
        {
            bool empty = blocks.Count == 0;
            emptyLabel.gameObject.SetActive(empty && !CanCapture);
            if (empty && !CanCapture)
                emptyLabel.text = "No blocks in this collection yet · "
                                  + $"switch to {UIChrome.ProLabel} to add one from your scene.";
        }
    }

    void BuildCard(PieceLibrary.PieceRecord record)
    {
        if (blockCardTemplate == null)
            return;

        GameObject card = Clone(blockCardTemplate, gallery);
        card.name = "Block_" + record.id;

        string id = record.id;
        bool editing = _editingBlockId == id;
        string status = "Editing " + record.name;

        // "Name/Label", not "Name". The name became a Button when clicking it
        // started a rename, and a SolidButton keeps its text in a child. The
        // lookup kept the old path, found nothing, and every card went on
        // showing the words baked into the template — so a rename saved
        // correctly and then appeared to have done nothing at all.
        var name = Find<TextMeshProUGUI>(card.transform, "Name/Label");
        if (name != null)
            name.text = record.name;

        // The pencil becomes a cross while editing, as the collections header
        // does — the toggle says what pressing it does next.
        Show(card.transform, "Actions/Btn_EditBlock/Icon_Edit", !editing);
        Show(card.transform, "Actions/Btn_EditBlock/Icon_Done", editing);
        Hover(card.transform, "Actions/Btn_EditBlock",
              editing ? "Finish editing?" : "Edit this block?",
              editing ? status : null);

        Wire(card.transform, "Actions/Btn_EditBlock", () => ToggleBlockEdit(id), true);
        Wire(card.transform, "Actions/Btn_Replace", () => ReplaceBlock(record), editing);
        Wire(card.transform, "Actions/Btn_Share", () => ShareBlock(record), editing);
        Wire(card.transform, "Actions/Btn_Delete", () => AskRemoveBlock(record), editing);

        foreach (string path in new[] { "Actions/Btn_Replace", "Actions/Btn_Share", "Actions/Btn_Delete" })
            Hover(card.transform, path, null, editing ? status : null);

        // The column unfolds out of the toggle and folds back into it. Snap
        // rather than animate when the card is being rebuilt for some other
        // reason — a gallery refresh should not replay every open card's
        // opening.
        Transform actions = card.transform.Find("Actions");
        if (actions != null && actions.TryGetComponent(out UIActionStack stack))
        {
            stack.Expanded = editing;
            if (!_animateBlockId.Equals(id))
                stack.Snap();
        }

        // Clicking the card places the block — the thing you came here to do,
        // so it needs no button of its own. Refused while editing: a card you
        // are renaming should not also be dropping copies into the scene.
        if (card.TryGetComponent(out Button cardButton))
        {
            cardButton.onClick.RemoveAllListeners();
            if (!editing)
                cardButton.onClick.AddListener(() => PlaceBlock(record));
        }

        // The field IS the invitation. It used to appear only after clicking
        // the name, which meant nothing on screen said the name could be
        // changed at all — the one affordance was invisible until you had
        // already guessed it was there. It is present for as long as the card
        // is being edited, and goes when editing does.
        Transform nameHolder = card.transform.Find("Name");
        var rename = Find<TMP_InputField>(card.transform, "NameEdit");
        if (rename != null)
        {
            rename.gameObject.SetActive(editing);
            if (nameHolder != null)
                nameHolder.gameObject.SetActive(!editing);
            if (editing)
                BeginBlockRename(rename, record, focus: _focusBlockId == id);
        }

        var price = Find<TextMeshProUGUI>(card.transform, "Price");
        if (price != null)
            price.text = record.price > 0f ? $"A${record.price:N0}" : string.Empty;

        var size = Find<TextMeshProUGUI>(card.transform, "Size");
        if (size != null)
            size.text = $"{record.widthMm} × {record.depthMm} × {record.heightMm} mm";

        // Drag it onto a collection row to file it there.
        var drag = card.AddComponent<BlockCardDrag>();
        drag.panel = this;
        drag.blockId = record.id;

        var thumb = Find<RawImage>(card.transform, "Thumb/Image");
        if (thumb != null)
        {
            Texture2D tex = record.hasThumbnail ? PieceLibrary.LoadThumbnail(record.id) : null;
            if (tex != null)
            {
                thumb.texture = tex;
                thumb.color = Color.white;
                _thumbnails.Add(tex);
            }
            else
            {
                thumb.color = Color.clear;   // the card's own surface shows through
            }
        }
    }

    // ------------------------------------------------------------------
    // Capturing a module as a block
    // ------------------------------------------------------------------

    /// <summary>
    /// Wired to the Add a Block card: asks which way rather than assuming.
    /// Picking a module is Pro-only; a code works in either mode.
    /// </summary>
    public void BeginCapture()
    {
        var dialog = FindFirstObjectByType<UIAddBlockDialog>();
        if (dialog == null)
        {
            // No dialog in this scene: fall back to the route that exists.
            if (CanCapture)
                PickModule();
            return;
        }

        dialog.Show(CanCapture, PickModule, code => ImportCode(dialog, code));
    }

    void PickModule()
    {
        if (!CanCapture)
            return;

        var build = FindFirstObjectByType<BuildController>();
        ModulePickSession.GetOrCreate(build).Arm(CaptureModule);
    }

    /// <summary>
    /// Turn a pasted block code into a card.
    ///
    /// The size and the picture are MEASURED, not guessed: the code is built
    /// into a frozen group far off-camera by PieceInstanceFactory — the same
    /// machinery Lite uses to make a placeable copy — then photographed and
    /// measured there. A card whose picture and dimensions came from the
    /// actual thing is worth the round trip; one showing a blank square and
    /// "0 × 0 × 0 mm" would be worse than no card.
    /// </summary>
    void ImportCode(UIAddBlockDialog dialog, string code)
    {
        if (SpaceCodec.LooksLikeSpaceCode(code))
        {
            dialog.Note("That is a space code, which holds a whole scene · "
                        + "open it from My Projects instead.", true);
            return;
        }

        ConfigurationCodeValidation check = ConfigurationCode.Validate(code);
        if (!check.IsValid)
        {
            dialog.Note(check.Error, true);
            return;
        }

        string collectionId = string.IsNullOrEmpty(_selectedId)
            ? BlockCollections.DefaultMineId
            : _selectedId;

        foreach (PieceLibrary.PieceRecord existing in BlockCollections.BlocksIn(collectionId))
        {
            if (existing.code == code)
            {
                dialog.Note($"Already in this collection as \"{existing.name}\".", true);
                return;
            }
        }

        var factory = FindFirstObjectByType<PieceInstanceFactory>();
        if (factory == null)
        {
            dialog.Note("Block importing is not available in this scene.", true);
            return;
        }

        var record = new PieceLibrary.PieceRecord
        {
            id = System.Guid.NewGuid().ToString("N"),
            name = BlockCollections.NextUntitledName(collectionId),
            collectionId = collectionId,
            code = code,
            createdUtc = PieceLibrary.NowUtc(),
            modifiedUtc = PieceLibrary.NowUtc(),
            beamCount = check.Model.Beams.Count,
            panelCount = check.Model.Panels.Count,
            price = PriceOfModel(check.Model),
        };

        dialog.Note("Reading the code…", false);

        factory.GetMaster(record.id, code, master =>
        {
            if (master == null)
            {
                dialog.Note("That code could not be built.", true);
                return;
            }

            Bounds bounds = RendererBounds(master.transform);
            float toMm = NeospaceUnits.MetersToMm;
            record.widthMm = Mathf.RoundToInt(bounds.size.x * toMm);
            record.depthMm = Mathf.RoundToInt(bounds.size.z * toMm);
            record.heightMm = Mathf.RoundToInt(bounds.size.y * toMm);

            var build = FindFirstObjectByType<BuildController>();
            Camera cam = build != null && build.cam != null ? build.cam : Camera.main;
            Texture2D shot = ThumbnailCapture.FramedIsolated(
                cam, bounds, new[] { master.transform });
            if (shot != null)
            {
                PieceLibrary.SaveThumbnail(record.id, shot);
                record.hasThumbnail = true;
                Destroy(shot);
            }

            PieceLibrary.Save(record);
            dialog.Close();

            // Opens in edit mode, as a captured block does: an imported block
            // arrives called "Untitled Block" too, and naming it is still the
            // next thing you were going to do.
            _editingBlockId = record.id;
            _focusBlockId = record.id;
            Refresh();
            _focusBlockId = string.Empty;
            SelectionStatus.Set("Imported · name the block, Enter to keep it.", 0f);
        });
    }

    static Bounds RendererBounds(Transform root)
    {
        var bounds = new Bounds(root.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (r == null)
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
        return bounds;
    }

    /// <summary>
    /// Price a decoded model from the catalogue, so an imported block costs
    /// what the same block captured here would. The part ids come back
    /// through PartRegistry, which is also what refuses a retired part.
    /// </summary>
    static float PriceOfModel(ConfigurationModel model)
    {
        var stats = FindFirstObjectByType<UIBuildStats>();
        if (stats == null)
            return 0f;

        float total = 0f;
        foreach (BeamRecord beam in model.Beams)
            if (PartRegistry.TryGetPart(beam.PartCode, out string partId, out _))
                total += stats.PriceForPart(partId);

        total += model.Panels.Count * stats.panelPrice;
        return total;
    }

    void CaptureModule(ModuleSolver.Module module)
    {
        var build = FindFirstObjectByType<BuildController>();

        // Only this module's parts. The module decides membership on both
        // sides, so a panel cannot be taken without its frames or left behind
        // by them.
        ConfigurationModel model = ConfigurationCapture.Capture(build, module);
        if (model.Beams.Count == 0 && model.Panels.Count == 0)
        {
            SelectionStatus.Set("That module could not be read · nothing was saved.", 6f);
            return;
        }

        string collectionId = string.IsNullOrEmpty(_selectedId)
            ? BlockCollections.DefaultMineId
            : _selectedId;

        // Normalising positions made an identical module produce an identical
        // code; this is what that is FOR. Capturing the same wall twice now
        // finds the first one instead of filing a look-alike beside it.
        // Scoped to this collection, because the same block deliberately
        // filed in two collections is a filing choice, not a duplicate.
        string code = ConfigurationCode.Encode(model);
        foreach (PieceLibrary.PieceRecord existing in BlockCollections.BlocksIn(collectionId))
        {
            if (existing.code != code)
                continue;

            SelectionStatus.Set($"Already saved here as \"{existing.name}\".", 5f);
            RebuildGallery();
            return;
        }

        var record = new PieceLibrary.PieceRecord
        {
            id = System.Guid.NewGuid().ToString("N"),
            name = BlockCollections.NextUntitledName(collectionId),
            collectionId = collectionId,
            createdUtc = PieceLibrary.NowUtc(),
            modifiedUtc = PieceLibrary.NowUtc(),
            code = code,
            beamCount = model.Beams.Count,
            panelCount = model.Panels.Count,
            widthMm = module.WidthMm,
            depthMm = module.DepthMm,
            heightMm = module.HeightMm,
            price = PriceOf(module),
        };

        // Isolated AND framed. Framing alone still photographs the grid floor
        // and whatever else falls behind the module, so three blocks captured
        // from one room came back as three pictures of the room.
        Camera cam = build != null && build.cam != null ? build.cam : Camera.main;
        Texture2D thumb = module.HasBounds
            ? ThumbnailCapture.FramedIsolated(cam, module.WorldBounds, module.Roots)
            : ThumbnailCapture.Plain(cam);
        if (thumb != null)
        {
            PieceLibrary.SaveThumbnail(record.id, thumb);
            record.hasThumbnail = true;
            Destroy(thumb);
        }

        PieceLibrary.Save(record);

        // Opens in edit mode with its name selected, the same gesture that
        // names a new collection and for the same reason: "Untitled Block" is
        // a placeholder, not a name.
        _editingBlockId = record.id;
        _focusBlockId = record.id;
        Refresh();
        _focusBlockId = string.Empty;
        SelectionStatus.Set("Name the block · Enter to keep it.", 0f);
    }

    /// <summary>
    /// What the module's parts cost, from the same catalogue table the price
    /// pill reads. UIBuildStats.TotalPrice is the whole scene, which for one
    /// module out of three would be wrong by a factor of three.
    /// </summary>
    static float PriceOf(ModuleSolver.Module module)
    {
        var stats = FindFirstObjectByType<UIBuildStats>();
        if (stats == null)
            return 0f;

        float total = 0f;
        foreach (Transform frame in module.Frames)
        {
            string partId = StructureClipboard.CleanPartId(frame.name);
            if (partId != null)
                total += stats.PriceForPart(partId);
        }
        total += module.Panels.Count * stats.panelPrice;
        return total;
    }

    // ------------------------------------------------------------------
    // Moving a block between collections
    // ------------------------------------------------------------------

    public void BeginBlockDrag(string blockId)
    {
        _draggingBlockId = blockId;
        SelectionStatus.Set("Drop it on a collection to move it there.", 0f);
    }

    /// <summary>
    /// Which collection row the pointer was over when the drag ended, if any.
    /// Tested against each row's own rectangle rather than by raycasting: the
    /// ghost has already gone by now, and the rows are a short list.
    /// </summary>
    public void EndBlockDrag(PointerEventData eventData)
    {
        string blockId = _draggingBlockId;
        _draggingBlockId = null;
        if (string.IsNullOrEmpty(blockId))
            return;

        Camera camera = eventData.pressEventCamera;
        foreach (KeyValuePair<string, RectTransform> row in _rowRects)
        {
            if (row.Value == null ||
                !RectTransformUtility.RectangleContainsScreenPoint(row.Value, eventData.position, camera))
                continue;

            MoveBlock(blockId, row.Key);
            return;
        }

        SelectionStatus.Set(string.Empty, 0f);
    }

    void MoveBlock(string blockId, string collectionId)
    {
        BlockCollections.Collection destination = BlockCollections.ById(collectionId);
        if (destination == null)
            return;

        if (!MayEdit(BlockCollections.ParentOf(destination)))
        {
            SelectionStatus.Set($"{BlockCollections.LabelOf(BlockCollections.ParentOf(destination))} "
                                + "is not yours to change.", 5f);
            return;
        }

        if (collectionId == _selectedId)
        {
            SelectionStatus.Set(string.Empty, 0f);
            return;   // dropped back where it came from
        }

        foreach (PieceLibrary.PieceRecord record in PieceLibrary.LoadAll())
        {
            if (record.id != blockId)
                continue;

            record.collectionId = collectionId;
            PieceLibrary.Save(record);
            Refresh();
            SelectionStatus.Set($"Moved \"{record.name}\" to \"{destination.name}\".", 4f);
            return;
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Templates are baked inactive and live OUTSIDE the lists they feed, so
    /// no layout group reserves a slot for them and Clear cannot eat them.
    /// A clone has to be switched on before its own components wake up.
    /// </summary>
    static GameObject Clone(GameObject template, Transform parent)
    {
        GameObject go = Instantiate(template, parent);
        go.SetActive(true);
        return go;
    }

    static void Clear(Transform holder)
    {
        for (int i = holder.childCount - 1; i >= 0; i--)
            Destroy(holder.GetChild(i).gameObject);
    }

    static T Find<T>(Transform root, string path) where T : Component
    {
        Transform t = root.Find(path);
        return t != null ? t.GetComponent<T>() : null;
    }

    void ReleaseThumbnails()
    {
        foreach (Texture2D tex in _thumbnails)
            if (tex != null)
                Destroy(tex);
        _thumbnails.Clear();
    }
}
