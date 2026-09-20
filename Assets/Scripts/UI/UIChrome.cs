using UnityEngine;

/// <summary>
/// Locates the configurator's chrome buttons without hard-coding which
/// container they sit in.
///
/// The utility buttons (undo, redo, dimensions, fullscreen, settings, saved
/// designs) moved out of the old top bar and into a single right-hand
/// <see cref="RailName"/> block, matching the NEOSPACE web UI mockup. Several
/// runtime bootstraps look these buttons up by name — some to wire their click
/// handlers, some to copy as a style template — so every one of those lookups
/// goes through here instead of assuming a parent.
///
/// Search order is rail, then the canvas root, so this resolves correctly
/// before, during and after the move. (There used to be a top-bar step in
/// between; the top bar has since been dissolved entirely.)
/// </summary>
public static class UIChrome
{
    public const string RailName = "UtilityRail";

    /// <summary>
    /// The NEOSPACE wordmark, centred at the top of the screen. It is all
    /// that is left of the top bar: the bar also carried a "Space
    /// Configurator" title and a live price readout, and the price belongs on
    /// the Checkout tab beside the thing being priced, not in a permanent
    /// band across the top of a 3D view.
    ///
    /// There is deliberately NO TopBarName any more. Keeping the constant
    /// would keep every lookup that searched the bar compiling and silently
    /// finding nothing.
    /// </summary>
    public const string WordmarkName = "Wordmark";

    /// <summary>The bottom dock, and the container its tab pages live in.</summary>
    public const string DockName = "Dock";
    public const string DockBodyPath = "Dock/Body";

    // ------------------------------------------------------------------
    // Dock geometry, in canvas reference units (1920x1080, bottom-left
    // origin). These live here rather than in ConfiguratorUIBuilder because
    // the builder is in the EDITOR assembly and the runtime bootstraps that
    // reposition chrome cannot see it. HintPillBootstrap hard-coding its own
    // copy of the pill baseline is exactly how the pill ended up back behind
    // the dock after the builder had moved it.
    // ------------------------------------------------------------------

    /// <summary>Gap between the dock and the screen edge, left and right.</summary>
    public const float DockInset = 96f;

    /// <summary>Gap between the dock and the bottom of the screen.</summary>
    public const float DockBottom = 24f;

    public const float DockNavHeight = 58f;
    public const float DockBodyHeight = 236f;
    public const float DockFooterHeight = 36f;

    /// <summary>Total dock height: 330.</summary>
    public const float DockHeight = DockNavHeight + DockBodyHeight + DockFooterHeight;

    /// <summary>Y of the dock's top edge: 354.</summary>
    public const float DockTop = DockBottom + DockHeight;

    /// <summary>
    /// Baseline of the band above the dock, which holds the mode switch, the
    /// collapse control and the status and hint pills. Everything in the band
    /// is bottom-aligned here so their lower edges read as one row.
    /// </summary>
    public const float BandY = DockTop + 12f;

    /// <summary>Height of the pills sitting in that band.</summary>
    public const float PillHeight = 38f;

    /// <summary>Gap between two neighbouring controls in the band.</summary>
    public const float BandGap = 12f;

    /// <summary>
    /// The dock's collapse control: a circle the same height as the pills, so
    /// the band reads as one row of equal-height controls rather than a short
    /// button sitting among tall ones.
    /// </summary>
    public const float ToggleSize = PillHeight;

    /// <summary>
    /// Left-to-right order in the band's left cluster: collapse, then the
    /// Pro | Lite switch. The collapse control used to be centred, which put
    /// it in the middle of the status message and left the far left empty.
    /// </summary>
    public const float ToggleX = DockInset;
    public const float ModeSwitchX = DockInset + ToggleSize + BandGap;

    /// <summary>
    /// Width of the Pro | Lite switch. Here rather than in the builder
    /// because <see cref="ModeSwitchX"/> is, and a control's position and its
    /// width are the same fact about where the band's left cluster ends.
    /// </summary>
    public const float ModeSwitchWidth = 184f;

    /// <summary>
    /// How far the dock travels to get off screen: its own height plus the
    /// gap it keeps from the bottom edge. Everything in the band rides the
    /// same distance, so the row lands 12 units above the bottom edge and the
    /// dock's top edge lands exactly on it.
    /// </summary>
    public const float CollapseDrop = DockBottom + DockHeight;

    /// <summary>
    /// Corner radius shared by the pills in the band, so they read as one
    /// family. The hint pill used 17 while the status pill used 19, which
    /// HintPillBootstrap then papered over at runtime by copying the status
    /// pill's — the two disagreed until play mode started.
    /// </summary>
    public const float PillCornerRadius = 19f;

    /// <summary>
    /// How far to scale the standard panel shadow down for a pill. The full
    /// spread is drawn for large surfaces; unscaled it put an 82-unit-tall
    /// halo behind a 38-unit-tall pill.
    /// </summary>
    public const float PillShadowScale = 0.36f;

    // ------------------------------------------------------------------
    // The Parts card gallery.
    //
    // Shared for the same reason as the band geometry: the builder bakes the
    // grid and UIPartsPalette.Rebuild() stamps over it at runtime. When those
    // two disagreed, Rebuild forced a single VERTICAL column into a short,
    // wide dock body -- six cards 636 units tall in a 208-tall viewport,
    // centred by childAlignment so it showed cards 3 and 4 and clipped the
    // other four. The ScrollRect only scrolls horizontally, so they could not
    // be reached at all. Four tools vanished from the UI while still being
    // built and wired.
    // ------------------------------------------------------------------

    /// <summary>A wide part card: small icon, title beside it, caption below.</summary>
    public static readonly Vector2 PartCardSize = new Vector2(304f, 96f);

    public static readonly Vector2 PartCardSpacing = new Vector2(14f, 12f);

    /// <summary>
    /// Rows in the gallery. Two, because six cards then occupy 948x204 and fit
    /// the dock body's 1520x208 viewport whole: every tool is visible without
    /// scrolling. One row would need 1894 units and hide the last two again.
    /// </summary>
    public const int PartCardRows = 2;

    /// <summary>
    /// The Pro | Lite mode switch. It moved out of the top bar into the band
    /// above the dock, so nothing may assume its parent — use
    /// <see cref="ModeSwitch"/>, which finds it at either location.
    ///
    /// The segment objects keep their historical names: "Build" is Pro and
    /// "Space" is Lite. Renaming the objects would break SpaceBootstrap's
    /// lookups and every scene built before the rename for no gain, so only
    /// the visible labels changed.
    /// </summary>
    public const string ModeSwitchName = "ModeSwitch";
    public const string ProSegmentName = "Btn_ModeBuild";
    public const string LiteSegmentName = "Btn_ModeSpace";

    /// <summary>User-facing names of the two modes. Pro = piece-by-piece
    /// building for people with a design background; Lite = assembling
    /// ready-made blocks, for people new to 3D software.</summary>
    public const string ProLabel = "Pro";
    public const string LiteLabel = "Lite";

    /// <summary>
    /// The mode switch wherever it currently sits, or null in a scene that
    /// predates it. Only the CURRENT switch is returned: a stale legacy
    /// Build/Select row is also called "ModeSwitch" but holds Btn_Build and
    /// Btn_Select instead, and matching that one would wire the mode buttons
    /// to controls that are about to be destroyed.
    /// </summary>
    public static Transform ModeSwitch(Transform canvas)
    {
        if (canvas == null)
            return null;

        foreach (Transform candidate in FindAllDeep(canvas, ModeSwitchName))
            if (candidate.Find(ProSegmentName) != null && candidate.Find(LiteSegmentName) != null)
                return candidate;

        return null;
    }

    static System.Collections.Generic.List<Transform> FindAllDeep(Transform parent, string name)
    {
        var found = new System.Collections.Generic.List<Transform>();
        Collect(parent);
        return found;

        void Collect(Transform t)
        {
            for (int i = 0; i < t.childCount; i++)
            {
                Transform child = t.GetChild(i);
                if (child.name == name)
                    found.Add(child);
                Collect(child);
            }
        }
    }

    /// <summary>The utility rail, or null while the UI predates it.</summary>
    public static Transform Rail(Transform canvas)
    {
        return canvas != null ? canvas.Find(RailName) : null;
    }

    /// <summary>
    /// Find a chrome button by name wherever it currently lives. Returns null
    /// when it is absent, so callers keep their existing "create it myself"
    /// fallbacks.
    /// </summary>
    public static Transform FindButton(Transform canvas, string name)
    {
        if (canvas == null || string.IsNullOrEmpty(name))
            return null;

        Transform rail = canvas.Find(RailName);
        if (rail != null)
        {
            Transform inRail = rail.Find(name);
            if (inRail != null)
                return inRail;
        }

        // No top-bar branch: the bar is gone, and a lookup that still searched
        // it would keep compiling while quietly finding nothing.
        return canvas.Find(name);
    }

    /// <summary>
    /// The container a newly created chrome button should be parented to:
    /// the rail once it exists, otherwise the caller's existing fallback.
    /// </summary>
    public static Transform PreferredParent(Transform canvas, Transform fallback)
    {
        Transform rail = Rail(canvas);
        return rail != null ? rail : fallback;
    }

    // ------------------------------------------------------------------
    // Text on the dock's black body.
    //
    // UIThemeController.MutedColor (#7D7D7D) is tuned for the light chrome —
    // panels, the pills, the rail. On the dock's near-black body it comes
    // out at about 3.9:1 against the fill, under the 4.5:1 that ordinary text
    // needs, and the column labels were genuinely hard to read.
    //
    // The mockup solves it by lifting the greys a long way (#aeb6ac on
    // #202421, about 7.3:1). These are those values at matched luminance but
    // neutral, because the mockup's palette leads green and that has been
    // corrected everywhere else in this UI.
    //
    // Anything using these must NOT be registered with the theme, which would
    // repaint it back to MutedColor on the next Apply().
    // ------------------------------------------------------------------

    /// <summary>Row labels and body text on the dock body. ~6.6:1.</summary>
    public static readonly Color DockText = new Color32(0xB4, 0xB4, 0xB4, 0xFF);

    /// <summary>Section labels, counts and captions there. ~5.1:1.</summary>
    public static readonly Color DockDimText = new Color32(0x92, 0x92, 0x92, 0xFF);

    /// <summary>
    /// Type size for a row in either of the dock's left columns — the
    /// Tools/Parts list and the collections list. Shared so the two cannot
    /// drift: they occupy the same place on screen one tab apart, and a
    /// reader should not be able to tell which is which by the type.
    /// </summary>
    public const float DockRowFontSize = 14.5f;

    /// <summary>Type size for the small capitals heading such a column.</summary>
    public const float DockSectionFontSize = 11f;

    /// <summary>Default height of a rail row, in canvas reference units.</summary>
    public const float RowHeight = 32f;

    /// <summary>
    /// Find a panel anywhere under the canvas, by name or by a path whose
    /// FIRST segment is searched for at any depth ("PartsPanel/PartsScroll").
    ///
    /// The dock's tab pages are nested in Dock/Body, so the canvas-root
    /// Transform.Find that every caller used to do returns null. This searches
    /// the whole subtree, which resolves the panels wherever they live —
    /// before the dock, inside it, and in older scenes.
    ///
    /// Inactive objects are included: most callers look these up precisely
    /// because a mode has hidden them.
    /// </summary>
    public static Transform FindPanel(Transform canvas, string path)
    {
        if (canvas == null || string.IsNullOrEmpty(path))
            return null;

        int slash = path.IndexOf('/');
        string head = slash < 0 ? path : path.Substring(0, slash);

        Transform found = FindDeep(canvas, head);
        if (found == null || slash < 0)
            return found;

        return found.Find(path.Substring(slash + 1));
    }

    static Transform FindDeep(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
                return child;

            Transform deeper = FindDeep(child, name);
            if (deeper != null)
                return deeper;
        }
        return null;
    }

    /// <summary>
    /// Give a button the same pill as the rest of the rail, by copying the
    /// sprite settings off a sibling the UI builder made. Buttons created by
    /// their own bootstraps otherwise pick a different corner rounding and
    /// read as a different kind of control.
    /// </summary>
    public static void ApplyRailPillSprite(UnityEngine.UI.Image img, Transform rail)
    {
        if (img == null || rail == null)
            return;

        foreach (Transform child in rail)
        {
            if (!child.name.StartsWith("Btn_"))
                continue;

            var reference = child.GetComponent<UnityEngine.UI.Image>();
            if (reference == null || reference == img || reference.sprite == null)
                continue;

            img.sprite = reference.sprite;
            img.type = reference.type;
            img.pixelsPerUnitMultiplier = reference.pixelsPerUnitMultiplier;
            return;
        }
    }

    /// <summary>
    /// Panels that open from a rail toggle. They share the slot immediately
    /// left of the rail, so only one may be open at a time.
    /// </summary>
    static readonly string[] RailPanels = { "ProjectsPanel", "ControlSettingsPanel" };

    /// <summary>
    /// Close every rail panel except <paramref name="keep"/>. Call this when
    /// opening one, so two panels never stack on the same spot.
    /// </summary>
    public static void CloseOtherRailPanels(Transform canvas, string keep)
    {
        if (canvas == null)
            return;

        for (int i = 0; i < RailPanels.Length; i++)
        {
            if (RailPanels[i] == keep)
                continue;

            Transform t = canvas.Find(RailPanels[i]);
            if (t != null && t.gameObject.activeSelf)
                t.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Normalise a button that has just been parented into the rail. The rail's
    /// VerticalLayoutGroup controls height from a LayoutElement, so a button
    /// created for a loose corner (40x40 with its own anchors) has to publish a
    /// preferred height or it collapses to nothing.
    /// Safe to call when the button is not in the rail — it does nothing.
    /// </summary>
    public static void MakeRailItem(Transform button, float height = RowHeight)
    {
        if (button == null || button.parent == null || button.parent.name != RailName)
            return;

        var rt = button as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
        }

        var le = button.GetComponent<UnityEngine.UI.LayoutElement>();
        if (le == null)
            le = button.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;
        le.flexibleHeight = 0f;
    }
}
