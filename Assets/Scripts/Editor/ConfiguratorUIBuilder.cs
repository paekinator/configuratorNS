using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// One-shot rebuild of the configurator UI ("Tools/Configurator/Rebuild UI").
///
/// Replaces the old canvas with a clean, light "product configurator" design:
///   - Floating top bar: brand, Build/Select segmented control, live part count
///     + price (UIBuildStats), veneer actions, and a dark/light theme toggle.
///   - Floating left catalog: Posts/Beams/Twist tabs + scrollable part cards with
///     baked 3D thumbnails, and a Panel-tool button at the bottom.
///   - Bottom-center status pill (UIStatusBar) and a camera-controls hint.
///
/// Every themed element is registered with a UIThemeController on the canvas so
/// the sun/moon button can swap the whole app (UI + environment) between the
/// light and dark palettes at runtime.
///
/// Visuals use the Evo UI pack (rounded 9-slice sprites, shadows, Inter fonts)
/// on top of standard uGUI components, so the existing controllers
/// (UIToolbarController, UIPartsPalette, UIStatusBar) keep working unchanged.
/// The whole operation is undoable with a single Undo step.
/// </summary>
public static class ConfiguratorUIBuilder
{
    // ------------------------------------------------------------------
    // Theme
    // ------------------------------------------------------------------

    static readonly Color Ink = Hex("26221E");        // warm near-black text
    static readonly Color Muted = Hex("8F8880");      // secondary text
    static readonly Color Card = Color.white;         // panel surface
    static readonly Color Surface = Hex("F3EFE9");    // inset fills, tracks, cards
    static readonly Color Accent = Hex("D96C47");     // terracotta
    static readonly Color Danger = Hex("BF4A40");     // matches UIThemeController.light.danger
    static readonly Color ShadowTint = new Color(0.12f, 0.09f, 0.06f, 0.35f);

    const string RoundedSpritePath = "Assets/Evo/Evo UI/Sprites/Borders/Radial/Filled/Radial Filled - 64px.png";
    const string ShadowSpritePath = "Assets/Evo/Evo UI/Sprites/Shadows/Rectangle/Rectangle Shadow.png";
    const string SunIconPath = "Assets/Evo/Evo UI/Sprites/Icons/Weather/Sun (Fill).png";
    const string MoonIconPath = "Assets/Evo/Evo UI/Sprites/Icons/Weather/Moon (Fill).png";
    const string CoinIconPath = "Assets/Evo/Evo UI/Sprites/Icons/Business/Coin (Fill).png";
    // Lives in Resources so the runtime bootstrap can load the same icon.
    // One swirl sprite covers both history buttons: redo uses it as-is,
    // undo mirrors it horizontally so the arrow curls the other way. The
    // Evo Refresh icon reads as a plain circle at button size, so this one
    // is drawn by the builder: a thick 240° arc with a big arrowhead.
    const string HistoryIconPath = "Assets/Resources/UI/HistorySwirlIcon.png";
    const string MoreIconPath = "Assets/Evo/Evo UI/Sprites/Icons/Navigation/More.png";
    const string GearIconPath = "Assets/Resources/UI/GearIcon.png";
    const string ToolIconPath = "Assets/Resources/UI/ToolIcon.png";
    const string PartsIconPath = "Assets/Resources/UI/PartsIcon.png";
    const string FrameToolIconPath = "Assets/Resources/UI/FrameToolIcon.png";
    const string BeamToolIconPath = "Assets/Resources/UI/BeamToolIcon.png";
    const string PanelToolIconPath = "Assets/Resources/UI/PanelToolIcon.png";
    const string FontFolder = "Assets/Evo/Evo UI/Fonts/Inter/TMP";
    const string ThumbAssetFolder = "Assets/UI/Resources/PartThumbs";

    static Sprite _rounded;
    static Sprite _shadow;
    static Sprite _sunIcon;
    static Sprite _moonIcon;
    static Sprite _coinIcon;
    static Sprite _gearIcon;
    static Sprite _historyIcon;
    static Sprite _moreIcon;
    static Sprite _toolIcon;
    static Sprite _partsIcon;
    static Sprite _frameToolIcon;
    static Sprite _beamToolIcon;
    static Sprite _panelToolIcon;
    static TMP_FontAsset _semiBold;
    static TMP_FontAsset _medium;
    static TMP_FontAsset _regular;

    /// Theme controller being filled during the build; every themed element
    /// registers itself here so the runtime dark/light switch can restyle it.
    static UIThemeController _theme;

    [MenuItem("Tools/Configurator/Rebuild UI")]
    public static void Rebuild() => RebuildCore(confirm: true);

    /// <summary>Rebuild without the confirmation dialog (automated recovery).</summary>
    public static bool RebuildNonInteractive() => RebuildCore(confirm: false);

    static bool RebuildCore(bool confirm)
    {
        if (!LoadAssets())
            return false;

        var buildController = Object.FindFirstObjectByType<BuildController>();
        if (buildController == null)
        {
            EditorUtility.DisplayDialog("Rebuild UI", "No BuildController found in the open scene.", "OK");
            return false;
        }

        PartDatabase database = buildController.partDatabase;
        if (database == null)
        {
            string guid = AssetDatabase.FindAssets("t:PartDatabase").FirstOrDefault();
            if (!string.IsNullOrEmpty(guid))
                database = AssetDatabase.LoadAssetAtPath<PartDatabase>(AssetDatabase.GUIDToAssetPath(guid));
        }

        if (database == null)
        {
            EditorUtility.DisplayDialog("Rebuild UI", "No PartDatabase found.", "OK");
            return false;
        }

        if (confirm && !EditorUtility.DisplayDialog("Rebuild UI",
                "This deletes the current Canvas and builds the new design in its place.\n" +
                "The operation is undoable (Cmd+Z).", "Rebuild", "Cancel"))
            return false;

        Undo.SetCurrentGroupName("Rebuild Configurator UI");
        int undoGroup = Undo.GetCurrentGroup();

        GenerateThumbnails(database);

        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas.transform.parent == null)
                Undo.DestroyObjectImmediate(canvas.gameObject);
        }

        // Previous rebuilds left their "GuidedTemplates" hosts behind (they live
        // outside the Canvas). Stale sessions keep running the Posts tool in the
        // background, so remove every old host before building a fresh one.
        DestroyStaleGuidedHosts();

        GameObject root = BuildCanvas(buildController, database);

        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = root;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("[ConfiguratorUIBuilder] UI rebuilt.");
        return true;
    }

    static void DestroyStaleGuidedHosts()
    {
        var hosts = new HashSet<GameObject>();
        foreach (var s in Object.FindObjectsByType<TemplateSession>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            hosts.Add(s.gameObject);
        foreach (var g in Object.FindObjectsByType<GuidedModeController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            hosts.Add(g.gameObject);
        foreach (var b in Object.FindObjectsByType<GuidedBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            hosts.Add(b.gameObject);

        foreach (GameObject host in hosts)
            Undo.DestroyObjectImmediate(host);
    }

    /// <summary>
    /// Draw the undo/redo swirl: a thick 240° arc (clockwise, gap at the
    /// bottom) ending in a chunky arrowhead, with a rounded cap on the other
    /// end. 128 px, white — the Image tint colors it. Regenerated on every
    /// rebuild so design tweaks here propagate.
    /// </summary>
    static Sprite EnsureHistorySwirlIcon()
    {
        const int size = 128;
        const float radius = 40f;
        const float strokeHalf = 11f;    // 22 px stroke — bold at 20 px display
        const float headLength = 32f;
        const float headHalfWidth = 20f;
        const float capDeg = 210f;       // rounded-cap end (top-left)
        const float headDeg = -30f;      // arrowhead end (bottom-right)

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

        Vector2 headAnchor = Angle(headDeg) * radius;
        Vector2 radial = Angle(headDeg);
        Vector2 tangentCw = new Vector2(radial.y, -radial.x);   // clockwise travel
        Vector2 tip = headAnchor + tangentCw * headLength;
        Vector2 baseA = headAnchor + radial * headHalfWidth;
        Vector2 baseB = headAnchor - radial * headHalfWidth;
        Vector2 capEnd = Angle(capDeg) * radius;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - center;

                // Arc band, only inside the swept range [headDeg, capDeg].
                float theta = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
                if (theta < headDeg)
                    theta += 360f;
                float arcAlpha = 0f;
                if (theta <= capDeg)
                    arcAlpha = Mathf.Clamp01(strokeHalf - Mathf.Abs(p.magnitude - radius) + 0.5f);

                float capAlpha = Mathf.Clamp01(strokeHalf - (p - capEnd).magnitude + 0.5f);
                float headAlpha = TriangleAlpha(p, tip, baseA, baseB);

                float alpha = Mathf.Max(arcAlpha, Mathf.Max(capAlpha, headAlpha));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        tex.SetPixels32(pixels);
        File.WriteAllBytes(HistoryIconPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(HistoryIconPath);
        if (AssetImporter.GetAtPath(HistoryIconPath) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(HistoryIconPath);

        static Vector2 Angle(float deg) =>
            new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));

        // Antialiased inside-ness of a triangle: min signed edge distance.
        static float TriangleAlpha(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            if (Cross(b - a, c - a) < 0f)
                (b, c) = (c, b);   // enforce CCW winding

            float d1 = Cross(b - a, p - a) / (b - a).magnitude;
            float d2 = Cross(c - b, p - b) / (c - b).magnitude;
            float d3 = Cross(a - c, p - c) / (a - c).magnitude;
            return Mathf.Clamp01(Mathf.Min(d1, Mathf.Min(d2, d3)) + 0.5f);
        }

        static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;
    }

    static bool LoadAssets()
    {
        _rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
        _shadow = AssetDatabase.LoadAssetAtPath<Sprite>(ShadowSpritePath);
        _sunIcon = AssetDatabase.LoadAssetAtPath<Sprite>(SunIconPath);
        _moonIcon = AssetDatabase.LoadAssetAtPath<Sprite>(MoonIconPath);
        _coinIcon = AssetDatabase.LoadAssetAtPath<Sprite>(CoinIconPath);
        _gearIcon = AssetDatabase.LoadAssetAtPath<Sprite>(GearIconPath);
        _historyIcon = EnsureHistorySwirlIcon();
        _moreIcon = AssetDatabase.LoadAssetAtPath<Sprite>(MoreIconPath);
        _toolIcon = AssetDatabase.LoadAssetAtPath<Sprite>(ToolIconPath);
        _partsIcon = AssetDatabase.LoadAssetAtPath<Sprite>(PartsIconPath);
        _frameToolIcon = AssetDatabase.LoadAssetAtPath<Sprite>(FrameToolIconPath);
        _beamToolIcon = AssetDatabase.LoadAssetAtPath<Sprite>(BeamToolIconPath);
        _panelToolIcon = AssetDatabase.LoadAssetAtPath<Sprite>(PanelToolIconPath);
        _semiBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontFolder}/Inter-SemiBold SDF.asset");
        _medium = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontFolder}/Inter-Medium SDF.asset");
        _regular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontFolder}/Inter-Regular SDF.asset");

        if (_rounded == null || _shadow == null || _semiBold == null || _medium == null || _regular == null)
        {
            EditorUtility.DisplayDialog("Rebuild UI",
                "Missing Evo UI assets (rounded sprite, shadow sprite or Inter TMP fonts).", "OK");
            return false;
        }

        if (_sunIcon == null || _moonIcon == null || _coinIcon == null)
            Debug.LogWarning("[ConfiguratorUIBuilder] Some Evo icons were not found; the affected buttons fall back to text.");

        return true;
    }

    // ------------------------------------------------------------------
    // Canvas + layout
    // ------------------------------------------------------------------

    static GameObject BuildCanvas(BuildController buildController, PartDatabase database)
    {
        var canvasGo = new GameObject("UI_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create UI");

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var veneerManager = Object.FindFirstObjectByType<VeneerManager>();
        var panelGhost = Object.FindFirstObjectByType<PanelGhostController>();

        _theme = Undo.AddComponent<UIThemeController>(canvasGo);
        _theme.sunSprite = _sunIcon;
        _theme.moonSprite = _moonIcon;

        BuildTopBar(canvasGo.transform, buildController, veneerManager, out var toolbar);
        BuildLeftPanel(canvasGo.transform, buildController, database, panelGhost, toolbar);
        BuildGuidedToolsPanel(canvasGo.transform, buildController, toolbar);
        BuildStatusPill(canvasGo.transform, buildController);
        BuildHint(canvasGo.transform);

        // Scene references for the environment half of the theme switch.
        _theme.toolbar = toolbar;
        _theme.targetCamera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        foreach (Light candidate in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (candidate.type != LightType.Directional)
                continue;
            _theme.sun = candidate;
            break;
        }

        GameObject floor = GameObject.Find("GridFloor");
        if (floor != null)
            _theme.floorRenderer = floor.GetComponent<MeshRenderer>();

        return canvasGo;
    }

    static void BuildTopBar(Transform canvas, BuildController buildController,
        VeneerManager veneerManager, out UIToolbarController toolbar)
    {
        // Slimmer bar: primary controls only (brand · history · mode ·
        // stats/settings); secondary actions live in the ⋯ overflow menu.
        RectTransform bar = Panel("TopBar", canvas, Card, 16f);
        SetAnchors(bar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        bar.offsetMin = new Vector2(24f, 0f);
        bar.offsetMax = new Vector2(-24f, 0f);
        bar.anchoredPosition = new Vector2(0f, -16f);
        bar.sizeDelta = new Vector2(bar.sizeDelta.x, 52f);
        AddShadow(bar);
        _theme.cardImages.Add(bar.GetComponent<Image>());

        toolbar = Undo.AddComponent<UIToolbarController>(bar.gameObject);
        toolbar.buildController = buildController;
        toolbar.useColorHighlight = true;
        toolbar.activeBgColor = Ink;
        toolbar.inactiveBgColor = Color.clear;
        toolbar.activeTextColor = Color.white;
        toolbar.inactiveTextColor = Muted;

        // Brand (left)
        RectTransform mark = Panel("BrandMark", bar, Accent, 8f);
        Place(mark, new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(24f, 24f), new Vector2(0f, 0.5f));
        _theme.accentImages.Add(mark.GetComponent<Image>());

        var title = Text("BrandTitle", bar, "Space Configurator", _semiBold, 16.5f, Ink, TextAlignmentOptions.MidlineLeft);
        Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(54f, 0f), new Vector2(220f, 26f), new Vector2(0f, 0.5f));
        _theme.inkTexts.Add(title);

        // No Build/Select switch: selection is always available by dragging
        // with the left mouse button ("click acts, drag selects").

        // (The Expert/Guided switch lives on the left panel as Templates/Parts tabs.)

        // --- Right side, outermost first: gear, theme toggle, veneer actions, build stats ---

        // Control settings (gear) — opens the camera-controls card.
        Button gear = SolidButton("Btn_Settings", bar, _gearIcon == null ? "..." : string.Empty, Surface, Ink, 18f);
        Place((RectTransform)gear.transform, new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(36f, 36f), new Vector2(1f, 0.5f));
        _theme.surfaceImages.Add(gear.GetComponent<Image>());

        if (_gearIcon != null)
        {
            var gearIconGo = NewUI("Icon", gear.transform);
            var gearIconRt = (RectTransform)gearIconGo.transform;
            Place(gearIconRt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f), new Vector2(0.5f, 0.5f));
            var gearIconImg = gearIconGo.AddComponent<Image>();
            gearIconImg.sprite = _gearIcon;
            gearIconImg.color = Muted;
            gearIconImg.preserveAspect = true;
            gearIconImg.raycastTarget = false;
        }

        BuildControlSettingsPanel(canvas, gear);

        // Dark/light toggle (circular, sun/moon icon)
        Button themeToggle = SolidButton("Btn_Theme", bar, string.Empty, Surface, Ink, 18f);
        Place((RectTransform)themeToggle.transform, new Vector2(1f, 0.5f), new Vector2(-58f, 0f), new Vector2(36f, 36f), new Vector2(1f, 0.5f));
        _theme.surfaceImages.Add(themeToggle.GetComponent<Image>());

        var themeIconGo = NewUI("Icon", themeToggle.transform);
        var themeIconRt = (RectTransform)themeIconGo.transform;
        Place(themeIconRt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f), new Vector2(0.5f, 0.5f));
        var themeIconImg = themeIconGo.AddComponent<Image>();
        themeIconImg.sprite = _moonIcon;
        themeIconImg.color = Muted;
        themeIconImg.preserveAspect = true;
        themeIconImg.raycastTarget = false;
        _theme.themeIcon = themeIconImg;
        UnityEventTools.AddPersistentListener(themeToggle.onClick, new UnityAction(_theme.Toggle));

        // Primary row (baked without listeners — the runtime bootstraps wire
        // them): icon-only Undo/Redo, then ONE segmented Build|Space mode
        // switch. Secondary actions (Load code, Clear all) live in the ⋯
        // overflow menu; Pieces is a quiet text action, Build mode only.
        IconButton("Btn_Undo", _historyIcon, "Undo", 292f, 44f, iconSize: 20f, mirrorX: true);
        IconButton("Btn_Redo", _historyIcon, "Redo", 340f, 44f, iconSize: 20f);

        BuildModeSwitch(bar, 400f);

        // Pieces: quiet text action (no pill), hidden by Space Mode.
        Button pieces = SolidButton("Btn_Pieces", bar, "Pieces", Color.clear, Ink, 12f);
        Place((RectTransform)pieces.transform, new Vector2(0f, 0.5f), new Vector2(600f, 0f), new Vector2(68f, 36f), new Vector2(0f, 0.5f));
        _theme.inkTexts.Add(pieces.GetComponentInChildren<TextMeshProUGUI>(true));

        BuildOverflowMenu(bar, 676f);

        void IconButton(string name, Sprite icon, string fallbackLabel, float x, float size,
            float iconSize = 16f, bool mirrorX = false)
        {
            Button b = SolidButton(name, bar, icon == null ? fallbackLabel : string.Empty, Surface, Ink, 18f);
            Place((RectTransform)b.transform, new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(size, 36f), new Vector2(0f, 0.5f));
            _theme.surfaceImages.Add(b.GetComponent<Image>());
            var lbl = b.GetComponentInChildren<TextMeshProUGUI>(true);
            if (lbl != null)
                _theme.inkTexts.Add(lbl);

            if (icon != null)
            {
                var iconGo = NewUI("Icon", b.transform);
                var iconRt = (RectTransform)iconGo.transform;
                Place(iconRt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(iconSize, iconSize), new Vector2(0.5f, 0.5f));
                if (mirrorX)
                    iconRt.localScale = new Vector3(-1f, 1f, 1f);
                var img = iconGo.AddComponent<Image>();
                img.sprite = icon;
                img.color = Ink;
                img.preserveAspect = true;
                img.raycastTarget = false;
                _theme.inkIcons.Add(img);
            }
        }

        BuildStatsReadout(bar, new Vector2(-104f, 0f));
    }

    /// <summary>
    /// One segmented Build | Space control instead of a lone "Space mode"
    /// pill. SpaceBootstrap wires both segments to SpaceModeController,
    /// which also owns the active-segment highlight at runtime.
    /// </summary>
    static void BuildModeSwitch(RectTransform bar, float x)
    {
        RectTransform track = Panel("ModeSwitch", bar, Surface, 19f);
        Place(track, new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(184f, 38f), new Vector2(0f, 0.5f));
        _theme.surfaceImages.Add(track.GetComponent<Image>());

        var layout = track.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        Segment("Btn_ModeBuild", "Build", active: true);
        Segment("Btn_ModeSpace", "Space", active: false);

        void Segment(string name, string label, bool active)
        {
            // Baked to the Build-active state; SpaceModeController repaints.
            // NOT theme-registered: the controller owns these colors.
            Button b = SolidButton(name, track, label, active ? Ink : Color.clear,
                active ? Color.white : Muted, 15f);
            b.GetComponentInChildren<TextMeshProUGUI>(true).fontSize = 12.5f;
        }
    }

    /// <summary>
    /// The ⋯ overflow menu: secondary actions that don't deserve permanent
    /// top-bar space. Load code (wired by ConfigurationCodeBootstrap) and
    /// Clear all (danger-tinted, two-step confirm, wired by
    /// BuildHistoryBootstrap via UIConfirmingButton.onConfirmed).
    /// </summary>
    static void BuildOverflowMenu(RectTransform bar, float x)
    {
        Button more = SolidButton("Btn_More", bar, _moreIcon == null ? "..." : string.Empty, Surface, Ink, 18f);
        Place((RectTransform)more.transform, new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(36f, 36f), new Vector2(0f, 0.5f));
        _theme.surfaceImages.Add(more.GetComponent<Image>());

        if (_moreIcon != null)
        {
            var iconGo = NewUI("Icon", more.transform);
            Place((RectTransform)iconGo.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f), new Vector2(0.5f, 0.5f));
            var img = iconGo.AddComponent<Image>();
            img.sprite = _moreIcon;
            img.color = Ink;
            img.preserveAspect = true;
            img.raycastTarget = false;
            _theme.inkIcons.Add(img);
        }

        RectTransform menu = Panel("MoreMenu", bar, Card, 14f);
        SetAnchors(menu, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f));
        menu.anchoredPosition = new Vector2(x, -8f);
        menu.sizeDelta = new Vector2(200f, 110f);
        AddShadow(menu);
        _theme.cardImages.Add(menu.GetComponent<Image>());

        Button load = MenuItem("Btn_LoadCode", "Load code", Ink, -8f);
        _theme.inkTexts.Add(load.GetComponentInChildren<TextMeshProUGUI>(true));

        RectTransform divider = Panel("Divider", menu, new Color(0.5f, 0.5f, 0.5f, 0.2f), 1f);
        Place(divider, new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(172f, 1f), new Vector2(0.5f, 1f));

        Button clear = MenuItem("Btn_ClearAll", "Clear all", Danger, -58f);
        var confirming = Undo.AddComponent<UIConfirmingButton>(clear.gameObject);
        confirming.label = clear.GetComponentInChildren<TextMeshProUGUI>(true);

        var menuController = Undo.AddComponent<UITopBarMenu>(bar.gameObject);
        menuController.panel = menu.gameObject;
        menuController.toggleButton = (RectTransform)more.transform;
        UnityEventTools.AddPersistentListener(more.onClick, new UnityAction(menuController.Toggle));

        menu.gameObject.SetActive(false);

        Button MenuItem(string name, string label, Color textColor, float y)
        {
            // Quiet menu row: invisible until hovered, then fills with the
            // surface tint (the ColorBlock multiplies the Image color, so
            // the theme can still restyle the fill).
            Button b = SolidButton(name, menu, label, Surface, textColor, 10f);
            Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(184f, 44f), new Vector2(0.5f, 1f));
            _theme.surfaceImages.Add(b.GetComponent<Image>());

            var colors = b.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.selectedColor = new Color(1f, 1f, 1f, 0f);
            b.colors = colors;

            var lbl = b.GetComponentInChildren<TextMeshProUGUI>(true);
            lbl.fontSize = 13f;
            lbl.alignment = TextAlignmentOptions.MidlineLeft;
            lbl.rectTransform.offsetMin = new Vector2(14f, 0f);
            return b;
        }
    }

    /// <summary>
    /// The camera-controls card opened by the gear button: pick between the
    /// Walkthrough (WASD) and Professional (CAD) schemes, with a legend.
    /// </summary>
    static void BuildControlSettingsPanel(Transform canvas, Button gearButton)
    {
        CameraControlManager manager = EnsureCameraControls();

        RectTransform panel = Panel("ControlSettingsPanel", canvas, Card, 20f);
        SetAnchors(panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
        panel.anchoredPosition = new Vector2(-24f, -96f);
        panel.sizeDelta = new Vector2(340f, 344f);
        AddShadow(panel);
        _theme.cardImages.Add(panel.GetComponent<Image>());

        var title = Text("Title", panel, "Controls", _semiBold, 18f, Ink, TextAlignmentOptions.TopLeft);
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -18f), new Vector2(200f, 26f), new Vector2(0f, 1f));
        _theme.inkTexts.Add(title);

        // Scheme options. Their colors are owned by UIControlSettings (active /
        // inactive highlight), so they are NOT registered with the theme.
        Button walk = SolidButton("Btn_SchemeWalkthrough", panel, "Walkthrough", Surface, Ink, 12f);
        Place((RectTransform)walk.transform, new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(300f, 44f), new Vector2(0.5f, 1f));

        Button cad = SolidButton("Btn_SchemeCad", panel, "Professional (CAD)", Surface, Ink, 12f);
        Place((RectTransform)cad.transform, new Vector2(0.5f, 1f), new Vector2(0f, -104f), new Vector2(300f, 44f), new Vector2(0.5f, 1f));

        var legend = Text("Txt_Legend", panel, string.Empty, _regular, 13f, Muted, TextAlignmentOptions.TopLeft);
        Place(legend.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -162f), new Vector2(300f, 166f), new Vector2(0f, 1f));
        legend.lineSpacing = 14f;   // air between shortcut lines
        _theme.mutedTexts.Add(legend);

        var ui = Undo.AddComponent<UIControlSettings>(panel.gameObject);
        ui.manager = manager;
        ui.panel = panel.gameObject;
        ui.walkthroughBg = walk.GetComponent<Image>();
        ui.cadBg = cad.GetComponent<Image>();
        ui.walkthroughLabel = walk.GetComponentInChildren<TextMeshProUGUI>(true);
        ui.cadLabel = cad.GetComponentInChildren<TextMeshProUGUI>(true);
        ui.legendText = legend;
        // Selection colors come from the live theme inside UIControlSettings.

        UnityEventTools.AddPersistentListener(gearButton.onClick, new UnityAction(ui.TogglePanel));
        UnityEventTools.AddPersistentListener(walk.onClick, new UnityAction(ui.SelectWalkthrough));
        UnityEventTools.AddPersistentListener(cad.onClick, new UnityAction(ui.SelectCad));

        panel.gameObject.SetActive(false);
    }

    /// <summary>Make sure the camera has both control schemes plus the manager.</summary>
    static CameraControlManager EnsureCameraControls()
    {
        var fly = Object.FindFirstObjectByType<FlyCameraController>(FindObjectsInactive.Include);
        GameObject camGo = fly != null ? fly.gameObject
            : Camera.main != null ? Camera.main.gameObject : null;
        if (camGo == null)
            return null;

        var cad = camGo.GetComponent<CadCameraController>();
        if (cad == null)
        {
            cad = Undo.AddComponent<CadCameraController>(camGo);
            cad.enabled = false; // the manager enables the persisted scheme at play time
        }

        var manager = camGo.GetComponent<CameraControlManager>();
        if (manager == null)
            manager = Undo.AddComponent<CameraControlManager>(camGo);
        manager.walkthroughController = fly;
        manager.cadController = cad;
        return manager;
    }

    /// <summary>Live part count + running price, shown as an inset pill in the top bar.</summary>
    static void BuildStatsReadout(RectTransform bar, Vector2 anchoredPos)
    {
        RectTransform pill = Panel("BuildStats", bar, Surface, 21f);
        Place(pill, new Vector2(1f, 0.5f), anchoredPos, new Vector2(232f, 42f), new Vector2(1f, 0.5f));
        _theme.surfaceImages.Add(pill.GetComponent<Image>());

        float textLeft = 16f;
        if (_coinIcon != null)
        {
            var iconGo = NewUI("Icon_Coin", pill);
            var iconRt = (RectTransform)iconGo.transform;
            Place(iconRt, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(18f, 18f), new Vector2(0f, 0.5f));
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite = _coinIcon;
            iconImg.color = Accent;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            _theme.accentImages.Add(iconImg);
            textLeft = 38f;
        }

        var price = Text("Txt_Price", pill, "$0", _semiBold, 15.5f, Ink, TextAlignmentOptions.MidlineLeft);
        Place(price.rectTransform, new Vector2(0f, 0.5f), new Vector2(textLeft, 0f), new Vector2(92f, 24f), new Vector2(0f, 0.5f));
        _theme.inkTexts.Add(price);

        RectTransform divider = Panel("Divider", pill, new Color(0.5f, 0.5f, 0.5f, 0.35f), 1f);
        Place(divider, new Vector2(0f, 0.5f), new Vector2(textLeft + 96f, 0f), new Vector2(1.5f, 18f), new Vector2(0f, 0.5f));

        var count = Text("Txt_PartCount", pill, "0 parts", _medium, 13f, Muted, TextAlignmentOptions.MidlineLeft);
        Place(count.rectTransform, new Vector2(0f, 0.5f), new Vector2(textLeft + 108f, 0f), new Vector2(84f, 20f), new Vector2(0f, 0.5f));
        _theme.mutedTexts.Add(count);

        var stats = Undo.AddComponent<UIBuildStats>(pill.gameObject);
        stats.priceText = price;
        stats.partCountText = count;
    }

    static void BuildLeftPanel(Transform canvas, BuildController buildController, PartDatabase database,
        PanelGhostController panelGhost, UIToolbarController toolbar)
    {
        RectTransform panel = Panel("PartsPanel", canvas, Card, 20f);
        SetAnchors(panel, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
        panel.offsetMin = new Vector2(24f, 24f);
        panel.offsetMax = new Vector2(24f + 336f, -108f);
        AddShadow(panel);
        _theme.cardImages.Add(panel.GetComponent<Image>());

        // Templates / Parts experience tabs at the very top of the panel.
        AddExperienceTabs(panel);

        var titleText = Text("Title", panel, "Parts", _semiBold, 20f, Ink, TextAlignmentOptions.TopLeft);
        Place(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -74f), new Vector2(200f, 26f), new Vector2(0f, 1f));
        _theme.inkTexts.Add(titleText);

        var subtitle = Text("Subtitle", panel, "Pick a part type — the size is chosen while placing", _regular, 12.5f, Muted, TextAlignmentOptions.TopLeft);
        Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -101f), new Vector2(300f, 18f), new Vector2(0f, 1f));
        _theme.mutedTexts.Add(subtitle);

        // No category tab row anymore: the three category cards ARE the
        // categories, so the grid starts right under the subtitle. The Panel
        // tool is a card in the same grid, not a separate bottom button.
        RectTransform scroll = BuildCardGrid(panel, out RectTransform gridContent);

        // Card template (kept outside the grid; UIPartsPalette clears grid children)
        Button template = BuildCardTemplate(panel);

        // Palette controller
        var palette = Undo.AddComponent<UIPartsPalette>(panel.gameObject);
        palette.panelGhost = panelGhost;
        palette.buildController = buildController;
        palette.gridParent = gridContent;
        palette.partButtonPrefab = template;
        palette.buttonFont = _semiBold;
        palette.useBgColors = true;
        palette.normalBgColor = Surface;
        palette.selectedBgColor = Accent;
        palette.normalTextColor = Ink;
        palette.selectedTextColor = Color.white;
        FillPartLists(palette, database);
        _theme.palette = palette;
    }

    static GuidedModeController BuildGuidedToolsPanel(
        Transform canvas,
        BuildController buildController,
        UIToolbarController toolbar)
    {
        RectTransform panel = Panel("GuidedToolsPanel", canvas, Card, 20f);
        SetAnchors(panel, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
        panel.offsetMin = new Vector2(24f, 24f);
        panel.offsetMax = new Vector2(24f + 336f, -108f);
        AddShadow(panel);
        _theme.cardImages.Add(panel.GetComponent<Image>());

        // Tools / Parts experience tabs at the very top of the panel.
        AddExperienceTabs(panel);

        var titleText = Text("Title", panel, "Tools", _semiBold, 20f, Ink, TextAlignmentOptions.TopLeft);
        Place(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -74f), new Vector2(280f, 26f), new Vector2(0f, 1f));
        _theme.inkTexts.Add(titleText);

        var subtitle = Text("Subtitle", panel, "Pick a tool, then follow the steps shown below", _regular, 12.5f, Muted, TextAlignmentOptions.TopLeft);
        Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -101f), new Vector2(300f, 18f), new Vector2(0f, 1f));
        _theme.mutedTexts.Add(subtitle);

        // No Beams tool here: the Parts tab's Horizontal beam card covers
        // single beam placement, so the guided panel keeps Frames + Panels.
        Button t1 = TemplateToolButton(panel, "Btn_T1_Posts", "Frames",
            "Stand frames on the grid · 4 clicks", -131f, _frameToolIcon);
        Button t3 = TemplateToolButton(panel, "Btn_T3_PanelBay", "Panels",
            "Add panels between frames · 3 clicks", -197f, _panelToolIcon);

        // Hint sits in an inset box so live feedback reads as part of the design.
        RectTransform hintBox = Panel("HintBox", panel, Surface, 14f);
        SetAnchors(hintBox, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
        hintBox.offsetMin = new Vector2(16f, 16f);
        hintBox.offsetMax = new Vector2(-16f, 126f);
        _theme.surfaceImages.Add(hintBox.GetComponent<Image>());

        var hint = Text("Txt_GuidedHint", hintBox, "Choose a tool.", _regular, 12.5f, Muted, TextAlignmentOptions.TopLeft);
        Stretch(hint.rectTransform);
        hint.rectTransform.offsetMin = new Vector2(14f, 12f);
        hint.rectTransform.offsetMax = new Vector2(-14f, -12f);
        hint.textWrappingMode = TextWrappingModes.Normal;
        _theme.mutedTexts.Add(hint);

        var host = new GameObject("GuidedTemplates");
        Undo.RegisterCreatedObjectUndo(host, "GuidedTemplates");
        var session = Undo.AddComponent<TemplateSession>(host);
        session.buildController = buildController;
        session.cam = buildController != null ? buildController.cam : null;
        if (buildController != null)
            session.floorMask = buildController.floorMask;

        var spawner = Undo.AddComponent<TemplateSpawner>(host);
        spawner.buildController = buildController;
        if (buildController != null)
            spawner.panelSlotManager = buildController.panelSlotManager;
        session.spawner = spawner;

        var guided = Undo.AddComponent<GuidedModeController>(host);
        guided.buildController = buildController;
        guided.templateSession = session;
        guided.panelGhost = Object.FindFirstObjectByType<PanelGhostController>();
        guided.expertPartsPanel = GameObject.Find("PartsPanel");
        guided.guidedToolsPanel = panel.gameObject;
        guided.guidedHintText = hint;

        UnityEventTools.AddPersistentListener(t1.onClick, new UnityAction(guided.SelectPostsTool));
        UnityEventTools.AddPersistentListener(t3.onClick, new UnityAction(guided.SelectPanelBayTool));

        if (toolbar != null)
            toolbar.guidedModeController = guided;

        panel.gameObject.SetActive(false);
        return guided;
    }

    /// <summary>
    /// "Tools | Parts" experience switch pinned to the top of a left panel.
    /// Both panels get one so the switch is always reachable; ExperienceTabs
    /// keeps every instance visually in sync via UIInteractionState.
    /// </summary>
    static void AddExperienceTabs(RectTransform panel)
    {
        // Segmented control in an inset track, matching the top bar's
        // Build/Select switch: quiet track, dark pill on the active tab.
        RectTransform row = Panel("ExperienceTabs", panel, Surface, 22f);
        SetAnchors(row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        row.offsetMin = new Vector2(16f, 0f);
        row.offsetMax = new Vector2(-16f, 0f);
        row.anchoredPosition = new Vector2(0f, -16f);
        row.sizeDelta = new Vector2(row.sizeDelta.x, 44f);
        _theme.surfaceImages.Add(row.GetComponent<Image>());

        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        Button templates = PillButton("Btn_Tab_Templates", row, "Tools", 18f, out Image templatesBg);
        Button parts = PillButton("Btn_Tab_Parts", row, "Parts", 18f, out Image partsBg);

        var tabs = Undo.AddComponent<ExperienceTabs>(row.gameObject);
        tabs.templatesButton = templates;
        tabs.partsButton = parts;
        tabs.templatesBg = templatesBg;
        tabs.partsBg = partsBg;
        tabs.templatesLabel = templates.GetComponentInChildren<TextMeshProUGUI>(true);
        tabs.partsLabel = parts.GetComponentInChildren<TextMeshProUGUI>(true);
        tabs.templatesIcon = ExperienceTabs.ApplyTabStyle(templates, "Tools", _toolIcon);
        tabs.partsIcon = ExperienceTabs.ApplyTabStyle(parts, "Parts", _partsIcon);
        // Tab colors come from the live theme inside ExperienceTabs.
    }

    static RectTransform BuildCardGrid(RectTransform panel, out RectTransform content)
    {
        var scrollGo = NewUI("PartsScroll", panel);
        var scrollRt = (RectTransform)scrollGo.transform;
        SetAnchors(scrollRt, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));
        scrollRt.offsetMin = new Vector2(16f, 16f);     // panel tool is a card now
        scrollRt.offsetMax = new Vector2(-16f, -131f);  // below the subtitle

        var viewportGo = NewUI("Viewport", scrollRt);
        var viewport = (RectTransform)viewportGo.transform;
        Stretch(viewport);
        viewportGo.AddComponent<RectMask2D>();
        var viewportImg = viewportGo.AddComponent<Image>();
        viewportImg.color = Color.clear;

        var contentGo = NewUI("Content_Grid", viewport);
        content = (RectTransform)contentGo.transform;
        SetAnchors(content, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        var grid = contentGo.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(146f, 140f);
        grid.spacing = new Vector2(12f, 12f);
        grid.padding = new RectOffset(0, 0, 4, 12);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.childAlignment = TextAnchor.UpperCenter;

        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;

        return scrollRt;
    }

    static Button BuildCardTemplate(RectTransform panel)
    {
        RectTransform card = Panel("Btn_PartTemplate", panel, Surface, 14f);
        card.sizeDelta = new Vector2(146f, 140f);

        var thumbGo = NewUI("Thumb", card);
        var thumb = (RectTransform)thumbGo.transform;
        SetAnchors(thumb, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        thumb.offsetMin = new Vector2(10f, 0f);
        thumb.offsetMax = new Vector2(-10f, -8f);
        thumb.sizeDelta = new Vector2(thumb.sizeDelta.x, 84f);
        var thumbImg = thumbGo.AddComponent<Image>();
        thumbImg.preserveAspect = true;
        thumbImg.raycastTarget = false;

        var label = Text("Label", card, "V5", _semiBold, 17f, Ink, TextAlignmentOptions.BottomLeft);
        Place(label.rectTransform, new Vector2(0f, 0f), new Vector2(12f, 26f), new Vector2(120f, 22f), new Vector2(0f, 0f));
        label.raycastTarget = false;

        var sub = Text("Sub", card, "Frame · 5 holes", _regular, 11f, Muted, TextAlignmentOptions.BottomLeft);
        Place(sub.rectTransform, new Vector2(0f, 0f), new Vector2(12f, 10f), new Vector2(126f, 16f), new Vector2(0f, 0f));
        sub.raycastTarget = false;

        var button = Undo.AddComponent<Button>(card.gameObject);
        button.targetGraphic = card.GetComponent<Image>();
        var colors = button.colors;
        colors.highlightedColor = new Color(0.94f, 0.94f, 0.94f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        button.colors = colors;

        card.gameObject.SetActive(false);
        return button;
    }

    static void BuildStatusPill(Transform canvas, BuildController buildController)
    {
        // Slimmer pill hugging the bottom edge so the floor feels bigger.
        RectTransform pill = Panel("StatusPill", canvas, Card, 19f);
        SetAnchors(pill, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        pill.anchoredPosition = new Vector2(168f, 12f);   // shifted right of the parts panel
        pill.sizeDelta = new Vector2(540f, 38f);
        AddShadow(pill);
        _theme.cardImages.Add(pill.GetComponent<Image>());

        RectTransform dot = Panel("Dot", pill, Accent, 4f);
        Place(dot, new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(8f, 8f), new Vector2(0f, 0.5f));
        _theme.accentImages.Add(dot.GetComponent<Image>());

        var status = Text("Txt_Status", pill, "Ready", _medium, 13f, Ink, TextAlignmentOptions.MidlineLeft);
        SetAnchors(status.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));
        status.rectTransform.offsetMin = new Vector2(34f, 0f);
        status.rectTransform.offsetMax = new Vector2(-16f, 0f);
        // Long guidance must stay inside the pill: wrap, shrink, then ellipsize.
        status.textWrappingMode = TextWrappingModes.Normal;
        status.overflowMode = TextOverflowModes.Ellipsis;
        status.enableAutoSizing = true;
        status.fontSizeMax = 13f;
        status.fontSizeMin = 9f;
        _theme.inkTexts.Add(status);

        var statusBar = Undo.AddComponent<UIStatusBar>(pill.gameObject);
        statusBar.buildController = buildController;
        statusBar.statusText = status;
    }

    static void BuildHint(Transform canvas)
    {
        RectTransform hint = Panel("HintPill", canvas, new Color(1f, 1f, 1f, 0.85f), 17f);
        SetAnchors(hint, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
        // Width and baseline are corrected at runtime by HintPillBootstrap,
        // which sizes the pill to hug the text exactly.
        hint.anchoredPosition = new Vector2(-24f, 12f);
        hint.sizeDelta = new Vector2(340f, 38f);
        _theme.hintImage = hint.GetComponent<Image>();

        var text = Text("Txt_Hint", hint, "Arrows nudge 88 mm  ·  Esc drops the tool  ·  Ctrl+Z undo",
            _regular, 12f, Muted, TextAlignmentOptions.Midline);
        Stretch(text.rectTransform);
        _theme.mutedTexts.Add(text);
    }

    // ------------------------------------------------------------------
    // Part lists + thumbnails
    // ------------------------------------------------------------------

    static void FillPartLists(UIPartsPalette palette, PartDatabase database)
    {
        palette.verticalParts = CollectIds(database, 'V');
        palette.horizontalParts = CollectIds(database, 'H');
        palette.twistParts = CollectIds(database, 'T');
    }

    static List<string> CollectIds(PartDatabase database, char prefix)
    {
        return database.parts
            .Where(p => p != null && p.realPrefab != null && !string.IsNullOrEmpty(p.partId))
            .Select(p => p.partId.Trim())
            .Where(id => char.ToUpperInvariant(id[0]) == prefix)
            // Keep only real catalogue sizes (e.g. drop a stray V14 prefab).
            .Where(id => prefix != 'V' ||
                         !int.TryParse(id.Substring(1), out int size) ||
                         System.Array.IndexOf(CatalogueData.VSizes, size) >= 0)
            .Distinct()
            .OrderBy(id => int.TryParse(id.Substring(1), out int n) ? n : int.MaxValue)
            .ToList();
    }

    static void GenerateThumbnails(PartDatabase database)
    {
        EnsureFolder(ThumbAssetFolder);

        var entries = database.parts
            .Where(p => p != null && p.realPrefab != null && !string.IsNullOrEmpty(p.partId))
            .ToList();

        try
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                EditorUtility.DisplayProgressBar("Rebuild UI", $"Rendering thumbnail {entry.partId}...", (float)i / entries.Count);

                Texture2D tex = RenderPrefabThumbnail(entry.realPrefab, 256);
                if (tex == null)
                    continue;

                string path = $"{ThumbAssetFolder}/{entry.partId.Trim()}.png";
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.Refresh();

        foreach (var entry in entries)
        {
            string path = $"{ThumbAssetFolder}/{entry.partId.Trim()}.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
                continue;

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 256;
                importer.SaveAndReimport();
            }
        }
    }

    static Texture2D RenderPrefabThumbnail(GameObject prefab, int size)
    {
        var preview = new PreviewRenderUtility();
        try
        {
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = Surface;
            preview.camera.fieldOfView = 26f;
            preview.camera.nearClipPlane = 0.01f;
            preview.camera.farClipPlane = 500f;

            preview.lights[0].intensity = 1.3f;
            preview.lights[0].transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            if (preview.lights.Length > 1)
            {
                preview.lights[1].intensity = 0.9f;
                preview.lights[1].transform.rotation = Quaternion.Euler(340f, 140f, 0f);
            }
            preview.ambientColor = new Color(0.45f, 0.45f, 0.45f, 1f);

            GameObject instance = preview.InstantiatePrefabInScene(prefab);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 0.1f);
            bool hasBounds = false;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
            {
                if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                else bounds.Encapsulate(renderer.bounds);
            }

            Quaternion viewDir = Quaternion.Euler(22f, -38f, 0f);
            float halfFov = preview.camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float distance = bounds.extents.magnitude / Mathf.Tan(halfFov) * 1.15f;

            preview.camera.transform.rotation = viewDir;
            preview.camera.transform.position = bounds.center - viewDir * Vector3.forward * distance;

            preview.BeginStaticPreview(new Rect(0f, 0f, size, size));
            preview.Render(true);
            return preview.EndStaticPreview();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ConfiguratorUIBuilder] Thumbnail render failed for {prefab.name}: {e.Message}");
            return null;
        }
        finally
        {
            preview.Cleanup();
        }
    }

    // ------------------------------------------------------------------
    // uGUI helpers
    // ------------------------------------------------------------------

    static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    static RectTransform Panel(string name, Transform parent, Color color, float cornerRadiusPx)
    {
        var go = NewUI(name, parent);
        var img = go.AddComponent<Image>();
        img.sprite = _rounded;
        img.type = Image.Type.Sliced;
        img.color = color;
        // Sprite: 64 ppu, 32px border => on a 100ppu canvas the corner is 50px / multiplier.
        img.pixelsPerUnitMultiplier = 50f / Mathf.Max(1f, cornerRadiusPx);
        return (RectTransform)go.transform;
    }

    static void AddShadow(RectTransform target)
    {
        var go = NewUI("Shadow", target);
        var rt = (RectTransform)go.transform;
        Stretch(rt);
        rt.offsetMin = new Vector2(-22f, -28f);
        rt.offsetMax = new Vector2(22f, 16f);
        rt.SetAsFirstSibling();

        var img = go.AddComponent<Image>();
        img.sprite = _shadow;
        img.color = ShadowTint;
        img.raycastTarget = false;
    }

    static TextMeshProUGUI Text(string name, Transform parent, string value,
        TMP_FontAsset font, float fontSize, Color color, TextAlignmentOptions align)
    {
        var go = NewUI(name, parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = value;
        tmp.font = font;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    /// <summary>Guided template tool card: bold title with a one-line caption.</summary>
    static Button TemplateToolButton(RectTransform panel, string name, string title, string caption, float y,
        Sprite icon = null)
    {
        RectTransform rt = Panel(name, panel, Surface, 14f);
        Place(rt, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(304f, 56f), new Vector2(0.5f, 1f));
        _theme.surfaceImages.Add(rt.GetComponent<Image>());

        float textX = 16f;
        if (icon != null)
        {
            var iconGo = NewUI("Icon", rt);
            var iconRt = (RectTransform)iconGo.transform;
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0f, 0.5f);
            iconRt.anchoredPosition = new Vector2(14f, 0f);
            iconRt.sizeDelta = new Vector2(26f, 26f);

            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            iconImg.color = Ink;

            textX = 52f;
        }

        var titleText = Text("Label", rt, title, _semiBold, 15f, Ink, TextAlignmentOptions.TopLeft);
        Place(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(textX, -10f), new Vector2(260f - (textX - 16f), 20f), new Vector2(0f, 1f));
        titleText.raycastTarget = false;
        _theme.inkTexts.Add(titleText);

        var captionText = Text("Caption", rt, caption, _regular, 11.5f, Muted, TextAlignmentOptions.TopLeft);
        Place(captionText.rectTransform, new Vector2(0f, 1f), new Vector2(textX, -31f), new Vector2(272f - (textX - 16f), 16f), new Vector2(0f, 1f));
        captionText.raycastTarget = false;
        _theme.mutedTexts.Add(captionText);

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = rt.GetComponent<Image>();
        return button;
    }

    /// <summary>Transparent pill button whose Image is toggled by UIToolbarController.</summary>
    static Button PillButton(string name, Transform parent, string label, float radiusPx, out Image bg)
    {
        RectTransform rt = Panel(name, parent, Color.clear, radiusPx);
        bg = rt.GetComponent<Image>();

        var text = Text("Label", rt, label, _medium, 14.5f, Muted, TextAlignmentOptions.Midline);
        Stretch(text.rectTransform);
        text.raycastTarget = false;

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        button.transition = Selectable.Transition.None;
        return button;
    }

    static Button SolidButton(string name, Transform parent, string label, Color bg, Color textColor, float radiusPx)
    {
        RectTransform rt = Panel(name, parent, bg, radiusPx);

        var text = Text("Label", rt, label, _semiBold, 14f, textColor, TextAlignmentOptions.Midline);
        Stretch(text.rectTransform);
        text.raycastTarget = false;

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = rt.GetComponent<Image>();

        // One subtle hover/pressed tint across the whole app (the uGUI
        // default pressed tint is a heavy 0.78 grey that reads as a glitch).
        var colors = button.colors;
        colors.highlightedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
        colors.pressedColor = new Color(0.90f, 0.90f, 0.90f, 1f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        return button;
    }

    static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = pivot;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        SetAnchors(rt, anchor, anchor, pivot);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }
}
