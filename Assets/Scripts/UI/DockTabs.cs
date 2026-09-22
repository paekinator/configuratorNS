using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The dock's tab row: three equal blocks reading Build, Blocks and Checkout.
///
/// Styled as the NEOSPACE mockup has it — the nav strip is white, and the
/// ACTIVE tab's block is filled with the same black as the body beneath it so
/// the two read as one surface, with a hairline of low-opacity white between
/// them. That replaces the earlier underline treatment.
///
/// It switches pages, not experiences. Build is not a page of its own: it
/// shows whichever of the parts/tools pages matches
/// <see cref="UIInteractionState.CurrentExperience"/>, which the Tools/Parts
/// list in the dock's left column drives. Blocks and Checkout own a page each.
/// Keeping Build delegated rather than making it a third page is what lets the
/// existing experience switching, and the eleven scripts that find those
/// panels by name, go on working untouched.
/// </summary>
public class DockTabs : MonoBehaviour
{
    public enum Page
    {
        /// <summary>Defers to CurrentExperience: the tools page or the parts page.</summary>
        Build,
        Blocks,
        Checkout,
        Style,
        LiteBlocks
    }

    /// <summary>Which mode a tab belongs to. The dock swaps sets, not labels.</summary>
    public enum Mode
    {
        Pro,
        Lite
    }

    [System.Serializable]
    public class Tab
    {
        public Button button;
        public TextMeshProUGUI label;

        /// <summary>
        /// The tab block's fill: a rounded rect, so the block has two rounded
        /// TOP corners while active.
        /// </summary>
        public Image background;

        /// <summary>
        /// Plain rectangle over the fill's bottom strip, squaring off the
        /// lower two corners so the block meets the body flush. Rounding all
        /// four would leave white notches at the join.
        /// </summary>
        public Image backgroundFoot;

        /// <summary>Hairline between the active block and the body below it.</summary>
        public Image seam;

        public Page page;

        /// <summary>The body page this tab shows. Null for Build, which defers.</summary>
        public GameObject body;

        /// <summary>Which mode this tab belongs to.</summary>
        public Mode mode;
    }

    public List<Tab> tabs = new List<Tab>();

    /// <summary>The tools page, shown under Build in the Guided experience.</summary>
    public GameObject guidedPage;

    /// <summary>The parts page, shown under Build in the Expert experience.</summary>
    public GameObject partsPage;

    /// <summary>The shared Tools/Parts column; only Build has one.</summary>
    public GameObject buildColumn;

    Page _current = Page.Build;
    Mode _mode = Mode.Pro;

    void Awake()
    {
        foreach (Tab tab in tabs)
        {
            if (tab.button == null)
                continue;

            Tab captured = tab;
            tab.button.onClick.AddListener(() => Show(captured.page));
        }
    }

    void OnEnable()
    {
        UIInteractionState.OnExperienceChanged += HandleExperienceChanged;
        UIThemeController.ThemeChanged += Refresh;
        SpaceModeController.ModeChanged += HandleModeChanged;

        // Read the mode rather than assume Pro: this component can be enabled
        // after a mode switch has already happened.
        _mode = SpaceModeController.Active ? Mode.Lite : Mode.Pro;
        _current = DefaultPage(_mode);
        Refresh();
    }

    void OnDisable()
    {
        UIInteractionState.OnExperienceChanged -= HandleExperienceChanged;
        UIThemeController.ThemeChanged -= Refresh;
        SpaceModeController.ModeChanged -= HandleModeChanged;
    }

    static Page DefaultPage(Mode mode) => mode == Mode.Pro ? Page.Build : Page.LiteBlocks;

    void HandleModeChanged(bool spaceActive) =>
        SetMode(spaceActive ? Mode.Lite : Mode.Pro);

    /// <summary>
    /// Swap the tab set. Public for the same reason BuildColumnTabs'
    /// RefreshCurrent is: the mode event is subscribed in OnEnable, so outside
    /// play mode nothing can drive this and any check of the dock's state
    /// reads whatever the builder baked.
    /// </summary>
    public void SetMode(Mode mode)
    {
        if (_mode == mode)
            return;

        ActiveInteraction.Exit();
        _mode = mode;
        // The page selected in the other mode does not exist in this one, so
        // land on that mode's first tab rather than on nothing.
        _current = DefaultPage(_mode);
        Refresh();
    }

    void HandleExperienceChanged(UIInteractionState.Experience experience)
    {
        // Picking a tool or a part means the user is building, so follow them
        // back to that tab rather than leaving them on Checkout with the
        // selection silently changed underneath. Lite has no Build tab, and
        // the experience means nothing there, so leave it where it is.
        if (_mode == Mode.Pro)
            _current = Page.Build;

        Refresh();
    }

    /// <summary>
    /// Whether the Build tab is the one on screen — and so whether either of
    /// its two panels has any business being visible. Exposed for the wiring
    /// check, which asserts that nothing else shows them.
    /// </summary>
    public bool ShowingBuild => _mode == Mode.Pro && _current == Page.Build;

    public Page CurrentPage => _current;
    public Mode CurrentMode => _mode;

    /// <summary>
    /// Raised after the dock has settled on a page. The footer's caption
    /// listens: it describes whatever is open, and only this knows what that
    /// is. Anything else would be a second opinion about the current page.
    /// </summary>
    public event System.Action Changed;

    public void Show(Page page)
    {
        if (_current == page)
            return;

        ActiveInteraction.Exit();
        _current = page;
        Refresh();
    }

    /// <summary>
    /// Reused between passes so a shared page is not flicked off and on.
    /// Pro's Blocks tab and Lite's Blocks tab point at the SAME page object,
    /// so visibility has to be decided across every tab before any of it is
    /// applied — set it tab by tab and the answer depends on which tab
    /// happens to come last in the list.
    /// </summary>
    readonly Dictionary<GameObject, bool> _wanted = new Dictionary<GameObject, bool>();

    void Refresh()
    {
        _wanted.Clear();

        foreach (Tab tab in tabs)
        {
            // Tabs of the other mode are switched off entirely. Both sets
            // occupy the same three thirds of the strip, so only one set may
            // be present at a time.
            bool inMode = tab.mode == _mode;
            if (tab.button != null && tab.button.gameObject.activeSelf != inMode)
                tab.button.gameObject.SetActive(inMode);

            bool active = inMode && tab.page == _current;

            if (tab.label != null)
                tab.label.color = active ? UIThemeController.CardColor : UIThemeController.MutedColor;

            Color fill = active ? UIThemeController.InkColor : Color.clear;
            if (tab.background != null)
                tab.background.color = fill;
            if (tab.backgroundFoot != null)
                tab.backgroundFoot.color = fill;

            if (tab.seam != null)
                tab.seam.enabled = active;

            // The selected tab does not offer a hover: its filled block
            // already says it is selected.
            if (tab.button != null &&
                tab.button.TryGetComponent(out UIHoverReveal hover))
                hover.Suppressed = active;

            // A page is shown when ANY tab pointing at it is the current one.
            if (tab.body != null)
                _wanted[tab.body] = _wanted.TryGetValue(tab.body, out bool already)
                    ? already || active
                    : active;
        }

        foreach (KeyValuePair<GameObject, bool> page in _wanted)
            if (page.Key != null && page.Key.activeSelf != page.Value)
                page.Key.SetActive(page.Value);

        // Build shows one of the two existing panels, chosen by the
        // experience; the other stays hidden, as do both on any other tab and
        // in Lite, which has no Build tab at all.
        //
        // This is the ONLY owner of those panels' visibility. Space mode used
        // to hide them too, which made two owners of one piece of state and
        // left whichever ran last in charge.
        bool build = ShowingBuild;
        bool guided = UIInteractionState.CurrentExperience == UIInteractionState.Experience.Guided;

        if (guidedPage != null)
            guidedPage.SetActive(build && guided);
        if (partsPage != null)
            partsPage.SetActive(build && !guided);
        if (buildColumn != null)
            buildColumn.SetActive(build);

        Changed?.Invoke();
    }
}
