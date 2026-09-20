using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Asserts that the configurator's UI is still wired together.
///
/// The logic selftests (NeospaceCore, ConfigCode, SpaceCode) cover the rules
/// of the system and would pass happily with a completely broken interface.
/// Every UI regression hit during the dock/rail restructure was invisible to
/// them and to the eye:
///
///   - a button whose onClick was never attached, because the bootstrap only
///     wires it on the path where it creates the button itself;
///   - a Transform.Find returning null after a panel was nested one level
///     deeper, silently dropping card styling and tab wiring;
///   - a self-healing bootstrap quietly building a SECOND copy of a panel it
///     could no longer find.
///
/// None of those throw. This does.
///
/// Run from Tools > Configurator > Check UI Wiring, in edit mode or play mode.
/// Checks that depend on runtime bootstraps are skipped (and reported as
/// skipped) outside play mode rather than reported as failures.
/// </summary>
public static class UIWiringCheck
{
    /// <summary>Paths that must resolve. First segment is searched at any depth.</summary>
    static readonly string[] RequiredPaths =
    {
        "Dock", "Dock/Nav", "Dock/Body", "Dock/Footer",
        "UtilityRail",
        "Wordmark",
        "UIServices",
        "PartsPanel", "PartsPanel/PartsScroll",
        "Dock/Body/BuildColumn", "Dock/Footer/Txt_DockHint",
        "GuidedToolsPanel", "GuidedToolsPanel/Btn_T1_Posts",
        "GuidedToolsPanel/Btn_T3_PanelBay",
        "ControlSettingsPanel", "ControlSettingsPanel/Btn_Close",
        "ProjectsPanel", "ProjectsPanel/Btn_Close",
        "ProjectsPanel/Btn_SaveProject", "ProjectsPanel/Btn_OpenFromCode",
        "ProjectsPanel/Btn_NewProject",
        "ProjectsPanel/ProjectScroll/Viewport/Content",
        "ProjectsPanel/ProjectRowTemplate",
        "ConfirmDialog", "ConfirmDialog/Card",
        "ConfirmDialog/Card/Btn_Confirm", "ConfirmDialog/Card/Btn_Cancel",
        "AddBlockDialog", "AddBlockDialog/Card",
        "AddBlockDialog/Card/Btn_PickModule", "AddBlockDialog/Card/CodeRow/Btn_Import",
        "AddBlockDialog/Card/CodeRow/CodeInput",
        "ProjectName", "ProjectName/Txt_ProjectName",
    };

    /// <summary>
    /// Controls on the projects row template. ProjectPanelUI clones the
    /// template and finds these by path, so a rename here is silent: the row
    /// still appears, and the button it could not find simply does nothing.
    /// </summary>
    static readonly string[] ProjectRowControls =
    {
        "Thumb", "Name", "Badge_Mode/Text", "Badge_Cost/Text",
        "Btn_Code", "Btn_Open", "Btn_Update", "Btn_Delete",
    };

    /// <summary>Buttons the UI builder bakes a persistent listener onto.</summary>
    static readonly string[] BakedClickTargets =
    {
        "ControlSettingsPanel/Btn_Close",
        "ControlSettingsPanel/Btn_SchemeWalkthrough",
        "ControlSettingsPanel/Btn_SchemeCad",
        "Btn_Settings",
        "Btn_Undo",
        "Btn_Redo",
        "ProjectsPanel/Btn_Close",
        "ProjectsPanel/Btn_SaveProject",
        "ProjectsPanel/Btn_OpenFromCode",
        "ProjectsPanel/Btn_NewProject",
        "Btn_Projects",
    };

    /// <summary>Objects that must be unique — a duplicate means a bootstrap re-created one.</summary>
    static readonly string[] MustBeUnique =
    {
        "Dock", "UtilityRail", "PartsPanel", "GuidedToolsPanel", "ControlSettingsPanel",
        "ProjectsPanel",
    };

    /// <summary>
    /// Every button the rail should hold, in order. Dimensions and fullscreen
    /// used to be created by their own bootstraps at runtime, so they could
    /// only be checked in play mode; the builder bakes them all now.
    /// </summary>
    static readonly string[] RailButtons =
    {
        "Btn_Dimensions", "Btn_Undo", "Btn_Redo",
        "Btn_Projects", "Btn_Settings", "Btn_Fullscreen",
    };

    public static int RunAll(out List<string> failures)
    {
        failures = new List<string>();
        int checks = 0;

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            failures.Add("no Canvas in the scene");
            return 1;
        }

        Transform root = canvas.transform;

        // --- structure -------------------------------------------------
        foreach (string path in RequiredPaths)
        {
            checks++;
            if (UIChrome.FindPanel(root, path) == null)
                failures.Add("missing: " + path);
        }

        // --- uniqueness ------------------------------------------------
        foreach (string name in MustBeUnique)
        {
            checks++;
            int n = CountByName(root, name);
            if (n != 1)
                failures.Add("expected exactly one '" + name + "', found " + n);
        }

        // --- baked click wiring ----------------------------------------
        foreach (string path in BakedClickTargets)
        {
            checks++;
            Transform t = UIChrome.FindPanel(root, path) ?? UIChrome.FindButton(root, path);
            if (t == null)
            {
                failures.Add("missing button: " + path);
                continue;
            }

            var button = t.GetComponent<Button>();
            if (button == null)
                failures.Add("not a Button: " + path);
            else if (button.onClick.GetPersistentEventCount() == 0)
                failures.Add("no baked onClick listener: " + path);
        }

        // --- no ExperienceTabs rows ------------------------------------
        // This rule used to be the opposite: EXACTLY ONE row per page, because
        // GuidedBootstrap rebuilt a missing one and shifted every sibling below
        // it down 56px. That bootstrap no longer builds anything, the rows are
        // gone with it, and the Tools/Parts choice lives in the dock's left
        // column. Kept inverted rather than deleted so a row creeping back in
        // is noticed, since one would sit invisibly on top of a page.
        foreach (string page in new[] { "PartsPanel", "GuidedToolsPanel" })
        {
            Transform t = UIChrome.FindPanel(root, page);
            if (t == null)
                continue;

            checks++;
            int rows = CountByName(t, "ExperienceTabs");
            if (rows != 0)
                failures.Add(page + " holds " + rows + " ExperienceTabs row(s); that control is "
                             + "the dock's left column now and the rows were retired with it");
        }

        // --- the Pro | Lite mode switch --------------------------------
        checks++;
        Transform modeSwitch = UIChrome.ModeSwitch(root);
        if (modeSwitch == null)
        {
            failures.Add("no Pro|Lite mode switch: expected a '" + UIChrome.ModeSwitchName
                         + "' holding " + UIChrome.ProSegmentName + " and " + UIChrome.LiteSegmentName);
        }
        else
        {
            // Labels, not object names. The objects keep the historical
            // Build/Space wording so existing scenes stay wired; what the user
            // reads must say Pro and Lite.
            checks += 2;
            CheckSegmentLabel(modeSwitch, UIChrome.ProSegmentName, UIChrome.ProLabel, failures);
            CheckSegmentLabel(modeSwitch, UIChrome.LiteSegmentName, UIChrome.LiteLabel, failures);
        }

        // --- the band above the dock -----------------------------------
        // The mode switch, the collapse control and the two pills share one
        // strip above the dock. Nothing in it may fall behind the dock, and
        // the two things on the left may not sit on top of each other.
        //
        // This class of regression is invisible to every other check: the
        // control exists, is wired, has its listener, and is simply hidden
        // under an opaque panel. It is also the easiest to reintroduce,
        // because two of these are repositioned at runtime by bootstraps that
        // overwrite whatever the builder baked.
        // C# will not let a local function capture an `out` parameter, so the
        // list is aliased for MustClearDock below.
        List<string> failuresRef = failures;

        var dock = UIChrome.FindPanel(root, UIChrome.DockName) as RectTransform;
        var statusPill = UIChrome.FindPanel(root, "StatusPill") as RectTransform;
        var hintPill = UIChrome.FindPanel(root, "HintPill") as RectTransform;
        var modeSwitchRt = modeSwitch as RectTransform;
        var toggle = UIChrome.FindPanel(root, "Btn_DockToggle") as RectTransform;

        MustClearDock("the mode switch", modeSwitchRt);
        MustClearDock("the status pill", statusPill);
        MustClearDock("the keyboard hint pill", hintPill);

        checks++;
        if (modeSwitchRt != null && statusPill != null && Overlaps(modeSwitchRt, statusPill))
            failures.Add("the status pill overlaps the mode switch");

        void MustClearDock(string what, RectTransform rt)
        {
            checks++;
            if (dock == null || rt == null)
                return;
            if (Overlaps(dock, rt))
                failuresRef.Add(what + " overlaps the dock — it belongs in the band above it");
        }

        checks++;
        if (statusPill != null && toggle != null && Overlaps(statusPill, toggle))
            failures.Add("the status pill overlaps the dock collapse control");

        checks++;
        if (modeSwitchRt != null && toggle != null && Overlaps(modeSwitchRt, toggle))
            failures.Add("the dock collapse control overlaps the mode switch");

        MustClearDock("the dock collapse control", toggle);

        // --- the collapse control actually collapses something ------------
        // It existed for several revisions as a chevron that did nothing when
        // clicked: present, styled, correctly placed, inert. Stated as the
        // symptom, because "inert" has more than one cause.
        checks++;
        var collapse = toggle != null ? toggle.GetComponent<DockCollapse>() : null;
        if (toggle == null)
            failures.Add("no Btn_DockToggle; there is no way to get the dock out of the way");
        else if (collapse == null)
            failures.Add("Btn_DockToggle has no DockCollapse — clicking it would do nothing");
        else if (collapse.dock == null)
            failures.Add("DockCollapse.dock is unassigned; the band would slide down and leave "
                         + "the dock behind");

        // Every band control has to ride down with the dock, or it is left
        // hanging in mid-air over an empty screen. DockCollapse finds its
        // riders by rule — bottom-anchored, sitting on the band baseline — so
        // this checks the rule holds for the controls that are meant to move,
        // rather than checking a list against a list.
        BandRider("the mode switch", modeSwitchRt);
        BandRider("the status pill", statusPill);
        BandRider("the keyboard hint pill", hintPill);
        BandRider("the dock collapse control", toggle);

        void BandRider(string what, RectTransform rt)
        {
            checks++;
            if (rt == null)
                return;

            if (rt.parent != root)
            {
                failuresRef.Add(what + " is not a direct child of the canvas; the dock's collapse "
                                + "would leave it behind");
                return;
            }

            bool bottomAnchored = Mathf.Approximately(rt.anchorMin.y, 0f)
                                  && Mathf.Approximately(rt.anchorMax.y, 0f);

            // In play mode the whole band may legitimately be displaced,
            // because the dock is collapsed; measure against where the band
            // currently is, not against where it sits when open.
            float baseline = UIChrome.BandY + (collapse != null ? collapse.Offset : 0f);
            bool onBaseline = Mathf.Abs(rt.anchoredPosition.y - baseline) <= 8f;

            if (!bottomAnchored || !onBaseline)
                failuresRef.Add(what + " is not on the band baseline (anchored y "
                                + rt.anchoredPosition.y.ToString("F0") + " against "
                                + baseline.ToString("F0")
                                + "); it would be left behind when the dock collapses");
        }

        // The two pills sit side by side in the same band and must read as
        // one family. Their look is set in THREE places — the builder bakes
        // both, UIThemeController paints the hint pill from palette.hintBg,
        // and HintPillBootstrap copies the status pill's Image over it at
        // runtime — so they drift apart easily and only differ subtly.
        if (statusPill != null && hintPill != null)
        {
            var a = statusPill.GetComponent<Image>();
            var b = hintPill.GetComponent<Image>();
            if (a != null && b != null)
            {
                checks++;
                if (a.sprite != b.sprite || a.type != b.type ||
                    !Mathf.Approximately(a.pixelsPerUnitMultiplier, b.pixelsPerUnitMultiplier))
                    failures.Add("the status and hint pills are drawn with different sprites or "
                                 + "corner rounding (" + a.pixelsPerUnitMultiplier.ToString("F2")
                                 + " vs " + b.pixelsPerUnitMultiplier.ToString("F2") + ")");

                checks++;
                if (a.color != b.color)
                    failures.Add("the status and hint pills are different colours ("
                                 + a.color + " vs " + b.color + ")");
            }

            // A shadow spread drawn for a large card leaves a halo taller
            // than the pill itself.
            CheckPillShadow("status pill", statusPill);
            CheckPillShadow("hint pill", hintPill);
        }

        void CheckPillShadow(string what, RectTransform pill)
        {
            Transform shadow = pill.Find("Shadow");
            if (!(shadow is RectTransform shadowRt))
                return;

            checks++;
            float overhang = shadowRt.rect.height - pill.rect.height;
            if (overhang > pill.rect.height * 0.5f)
                failuresRef.Add("the " + what + "'s shadow overhangs it by "
                                + Mathf.RoundToInt(overhang) + " units on a "
                                + Mathf.RoundToInt(pill.rect.height)
                                + "-unit pill — that reads as a grey block, not a shadow");
        }

        // --- the status pill is a single line --------------------------
        // It sits in a band of single-line controls; wrapping put a two-line
        // block among them. The pill widens, tightens and shrinks instead.
        checks++;
        var statusBar = statusPill != null ? statusPill.GetComponent<UIStatusBar>() : null;
        if (statusBar == null)
            failures.Add("StatusPill has no UIStatusBar");
        else if (statusBar.statusText != null &&
                 statusBar.statusText.textWrappingMode != TMPro.TextWrappingModes.NoWrap)
            failures.Add("the status pill's text may wrap — it must stay on one line");

        // --- the dock footer carries the page caption, not the hint ----
        // Both showing the live message is the repetition this replaced.
        checks++;
        Transform footer = UIChrome.FindPanel(root, "Dock/Footer");
        var footerCaption = footer != null ? footer.GetComponent<DockFooterCaption>() : null;
        if (footerCaption == null)
            failures.Add("Dock/Footer has no DockFooterCaption — its line will never follow "
                         + "the Tools/Parts switch");
        else if (footerCaption.caption == null)
            failures.Add("DockFooterCaption.caption is not wired");

        // --- the dock's three tabs -------------------------------------
        checks++;
        Transform tabsRow = UIChrome.FindPanel(root, "Dock/Nav/Tabs");
        var dockTabs = tabsRow != null ? tabsRow.GetComponent<DockTabs>() : null;
        if (dockTabs == null)
        {
            failures.Add("no DockTabs on Dock/Nav/Tabs");
        }
        else
        {
            // Two sets of three sharing the same three thirds: Pro is
            // Build/Blocks/Checkout, Lite is Blocks/Style/Checkout. Both sets
            // must be complete, or switching mode lands on a third of the
            // strip with nothing in it.
            int pro = 0, lite = 0;
            foreach (DockTabs.Tab t in dockTabs.tabs)
            {
                if (t.mode == DockTabs.Mode.Pro) pro++;
                else lite++;
            }

            checks++;
            if (pro != 3 || lite != 3)
                failures.Add("expected 3 Pro tabs and 3 Lite tabs, found "
                             + pro + " and " + lite);

            // Every Lite tab needs a page: Lite has no equivalent of Build,
            // which is the one tab allowed to defer to the experience.
            foreach (DockTabs.Tab t in dockTabs.tabs)
            {
                if (t.mode != DockTabs.Mode.Lite)
                    continue;

                checks++;
                if (t.body == null)
                    failures.Add("Lite tab '" + (t.label != null ? t.label.text : "?")
                                 + "' has no page — it would open an empty dock");
            }

            // Build has no page of its own: it defers to the experience. If
            // these are unwired it silently shows nothing at all.
            checks++;
            if (dockTabs.guidedPage == null || dockTabs.partsPage == null ||
                dockTabs.buildColumn == null)
                failures.Add("DockTabs is missing a page reference (guided=" +
                             (dockTabs.guidedPage != null) + " parts=" +
                             (dockTabs.partsPage != null) + " column=" +
                             (dockTabs.buildColumn != null) + ")");

            foreach (DockTabs.Tab tab in dockTabs.tabs)
            {
                checks++;
                if (tab.button == null || tab.label == null ||
                    tab.background == null || tab.seam == null)
                    failures.Add("dock tab '" + (tab.label != null ? tab.label.text : "?")
                                 + "' is missing a part (button/label/background/seam)");

                CheckHover(tab.button != null ? tab.button.transform : null);
            }
        }

        // --- hover decorations -----------------------------------------
        // These sit at alpha 0 until the pointer arrives, so in the editor an
        // unwired one looks exactly like a wired one — there is nothing to
        // see either way. Only the reference says which it is.
        Transform buildColumn = UIChrome.FindPanel(root, "Dock/Body/BuildColumn");
        if (buildColumn != null)
            foreach (Transform row in buildColumn)
                if (row.name.StartsWith("Btn_"))
                    CheckHover(row);

        void CheckHover(Transform control)
        {
            if (control == null)
                return;

            checks++;
            var hover = control.GetComponent<UIHoverReveal>();
            if (hover == null)
                failuresRef.Add(control.name + " has no UIHoverReveal — it will not respond to hover");
            else if (hover.target == null)
                failuresRef.Add(control.name + "'s UIHoverReveal has no target to reveal");
        }


        // --- nothing invisible may swallow the column's clicks ---------
        // A fully transparent Graphic still absorbs raycasts. The dock pages
        // stretch across the whole body, including over the shared
        // Tools/Parts column, and are LATER siblings — so a transparent page
        // image made both column rows unclickable while they looked, and
        // tested, perfectly fine. Nothing here checks pixels, so this is the
        // one failure mode the rest of the file cannot see.
        Transform columnT = UIChrome.FindPanel(root, "Dock/Body/BuildColumn");
        if (columnT != null && columnT.parent != null)
        {
            int columnIndex = columnT.GetSiblingIndex();

            for (int i = columnIndex + 1; i < columnT.parent.childCount; i++)
            {
                Transform sibling = columnT.parent.GetChild(i);
                var img = sibling.GetComponent<Image>();
                if (img == null)
                    continue;

                checks++;
                if (img.raycastTarget && img.color.a < 0.01f)
                    failures.Add(sibling.name + " is invisible but still takes raycasts, and is "
                                 + "drawn over the Tools/Parts column — the column's rows will "
                                 + "look normal and do nothing");
            }
        }

        // --- the retired hint box --------------------------------------
        // A grey slab filling the right half of the Tools page, restating
        // what the status pill already says. UIStatusBar publishes
        // TemplateSession.StatusMessage verbatim in the Guided experience and
        // prefixes it with the armed tool, so the pill carries strictly more.
        // Both builders that used to create one have stopped.
        checks++;
        if (UIChrome.FindPanel(root, "GuidedToolsPanel/HintBox") != null)
            failures.Add("GuidedToolsPanel/HintBox is back — the status pill is the one place "
                         + "guided step text belongs");

        // --- dock pages stay inside the dock body ----------------------
        // The scroll rule above only covers galleries. A page laid out for
        // the old tall left panel puts its controls below the dock body's
        // bottom edge instead, where they are simply not drawn — same
        // invisible failure, no ScrollRect involved. Only active children are
        // checked: a hidden card template is parked, not displayed.
        Transform bodyT = UIChrome.FindPanel(root, UIChrome.DockBodyPath);
        if (bodyT is RectTransform body)
        {
            foreach (Transform page in body)
            {
                foreach (Transform item in page)
                {
                    if (!item.gameObject.activeSelf || !(item is RectTransform itemRt))
                        continue;

                    checks++;
                    if (!Contains(body, itemRt))
                        failures.Add(page.name + "/" + item.name
                                     + " falls outside the dock body and will not be drawn");
                }
            }
        }

        // --- scroll galleries ------------------------------------------
        // Content on an axis a ScrollRect does NOT scroll has to fit the
        // viewport. Anything past that edge is clipped by the mask, and with
        // no scrolling on that axis it cannot be reached at all — no
        // scrollbar, no overflow indicator, no error. The Parts gallery lost
        // four of its six tools that way: a grid constrained to one COLUMN
        // inside a ScrollRect that only scrolls horizontally.
        //
        // Stated against the grid's configuration rather than its children,
        // so it holds in edit mode too — the part cards are built at runtime,
        // and a rule that needed them would only ever fire in play mode.
        foreach (ScrollRect sr in canvas.GetComponentsInChildren<ScrollRect>(true))
        {
            if (sr.content == null || sr.viewport == null)
                continue;

            var grid = sr.content.GetComponent<GridLayoutGroup>();
            if (grid == null)
                continue;

            Vector2 view = sr.viewport.rect.size;

            if (!sr.vertical)
                CheckFits("vertically", "rows", GridLayoutGroup.Constraint.FixedRowCount,
                          grid.cellSize.y, grid.spacing.y, grid.padding.vertical, view.y);

            if (!sr.horizontal)
                CheckFits("horizontally", "columns", GridLayoutGroup.Constraint.FixedColumnCount,
                          grid.cellSize.x, grid.spacing.x, grid.padding.horizontal, view.x);

            void CheckFits(string axis, string lanes, GridLayoutGroup.Constraint required,
                           float cell, float gap, float pad, float available)
            {
                checks++;

                if (grid.constraint != required)
                {
                    failuresRef.Add(Path(sr.transform) + " does not scroll " + axis
                                    + ", so its grid must fix the number of " + lanes
                                    + " (it is " + grid.constraint + ") — otherwise they grow "
                                    + "past the viewport and are clipped out of reach");
                    return;
                }

                int n = grid.constraintCount;
                float needed = n * cell + (n - 1) * gap + pad;
                if (needed > available + 0.5f)
                    failuresRef.Add(Path(sr.transform) + ": " + n + " " + lanes + " need "
                                    + Mathf.RoundToInt(needed) + " units but the viewport gives "
                                    + Mathf.RoundToInt(available)
                                    + ", and it does not scroll " + axis);
            }
        }

        // --- the style source six scripts clone ------------------------
        checks++;
        Transform parts = UIChrome.FindPanel(root, "PartsPanel");
        var partsImage = parts != null ? parts.GetComponent<Image>() : null;
        if (partsImage == null || partsImage.sprite == null)
            failures.Add("PartsPanel must keep an Image with a sprite: "
                         + "CursorTooltip, ConfigurationCodeUI, MarqueeSelectionController, "
                         + "SpaceInteractionController, SpaceEditSession and DimensionsToggleBootstrap "
                         + "all copy it to style their own cards");

        // --- rail rows -------------------------------------------------
        Transform rail = UIChrome.Rail(root);
        if (rail != null)
        {
            float ppu = -1f;
            foreach (Transform child in rail)
            {
                if (!child.name.StartsWith("Btn_"))
                    continue;

                checks++;
                if (child.GetComponent<LayoutElement>() == null)
                    failures.Add("rail row without a LayoutElement (it will collapse): " + child.name);

                var img = child.GetComponent<Image>();
                if (img == null)
                    continue;

                if (ppu < 0f)
                    ppu = img.pixelsPerUnitMultiplier;
                else if (!Mathf.Approximately(ppu, img.pixelsPerUnitMultiplier))
                    failures.Add("rail pill shape differs on " + child.name
                                 + " (" + img.pixelsPerUnitMultiplier.ToString("F2")
                                 + " vs " + ppu.ToString("F2") + ")");
            }
        }

        // --- glyphs the font actually has ------------------------------
        // TMP substitutes U+25A1 (□) for any missing character and only warns
        // at runtime, so a button can ship showing a white box. This has
        // already happened twice: U+2715 (✕) on the settings close button and
        // U+2304 (⌄) on the dock collapse control, neither of which exists in
        // DM Sans. Icons should be sprites; where a glyph is used, it must be
        // one the font can draw.
        foreach (TMPro.TMP_Text text in canvas.GetComponentsInChildren<TMPro.TMP_Text>(true))
        {
            if (text.font == null || string.IsNullOrEmpty(text.text))
                continue;

            checks++;
            foreach (char c in text.text)
            {
                // searchFallbacks + tryAddCharacter: these atlases are DYNAMIC,
                // so the plain HasCharacter(c) overload answers "is this glyph
                // rasterised yet", not "can this font draw it" — every
                // not-yet-rendered letter would be reported as missing.
                if (char.IsWhiteSpace(c) ||
                    text.font.HasCharacter(c, searchFallbacks: true, tryAddCharacter: true))
                    continue;

                failures.Add(string.Format(
                    "'{0}' uses U+{1:X4} which {2} cannot draw — it will render as a box",
                    Path(text.transform), (int)c, text.font.name));
                break;
            }
        }

        // --- the projects panel ----------------------------------------
        // Baked whole, template included, so all of this is checkable here.
        // ProjectPanelUI resolves the row's controls by PATH on a clone: a
        // renamed child does not throw, it just quietly stops responding.
        Transform projects = UIChrome.FindPanel(root, "ProjectsPanel");
        if (projects != null)
        {
            checks++;
            var ui = projects.GetComponent<ProjectPanelUI>();
            if (ui == null)
            {
                failures.Add("ProjectsPanel carries no ProjectPanelUI");
            }
            else
            {
                checks += 4;
                if (ui.panel == null)
                    failures.Add("ProjectPanelUI.panel is unassigned");
                if (ui.listContent == null)
                    failures.Add("ProjectPanelUI.listContent is unassigned");
                if (ui.nameInput == null)
                    failures.Add("ProjectPanelUI.nameInput is unassigned");
                if (ui.rowTemplate == null)
                    failures.Add("ProjectPanelUI.rowTemplate is unassigned");
            }

            Transform template = projects.Find("ProjectRowTemplate");
            if (template != null)
            {
                checks++;
                if (template.gameObject.activeSelf)
                    failures.Add("ProjectRowTemplate is active; the prototype row would "
                                 + "show up in the panel as a blank project");

                // Parented to the PANEL, never to the list: inside it the
                // VerticalLayoutGroup would reserve a 64-unit gap for it, and
                // Refresh's "destroy every child" would eat it on first open.
                checks++;
                if (ui != null && ui.listContent != null && template.IsChildOf(ui.listContent))
                    failures.Add("ProjectRowTemplate sits inside the list content; "
                                 + "Refresh destroys every child there and would delete it");

                foreach (string path in ProjectRowControls)
                {
                    checks++;
                    if (template.Find(path) == null)
                        failures.Add("ProjectRowTemplate is missing " + path
                                     + "; ProjectPanelUI looks that up by path on every clone");
                }

                // Update and Delete overwrite or destroy a saved project, so
                // both take a second click. Their actions hang off
                // onConfirmed — without the component nothing is ever wired.
                foreach (string path in new[] { "Btn_Update", "Btn_Delete" })
                {
                    checks++;
                    Transform t = template.Find(path);
                    if (t != null && t.GetComponent<UIConfirmingButton>() == null)
                        failures.Add("ProjectRowTemplate/" + path + " has no UIConfirmingButton; "
                                     + "it would act on the first click, and ProjectPanelUI "
                                     + "wires its action to onConfirmed, so it would not act at all");
                }
            }
        }

        // --- the Blocks page --------------------------------------------
        // Both modes' Blocks tabs point at this ONE page, so it must exist
        // once and the retired Lite copy must not linger: two pages stacked
        // in the same body look like one and answer clicks at random.
        checks++;
        if (UIChrome.FindPanel(root, "LiteBlocksPage") != null)
            failures.Add("LiteBlocksPage still exists; Pro and Lite share one BlocksPage now");

        Transform blocks = UIChrome.FindPanel(root, "BlocksPage");
        if (blocks != null)
        {
            checks++;
            var ui = blocks.GetComponent<BlocksPanelUI>();
            if (ui == null)
            {
                failures.Add("BlocksPage carries no BlocksPanelUI");
            }
            else
            {
                checks += 6;
                if (ui.collectionList == null) failures.Add("BlocksPanelUI.collectionList is unassigned");
                if (ui.gallery == null) failures.Add("BlocksPanelUI.gallery is unassigned");
                if (ui.parentHeaderTemplate == null) failures.Add("BlocksPanelUI.parentHeaderTemplate is unassigned");
                if (ui.collectionRowTemplate == null) failures.Add("BlocksPanelUI.collectionRowTemplate is unassigned");
                if (ui.blockCardTemplate == null) failures.Add("BlocksPanelUI.blockCardTemplate is unassigned");
                if (ui.addCardTemplate == null) failures.Add("BlocksPanelUI.addCardTemplate is unassigned");

                // Every template must be baked OFF and parented outside the
                // list it feeds. Inside, the layout group reserves a slot for
                // a row nobody can see, and the rebuild's "destroy every
                // child" deletes the template on first open — after which the
                // page silently stops producing rows.
                CheckTemplate(ui.parentHeaderTemplate, ui.collectionList, "parentHeaderTemplate", ref checks, failuresRef);
                CheckTemplate(ui.collectionRowTemplate, ui.collectionList, "collectionRowTemplate", ref checks, failuresRef);
                CheckTemplate(ui.blockCardTemplate, ui.gallery, "blockCardTemplate", ref checks, failuresRef);
                CheckTemplate(ui.addCardTemplate, ui.gallery, "addCardTemplate", ref checks, failuresRef);

                // Children BlocksPanelUI resolves by path on every clone. A
                // rename here does not throw: the card just draws blank.
                // The paths BlocksPanelUI actually uses, not a second copy of
                // them. Spelling them here separately is how the pencil ended
                // up asserted at one path and looked up at another.
                CheckChildren(ui.blockCardTemplate, BlocksPanelUI.CardControls,
                              ref checks, failuresRef);

                // The card places the block when clicked, so it needs both a
                // Button and something to raycast against. A Button with no
                // Graphic under it is inert.
                checks += 2;
                if (ui.blockCardTemplate != null)
                {
                    if (ui.blockCardTemplate.GetComponent<Button>() == null)
                        failures.Add("BlockCardTemplate has no Button; clicking a card "
                                     + "would not place the block");
                    if (ui.blockCardTemplate.GetComponent<Graphic>() == null)
                        failures.Add("BlockCardTemplate has no Graphic; its Button would "
                                     + "never receive a click");
                }

                // The snapshot is clipped to the card's rounded corner, as in
                // the mockup. Without the Mask the picture sits square inside
                // a rounded frame, which is what it looked like before.
                checks++;
                Transform thumb = ui.blockCardTemplate != null
                    ? ui.blockCardTemplate.transform.Find("Thumb") : null;
                if (thumb != null && thumb.GetComponent<Mask>() == null)
                    failures.Add("BlockCardTemplate/Thumb has no Mask; the snapshot "
                                 + "would render square inside a rounded card");

                // The three that unfold belong to the card's edit mode; the
                // toggle that opens them does not.
                Transform cardActions = ui.blockCardTemplate != null
                    ? ui.blockCardTemplate.transform.Find("Actions") : null;
                if (cardActions != null)
                {
                    foreach (string folded in new[] { "Btn_Replace", "Btn_Share", "Btn_Delete" })
                    {
                        checks++;
                        Transform t = cardActions.Find(folded);
                        if (t != null && t.gameObject.activeSelf)
                            failures.Add("BlockCardTemplate/Actions/" + folded + " is baked ON; "
                                         + "it should unfold only while editing that block");
                    }

                    checks += 2;
                    Transform anchor = cardActions.Find("Btn_EditBlock");
                    if (anchor != null && !anchor.gameObject.activeSelf)
                        failures.Add("BlockCardTemplate/Actions/Btn_EditBlock is baked OFF; "
                                     + "it is the toggle that opens the others");

                    var cardStack = cardActions.GetComponent<UIActionStack>();
                    if (cardStack == null || cardStack.items.Count != 3)
                        failures.Add("BlockCardTemplate/Actions needs a UIActionStack holding "
                                     + "the three unfolding buttons; found "
                                     + (cardStack == null ? "no stack" : cardStack.items.Count + ""));
                }
                CheckChildren(ui.collectionRowTemplate,
                    new[] { "Label", "Count", "Btn_Remove", "NameEdit" }, ref checks, failuresRef);
                CheckChildren(ui.parentHeaderTemplate,
                    new[] { "Label", "Btn_EditCollections",
                            "Btn_EditCollections/Icon_Edit", "Btn_EditCollections/Icon_Done" },
                    ref checks, failuresRef);

                // The toggle's two faces: exactly one baked on, or it opens
                // showing both icons stacked on each other.
                Transform editToggle = ui.parentHeaderTemplate != null
                    ? ui.parentHeaderTemplate.transform.Find("Btn_EditCollections") : null;
                if (editToggle != null)
                {
                    checks++;
                    bool pencil = editToggle.Find("Icon_Edit") is Transform p && p.gameObject.activeSelf;
                    bool cross = editToggle.Find("Icon_Done") is Transform c && c.gameObject.activeSelf;
                    if (pencil == cross)
                        failures.Add("Btn_EditCollections should bake exactly one of "
                                     + "Icon_Edit / Icon_Done active, not " + (pencil ? "both" : "neither"));
                }
                CheckTemplate(ui.addCollectionTemplate, ui.collectionList,
                    "addCollectionTemplate", ref checks, failuresRef);

                checks++;
                if (ui.addCollectionTemplate == null)
                    failures.Add("BlocksPanelUI.addCollectionTemplate is unassigned");

                // The row's delete is baked OFF: it belongs to cleanup mode,
                // and a delete sitting permanently beside every collection is
                // a delete waiting to be hit by accident.
                checks++;
                Transform bakedRemove = ui.collectionRowTemplate != null
                    ? ui.collectionRowTemplate.transform.Find("Btn_Remove") : null;
                if (bakedRemove != null && bakedRemove.gameObject.activeSelf)
                    failures.Add("CollectionRowTemplate/Btn_Remove is baked ON; "
                                 + "it should only appear in cleanup mode");

                // Both rename fields start hidden, or every row would open
                // showing an edit box instead of its name.
                foreach (GameObject t in new[] { ui.collectionRowTemplate, ui.blockCardTemplate })
                {
                    Transform edit = t != null ? t.transform.Find("NameEdit") : null;
                    if (edit == null)
                        continue;
                    checks++;
                    if (edit.gameObject.activeSelf)
                        failures.Add(t.name + "/NameEdit is baked ON; it should replace "
                                     + "the label only while renaming");
                }
            }
        }

        // --- the confirmation modal --------------------------------------
        // It must be the LAST child of the canvas. Depth-first order is draw
        // order on a screen-space overlay canvas, so a modal built before the
        // dock draws underneath it — visible, and unclickable.
        Transform confirm = root.Find("ConfirmDialog");
        if (confirm != null)
        {
            checks += 4;
            if (confirm.GetSiblingIndex() != root.childCount - 1)
                failures.Add("ConfirmDialog is not the last child of the canvas; "
                             + "it would draw beneath the dock and be unclickable");

            var dialog = confirm.GetComponent<UIConfirmDialog>();
            if (dialog == null)
                failures.Add("ConfirmDialog carries no UIConfirmDialog");
            else if (dialog.card == null || dialog.backdrop == null ||
                     dialog.confirmButton == null || dialog.cancelButton == null)
                failures.Add("UIConfirmDialog has unassigned references");

            // Both baked off, or the app opens behind a modal nobody asked for.
            if (confirm.Find("Card") is Transform dialogCard && dialogCard.gameObject.activeSelf)
                failures.Add("ConfirmDialog/Card is baked ON; the app would open behind a modal");
            if (confirm.Find("Backdrop") is Transform back && back.gameObject.activeSelf)
                failures.Add("ConfirmDialog/Backdrop is baked ON; it would swallow every click");
        }

        // --- the footer caption knows which page is open ------------------
        // It followed the Tools/Parts switch alone once, and went on saying
        // "Pick a tool…" underneath the Blocks gallery — describing a page
        // that was not on screen, which is worse than describing nothing.
        Transform footerHint = UIChrome.FindPanel(root, "Dock/Footer/Txt_DockHint");
        if (footerHint != null)
        {
            checks += 2;
            var captionOwner = Object.FindFirstObjectByType<DockFooterCaption>();
            if (captionOwner == null)
                failures.Add("no DockFooterCaption; the footer line would never change");
            else if (captionOwner.dockTabs == null)
                failures.Add("DockFooterCaption.dockTabs is unassigned; the footer would "
                             + "describe the Build page whatever tab is open");
            else if (captionOwner.caption == null)
                failures.Add("DockFooterCaption.caption is unassigned");
        }

        // --- text that is set but renders nothing -------------------------
        // The project name was invisible for a day because its rect was ONE
        // unit shorter than a 24-point line needs, and TextOverflowModes
        // .Ellipsis draws nothing at all when the line does not fit — not a
        // clipped line, not an ellipsis, nothing. Every other signal said the
        // label was fine: present, active, opaque, correct colour, correct
        // text, a font that had the characters.
        //
        // So this asserts the symptom rather than any one cause. A visible
        // label with text in it must lay out at least one character; a label
        // that lays out none is invisible, whatever the reason — a short
        // rect, a missing glyph, a zero alpha, a font that failed to load.
        foreach (TMPro.TMP_Text label in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
        {
            if (label == null || !label.isActiveAndEnabled)
                continue;
            if (string.IsNullOrWhiteSpace(label.text))
                continue;

            checks++;
            label.ForceMeshUpdate();
            if (label.textInfo == null || label.textInfo.characterCount != 0)
                continue;

            failures.Add($"'{Path(label.transform, root)}' has text (\"{Trim(label.text)}\") "
                         + "but lays out no characters — it is on screen and invisible. "
                         + $"rect {label.rectTransform.rect.width:F0}x{label.rectTransform.rect.height:F0}, "
                         + $"needs {label.preferredWidth:F0}x{label.preferredHeight:F0}");
        }

        // --- the ⋯ menu is gone ------------------------------------------
        // Inverted, like the PiecesPanel rule: it held a second copy of two
        // things that now live where a person goes looking for them, and a
        // copy creeping back would be one more place to keep in step.
        foreach (string retired in new[] { "Btn_More", "MoreMenu" })
        {
            checks++;
            if (UIChrome.FindPanel(root, retired) != null)
                failures.Add(retired + " exists; the ⋯ menu is retired — Load code is "
                             + "\"Open from code\" and Clear all is \"New project\", "
                             + "both in My Projects");
        }

        // --- the top bar is gone ------------------------------------------
        // Inverted, like the ⋯ menu and PiecesPanel rules. A bar rebuilt by an
        // older builder would cover the wordmark with a white strip and put
        // the price back over the model.
        checks++;
        if (UIChrome.FindPanel(root, "TopBar") != null)
            failures.Add("TopBar exists; it is retired — the wordmark is the header, and the "
                         + "price readout belongs on the Checkout tab");

        // --- the wordmark --------------------------------------------------
        // A sprite that failed to import draws as a white box, which on a
        // light background is invisible rather than wrong-looking. (The
        // typed-capitals fallback is caught by the "lays out no characters"
        // rule further down instead.)
        Transform wordmark = root.Find(UIChrome.WordmarkName);
        if (wordmark != null)
        {
            var markImg = wordmark.GetComponent<Image>();
            if (markImg != null)
            {
                checks++;
                if (markImg.sprite == null)
                    failures.Add("the wordmark has an Image with no sprite — it would draw as a "
                                 + "solid block, not the logo");
            }
        }

        // --- the project name --------------------------------------------
        // Unwired, it would sit there reading "Untitled Project" forever,
        // whatever you opened.
        Transform projectName = root.Find("ProjectName");
        if (projectName != null)
        {
            checks += 3;
            var display = projectName.GetComponent<ProjectNameDisplay>();
            if (display == null)
                failures.Add("ProjectName carries no ProjectNameDisplay; it would never change");
            else if (display.nameText == null)
                failures.Add("ProjectNameDisplay.nameText is unassigned");

            // It sits under the wordmark at the left. Overlapping either that
            // or the rail would put two headings on top of each other.
            if (Overlaps(projectName as RectTransform, wordmark as RectTransform))
                failures.Add("the project name overlaps the wordmark");
            if (Overlaps(projectName as RectTransform, UIChrome.Rail(root) as RectTransform))
                failures.Add("the project name overlaps the utility rail");
        }

        // --- the Add a Block dialog --------------------------------------
        // Same rules as the confirmation modal: both halves baked off, or the
        // app opens behind a window nobody asked for.
        Transform addBlock = root.Find("AddBlockDialog");
        if (addBlock != null)
        {
            checks += 3;
            var addUi = addBlock.GetComponent<UIAddBlockDialog>();
            if (addUi == null)
                failures.Add("AddBlockDialog carries no UIAddBlockDialog");
            else if (addUi.card == null || addUi.backdrop == null || addUi.pickButton == null ||
                     addUi.importButton == null || addUi.codeInput == null || addUi.noteText == null)
                failures.Add("UIAddBlockDialog has unassigned references");

            if (addBlock.Find("Card") is Transform addCard && addCard.gameObject.activeSelf)
                failures.Add("AddBlockDialog/Card is baked ON; the app would open behind it");
            if (addBlock.Find("Backdrop") is Transform addBack && addBack.gameObject.activeSelf)
                failures.Add("AddBlockDialog/Backdrop is baked ON; it would swallow every click");
        }

        // --- the rail holds every button, in order ----------------------
        // Checkable in edit mode now: dimensions and fullscreen used to be
        // created by their own bootstraps at runtime, which is also why they
        // could land outside the rail.
        if (rail != null)
        {
            int expected = 0;
            foreach (Transform child in rail)
            {
                if (!child.name.StartsWith("Btn_"))
                    continue;

                checks++;
                if (expected >= RailButtons.Length)
                    failures.Add("unexpected extra rail button: " + child.name);
                else if (child.name != RailButtons[expected])
                    failures.Add("rail button " + expected + " is " + child.name
                                 + ", expected " + RailButtons[expected]);
                expected++;
            }

            checks++;
            if (expected != RailButtons.Length)
                failures.Add("the rail holds " + expected + " buttons, expected "
                             + RailButtons.Length);
        }

        // --- runtime-only ----------------------------------------------
        if (Application.isPlaying)
        {
            // PiecesPanel used to be asserted here. It is gone: the block
            // library is the dock's Blocks tab, and the rail panel was a
            // second door to it. Inverted rather than deleted, so a copy
            // creeping back in is noticed.
            checks++;
            if (UIChrome.FindPanel(root, "PiecesPanel") != null)
                failures.Add("PiecesPanel exists; the block library is the Blocks tab now "
                             + "and a second door to it is how the two got out of step");

            // Nothing but DockTabs may decide what the dock body shows.
            // This has now gone wrong three times, each time because a second
            // script called SetActive on a panel from its own OnEnable and so
            // ran with no idea which tab was open: GuidedBootstrap rebuilding
            // the tools panel, SpaceModeController hiding it on a mode
            // switch, and GuidedModeController putting it back whenever
            // anything disabled and re-enabled it — which the block picker
            // does every time it is armed.
            //
            // Stated as the symptom rather than the cause, so it catches the
            // next one too: if the Build tab is not the one on screen,
            // neither of its panels may be visible.
            var dockOwner = Object.FindFirstObjectByType<DockTabs>();
            if (dockOwner != null && !dockOwner.ShowingBuild)
            {
                foreach (string page in new[] { "GuidedToolsPanel", "PartsPanel" })
                {
                    checks++;
                    Transform t = UIChrome.FindPanel(root, page);
                    if (t != null && t.gameObject.activeSelf)
                        failures.Add(page + " is visible while the Build tab is not the "
                                     + "one on screen; something other than DockTabs showed it");
                }
            }
        }

        return checks;
    }

    /// <summary>Path from the canvas down, for naming a failure precisely.</summary>
    static string Path(Transform t, Transform root)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null && p != root; p = p.parent)
            path = p.name + "/" + path;
        return path;
    }

    static string Trim(string text) =>
        text.Length <= 24 ? text : text.Substring(0, 21) + "…";

    /// <summary>
    /// A cloned template must be baked OFF and live outside the list it
    /// feeds. Inside it, the layout group reserves a slot for a row nobody
    /// can see, and the rebuild's "destroy every child" deletes the template
    /// itself on first open — after which the panel silently stops producing
    /// rows, with nothing in the console to say why.
    /// </summary>
    static void CheckTemplate(GameObject template, Transform list, string what,
                              ref int checks, List<string> failures)
    {
        if (template == null)
            return;

        checks += 2;
        if (template.activeSelf)
            failures.Add(what + " is active; the prototype would show as a blank entry");
        if (list != null && template.transform.IsChildOf(list))
            failures.Add(what + " sits inside the list it feeds; the rebuild destroys "
                         + "every child there and would delete it on first open");
    }

    /// <summary>
    /// Children a runtime script resolves BY PATH on every clone. A rename
    /// here throws nothing — the control simply stops responding, or the
    /// field draws blank, which is far harder to notice than a crash.
    /// </summary>
    static void CheckChildren(GameObject template, string[] paths,
                              ref int checks, List<string> failures)
    {
        if (template == null)
            return;

        foreach (string path in paths)
        {
            checks++;
            if (template.transform.Find(path) == null)
                failures.Add(template.name + " is missing " + path
                             + "; it is looked up by path on every clone");
        }
    }

    /// <summary>
    /// One mode segment must carry the user-facing label. A separate method
    /// rather than a local function: the failures list is an `out` parameter,
    /// and C# will not let a local function capture one.
    /// </summary>
    static void CheckSegmentLabel(Transform modeSwitch, string segment, string expected,
                                  List<string> failures)
    {
        Transform t = modeSwitch.Find(segment);
        var label = t != null ? t.GetComponentInChildren<TMPro.TMP_Text>(true) : null;

        if (label == null)
            failures.Add("mode segment has no label: " + segment);
        else if (label.text != expected)
            failures.Add("mode segment " + segment + " reads '" + label.text
                         + "', expected '" + expected + "'");
    }

    /// <summary>
    /// Do two UI rects cover any common screen area? Uses world corners, so
    /// it is independent of anchoring, pivots and parent chains — the things
    /// that actually differ between two controls being compared.
    /// </summary>
    static bool Overlaps(RectTransform a, RectTransform b)
    {
        var ca = new Vector3[4];
        var cb = new Vector3[4];
        a.GetWorldCorners(ca);
        b.GetWorldCorners(cb);

        // corner 0 is bottom-left, corner 2 is top-right
        return ca[0].x < cb[2].x && cb[0].x < ca[2].x &&
               ca[0].y < cb[2].y && cb[0].y < ca[2].y;
    }

    /// <summary>Does <paramref name="outer"/> fully enclose <paramref name="inner"/>?</summary>
    static bool Contains(RectTransform outer, RectTransform inner)
    {
        var co = new Vector3[4];
        var ci = new Vector3[4];
        outer.GetWorldCorners(co);
        inner.GetWorldCorners(ci);

        const float slack = 0.5f;   // rounding on the layout pass
        return ci[0].x >= co[0].x - slack && ci[2].x <= co[2].x + slack &&
               ci[0].y >= co[0].y - slack && ci[2].y <= co[2].y + slack;
    }

    /// <summary>Readable hierarchy path, for naming the offender in a failure.</summary>
    static string Path(Transform t)
    {
        string path = t.name;
        Transform p = t.parent;
        for (int depth = 0; p != null && depth < 3; depth++)
        {
            path = p.name + "/" + path;
            p = p.parent;
        }
        return path;
    }

    static int CountByName(Transform parent, string name)
    {
        int n = 0;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
                n++;
            n += CountByName(child, name);
        }
        return n;
    }
}
