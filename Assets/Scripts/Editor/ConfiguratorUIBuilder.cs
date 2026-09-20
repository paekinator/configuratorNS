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
///   - The NEOSPACE wordmark alone at the top centre. There is no top bar:
///     it held a title, a brand square and a running price, and the price is
///     going to the Checkout tab where it sits beside what it prices.
///   - A bottom dock (Build / Blocks / Checkout) over a band of controls:
///     the collapse chevron and the Pro | Lite switch at the left, the status
///     pill centred, the keyboard hint at the right.
///   - A right-hand utility rail: dimensions, undo/redo, projects, settings,
///     fullscreen.
///
/// Every themed element is registered with a UIThemeController on the canvas so
/// the one NEOSPACE palette (UI + environment) is applied consistently at
/// runtime. There is no light/dark switch.
///
/// Visuals use the Evo UI pack (rounded 9-slice sprites, shadows) with DM Sans
/// and Manrope fonts
/// on top of standard uGUI components, so the existing controllers
/// (UIToolbarController, UIPartsPalette, UIStatusBar) keep working unchanged.
/// The whole operation is undoable with a single Undo step.
/// </summary>
public static class ConfiguratorUIBuilder
{
    // ------------------------------------------------------------------
    // Theme
    // ------------------------------------------------------------------

    // Neutral greys at the mockup's luminance — see UIThemeController.light
    // for why the mockup's green-leaning hue was dropped. Keep the two in step.
    static readonly Color Ink = Hex("242424");        // near-black text
    static readonly Color Muted = Hex("7D7D7D");      // secondary text
    static readonly Color Card = Color.white;         // panel surface
    static readonly Color Surface = Hex("E2E2E2");    // inset fills, tracks, cards
    static readonly Color Accent = Hex("3F3F3F");     // active/selected fill
    static readonly Color Danger = Hex("BF4A40");     // matches UIThemeController.DangerColor

    /// <summary>Hairline around the round card controls — separates, does not announce.</summary>
    static readonly Color CircleRing = Hex("9B9B9B");
    // Mockup uses one soft shadow (0 10px 32px #1d211d1c) rather than a drop
    // shadow on every panel.
    static readonly Color ShadowTint = new Color(0.12f, 0.12f, 0.12f, 0.14f);

    const string RoundedSpritePath = "Assets/Evo/Evo UI/Sprites/Borders/Radial/Filled/Radial Filled - 64px.png";
    const string ShadowSpritePath = "Assets/Evo/Evo UI/Sprites/Shadows/Rectangle/Rectangle Shadow.png";
    // No CoinIconPath: it dressed the top bar's price readout, which has gone
    // to the Checkout tab. Bring it back when Checkout draws the total.
    // Stands in for the mockup's floppy "Saved designs" glyph — the Evo pack
    // has no disk icon, and a folder reads as the same library idea.
    const string FolderIconPath = "Assets/Evo/Evo UI/Sprites/Icons/File/Folder (Fill).png";

    /// <summary>
    /// My Projects. A document rather than a second folder: the rail already
    /// carries a folder for the block library, and two folders side by side
    /// say nothing about which is which. A project is one saved scene — a
    /// file you open — so a page reads correctly next to a drawer of blocks.
    /// </summary>
    const string DocumentIconPath = "Assets/Evo/Evo UI/Sprites/Icons/File/Document (Fill).png";

    /// <summary>
    /// The two round controls on a block card. Real icons rather than typed
    /// characters: the UI font carries no dingbats, so the obvious glyphs
    /// (U+2715 for a cross, anything trash-shaped at all) draw as a
    /// placeholder box. A trash can is also what was actually asked for, and
    /// no character is one.
    /// </summary>
    const string TrashIconPath = "Assets/Evo/Evo UI/Sprites/Icons/System/Trash Can (Fill).png";
    const string AddIconPath = "Assets/Evo/Evo UI/Sprites/Icons/Navigation/Add.png";
    const string RefreshIconPath = "Assets/Evo/Evo UI/Sprites/Icons/Navigation/Refresh.png";
    const string ShareIconPath = "Assets/Evo/Evo UI/Sprites/Icons/Communication/Share.png";
    const string ArrowDownIconPath = "Assets/Evo/Evo UI/Sprites/Icons/Navigation/Arrow Down.png";
    // A real rounded-rect STROKE, for the hover outline on the Tools/Parts
    // rows. Drawing one from the filled sprite is not possible: with
    // fillCenter off, a sliced sprite's border thickness and its corner radius
    // are the same number, so a hairline stroke would have square corners.
    const string OutlineSpritePath =
        "Assets/Evo/Evo UI/Sprites/Borders/Radial/Outline/64px/Radial Outline 2x - 64px.png";
    // Lives in Resources so the runtime bootstrap can load the same icon.
    // One swirl sprite covers both history buttons: redo uses it as-is,
    // undo mirrors it horizontally so the arrow curls the other way. The
    // Evo Refresh icon reads as a plain circle at button size, so this one
    // is drawn by the builder: a thick 240° arc with a big arrowhead.
    const string HistoryIconPath = "Assets/Resources/UI/HistorySwirlIcon.png";
    const string DashedOutlinePath = "Assets/Resources/UI/DashedOutline.png";
    const string FolderOpenIconPath = "Assets/Evo/Evo UI/Sprites/Icons/File/Folder Open (Fill).png";
    const string PencilIconPath = "Assets/Evo/Evo UI/Sprites/Icons/Editor/Pencil.png";
    const string CloseIconPath = "Assets/Evo/Evo UI/Sprites/Icons/Navigation/Close.png";
    const string MoreIconPath = "Assets/Evo/Evo UI/Sprites/Icons/Navigation/More.png";
    /// <summary>
    /// The NEOSPACE wordmark, copied from the web UI mockup's own asset so the
    /// two products draw the same logo rather than two letterings that are
    /// nearly the same. Drawn as a sprite, never as typed capitals: the
    /// wordmark is a drawing, and DM Sans set wide is a lookalike.
    /// </summary>
    const string WordmarkPath = "Assets/UI/Textures/Neospace_Wordmark.png";
    const string GearIconPath = "Assets/Resources/UI/GearIcon.png";
    const string FullscreenIconPath = "Assets/Resources/UI/FullscreenIcon.png";
    const string ToolIconPath = "Assets/Resources/UI/ToolIcon.png";
    const string PartsIconPath = "Assets/Resources/UI/PartsIcon.png";
    const string FrameToolIconPath = "Assets/Resources/UI/FrameToolIcon.png";
    const string BeamToolIconPath = "Assets/Resources/UI/BeamToolIcon.png";
    const string PanelToolIconPath = "Assets/Resources/UI/PanelToolIcon.png";
    // DM Sans carries body/UI text, Manrope the display numerals and titles —
    // the pairing used by the NEOSPACE web UI mockup.
    const string FontFolder = "Assets/Fonts/Families/DM Sans/TMP";
    const string DisplayFontFolder = "Assets/Fonts/Families/Manrope/TMP";
    const string ThumbAssetFolder = "Assets/UI/Resources/PartThumbs";

    static Sprite _rounded;
    static Sprite _shadow;
    static Sprite _folderIcon;
    static Sprite _documentIcon;
    static Sprite _trashIcon;
    static Sprite _addIcon;
    static Sprite _folderOpenIcon;
    static Sprite _pencilIcon;
    static Sprite _refreshIcon;
    static Sprite _shareIcon;
    static Sprite _closeIcon;
    static Sprite _dashedOutline;
    static Sprite _arrowDownIcon;
    static Sprite _wordmark;
    static Sprite _outlineSprite;
    static Sprite _gearIcon;
    static Sprite _fullscreenIcon;
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
    /// <summary>Manrope — display face for titles and prices (mockup pairing).</summary>
    static TMP_FontAsset _display;

    /// <summary>Manrope Medium — the mockup sets its scene heading at weight 400,
    /// and the SemiBold display face reads a good deal heavier than that.</summary>
    static TMP_FontAsset _displayMedium;

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
    /// <summary>The dashed outline's authored geometry; see the generator.</summary>
    const int DashedOutlineSize = 64;
    const int DashedOutlineBorder = 16;

    /// <summary>
    /// A rounded rectangle drawn as a dashed outline, generated because the
    /// Evo set has no dashed border and nothing else in the project draws one.
    ///
    /// The first version drew dashes all the way round a 9-SLICED sprite, and
    /// came out broken: slicing STRETCHES the middle of each edge, so the two
    /// or three dashes that happened to land there were smeared into long
    /// bars while the corners kept their authored size. A dashed border
    /// cannot be stretched — only repeated.
    ///
    /// So it is authored to TILE instead:
    ///
    ///   corners (16x16)  a rounded corner arc, drawn solid. At the size this
    ///                    displays, a dash inside the curve would be a
    ///                    fleck — and it is the corners that must not repeat.
    ///   edges            a dash pattern along the strip, period 16 px, so
    ///                    the 32 px centre holds exactly two whole periods
    ///                    and tiles with no seam. A period that did not
    ///                    divide the strip would show a short dash at every
    ///                    repeat.
    ///   centre           empty; the box has no fill.
    ///
    /// Drawn with Image.Type.Tiled, the dashes then keep their authored size
    /// at any width, which is the whole point.
    /// </summary>
    static Sprite EnsureDashedOutlineSprite()
    {
        const int size = DashedOutlineSize;
        const int border = DashedOutlineBorder;
        const float stroke = 2.5f;
        const float corner = 13f;      // must be < border, or slicing cuts the curve
        const float period = 16f;      // divides the 32 px centre exactly
        const float dash = 10f;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        float inset = stroke * 0.5f + 0.5f;
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f;
                float fy = y + 0.5f;

                // Signed distance to the rounded rectangle.
                float px = Mathf.Abs(fx - half) - (half - inset - corner);
                float py = Mathf.Abs(fy - half) - (half - inset - corner);
                float outside = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude;
                float distance = outside + Mathf.Min(Mathf.Max(px, py), 0f) - corner;

                float band = Mathf.Clamp01(stroke * 0.5f - Mathf.Abs(distance) + 0.5f);
                if (band <= 0f)
                {
                    pixels[y * size + x] = new Color32(255, 255, 255, 0);
                    continue;
                }

                bool inCornerColumn = x < border || x >= size - border;
                bool inCornerRow = y < border || y >= size - border;

                // A corner cell is solid; an edge cell dashes along the axis
                // it runs in. Phase is measured from the texture origin, so
                // the pattern is continuous across a tile seam.
                bool on = true;
                if (!(inCornerColumn && inCornerRow))
                {
                    float along = inCornerColumn ? fy : fx;
                    on = Mathf.Repeat(along, period) < dash;
                }

                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(on ? band * 255f : 0f));
            }
        }

        tex.SetPixels32(pixels);
        EnsureFolder(Path.GetDirectoryName(DashedOutlinePath));
        File.WriteAllBytes(DashedOutlinePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(DashedOutlinePath);
        if (AssetImporter.GetAtPath(DashedOutlinePath) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = size;   // makes the maths below exact
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(DashedOutlinePath);
    }

    /// <summary>
    /// The pixelsPerUnitMultiplier that draws this sprite's corner at
    /// <paramref name="cornerUnits"/> canvas units, so a dashed box can be
    /// given the same corner as the solid rows beside it.
    ///
    /// Sprite imported at `size` ppu with a `border` px border, on a 100 ppu
    /// canvas: displayed border = (border / size) * 100 / multiplier.
    /// </summary>
    static float DashedOutlinePpu(float cornerUnits) =>
        DashedOutlineBorder / (float)DashedOutlineSize * 100f / Mathf.Max(1f, cornerUnits);

    public static Sprite EnsureHistorySwirlIcon()
    {
        const int size = 128;
        const float radius = 40f;
        const float strokeHalf = 3.5f;
        const float headLength = 17f;
        const float headHalfWidth = 9f;
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

    /// <summary>
    /// Null out a font asset with no usable atlas, so a caller's fallback
    /// takes over instead of the text silently rendering as nothing.
    ///
    /// NOT why the project name was invisible, despite what this comment
    /// said when it was written. Manrope-Medium SDF serialises a 1x1 atlas
    /// texture, which looked damning next to the SemiBold's 1024x1024 — but
    /// that is simply what a DYNAMIC font asset stores when it has not
    /// rendered anything yet. Unity sizes the atlas on load and fills it on
    /// demand; asked at runtime the same font reports 1024x1024 with real
    /// glyphs, and this guard never fires for it.
    ///
    /// Kept anyway, because a font with genuinely no atlas would still be
    /// worth catching and the cost is one comparison. Its comment is worth
    /// more: a wrong explanation left in the code is worse than none.
    /// </summary>
    static TMP_FontAsset Usable(TMP_FontAsset font)
    {
        if (font == null)
            return null;

        Texture atlas = font.atlasTexture;
        if (atlas != null && atlas.width > 1 && atlas.height > 1)
            return font;

        Debug.LogWarning(
            $"[ConfiguratorUIBuilder] Font '{font.name}' has no generated atlas "
            + $"({(atlas == null ? "none" : atlas.width + "x" + atlas.height)}); "
            + "text set in it would render as nothing. Falling back. "
            + "Regenerate it with Window > TextMeshPro > Font Asset Creator.");
        return null;
    }

    static bool LoadAssets()
    {
        _rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
        _shadow = AssetDatabase.LoadAssetAtPath<Sprite>(ShadowSpritePath);
        _folderIcon = AssetDatabase.LoadAssetAtPath<Sprite>(FolderIconPath);
        _documentIcon = AssetDatabase.LoadAssetAtPath<Sprite>(DocumentIconPath);
        _trashIcon = AssetDatabase.LoadAssetAtPath<Sprite>(TrashIconPath);
        _addIcon = AssetDatabase.LoadAssetAtPath<Sprite>(AddIconPath);
        _arrowDownIcon = AssetDatabase.LoadAssetAtPath<Sprite>(ArrowDownIconPath);
        _wordmark = AssetDatabase.LoadAssetAtPath<Sprite>(WordmarkPath);
        _outlineSprite = AssetDatabase.LoadAssetAtPath<Sprite>(OutlineSpritePath);
        _gearIcon = AssetDatabase.LoadAssetAtPath<Sprite>(GearIconPath);
        _fullscreenIcon = AssetDatabase.LoadAssetAtPath<Sprite>(FullscreenIconPath);
        _folderOpenIcon = AssetDatabase.LoadAssetAtPath<Sprite>(FolderOpenIconPath);
        _pencilIcon = AssetDatabase.LoadAssetAtPath<Sprite>(PencilIconPath);
        _refreshIcon = AssetDatabase.LoadAssetAtPath<Sprite>(RefreshIconPath);
        _shareIcon = AssetDatabase.LoadAssetAtPath<Sprite>(ShareIconPath);
        _closeIcon = AssetDatabase.LoadAssetAtPath<Sprite>(CloseIconPath);
        _historyIcon = EnsureHistorySwirlIcon();
        _dashedOutline = EnsureDashedOutlineSprite();
        _moreIcon = AssetDatabase.LoadAssetAtPath<Sprite>(MoreIconPath);
        _toolIcon = AssetDatabase.LoadAssetAtPath<Sprite>(ToolIconPath);
        _partsIcon = AssetDatabase.LoadAssetAtPath<Sprite>(PartsIconPath);
        _frameToolIcon = AssetDatabase.LoadAssetAtPath<Sprite>(FrameToolIconPath);
        _beamToolIcon = AssetDatabase.LoadAssetAtPath<Sprite>(BeamToolIconPath);
        _panelToolIcon = AssetDatabase.LoadAssetAtPath<Sprite>(PanelToolIconPath);
        _semiBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontFolder}/DMSans-SemiBold SDF.asset");
        _medium = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontFolder}/DMSans-Medium SDF.asset");
        _regular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontFolder}/DMSans-Regular SDF.asset");
        _display = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{DisplayFontFolder}/Manrope-SemiBold SDF.asset");
        _displayMedium = Usable(
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{DisplayFontFolder}/Manrope-Medium SDF.asset"));

        if (_rounded == null || _shadow == null || _semiBold == null || _medium == null || _regular == null)
        {
            EditorUtility.DisplayDialog("Rebuild UI",
                "Missing Evo UI assets (rounded sprite, shadow sprite) or the DM Sans TMP fonts in Assets/Fonts/Families/DM Sans/TMP.", "OK");
            return false;
        }

        if (_arrowDownIcon == null)
            Debug.LogWarning("[ConfiguratorUIBuilder] Some Evo icons were not found; the affected buttons fall back to text.");

        if (_wordmark == null)
            Debug.LogWarning("[ConfiguratorUIBuilder] The NEOSPACE wordmark was not found at " + WordmarkPath
                             + "; the header falls back to typed capitals, which are NOT the logo.");

        if (_display == null)
            Debug.LogWarning("[ConfiguratorUIBuilder] Manrope display font not found; titles fall back to DM Sans.");

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

        // The VeneerManager lookup went with the top bar: its only reader was
        // the bar's veneer action row, which was removed several revisions ago
        // and left the argument being fetched and passed to nobody.
        var panelGhost = Object.FindFirstObjectByType<PanelGhostController>();

        _theme = Undo.AddComponent<UIThemeController>(canvasGo);

        BuildWordmark(canvasGo.transform);
        BuildUIServices(canvasGo.transform, buildController, out var toolbar);
        BuildUtilityRail(canvasGo.transform, buildController);
        BuildProjectName(canvasGo.transform);
        BuildDock(canvasGo.transform);
        BuildModeSwitch(canvasGo.transform);
        BuildLeftPanel(canvasGo.transform, buildController, database, panelGhost, toolbar);
        BuildGuidedToolsPanel(canvasGo.transform, buildController, toolbar);
        BuildStatusPill(canvasGo.transform, buildController);
        BuildHint(canvasGo.transform);

        // The dock's Build tab shows one of the two panels above, so it can
        // only be wired once they exist.
        if (_dockTabs != null)
        {
            // UIChrome.FindPanel, not GameObject.Find: the guided page is
            // built hidden, and GameObject.Find skips inactive objects — it
            // returned null here, leaving the Tools half of Build dead.
            Transform guided = UIChrome.FindPanel(canvasGo.transform, "GuidedToolsPanel");
            Transform parts = UIChrome.FindPanel(canvasGo.transform, "PartsPanel");

            _dockTabs.guidedPage = guided != null ? guided.gameObject : null;
            _dockTabs.partsPage = parts != null ? parts.gameObject : null;
            _dockTabs.buildColumn = _buildColumn;

            if (_dockFooterCaption != null)
                _dockFooterCaption.dockTabs = _dockTabs;

            // The column is section one; the pages fill the body behind it.
            // Last sibling so it draws, and is hit, above them.
            if (_buildColumn != null)
                _buildColumn.transform.SetAsLastSibling();
        }

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

        // No floor reference. The ground is a shadow catcher now — invisible
        // except where the key light is blocked — so the theme has nothing to
        // tint on it.

        // Last, so it draws over everything: depth-first order is draw order
        // on a screen-space overlay canvas, and a modal under the dock would
        // be unreachable.
        BuildAddBlockDialog(canvasGo.transform);
        CameraViewShortcuts.Install(canvas);
        BuildConfirmDialog(canvasGo.transform);

        // Scene materials, not canvas: one colour answers "which one?"
        // everywhere, and it is decided in exactly one place.
        ApplyHighlightMaterials();

        return canvasGo;
    }

    /// <summary>
    /// The NEOSPACE wordmark, centred at the top of the screen, as the mockup
    /// places it.
    ///
    /// This is all that remains of the top bar. The bar carried three things
    /// and lost all three: a "Space Configurator" title (the wordmark says it
    /// better), a decorative brand square, and a live price readout that is
    /// moving to the Checkout tab, where the price sits beside what is being
    /// priced instead of hovering over the model at all times.
    ///
    /// The bar's own white card went with them. It ran the full width of the
    /// screen to hold a logo and a number, and the scene is the thing worth
    /// looking at.
    /// </summary>
    static void BuildWordmark(Transform canvas)
    {
        var host = NewUI(UIChrome.WordmarkName, canvas);
        var rt = (RectTransform)host.transform;
        // Aspect comes from the artwork (925 x 139), so the logo cannot be
        // stretched by picking a height that disagrees with the width.
        const float markWidth = 180f;
        Place(rt, new Vector2(0.5f, 1f), new Vector2(0f, -40f),
              new Vector2(markWidth, markWidth * 139f / 925f), new Vector2(0.5f, 1f));

        if (_wordmark != null)
        {
            var img = host.AddComponent<Image>();
            img.sprite = _wordmark;
            img.color = Ink;
            img.preserveAspect = true;
            img.raycastTarget = false;
            _theme.inkIcons.Add(img);
            return;
        }

        // Fallback only. Typed capitals are NOT the wordmark — the letterforms
        // differ — so this exists to say "the logo asset is missing", not to
        // stand in for it indefinitely.
        var text = Text("Txt_Wordmark", rt, "NEOSPACE", _display, 22f, Ink, TextAlignmentOptions.Midline);
        Stretch(text.rectTransform);
        text.characterSpacing = 6f;
        text.raycastTarget = false;
        _theme.inkTexts.Add(text);
    }

    /// <summary>
    /// Two components that have no UI of their own but must exist somewhere.
    ///
    /// Both used to hang off the top bar, and both would have quietly gone
    /// with it. <see cref="UIBuildStats"/> in particular is not a readout: it
    /// is the price oracle that the block cards, the project Cost badge,
    /// Space Mode's merge and the edit session all reach for with
    /// FindFirstObjectByType. Deleting the pill that displayed it would have
    /// returned null in six places and priced the whole app at zero, with no
    /// error anywhere.
    ///
    /// So the readout goes and the component stays, with no text assigned —
    /// which UIBuildStats already handles, because it always computed the
    /// numbers first and displayed them second.
    /// </summary>
    static void BuildUIServices(Transform canvas, BuildController buildController,
        out UIToolbarController toolbar)
    {
        var host = NewUI("UIServices", canvas);
        var rt = (RectTransform)host.transform;
        Place(rt, new Vector2(0f, 1f), Vector2.zero, Vector2.zero, new Vector2(0f, 1f));

        toolbar = Undo.AddComponent<UIToolbarController>(host);
        toolbar.buildController = buildController;
        toolbar.useColorHighlight = true;
        toolbar.activeBgColor = Ink;
        toolbar.inactiveBgColor = Color.clear;
        toolbar.activeTextColor = Color.white;
        toolbar.inactiveTextColor = Muted;

        // No priceText, no partCountText: the numbers are still computed every
        // time the structure changes, they are simply not drawn until the
        // Checkout tab shows them.
        Undo.AddComponent<UIBuildStats>(host);
    }

    /// <summary>
    /// The Pro | Lite mode switch, sitting in the band above the dock, second
    /// after the collapse control. It used to live in the top bar; the top bar
    /// is gone, and this control belongs with the dock whose contents it
    /// changes.
    ///
    /// Pro is the piece-by-piece builder (internally "Build"), Lite is the
    /// block-assembly experience (internally "Space"). Only the LABELS are
    /// renamed — the object names stay Btn_ModeBuild / Btn_ModeSpace so
    /// SpaceBootstrap's lookups and existing scenes keep working, the same
    /// convention ExperienceTabs uses for its historical "templates" naming.
    ///
    /// SpaceBootstrap wires both segments to SpaceModeController, which also
    /// owns the active-segment highlight at runtime.
    /// </summary>
    static void BuildModeSwitch(Transform canvas)
    {
        // White, like the status and hint pills beside it — not Surface grey.
        // Grey was chosen when the switch sat in a white top bar and needed to
        // read as an inset track. Now it sits on the backdrop, and #E2E2E2
        // against a gradient that runs from white to 80% grey simply vanished
        // into it. Same card colour, radius and pill shadow as its neighbours,
        // so the band reads as one row of the same kind of control.
        RectTransform track = Panel(UIChrome.ModeSwitchName, canvas, Card, UIChrome.PillCornerRadius);
        SetAnchors(track, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f));
        track.sizeDelta = new Vector2(ModeSwitchWidth, UIChrome.PillHeight);
        // Second in the band's left cluster, after the collapse control. Both
        // X values come from UIChrome so the two cannot be moved apart.
        track.anchoredPosition = new Vector2(UIChrome.ModeSwitchX, BandY);
        _theme.cardImages.Add(track.GetComponent<Image>());

        AddShadow(track, UIChrome.PillShadowScale);
        // The track lays its segments out with a HorizontalLayoutGroup, which
        // would otherwise take the shadow for a third segment and squeeze Pro
        // and Lite into two thirds of the width.
        var shadowLayout = track.Find("Shadow").gameObject.AddComponent<LayoutElement>();
        shadowLayout.ignoreLayout = true;

        var layout = track.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        Segment("Btn_ModeBuild", UIChrome.ProLabel, active: true);
        Segment("Btn_ModeSpace", UIChrome.LiteLabel, active: false);

        void Segment(string name, string label, bool active)
        {
            // Baked to the Pro-active state; SpaceModeController repaints.
            // NOT theme-registered: the controller owns these colors.
            Button b = SolidButton(name, track, label, active ? Ink : Color.clear,
                active ? Color.white : Muted, 15f);
            b.GetComponentInChildren<TextMeshProUGUI>(true).fontSize = 12.5f;
        }
    }

    /// <summary>
    /// The camera-controls card opened by the gear button: pick between the
    /// Walkthrough (WASD) and Professional (CAD) schemes, with a legend.
    /// </summary>
    /// <summary>
    /// "My Projects" — the card that opens from the rail's document button.
    ///
    /// Baked whole, including the hidden row template. ProjectPanelUI clones
    /// that template once per saved project and fills in the text; it builds
    /// no UI of its own. Its older sibling, the block library's PiecesPanel,
    /// still constructs itself at runtime — this one arrived after the
    /// builder became the single owner of the interface, so it never had a
    /// second owner to inherit.
    ///
    /// It shares the slot beside the rail with the controls card, so the two
    /// use identical anchors and UIChrome.CloseOtherRailPanels keeps only one
    /// of them open.
    /// </summary>
    static void BuildProjectsPanel(Transform canvas, Button projectsButton, BuildController buildController)
    {
        const float Width = 372f;
        const float FooterHeight = 58f;

        RectTransform panel = Panel("ProjectsPanel", canvas, Card, 20f);
        SetAnchors(panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
        panel.anchoredPosition = new Vector2(-80f, -104f);
        panel.sizeDelta = new Vector2(Width, 560f);
        AddShadow(panel);
        _theme.cardImages.Add(panel.GetComponent<Image>());

        var title = Text("Title", panel, "My Projects", _semiBold, 18f, Ink, TextAlignmentOptions.TopLeft);
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -18f), new Vector2(220f, 26f), new Vector2(0f, 1f));
        _theme.inkTexts.Add(title);

        var hint = Text("Hint", panel,
            "A project is one saved scene. Opening one replaces what is on screen (undoable).",
            _regular, 11f, Muted, TextAlignmentOptions.TopLeft);
        Place(hint.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -46f), new Vector2(Width - 40f, 30f), new Vector2(0f, 1f));
        hint.textWrappingMode = TextWrappingModes.Normal;
        _theme.mutedTexts.Add(hint);

        // × is the multiplication sign, not U+2715: the dingbat is missing
        // from the UI font and renders as a placeholder box.
        Button close = SolidButton("Btn_Close", panel, "×", Surface, Ink, 10f);
        Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-12f, -12f),
              new Vector2(32f, 32f), new Vector2(1f, 1f));
        _theme.surfaceImages.Add(close.GetComponent<Image>());
        _theme.inkTexts.Add(close.GetComponentInChildren<TextMeshProUGUI>(true));

        // Save row: name field, then the button that consumes it.
        const float SaveWidth = 118f;
        TMP_InputField nameInput = InputField("NameInput", panel, "Name this project…");
        Place((RectTransform)nameInput.transform, new Vector2(0f, 1f), new Vector2(16f, -84f),
              new Vector2(Width - 16f - SaveWidth - 8f - 16f, 40f), new Vector2(0f, 1f));

        Button save = SolidButton("Btn_SaveProject", panel, "Save project", Accent, Color.white, 12f);
        Place((RectTransform)save.transform, new Vector2(1f, 1f), new Vector2(-16f, -84f),
              new Vector2(SaveWidth, 40f), new Vector2(1f, 1f));
        save.GetComponentInChildren<TextMeshProUGUI>(true).fontSize = 12f;
        _theme.accentImages.Add(save.GetComponent<Image>());

        // The list, between the save row and the footer.
        RectTransform content = ProjectList(panel, FooterHeight);

        var empty = Text("Empty", panel,
            "No projects yet.\nBuild something and press \"Save project\".",
            _regular, 12.5f, Muted, TextAlignmentOptions.Center);
        SetAnchors(empty.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
        empty.rectTransform.anchoredPosition = new Vector2(0f, 200f);
        empty.rectTransform.sizeDelta = new Vector2(-40f, 60f);
        empty.textWrappingMode = TextWrappingModes.Normal;
        _theme.mutedTexts.Add(empty);

        // Footer: a hairline, then the secondary way in. Someone else's
        // project arrives as a code, and the paste dialog already accepts
        // both kinds and switches modes on its own — so it belongs here,
        // where a person looks for the projects they can open, rather than
        // buried in an overflow menu.
        RectTransform rule = Panel("FooterRule", panel, new Color(0f, 0f, 0f, 0.10f), 1f);
        Place(rule, new Vector2(0.5f, 0f), new Vector2(0f, FooterHeight), new Vector2(Width - 32f, 1f), new Vector2(0.5f, 0f));

        Button fromCode = SolidButton("Btn_OpenFromCode", panel, "Open from code", Surface, Ink, 10f);
        Place((RectTransform)fromCode.transform, new Vector2(0f, 0f), new Vector2(16f, 14f),
              new Vector2(150f, 32f), new Vector2(0f, 0f));
        fromCode.GetComponentInChildren<TextMeshProUGUI>(true).fontSize = 11.5f;
        _theme.surfaceImages.Add(fromCode.GetComponent<Image>());
        _theme.inkTexts.Add(fromCode.GetComponentInChildren<TextMeshProUGUI>(true));

        // Starting over belongs beside the two ways of arriving at existing
        // work: all three answer "what am I working on next".
        Button newProject = SolidButton("Btn_NewProject", panel, "New project", Surface, Ink, 10f);
        Place((RectTransform)newProject.transform, new Vector2(1f, 0f), new Vector2(-16f, 14f),
              new Vector2(126f, 32f), new Vector2(1f, 0f));
        newProject.GetComponentInChildren<TextMeshProUGUI>(true).fontSize = 11.5f;
        _theme.surfaceImages.Add(newProject.GetComponent<Image>());
        _theme.inkTexts.Add(newProject.GetComponentInChildren<TextMeshProUGUI>(true));

        GameObject template = ProjectRowTemplate(panel);

        var ui = Undo.AddComponent<ProjectPanelUI>(panel.gameObject);
        ui.buildController = buildController;
        ui.panel = panel.gameObject;
        ui.listContent = content;
        ui.nameInput = nameInput;
        ui.emptyLabel = empty;
        ui.rowTemplate = template;

        UnityEventTools.AddPersistentListener(projectsButton.onClick, new UnityAction(ui.TogglePanel));
        UnityEventTools.AddPersistentListener(close.onClick, new UnityAction(ui.ClosePanel));
        UnityEventTools.AddPersistentListener(save.onClick, new UnityAction(ui.SaveNewProject));
        UnityEventTools.AddPersistentListener(fromCode.onClick, new UnityAction(ui.OpenFromCode));
        UnityEventTools.AddPersistentListener(newProject.onClick, new UnityAction(ui.StartNewProject));

        panel.gameObject.SetActive(false);
    }

    /// <summary>Scrolling column of project rows; returns the content holder.</summary>
    static RectTransform ProjectList(RectTransform panel, float footerHeight)
    {
        var scrollGo = NewUI("ProjectScroll", panel);
        var scrollRt = (RectTransform)scrollGo.transform;
        Stretch(scrollRt);
        scrollRt.offsetMin = new Vector2(16f, footerHeight + 8f);
        scrollRt.offsetMax = new Vector2(-16f, -136f);

        var viewportGo = NewUI("Viewport", scrollRt);
        var viewport = (RectTransform)viewportGo.transform;
        Stretch(viewport);
        viewportGo.AddComponent<RectMask2D>();
        // Clear, but NOT raycast-disabled: this is the surface that catches
        // drag-scrolling inside the list.
        viewportGo.AddComponent<Image>().color = Color.clear;

        var contentGo = NewUI("Content", viewport);
        var content = (RectTransform)contentGo.transform;
        SetAnchors(content, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        var layout = contentGo.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;

        return content;
    }

    /// <summary>
    /// The hidden prototype row. Baked inactive and parented to the PANEL
    /// rather than the list, so the VerticalLayoutGroup never reserves space
    /// for it and Refresh's "destroy every child" never eats it.
    /// </summary>
    static GameObject ProjectRowTemplate(RectTransform panel)
    {
        RectTransform row = Panel("ProjectRowTemplate", panel, Surface, 12f);
        row.sizeDelta = new Vector2(340f, 64f);

        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;

        var thumbGo = NewUI("Thumb", row);
        var thumbRt = (RectTransform)thumbGo.transform;
        Place(thumbRt, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(72f, 48f), new Vector2(0f, 0.5f));
        var raw = thumbGo.AddComponent<RawImage>();
        raw.raycastTarget = false;

        // Text column: from the thumbnail's right edge to the first icon.
        // Four 26-unit icons and their gaps come to 116 including the right
        // inset, which leaves 132 for the name — where four WORD buttons left
        // six, and six units of name renders as nothing.
        const float TextLeft = 92f;
        const float TextRight = 116f;

        var name = Text("Name", row, "Project", _semiBold, 13.5f, Ink, TextAlignmentOptions.TopLeft);
        SetAnchors(name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
        name.rectTransform.anchoredPosition = new Vector2(TextLeft, -9f);
        name.rectTransform.sizeDelta = new Vector2(-(TextLeft + TextRight), 20f);
        name.overflowMode = TextOverflowModes.Ellipsis;
        name.raycastTarget = false;

        // Two badges and nothing else. The line used to read "Pro · 24 parts
        // · 880x880x1408 mm · $1,240", which is four facts where the eye
        // wants one: which kind of project this is, and what it costs. The
        // part count and dimensions belong to the thing once it is open, not
        // to choosing between things in a list.
        Badge("Badge_Mode", UIChrome.ProLabel, TextLeft, 46f);
        Badge("Badge_Cost", "A$0", TextLeft + 52f, 70f);

        // Icons, in the order you would reach for them: open the project,
        // save over it, copy its code, throw it away. Words cost four times
        // the width and said nothing the icon does not — "Code" least of all,
        // which named the format rather than the action.
        const float IconSize = 26f;
        const float IconStep = IconSize + 2f;
        RowButton("Btn_Open", _folderOpenIcon, "Open", -10f - IconStep * 3f, Ink, confirming: false);
        RowButton("Btn_Update", _refreshIcon, "Upd", -10f - IconStep * 2f, Ink, confirming: true);
        RowButton("Btn_Code", _shareIcon, "Code", -10f - IconStep, Ink, confirming: false);
        RowButton("Btn_Delete", _trashIcon, "Del", -10f, Danger, confirming: true);

        row.gameObject.SetActive(false);
        return row.gameObject;

        void Badge(string name_, string value, float x, float width)
        {
            // A darker pill on the row's light surface. Noticeable without
            // being loud: these label the card, they are not controls, so
            // they must not compete with the icons beside them.
            RectTransform pill = Panel(name_, row, new Color(0f, 0f, 0f, 0.09f), 9f);
            SetAnchors(pill, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            pill.anchoredPosition = new Vector2(x, -30f);
            pill.sizeDelta = new Vector2(width, 19f);
            pill.GetComponent<Image>().raycastTarget = false;

            var label = Text("Text", pill, value, _semiBold, 10.5f, Ink, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            label.raycastTarget = false;
        }

        void RowButton(string name_, Sprite icon, string fallback, float right,
                       Color fg, bool confirming)
        {
            // Transparent: four filled pills in a row read as a toolbar
            // bolted to the card. The icons carry it, and the row's own hover
            // tint says the whole card is live.
            Button b = SolidButton(name_, row, icon != null ? string.Empty : fallback,
                                   Color.clear, fg, 9f);
            Place((RectTransform)b.transform, new Vector2(1f, 0.5f), new Vector2(right, 0f),
                  new Vector2(IconSize, IconSize), new Vector2(1f, 0.5f));

            var lbl = b.GetComponentInChildren<TextMeshProUGUI>(true);
            lbl.fontSize = 10f;

            if (icon != null)
            {
                var iconGo = NewUI("Icon", b.transform);
                Place((RectTransform)iconGo.transform, new Vector2(0.5f, 0.5f), Vector2.zero,
                      new Vector2(14f, 14f), new Vector2(0.5f, 0.5f));
                var img = iconGo.AddComponent<UnityEngine.UI.Image>();
                img.sprite = icon;
                img.color = fg;
                img.preserveAspect = true;
                img.raycastTarget = false;
            }

            // Deliberately NOT registered with the theme. Rows are clones the
            // theme has never seen, so ProjectPanelUI.RestyleRows repaints
            // them on a palette change; registering the template would only
            // hand the theme a permanently hidden object to repaint.

            if (!confirming)
                return;

            var confirm = Undo.AddComponent<UIConfirmingButton>(b.gameObject);
            confirm.label = lbl;
        }
    }

    /// <summary>
    /// A single-line text box. TMP_InputField needs its viewport, text and
    /// placeholder wired by hand; nothing else in the baked UI needed one
    /// until the projects panel.
    /// </summary>
    static TMP_InputField InputField(string name, Transform parent, string placeholder)
    {
        RectTransform box = Panel(name, parent, Surface, 10f);
        _theme.surfaceImages.Add(box.GetComponent<Image>());

        var areaGo = NewUI("TextArea", box);
        var area = (RectTransform)areaGo.transform;
        Stretch(area);
        area.offsetMin = new Vector2(12f, 6f);
        area.offsetMax = new Vector2(-12f, -6f);
        areaGo.AddComponent<RectMask2D>();

        var ghost = Text("Placeholder", area, placeholder, _regular, 13f, Muted, TextAlignmentOptions.MidlineLeft);
        Stretch(ghost.rectTransform);
        ghost.fontStyle = FontStyles.Italic;
        _theme.mutedTexts.Add(ghost);

        var value = Text("Text", area, string.Empty, _regular, 13f, Ink, TextAlignmentOptions.MidlineLeft);
        Stretch(value.rectTransform);
        _theme.inkTexts.Add(value);

        var input = Undo.AddComponent<TMP_InputField>(box.gameObject);
        input.targetGraphic = box.GetComponent<Image>();
        input.textViewport = area;
        input.textComponent = value;
        input.placeholder = ghost;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 48;
        input.fontAsset = _regular;
        input.caretColor = Ink;
        input.customCaretColor = true;
        input.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.35f);
        return input;
    }

    static void BuildControlSettingsPanel(Transform canvas, Button gearButton)
    {
        CameraControlManager manager = EnsureCameraControls();

        RectTransform panel = Panel("ControlSettingsPanel", canvas, Card, 20f);
        SetAnchors(panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
        // Immediately left of the utility rail (rail sits at -24 and is 44
        // wide), top-aligned with it, so the panel reads as belonging to the
        // gear that opened it.
        panel.anchoredPosition = new Vector2(-80f, -104f);
        panel.sizeDelta = new Vector2(340f, 344f);
        AddShadow(panel);
        _theme.cardImages.Add(panel.GetComponent<Image>());

        var title = Text("Title", panel, "Controls", _semiBold, 18f, Ink, TextAlignmentOptions.TopLeft);
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -18f), new Vector2(200f, 26f), new Vector2(0f, 1f));
        _theme.inkTexts.Add(title);

        // Close button — the panel previously had no way out except the gear.
        // Matches the one on the Projects panel: same grey pill, same size and
        // offset, and the multiplication sign rather than U+2715 (the dingbat
        // is missing from the UI font and renders as a placeholder box).
        Button close = SolidButton("Btn_Close", panel, "×", Surface, Ink, 10f);
        Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-12f, -12f),
              new Vector2(32f, 32f), new Vector2(1f, 1f));
        _theme.surfaceImages.Add(close.GetComponent<Image>());
        var closeLabel = close.GetComponentInChildren<TextMeshProUGUI>(true);
        if (closeLabel != null)
            _theme.inkTexts.Add(closeLabel);

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
        UnityEventTools.AddPersistentListener(close.onClick, new UnityAction(ui.ClosePanel));
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

    // BuildStatsReadout is gone with the top bar it was inset into. It drew a
    // running "$0 · 0 parts" across the top of the screen at all times; the
    // price belongs on the Checkout tab, beside the thing being priced.
    //
    // The UIBuildStats COMPONENT was not removed with it — see BuildUIServices
    // for why deleting it would have silently priced the whole app at zero.

    static void BuildLeftPanel(Transform canvas, BuildController buildController, PartDatabase database,
        PanelGhostController panelGhost, UIToolbarController toolbar)
    {
        RectTransform panel = DockPage("PartsPanel", canvas);

        // No page title or subtitle. The left section is the shared Tools /
        // Parts list (BuildColumn) — which is what the title used to name —
        // and the description moved to the dock footer, where it has a
        // full-width line instead of a 280-wide box.

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
        palette.buttonFont = null; // Inherit the selected typography set from the template.
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
        RectTransform panel = DockPage("GuidedToolsPanel", canvas);

        // No page title or subtitle here either — see BuildLeftPanel.

        // No Beams tool here: the Parts tab's Horizontal beam card covers
        // single beam placement, so the guided panel keeps Frames + Panels.
        Button t1 = TemplateToolButton(panel, "Btn_T1_Posts", "Frames",
            "Stand frames on the grid · 4 clicks", -131f, _frameToolIcon);
        Button t3 = TemplateToolButton(panel, "Btn_T3_PanelBay", "Panels",
            "Add panels between frames · 3 clicks", -197f, _panelToolIcon);

        // The tools run across the dock instead of stacking down a column.
        PlaceToolRow(t1, 0);
        PlaceToolRow(t3, 1);

        // No hint box. It filled the right half of the dock with a large grey
        // slab restating what the status pill above the dock already says:
        // UIStatusBar publishes TemplateSession.StatusMessage verbatim while
        // the Guided experience is active, and prefixes it with the armed
        // tool's name, so the pill carried strictly more than the box did.
        // GuidedModeController.RefreshHint already no-ops on a null
        // guidedHintText, so the step guidance keeps flowing either way.

        void PlaceToolRow(Button b, int index)
        {
            var rt = (RectTransform)b.transform;
            SetAnchors(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            // Same cell and gutter as the Parts gallery, so the two tabs line
            // up card for card.
            rt.sizeDelta = UIChrome.PartCardSize;
            rt.anchoredPosition = new Vector2(
                DockColumnWidth + SectionPadding
                    + index * (UIChrome.PartCardSize.x + UIChrome.PartCardSpacing.x),
                -16f);
        }

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
        // Only the tools panel, and only so the controller can find the two
        // tool buttons to highlight. Which panel is SHOWN is DockTabs' alone.
        guided.guidedToolsPanel = panel.gameObject;

        UnityEventTools.AddPersistentListener(t1.onClick, new UnityAction(guided.SelectPostsTool));
        UnityEventTools.AddPersistentListener(t3.onClick, new UnityAction(guided.SelectPanelBayTool));

        if (toolbar != null)
            toolbar.guidedModeController = guided;

        panel.gameObject.SetActive(false);
        return guided;
    }

    // AddExperienceTabs is gone. It built a "Tools | Parts" switch into the top
    // of EACH page, which the dock's left column replaced — after which the
    // rows were baked hidden purely so GuidedBootstrap would find one and not
    // rebuild it (rebuilding shifted every sibling below down 56px). That
    // bootstrap no longer looks, so the rows had nothing left to do.

    /// <summary>Width of the dock's left column: the Tools/Parts list.</summary>
    const float DockColumnWidth = 320f;

    // Dock geometry lives on UIChrome, not here: the runtime bootstraps that
    // reposition chrome (HintPillBootstrap in particular) cannot see the
    // editor assembly, and a second hard-coded copy of the pill baseline is
    // precisely how the hint pill ended up back behind the dock.
    const float DockInset = UIChrome.DockInset;
    const float DockBottom = UIChrome.DockBottom;
    const float DockNavHeight = UIChrome.DockNavHeight;
    const float DockBodyHeight = UIChrome.DockBodyHeight;
    const float DockFooterHeight = UIChrome.DockFooterHeight;
    const float DockHeight = UIChrome.DockHeight;     // 330
    const float DockTop = UIChrome.DockTop;           // 354
    const float BandY = UIChrome.BandY;               // 366

    /// <summary>Width of the Pro | Lite switch. Defined on UIChrome so the
    /// runtime can see where the band's left cluster ends.</summary>
    const float ModeSwitchWidth = UIChrome.ModeSwitchWidth;

    /// <summary>
    /// The dock's tab controller, kept from BuildDock so the pages built
    /// after it (BuildLeftPanel, BuildGuidedToolsPanel) can be handed to it.
    /// Wired in RebuildCore once every page exists.
    /// </summary>
    static DockTabs _dockTabs;
    static DockFooterCaption _dockFooterCaption;

    /// <summary>The shared Tools/Parts column, hidden on non-Build tabs.</summary>
    static GameObject _buildColumn;

    /// <summary>Width of a guided tool button laid out across the dock.</summary>
    const float ToolButtonWidth = 300f;

    static RectTransform BuildCardGrid(RectTransform panel, out RectTransform content)
    {
        var scrollGo = NewUI("PartsScroll", panel);
        var scrollRt = (RectTransform)scrollGo.transform;
        SetAnchors(scrollRt, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));
        // Fills the dock body to the right of the left column, clear of the
        // section divider rather than hard against it.
        scrollRt.offsetMin = new Vector2(DockColumnWidth + SectionPadding, 14f);
        scrollRt.offsetMax = new Vector2(-16f, -14f);

        var viewportGo = NewUI("Viewport", scrollRt);
        var viewport = (RectTransform)viewportGo.transform;
        Stretch(viewport);
        viewportGo.AddComponent<RectMask2D>();
        var viewportImg = viewportGo.AddComponent<Image>();
        viewportImg.color = Color.clear;

        // Wide, short gallery: two rows of cards running to the right, rather
        // than a column down a tall panel. Metrics come from UIChrome because
        // UIPartsPalette.Rebuild() re-applies them at runtime; see the note
        // there on the four tools this hid.
        var contentGo = NewUI("Content_Grid", viewport);
        content = (RectTransform)contentGo.transform;
        SetAnchors(content, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        var grid = contentGo.AddComponent<GridLayoutGroup>();
        grid.cellSize = UIChrome.PartCardSize;
        grid.spacing = UIChrome.PartCardSpacing;
        // No vertical padding: two 96 rows plus one 12 gap is 204, and the
        // viewport is 208. The 4px top and bottom this used to carry pushed
        // that to 212 and put the second row back outside the mask.
        grid.padding = new RectOffset(0, 8, 0, 0);
        grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
        grid.constraintCount = UIChrome.PartCardRows;
        grid.childAlignment = TextAnchor.MiddleLeft;

        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = true;
        scrollRect.vertical = false;
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
        // Centred in the band above the dock.
        //
        // Anchored bottom-CENTRE, with a centre pivot, so the pill grows
        // equally in both directions as UIStatusBar sizes it to its message
        // and the text stays on the screen's centre line whatever it says.
        // It was pinned to the left edge of the band precisely because the
        // collapse control used to sit in the middle and a centred pill would
        // have grown into it; the collapse control is now at the far left, so
        // the middle is free and the message can have it.
        RectTransform pill = Panel("StatusPill", canvas, Card, UIChrome.PillCornerRadius);
        SetAnchors(pill, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        pill.anchoredPosition = new Vector2(0f, BandY);
        pill.sizeDelta = new Vector2(420f, UIChrome.PillHeight);
        AddShadow(pill, UIChrome.PillShadowScale);
        _theme.cardImages.Add(pill.GetComponent<Image>());

        RectTransform dot = Panel("Dot", pill, Accent, 4f);
        Place(dot, new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(8f, 8f), new Vector2(0f, 0.5f));
        dot.GetComponent<Image>().color = new Color32(66, 126, 220, 255);

        var status = Text("Txt_Status", pill, "Ready", _medium, 13f, Ink, TextAlignmentOptions.MidlineLeft);
        SetAnchors(status.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));
        status.rectTransform.offsetMin = new Vector2(34f, 0f);
        status.rectTransform.offsetMax = new Vector2(-16f, 0f);
        // One fixed-size line. UIStatusBar sizes and centres the pill in the
        // space between its neighbours; only overflow is ellipsized.
        status.textWrappingMode = TextWrappingModes.NoWrap;
        status.overflowMode = TextOverflowModes.Ellipsis;
        status.enableAutoSizing = false;
        status.fontSize = 12f;
        _theme.inkTexts.Add(status);

        var statusBar = Undo.AddComponent<UIStatusBar>(pill.gameObject);
        statusBar.buildController = buildController;
        statusBar.statusText = status;
    }

    static void BuildHint(Transform canvas)
    {
        // Baked exactly like the status pill: same card colour, same corner
        // radius, same shadow. It used to be baked at 85% white with a 17
        // radius, and HintPillBootstrap then copied the status pill's values
        // over the top at runtime — so the pill changed appearance the moment
        // play started, and the editor never showed what shipped.
        RectTransform hint = Panel("HintPill", canvas, Card, UIChrome.PillCornerRadius);
        SetAnchors(hint, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
        AddShadow(hint, UIChrome.PillShadowScale);
        // Right end of the band above the dock, flush with the dock's right
        // edge. Width is corrected at runtime by HintPillBootstrap, which
        // sizes the pill to hug the text exactly — it reads the same
        // UIChrome geometry, so the baked and runtime positions agree.
        hint.anchoredPosition = new Vector2(-DockInset, BandY);
        hint.sizeDelta = new Vector2(340f, UIChrome.PillHeight);
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

    /// <summary>
    /// The bottom dock from the NEOSPACE web UI mockup — a light nav strip of
    /// tabs over a dark body, with a hint footer, spanning the width of the
    /// screen.
    ///
    /// This builds the SHELL only. The existing PartsPanel, GuidedToolsPanel
    /// and SpacePanel are reparented into <c>Dock/Body</c> as pages in a
    /// follow-up, rather than being rebuilt: eleven runtime scripts find them
    /// by name, six copy PartsPanel's sprite as a style source, and
    /// SelectionBootstrap reaches into PartsPanel/Tabs, /PartsScroll,
    /// /Subtitle and /Btn_PanelTool. Keeping the objects keeps all of that
    /// working.
    /// </summary>
    static void BuildDock(Transform canvas)
    {
        RectTransform dock = Panel(UIChrome.DockName, canvas, Ink, 13f);
        SetAnchors(dock, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
        dock.sizeDelta = new Vector2(-2f * DockInset, DockHeight);
        dock.anchoredPosition = new Vector2(0f, DockBottom);
        AddShadow(dock);

        // Nav strip: pure white, full width, split into three equal blocks.
        // Card, not Surface — Surface is the light grey used for controls, and
        // against it the strip read as off-white rather than white.
        RectTransform nav = Panel("Nav", dock, Card, 13f);
        SetAnchors(nav, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        nav.sizeDelta = new Vector2(0f, DockNavHeight);
        nav.anchoredPosition = Vector2.zero;
        _theme.cardImages.Add(nav.GetComponent<Image>());

        // Body: the dock's own dark fill shows through; pages are parented here.
        var bodyGo = NewUI("Body", dock);
        var body = (RectTransform)bodyGo.transform;
        SetAnchors(body, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        body.sizeDelta = new Vector2(0f, DockBodyHeight);
        body.anchoredPosition = new Vector2(0f, -DockNavHeight);

        // Tabs need the body: two of the three own a page in it.
        BuildDockTabs(nav, body);
        BuildColumn(body);

        // Footer: one line of contextual hint, plus the wordmark.
        var footerGo = NewUI("Footer", dock);
        var footer = (RectTransform)footerGo.transform;
        SetAnchors(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
        footer.sizeDelta = new Vector2(0f, DockFooterHeight);
        footer.anchoredPosition = Vector2.zero;

        // Hairline along the top of the footer, matching the section dividers
        // and the tab seam, so the footer reads as its own band rather than
        // as text floating at the bottom of the content.
        RectTransform footerRule = Panel("FooterSeam", footer, Seam, 0f);
        SetAnchors(footerRule, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        footerRule.sizeDelta = new Vector2(-2f * SectionPadding, 1f);
        footerRule.anchoredPosition = Vector2.zero;
        var footerRuleImg = footerRule.GetComponent<Image>();
        footerRuleImg.sprite = null;
        footerRuleImg.raycastTarget = false;

        // The open page's standing description — the line that used to sit
        // under the "Tools"/"Parts" heading in the dock's left column, which
        // now holds the Tools/Parts list itself. NOT the live step guidance:
        // that stays in the status pill, which also carries selection counts
        // and placement errors, and showing it here too put the same sentence
        // on screen twice.
        var hint = Text("Txt_DockHint", footer, "Pick a part type — the size is chosen while placing",
                        _regular, 12f, Muted, TextAlignmentOptions.MidlineLeft);
        Place(hint.rectTransform, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(700f, 20f), new Vector2(0f, 0.5f));
        _theme.mutedTexts.Add(hint);

        var caption = Undo.AddComponent<DockFooterCaption>(footerGo);
        caption.caption = hint;
        // The footer describes whichever page is open, and only DockTabs
        // knows which that is. Assigned rather than found at runtime so the
        // reference is visible in the scene and checkable in edit mode.
        _dockFooterCaption = caption;

        var mark = Text("Txt_DockMark", footer, "Designed to come together.  NEOSPACE",
                        _regular, 11f, Muted, TextAlignmentOptions.MidlineRight);
        Place(mark.rectTransform, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(340f, 20f), new Vector2(1f, 0.5f));
        _theme.mutedTexts.Add(mark);

        // Collapse control: FIRST in the band, at the far left, ahead of the
        // Pro | Lite switch. It was centred, which put a button in the middle
        // of the status message and left the far left of the band empty.
        //
        // Sized and rounded like the pills (38 tall, radius 19) rather than as
        // its own 32-unit square, so the band reads as one row of controls of
        // equal height. At that size the radius makes it a circle.
        //
        // Drawn with a sprite, not a glyph: DM Sans has neither U+2304 (⌄) nor
        // U+2715 (✕), and TMP silently substitutes U+25A1 (□) for a missing
        // character, so a typographic chevron renders as a white box. Text
        // fallback only for the case where the icon asset is missing.
        Button collapse = SolidButton("Btn_DockToggle", canvas,
                                      _arrowDownIcon == null ? "v" : string.Empty,
                                      Card, Ink, UIChrome.PillCornerRadius);
        var collapseRt = (RectTransform)collapse.transform;
        SetAnchors(collapseRt, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f));
        collapseRt.sizeDelta = new Vector2(UIChrome.ToggleSize, UIChrome.ToggleSize);
        collapseRt.anchoredPosition = new Vector2(UIChrome.ToggleX, BandY);
        // No shadow: the rectangular shadow sprite behind a 38-unit circle
        // reads as a smudge, and the mode switch beside it carries none either.
        _theme.cardImages.Add(collapse.GetComponent<Image>());

        var collapseBehaviour = Undo.AddComponent<DockCollapse>(collapse.gameObject);
        collapseBehaviour.dock = dock;

        if (_arrowDownIcon != null)
        {
            var chevGo = NewUI("Icon", collapse.transform);
            Place((RectTransform)chevGo.transform, new Vector2(0.5f, 0.5f), Vector2.zero,
                  new Vector2(16f, 16f), new Vector2(0.5f, 0.5f));
            var chevImg = chevGo.AddComponent<Image>();
            chevImg.sprite = _arrowDownIcon;
            chevImg.color = Muted;
            chevImg.preserveAspect = true;
            chevImg.raycastTarget = false;
            _theme.mutedIcons.Add(chevImg);
            // Rotated 180° while collapsed, so the arrow always points the way
            // the dock will go.
            collapseBehaviour.icon = (RectTransform)chevGo.transform;
        }
        else
        {
            var collapseLabel = collapse.GetComponentInChildren<TextMeshProUGUI>(true);
            if (collapseLabel != null)
            {
                _theme.inkTexts.Add(collapseLabel);
                collapseBehaviour.icon = collapseLabel.rectTransform;
            }
        }
    }

    /// <summary>
    /// Hairline used to divide the dock's sections, and the active tab from
    /// the body below it: white at low opacity, as in the mockup.
    /// </summary>
    static readonly Color Seam = new Color(1f, 1f, 1f, 0.14f);

    /// <summary>Corner radius on the top of the selected tab's block.</summary>
    const float TabCornerRadius = 14f;

    /// <summary>
    /// Gap between a section divider and the content on either side of it, so
    /// the cards do not sit hard against the hairline.
    /// </summary>
    const float SectionPadding = 28f;

    /// <summary>
    /// The dock's first section: the Tools / Parts list, laid out like the
    /// mockup's left-hand column.
    ///
    /// This choice used to be the dock's own tab row. The dock's tabs are now
    /// Build / Blocks / Checkout, so it moved down here. It is built once and
    /// SHARED by both pages rather than duplicated into each, which is also
    /// why it is a child of Body and not of either panel — DockTabs hides it
    /// on the tabs that have no use for it.
    /// </summary>
    static void BuildColumn(RectTransform body)
    {
        var columnGo = NewUI("BuildColumn", body);
        var column = (RectTransform)columnGo.transform;
        SetAnchors(column, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
        column.sizeDelta = new Vector2(DockColumnWidth, 0f);
        column.anchoredPosition = Vector2.zero;

        var tabs = Undo.AddComponent<BuildColumnTabs>(columnGo);
        _buildColumn = columnGo;

        Text("ColumnHeading", column, "BUILD", _medium, UIChrome.DockSectionFontSize,
             UIChrome.DockDimText, TextAlignmentOptions.MidlineLeft);

        AddRow("Btn_Build_Tools", "Tools", UIInteractionState.Experience.Guided, 0);
        AddRow("Btn_Build_Parts", "Parts", UIInteractionState.Experience.Expert, 1);

        StyleColumn(columnGo);

        SectionSeam(column, DockColumnWidth);

        void AddRow(string name, string label, UIInteractionState.Experience experience, int index)
        {
            const float rowHeight = 40f;

            Button b = SolidButton(name, column, string.Empty, Color.clear, Card, 9f);
            var rt = (RectTransform)b.transform;
            SetAnchors(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            rt.sizeDelta = new Vector2(DockColumnWidth - 56f, rowHeight);
            rt.anchoredPosition = new Vector2(28f, -28f - index * (rowHeight + 4f));

            // Same face, size and colour as the collections column a tab
            // away: the two lists sit in the same place on screen and a
            // reader should not be able to tell which is which by the type.
            var text = Text("Label", rt, label, _regular, UIChrome.DockRowFontSize,
                            UIChrome.DockText, TextAlignmentOptions.MidlineLeft);
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(14f, 0f);
            text.raycastTarget = false;

            AddHoverOutline(rt, 9f);

            tabs.rows.Add(new BuildColumnTabs.Row
            {
                button = b,
                label = text,
                background = b.GetComponent<Image>(),
                experience = experience
            });
        }
    }

    /// <summary>
    /// The dock's tab row: Build, Blocks and Checkout as three equal blocks
    /// with centred titles, following the mockup.
    ///
    /// The active block is filled with the same Ink as the body beneath it so
    /// the tab and its content read as one surface, separated only by a
    /// hairline. That is why the blocks are square-cornered and flush: any
    /// rounding or gap would break the join.
    ///
    /// Blocks and Checkout have no real content yet and say so on their page,
    /// rather than opening a blank panel.
    /// </summary>
    static void BuildDockTabs(RectTransform nav, RectTransform body)
    {
        var tabsGo = NewUI("Tabs", nav);
        var tabsRt = (RectTransform)tabsGo.transform;
        Stretch(tabsRt);
        tabsRt.offsetMin = Vector2.zero;
        tabsRt.offsetMax = Vector2.zero;

        var dockTabs = Undo.AddComponent<DockTabs>(tabsGo);
        _dockTabs = dockTabs;

        // One Blocks page, shown by BOTH modes' Blocks tabs. They list the
        // same library; only what you may do there differs, and that is the
        // runtime script's business rather than a second copy of the layout.
        GameObject blocksPage = BuildBlocksPage(body);

        // Pro: Build | Blocks | Checkout
        AddTab("Btn_DockTab_Build", "Build", DockTabs.Page.Build, DockTabs.Mode.Pro, 0, null);
        AddTab("Btn_DockTab_Blocks", "Blocks", DockTabs.Page.Blocks, DockTabs.Mode.Pro, 1, blocksPage);
        AddTab("Btn_DockTab_Checkout", "Checkout", DockTabs.Page.Checkout, DockTabs.Mode.Pro, 2,
               PlaceholderPage("CheckoutPage", body, "Checkout",
                               "Your parts list and order summary will live here. Not built yet."));

        // Lite: Blocks | Style | Checkout. The same three thirds of the strip;
        // DockTabs switches whole sets, so only one occupies them at a time.
        AddTab("Btn_LiteTab_Blocks", "Blocks", DockTabs.Page.LiteBlocks, DockTabs.Mode.Lite, 0, blocksPage);
        AddTab("Btn_LiteTab_Style", "Style", DockTabs.Page.Style, DockTabs.Mode.Lite, 1,
               PlaceholderPage("LiteStylePage", body, "Style",
                               "Colour distributions and finishes will live here. Not built yet."));
        AddTab("Btn_LiteTab_Checkout", "Checkout", DockTabs.Page.Checkout, DockTabs.Mode.Lite, 2,
               PlaceholderPage("LiteCheckoutPage", body, "Checkout",
                               "Your block list and order summary will live here. Not built yet."));

        void AddTab(string name, string label, DockTabs.Page page, DockTabs.Mode mode,
                    int index, GameObject pageBody)
        {
            // Thirds of the strip, by anchor, so they stay equal at any width.
            Button b = SolidButton(name, tabsRt, string.Empty, Color.clear, Ink, 0f);
            var rt = (RectTransform)b.transform;
            SetAnchors(rt, new Vector2(index / 3f, 0f), new Vector2((index + 1) / 3f, 1f), new Vector2(0.5f, 0.5f));
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // The button's own image is the raycast target only.
            var hit = b.GetComponent<Image>();
            hit.sprite = null;
            hit.color = Color.clear;

            // The fill, in two parts. A rounded rect gives the block its two
            // rounded TOP corners; a plain rectangle over the bottom strip
            // squares the lower two off again, so the block meets the body
            // flush. Rounding all four would leave two white notches where it
            // joins, since the nav behind it is white.
            RectTransform fill = Panel("Fill", rt, Ink, TabCornerRadius);
            Stretch(fill);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            var fillImg = fill.GetComponent<Image>();
            fillImg.raycastTarget = false;

            RectTransform foot = Panel("FillFoot", rt, Ink, 0f);
            SetAnchors(foot, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            foot.sizeDelta = new Vector2(0f, TabCornerRadius);
            foot.anchoredPosition = Vector2.zero;
            var footImg = foot.GetComponent<Image>();
            footImg.sprite = null;
            footImg.raycastTarget = false;

            var text = Text("Label", rt, label, _display != null ? _display : _semiBold,
                            16f, Muted, TextAlignmentOptions.Midline);
            Stretch(text.rectTransform);

            // Hairline where the filled block meets the body.
            RectTransform seam = Panel("Seam", rt, Seam, 0f);
            SetAnchors(seam, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            seam.sizeDelta = new Vector2(0f, 1f);
            seam.anchoredPosition = Vector2.zero;

            var seamImg = seam.GetComponent<Image>();
            seamImg.sprite = null;
            seamImg.enabled = false;   // DockTabs turns the active one on

            AddHoverUnderline(rt, text, 14f);

            dockTabs.tabs.Add(new DockTabs.Tab
            {
                button = b,
                label = text,
                background = fillImg,
                backgroundFoot = footImg,
                seam = seamImg,
                page = page,
                body = pageBody,
                mode = mode
            });
        }
    }

    /// <summary>
    /// A dock page for a tab whose real content does not exist yet: the same
    /// frame as the built pages, saying plainly what will go there. Better
    /// than a blank black panel, and better than a tab that does nothing.
    /// </summary>
    static void StyleColumn(GameObject go)
    {
        var style = Undo.AddComponent<UIDockColumnStyle>(go);
        style.regularFont = _regular;
        style.emphasisFont = _semiBold;
        style.Apply();
    }

    static GameObject PlaceholderPage(string name, RectTransform body, string title, string blurb)
    {
        var go = NewUI(name, body);
        var rt = (RectTransform)go.transform;
        Stretch(rt);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var heading = Text("Title", rt, title, _display != null ? _display : _semiBold,
                           17f, Card, TextAlignmentOptions.TopLeft);
        Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(28f, -28f),
              new Vector2(DockColumnWidth - 40f, 26f), new Vector2(0f, 1f));

        var text = Text("Blurb", rt, blurb, _regular, 12.5f, Muted, TextAlignmentOptions.TopLeft);
        Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(28f, -60f),
              new Vector2(DockColumnWidth - 2f * SectionPadding, 60f), new Vector2(0f, 1f));
        text.textWrappingMode = TextWrappingModes.Normal;

        SectionSeam(rt, DockColumnWidth);

        StyleColumn(go);
        go.SetActive(false);
        return go;
    }

    /// <summary>
    /// A hover underline that draws itself out from the centre, under a tab's
    /// title. Sized to the title rather than the tab: the tabs are a third of
    /// the dock wide each, and a full-width rule would read as a border.
    /// </summary>
    static void AddHoverUnderline(RectTransform tab, TextMeshProUGUI title, float y)
    {
        float width = Mathf.Max(48f, Mathf.Ceil(title.GetPreferredValues(title.text).x) + 6f);

        RectTransform rule = Panel("HoverUnderline", tab, Muted, 0f);
        SetAnchors(rule, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        // 1.5 rather than 2: the canvas scales up from the 1920 reference, so
        // a 2-unit rule landed near 3 real pixels and read as a heavy bar.
        rule.sizeDelta = new Vector2(width, 1.5f);
        rule.anchoredPosition = new Vector2(0f, y);

        var img = rule.GetComponent<Image>();
        img.sprite = null;
        img.raycastTarget = false;
        img.color = new Color(Muted.r, Muted.g, Muted.b, 0f);

        var hover = Undo.AddComponent<UIHoverReveal>(tab.gameObject);
        hover.target = img;
        hover.shownAlpha = 1f;
        hover.sweepHorizontally = true;
    }

    /// <summary>
    /// A hover outline around a Tools/Parts row, using a real stroke sprite so
    /// the corners stay rounded at hairline thickness.
    /// </summary>
    static void AddHoverOutline(RectTransform row, float cornerRadiusPx)
    {
        RectTransform ring = Panel("HoverOutline", row, Card, cornerRadiusPx);
        Stretch(ring);
        ring.offsetMin = ring.offsetMax = Vector2.zero;

        var img = ring.GetComponent<Image>();
        if (_outlineSprite != null)
            img.sprite = _outlineSprite;
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0f);

        var hover = Undo.AddComponent<UIHoverReveal>(row.gameObject);
        hover.target = img;
        hover.shownAlpha = 0.38f;
    }

    /// <summary>Vertical hairline dividing two sections of the dock body.</summary>
    /// <summary>
    /// Write UIThemeController.HighlightColor into the scene materials that
    /// answer "which one?" — the valid placement ghost and the selection.
    ///
    /// They are ASSETS, so the colour would otherwise live in three places
    /// (two .mat files and the constant) and drift the moment one changed.
    /// Writing them here makes the constant the only decision and the
    /// materials derived from it, the same way every other appearance in
    /// this UI is derived from the builder.
    ///
    /// MAT_Ghost_Invalid is deliberately left alone. Red does not mean
    /// "here", it means "no", and a refusal that looked like a highlight
    /// would be the one genuinely dangerous confusion in the set.
    ///
    /// Alpha is preserved: these are translucent overlays and each was tuned
    /// for how much of the part beneath should still read.
    /// </summary>
    static void ApplyHighlightMaterials()
    {
        foreach (string path in new[]
        {
            "Assets/Materials/Ghost/MAT_Ghost_Valid.mat",
            "Assets/Materials/MAT_Selected.mat",
        })
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Debug.LogWarning($"[ConfiguratorUIBuilder] Highlight material not found: {path}");
                continue;
            }

            Undo.RecordObject(material, "Highlight colour");
            foreach (string property in new[] { "_BaseColor", "_Color" })
            {
                if (!material.HasProperty(property))
                    continue;
                Color tinted = UIThemeController.HighlightColor;
                tinted.a = material.GetColor(property).a;
                material.SetColor(property, tinted);
            }
            EditorUtility.SetDirty(material);
        }
    }

    /// <summary>
    /// "Add a Block" asks which way. Two routes to the same result, and
    /// neither is obviously the default — one makes a block from what you
    /// have built, the other brings in one someone sent you — so it asks
    /// rather than guessing and hiding the other behind a menu.
    ///
    /// Baked beside the confirmation modal and for the same reasons: one of
    /// it, at canvas level, last, so it draws over the dock.
    /// </summary>
    static void BuildAddBlockDialog(Transform canvas)
    {
        var host = NewUI("AddBlockDialog", canvas);
        var ui = Undo.AddComponent<UIAddBlockDialog>(host);

        RectTransform backdrop = Panel("Backdrop", host.transform, new Color(0f, 0f, 0f, 0.45f), 0f);
        Stretch(backdrop);
        backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
        backdrop.GetComponent<Image>().sprite = null;
        Button dismiss = Undo.AddComponent<Button>(backdrop.gameObject);
        dismiss.transition = Selectable.Transition.None;

        RectTransform card = Panel("Card", host.transform, Card, 18f);
        Place(card, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 304f), new Vector2(0.5f, 0.5f));
        AddShadow(card);
        _theme.cardImages.Add(card.GetComponent<Image>());

        var title = Text("Title", card, "Add a Block", _display != null ? _display : _semiBold,
                         21f, Ink, TextAlignmentOptions.TopLeft);
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(28f, -26f),
              new Vector2(400f, 28f), new Vector2(0f, 1f));
        _theme.inkTexts.Add(title);

        Button close = SolidButton("Btn_Close", card, "×", Surface, Ink, 10f);
        Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-16f, -16f),
              new Vector2(30f, 30f), new Vector2(1f, 1f));
        _theme.surfaceImages.Add(close.GetComponent<Image>());
        _theme.inkTexts.Add(close.GetComponentInChildren<TextMeshProUGUI>(true));

        // Route one: point at the scene. Pro only — Lite has no editable
        // modules to pick from — and the row says so rather than vanishing,
        // because a choice that disappears looks like a bug.
        Button pick = SolidButton("Btn_PickModule", card, string.Empty, Surface, Ink, 12f);
        Place((RectTransform)pick.transform, new Vector2(0.5f, 1f), new Vector2(0f, -72f),
              new Vector2(464f, 72f), new Vector2(0.5f, 1f));
        _theme.surfaceImages.Add(pick.GetComponent<Image>());
        RowText(pick.transform, "Title", "Pick a module in the scene", _semiBold, 14f, Ink, -14f);
        var pickHint = RowText(pick.transform, "Caption",
            "Point at a structure and click it. The whole module is taken, never one part.",
            _regular, 11.5f, Muted, -38f);
        _theme.mutedTexts.Add(pickHint);

        // Route two: a code someone sent you.
        var codeRow = NewUI("CodeRow", card);
        var codeRt = (RectTransform)codeRow.transform;
        Place(codeRt, new Vector2(0.5f, 1f), new Vector2(0f, -156f),
              new Vector2(464f, 96f), new Vector2(0.5f, 1f));

        var codeTitle = Text("Title", codeRt, "Or paste a block code", _semiBold, 14f, Ink,
                             TextAlignmentOptions.TopLeft);
        Place(codeTitle.rectTransform, new Vector2(0f, 1f), new Vector2(2f, 0f),
              new Vector2(400f, 20f), new Vector2(0f, 1f));
        _theme.inkTexts.Add(codeTitle);

        const float ImportWidth = 104f;
        TMP_InputField codeInput = InputField("CodeInput", codeRt, "NS1-…");
        Place((RectTransform)codeInput.transform, new Vector2(0f, 1f), new Vector2(0f, -28f),
              new Vector2(464f - ImportWidth - 8f, 40f), new Vector2(0f, 1f));

        Button import = SolidButton("Btn_Import", codeRt, "Import", Accent, Color.white, 10f);
        Place((RectTransform)import.transform, new Vector2(1f, 1f), new Vector2(0f, -28f),
              new Vector2(ImportWidth, 40f), new Vector2(1f, 1f));
        import.GetComponentInChildren<TextMeshProUGUI>(true).fontSize = 13f;
        _theme.accentImages.Add(import.GetComponent<Image>());

        var note = Text("Note", codeRt, string.Empty, _regular, 11.5f, Muted,
                        TextAlignmentOptions.TopLeft);
        Place(note.rectTransform, new Vector2(0f, 1f), new Vector2(2f, -74f),
              new Vector2(464f, 20f), new Vector2(0f, 1f));
        _theme.mutedTexts.Add(note);

        ui.backdrop = backdrop.gameObject;
        ui.card = card.gameObject;
        ui.pickButton = pick;
        ui.pickRowHint = pickHint;
        ui.codeInput = codeInput;
        ui.importButton = import;
        ui.noteText = note;
        ui.closeButton = close;
        ui.dismissButton = dismiss;

        backdrop.gameObject.SetActive(false);
        card.gameObject.SetActive(false);

        TextMeshProUGUI RowText(Transform parent, string name, string value,
                                TMP_FontAsset font, float size, Color color, float y)
        {
            var t = Text(name, parent, value, font, size, color, TextAlignmentOptions.TopLeft);
            Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(16f, y),
                  new Vector2(432f, 22f), new Vector2(0f, 1f));
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            return t;
        }
    }

    /// <summary>
    /// The app's single confirmation modal. Built at canvas level, last, so
    /// it draws over everything — depth-first order is draw order on a
    /// screen-space overlay canvas, and a dialog under the dock would be
    /// unreachable.
    /// </summary>
    static void BuildConfirmDialog(Transform canvas)
    {
        var host = NewUI("ConfirmDialog", canvas);
        var ui = Undo.AddComponent<UIConfirmDialog>(host);

        RectTransform backdrop = Panel("Backdrop", host.transform, new Color(0f, 0f, 0f, 0.45f), 0f);
        Stretch(backdrop);
        backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
        backdrop.GetComponent<Image>().sprite = null;
        // A button that does nothing: the backdrop exists to STOP clicks
        // reaching what is behind it. Closing on a stray click outside is
        // wrong here — the question is destructive and deserves an answer.
        Undo.AddComponent<Button>(backdrop.gameObject).transition = Selectable.Transition.None;

        RectTransform card = Panel("Card", host.transform, Card, 18f);
        Place(card, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440f, 208f), new Vector2(0.5f, 0.5f));
        AddShadow(card);
        _theme.cardImages.Add(card.GetComponent<Image>());

        var title = Text("Title", card, "Delete?", _display != null ? _display : _semiBold,
                         21f, Ink, TextAlignmentOptions.TopLeft);
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(28f, -26f),
              new Vector2(384f, 28f), new Vector2(0f, 1f));
        _theme.inkTexts.Add(title);

        var message = Text("Message", card, string.Empty, _regular, 13f, Muted, TextAlignmentOptions.TopLeft);
        Place(message.rectTransform, new Vector2(0f, 1f), new Vector2(28f, -64f),
              new Vector2(384f, 76f), new Vector2(0f, 1f));
        message.textWrappingMode = TextWrappingModes.Normal;
        message.lineSpacing = 8f;
        _theme.mutedTexts.Add(message);

        Button cancel = SolidButton("Btn_Cancel", card, "Cancel", Surface, Ink, 10f);
        Place((RectTransform)cancel.transform, new Vector2(1f, 0f), new Vector2(-150f, 24f),
              new Vector2(112f, 40f), new Vector2(1f, 0f));
        _theme.surfaceImages.Add(cancel.GetComponent<Image>());
        _theme.inkTexts.Add(cancel.GetComponentInChildren<TextMeshProUGUI>(true));

        Button confirm = SolidButton("Btn_Confirm", card, "Delete", Danger, Color.white, 10f);
        Place((RectTransform)confirm.transform, new Vector2(1f, 0f), new Vector2(-28f, 24f),
              new Vector2(112f, 40f), new Vector2(1f, 0f));

        ui.backdrop = backdrop.gameObject;
        ui.card = card.gameObject;
        ui.titleText = title;
        ui.messageText = message;
        ui.confirmButton = confirm;
        ui.cancelButton = cancel;
        ui.confirmLabel = confirm.GetComponentInChildren<TextMeshProUGUI>(true);

        backdrop.gameObject.SetActive(false);
        card.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------
    // The Blocks page
    // ------------------------------------------------------------------

    /// <summary>
    /// Block card metrics. The mockup's own (thumb 113, card ~153) came from a
    /// six-column browser grid; in a single sideways-scrolling row there is
    /// height going spare, and a block deserves the room — you are meant to
    /// recognise the thing in the picture.
    ///
    /// 192 is what the dock body affords: 236 less the gallery's 14 top and
    /// bottom leaves 208, so this fills it with a little air rather than
    /// touching the seams.
    /// </summary>
    const float BlockCardWidth = 220f;
    const float BlockThumbHeight = 140f;
    const float BlockCardHeight = 192f;
    const float BlockCardGap = 13f;
    const float BlockRoundIcon = 23f;

    /// <summary>
    /// Corner of a collection row, its hover outline, and the dashed "add"
    /// box. Shared so the empty slot is the same shape as a full one: the
    /// dashed box read as a pill while the rows beside it were barely
    /// rounded, and the two did not look like the same kind of thing.
    /// </summary>
    const float CollectionRowCorner = 7f;

    /// <summary>
    /// Columns in the block gallery, as the mockup has it. Six 220-wide cards
    /// and their gaps come to 1387, inside the ~1490 the gallery has, so the
    /// first six blocks need no scrolling at all.
    /// </summary>
    const int BlockGalleryColumns = 6;

    /// <summary>
    /// The Blocks tab: collections down the left, a gallery of block cards on
    /// the right — the mockup's Build panel, on its own metrics.
    ///
    /// ONE page serves both modes. Pro and Lite each have a Blocks tab and
    /// they show the same library; what differs is only what you may do there
    /// (capturing a block from the scene is a Pro action — Lite has no
    /// editable modules to pick from), and that is the runtime script's
    /// business, not a second copy of the layout.
    ///
    /// The left column is built INSIDE the page rather than being the shared
    /// BuildColumn: that one is the Tools/Parts choice and belongs to the
    /// Build tab, and DockTabs hides it everywhere else. Two columns at the
    /// same width, so the section divider does not jump as you change tabs.
    ///
    /// Everything repeating is a hidden template cloned at runtime. Nothing
    /// here is built by script.
    /// </summary>
    static GameObject BuildBlocksPage(RectTransform body)
    {
        var go = NewUI("BlocksPage", body);
        var page = (RectTransform)go.transform;
        Stretch(page);
        page.offsetMin = page.offsetMax = Vector2.zero;

        var ui = Undo.AddComponent<BlocksPanelUI>(go);

        // --- collections column ---------------------------------------
        var columnGo = NewUI("CollectionsColumn", page);
        var column = (RectTransform)columnGo.transform;
        SetAnchors(column, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
        column.sizeDelta = new Vector2(DockColumnWidth, 0f);
        column.anchoredPosition = Vector2.zero;

        var heading = Text("Label", column, "COLLECTIONS", _medium, UIChrome.DockSectionFontSize,
                           UIChrome.DockDimText, TextAlignmentOptions.TopLeft);
        Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(SectionPadding, -20f),
              new Vector2(DockColumnWidth - 2f * SectionPadding, 16f), new Vector2(0f, 1f));
        heading.characterSpacing = 14f;   // mockup: letter-spacing 1.6px
        heading.raycastTarget = false;

        ui.collectionList = ScrollColumn(column, "CollectionScroll", topInset: 44f);
        SectionSeam(column, DockColumnWidth);

        // --- gallery ---------------------------------------------------
        ui.gallery = BlockGallery(page);

        // --- templates, cloned at runtime ------------------------------
        ui.parentHeaderTemplate = CollectionHeaderTemplate(column);
        ui.collectionRowTemplate = CollectionRowTemplate(column);
        ui.addCollectionTemplate = AddCollectionRowTemplate(column);
        StyleColumn(columnGo);
        ui.blockCardTemplate = BlockCardTemplate(page);
        ui.addCardTemplate = AddBlockCardTemplate(page);

        ui.emptyLabel = Text("Empty", page, string.Empty, _regular, 12.5f, Muted, TextAlignmentOptions.TopLeft);
        Place(ui.emptyLabel.rectTransform, new Vector2(0f, 1f),
              new Vector2(DockColumnWidth + SectionPadding, -28f),
              new Vector2(560f, 44f), new Vector2(0f, 1f));
        ui.emptyLabel.textWrappingMode = TextWrappingModes.Normal;
        ui.emptyLabel.raycastTarget = false;

        go.SetActive(false);
        return go;
    }

    /// <summary>A vertical scroller filling a column below <paramref name="topInset"/>.</summary>
    static RectTransform ScrollColumn(RectTransform parent, string name, float topInset)
    {
        var scrollGo = NewUI(name, parent);
        var scrollRt = (RectTransform)scrollGo.transform;
        Stretch(scrollRt);
        scrollRt.offsetMin = new Vector2(SectionPadding - 10f, 14f);
        scrollRt.offsetMax = new Vector2(-SectionPadding, -topInset);

        var viewportGo = NewUI("Viewport", scrollRt);
        var viewport = (RectTransform)viewportGo.transform;
        Stretch(viewport);
        viewportGo.AddComponent<RectMask2D>();
        viewportGo.AddComponent<Image>().color = Color.clear;   // catches drag-scroll

        var contentGo = NewUI("Content", viewport);
        var content = (RectTransform)contentGo.transform;
        SetAnchors(content, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        var layout = contentGo.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 3f;                  // mockup: margin-bottom 3
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;
        return content;
    }

    /// <summary>
    /// A grid of six columns that scrolls DOWN, as the mockup has it.
    ///
    /// This ran sideways in one row first, on the reasoning that a 192-tall
    /// card cannot show a second row in a 236-tall body. True, but it missed
    /// what the six columns are for: six blocks fit without scrolling at all,
    /// so the common case never scrolls, and past that, down is the direction
    /// a grid grows. Sideways made the gallery behave unlike every other
    /// grid of cards the reader has ever used, to optimise a case that mostly
    /// does not arise.
    /// </summary>
    static RectTransform BlockGallery(RectTransform page)
    {
        var scrollGo = NewUI("BlockGallery", page);
        var scrollRt = (RectTransform)scrollGo.transform;
        Stretch(scrollRt);
        scrollRt.offsetMin = new Vector2(DockColumnWidth + SectionPadding, 14f);
        scrollRt.offsetMax = new Vector2(-16f, -14f);

        var viewportGo = NewUI("Viewport", scrollRt);
        var viewport = (RectTransform)viewportGo.transform;
        Stretch(viewport);
        viewportGo.AddComponent<RectMask2D>();
        viewportGo.AddComponent<Image>().color = Color.clear;

        var contentGo = NewUI("Content", viewport);
        var content = (RectTransform)contentGo.transform;
        SetAnchors(content, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        var grid = contentGo.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(BlockCardWidth, BlockCardHeight);
        grid.spacing = new Vector2(BlockCardGap, BlockCardGap);
        grid.padding = new RectOffset(0, 8, 0, 8);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = BlockGalleryColumns;
        grid.childAlignment = TextAnchor.UpperLeft;

        contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        return content;
    }

    /// <summary>
    /// "NEOSPACE'S COLLECTION" / "MY COLLECTIONS", with the one control that
    /// acts on the whole group: a pencil that puts it into EDIT MODE.
    ///
    /// This replaced a + and a trash sitting here permanently. Outside edit
    /// mode a collection row now does exactly one thing — open its gallery —
    /// and the rest of the column is inert. That matters more than the extra
    /// click: rename used to fire on an ordinary click, so going to look at a
    /// collection put you in a text field you never asked for.
    ///
    /// Adding, renaming and deleting are all inside the mode together,
    /// because they are one activity — organising the folders — and splitting
    /// them across a permanent + and a separate cleanup mode made you pick
    /// which kind of tidying you were about to do before you started.
    /// </summary>
    static GameObject CollectionHeaderTemplate(RectTransform parent)
    {
        var go = NewUI("CollectionHeaderTemplate", parent);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(DockColumnWidth, 28f);
        go.AddComponent<LayoutElement>().preferredHeight = 28f;

        var label = Text("Label", rt, "COLLECTION", _medium, UIChrome.DockSectionFontSize,
                         UIChrome.DockDimText, TextAlignmentOptions.MidlineLeft);
        Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(12f, 0f);
        label.rectTransform.offsetMax = new Vector2(-34f, 0f);
        label.characterSpacing = 14f;
        label.raycastTarget = false;
        label.overflowMode = TextOverflowModes.Ellipsis;

        Button edit = SolidButton("Btn_EditCollections", rt,
                                  _pencilIcon != null ? string.Empty : "Edit",
                                  Color.clear, UIChrome.DockDimText, 7f);
        Place((RectTransform)edit.transform, new Vector2(1f, 0.5f), new Vector2(-6f, 0f),
              new Vector2(22f, 22f), new Vector2(1f, 0.5f));
        edit.GetComponentInChildren<TextMeshProUGUI>(true).fontSize = 10f;
        Undo.AddComponent<UIHoverStatus>(edit.gameObject);

        // Both states baked, one shown at a time. The control is a toggle, so
        // it should say what pressing it does next: a pencil to start
        // organising, a cross to stop.
        IconChild(edit.transform, "Icon_Edit", _pencilIcon, true);
        IconChild(edit.transform, "Icon_Done", _closeIcon, false);

        go.SetActive(false);
        return go;

        void IconChild(Transform host, string name, Sprite sprite, bool shown)
        {
            if (sprite == null)
                return;

            var iconGo = NewUI(name, host);
            Place((RectTransform)iconGo.transform, new Vector2(0.5f, 0.5f), Vector2.zero,
                  new Vector2(13f, 13f), new Vector2(0.5f, 0.5f));
            var img = iconGo.AddComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.color = UIChrome.DockDimText;
            img.preserveAspect = true;
            img.raycastTarget = false;
            iconGo.SetActive(shown);
        }
    }

    /// <summary>
    /// A round action button for a block card: solid white, ringed in black.
    ///
    /// The ring is the parent's own fill showing past the child's, rather
    /// than an outline component — one extra Image and no per-vertex work,
    /// and it stays a clean circle at any size. It exists because these sit
    /// on top of a photograph: a white circle on a pale snapshot has no edge
    /// at all, and the controls read as part of the picture.
    /// </summary>
    static Button CircleButton(string name, Transform parent, Color iconColor)
    {
        float radius = BlockRoundIcon * 0.5f;

        // Parent draws first, so this is the ring. A medium grey, not Ink:
        // the ring is there to separate a white circle from a pale snapshot,
        // not to be noticed. Black read as a control shouting for attention
        // over the thing it sits on.
        RectTransform ring = Panel(name, parent, CircleRing, radius);
        Place(ring, new Vector2(0.5f, 0.5f), Vector2.zero,
              new Vector2(BlockRoundIcon, BlockRoundIcon), new Vector2(0.5f, 0.5f));

        RectTransform fill = Panel("Fill", ring, Card, radius - 1f);
        Stretch(fill);
        fill.offsetMin = new Vector2(1f, 1f);
        fill.offsetMax = new Vector2(-1f, -1f);
        fill.GetComponent<Image>().raycastTarget = false;

        var button = Undo.AddComponent<Button>(ring.gameObject);
        button.targetGraphic = ring.GetComponent<Image>();
        var colors = button.colors;
        colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        return button;
    }

    /// <summary>
    /// The "new sub-collection" box, shown only in edit mode: a row-sized
    /// dashed outline with a plus in it, and no fill. It sits at the end of
    /// its group's rows, so the place a collection appears is the place you
    /// made it.
    ///
    /// Row-sized and unfilled on purpose — it is the empty slot in a list of
    /// full ones, the same idea as the Add a Block card in the gallery.
    /// </summary>
    static GameObject AddCollectionRowTemplate(RectTransform parent)
    {
        RectTransform rt = Panel("AddCollectionRowTemplate", parent, Color.clear, CollectionRowCorner);
        rt.sizeDelta = new Vector2(DockColumnWidth, 38f);
        rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;

        RectTransform outline = Panel("Outline", rt, Color.clear, 7f);
        Stretch(outline);
        outline.offsetMin = new Vector2(4f, 3f);
        outline.offsetMax = new Vector2(-4f, -3f);
        var outlineImg = outline.GetComponent<Image>();
        if (_dashedOutline != null)
        {
            outlineImg.sprite = _dashedOutline;
            // Tiled, not Sliced: slicing stretches the middle of each edge
            // and smears the dashes into bars. Corner matched to the solid
            // rows above it, so the empty slot is the same shape as a full
            // one rather than a pill.
            outlineImg.type = Image.Type.Tiled;
            outlineImg.pixelsPerUnitMultiplier = DashedOutlinePpu(CollectionRowCorner);
        }
        outlineImg.color = new Color(UIChrome.DockDimText.r, UIChrome.DockDimText.g,
                                     UIChrome.DockDimText.b, 0.75f);
        outlineImg.raycastTarget = false;

        var plus = Text("Plus", rt, "+", _regular, 19f, UIChrome.DockDimText, TextAlignmentOptions.Center);
        Stretch(plus.rectTransform);
        plus.raycastTarget = false;

        var button = Undo.AddComponent<Button>(rt.gameObject);
        button.targetGraphic = rt.GetComponent<Image>();
        Undo.AddComponent<UIHoverStatus>(rt.gameObject).hint = "Add a collection?";

        rt.gameObject.SetActive(false);
        return rt.gameObject;
    }

    /// <summary>
    /// One sub-collection: its name, how many blocks it holds, a red trash
    /// that only appears in cleanup mode, and a rename field that replaces
    /// the label in place.
    ///
    /// The trash leads the row rather than trailing it, so a column in
    /// cleanup mode is unmistakable at a glance — every row grows a red mark
    /// down its left edge — and so it never lands where the count was, which
    /// is where a finger is already heading.
    /// </summary>
    static GameObject CollectionRowTemplate(RectTransform parent)
    {
        RectTransform rt = Panel("CollectionRowTemplate", parent, Color.clear, CollectionRowCorner);
        rt.sizeDelta = new Vector2(DockColumnWidth, 38f);
        rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;

        Button remove = SolidButton("Btn_Remove", rt, _trashIcon != null ? string.Empty : "-",
                                    Color.clear, Danger, 6f);
        Place((RectTransform)remove.transform, new Vector2(0f, 0.5f), new Vector2(6f, 0f),
              new Vector2(22f, 22f), new Vector2(0f, 0.5f));
        remove.GetComponentInChildren<TextMeshProUGUI>(true).fontSize = 14f;
        if (_trashIcon != null)
        {
            var iconGo = NewUI("Icon", remove.transform);
            Place((RectTransform)iconGo.transform, new Vector2(0.5f, 0.5f), Vector2.zero,
                  new Vector2(13f, 13f), new Vector2(0.5f, 0.5f));
            var img = iconGo.AddComponent<UnityEngine.UI.Image>();
            img.sprite = _trashIcon;
            img.color = Danger;
            img.preserveAspect = true;
            img.raycastTarget = false;
        }
        Undo.AddComponent<UIHoverStatus>(remove.gameObject).hint = "Remove a collection?";
        remove.gameObject.SetActive(false);

        var label = Text("Label", rt, "Collection", _regular, UIChrome.DockRowFontSize,
                         UIChrome.DockText, TextAlignmentOptions.MidlineLeft);
        Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(12f, 0f);
        label.rectTransform.offsetMax = new Vector2(-46f, 0f);
        label.raycastTarget = false;
        label.overflowMode = TextOverflowModes.Ellipsis;

        var count = Text("Count", rt, "00", _regular, UIChrome.DockSectionFontSize,
                         UIChrome.DockDimText, TextAlignmentOptions.MidlineRight);
        Place(count.rectTransform, new Vector2(1f, 0.5f), new Vector2(-12f, 0f),
              new Vector2(30f, 16f), new Vector2(1f, 0.5f));
        count.raycastTarget = false;

        // Renaming a collection and renaming a block are the same gesture, so
        // they are the same control: a field baked over the label, hidden,
        // rather than built when needed. A new collection opens straight into
        // it — naming the thing you just made is the next thing you do.
        RectTransform editBox = Panel("NameEdit", rt, Card, 5f);
        Stretch(editBox);
        editBox.offsetMin = new Vector2(8f, 6f);
        editBox.offsetMax = new Vector2(-46f, -6f);
        BuildNameField(editBox);
        editBox.gameObject.SetActive(false);

        var button = Undo.AddComponent<Button>(rt.gameObject);
        button.targetGraphic = rt.GetComponent<Image>();
        AddHoverOutline(rt, CollectionRowCorner);

        rt.gameObject.SetActive(false);
        return rt.gameObject;
    }

    /// <summary>A saved block: thumbnail, + to place, trash to remove, name, price, size.</summary>
    static GameObject BlockCardTemplate(RectTransform parent)
    {
        var go = NewUI("BlockCardTemplate", parent);
        var card = (RectTransform)go.transform;
        card.sizeDelta = new Vector2(BlockCardWidth, BlockCardHeight);

        // The whole card is the button that places the block. It needs a
        // Graphic to receive the click at all — a Button with nothing to
        // raycast against is inert, which is exactly what this was. Clear and
        // on the PARENT, so it draws behind every child and the pencil and
        // the name still take their own clicks.
        var surface = go.AddComponent<Image>();
        surface.color = Color.clear;
        Undo.AddComponent<Button>(go).transition = Selectable.Transition.None;

        // The snapshot is clipped to the card's own rounded rectangle, as in
        // the mockup. A Mask uses this Image's alpha as the shape, so the
        // rendered picture takes the corner rather than sitting square inside
        // a rounded frame.
        RectTransform thumbBox = Panel("Thumb", card, Surface, 9f);
        SetAnchors(thumbBox, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        thumbBox.anchoredPosition = Vector2.zero;
        thumbBox.sizeDelta = new Vector2(0f, BlockThumbHeight);
        var mask = Undo.AddComponent<Mask>(thumbBox.gameObject);
        mask.showMaskGraphic = true;

        var image = NewUI("Image", thumbBox);
        Stretch((RectTransform)image.transform);
        var raw = image.AddComponent<RawImage>();
        raw.raycastTarget = false;

        // The card's controls: a pencil that opens a column of four, and
        // folds it away again. Anchored to the card, NOT the thumbnail it
        // sits over — the thumbnail is a Mask, and a mask clips its children
        // to its own rounded shape, which is exactly where a 23-unit control
        // inset 9 from a corner of radius 9 starts getting cut.
        var actionsGo = NewUI("Actions", card);
        var actions = (RectTransform)actionsGo.transform;
        Place(actions, new Vector2(1f, 1f),
              new Vector2(-9f, -(BlockThumbHeight - BlockRoundIcon - 9f)),
              new Vector2(BlockRoundIcon, BlockRoundIcon), new Vector2(1f, 1f));

        var stack = Undo.AddComponent<UIActionStack>(actionsGo);
        stack.step = BlockRoundIcon + 7f;

        // The toggle is the ANCHOR and stays put: it is what you pressed to
        // open the column, and it is the way out of it. Pencil closed, cross
        // open — the control says what pressing it does next.
        Button edit = CircleButton("Btn_EditBlock", actions, Ink);
        Undo.AddComponent<UIHoverStatus>(edit.gameObject);
        CardIcon(edit.transform, "Icon_Edit", _pencilIcon, Ink, true);
        CardIcon(edit.transform, "Icon_Done", _closeIcon, Ink, false);

        // Upward, nearest first. Replace, then share, then delete: the two
        // that change the block sit next to the way out, and the one that
        // destroys it is furthest from the button your finger is already on.
        stack.items.Add(StackItem("Btn_Replace", _refreshIcon, Ink, "Replace with another module?"));
        stack.items.Add(StackItem("Btn_Share", _shareIcon, Ink, "Copy this block's code?"));
        stack.items.Add(StackItem("Btn_Delete", _trashIcon, Danger, "Remove this block?"));

        // A holder, NOT a button. Clicking the name used to be how you
        // renamed, which meant this had to swallow clicks — and swallow them
        // it did, including the ones meant for the card underneath. The
        // rename field is simply present the whole time a card is being
        // edited now, so the label never needs to be clicked, and it must not
        // block the card's own click. A transparent Graphic still absorbs
        // raycasts in uGUI, so this says so explicitly.
        var nameGo = NewUI("Name", card);
        var nameRt = (RectTransform)nameGo.transform;
        SetAnchors(nameRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
        nameRt.anchoredPosition = new Vector2(0f, -(BlockThumbHeight + 6f));
        nameRt.sizeDelta = new Vector2(-62f, 20f);

        var name = Text("Label", nameRt, "Untitled Block", _semiBold, 12f, Card,
                        TextAlignmentOptions.MidlineLeft);
        Stretch(name.rectTransform);
        name.overflowMode = TextOverflowModes.Ellipsis;
        name.raycastTarget = false;

        var price = Text("Price", card, "A$0", _regular, 11f, UIChrome.DockDimText, TextAlignmentOptions.TopRight);
        SetAnchors(price.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
        price.rectTransform.anchoredPosition = new Vector2(0f, -(BlockThumbHeight + 10f));
        price.rectTransform.sizeDelta = new Vector2(58f, 16f);
        price.raycastTarget = false;

        var size = Text("Size", card, "0 × 0 × 0 mm", _regular, 10f,
                        UIChrome.DockDimText, TextAlignmentOptions.TopLeft);
        SetAnchors(size.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
        size.rectTransform.anchoredPosition = new Vector2(0f, -(BlockThumbHeight + 31f));
        size.rectTransform.sizeDelta = new Vector2(0f, 14f);
        size.raycastTarget = false;

        // The name becomes editable in place when a block is first captured.
        // Baked here, hidden, rather than built on demand: it has to match the
        // label it replaces exactly, and two authors of one look never do.
        RectTransform editBox = Panel("NameEdit", card, Card, 5f);
        SetAnchors(editBox, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
        editBox.anchoredPosition = new Vector2(0f, -(BlockThumbHeight + 7f));
        editBox.sizeDelta = new Vector2(-62f, 22f);
        BuildNameField(editBox);
        editBox.gameObject.SetActive(false);

        go.SetActive(false);
        return go;

        RectTransform StackItem(string itemName, Sprite icon, Color fg, string hint)
        {
            Button b = CircleButton(itemName, actions, fg);
            CardIcon(b.transform, "Icon", icon, fg, true);
            Undo.AddComponent<UIHoverStatus>(b.gameObject).hint = hint;
            b.gameObject.SetActive(false);   // folded away until edit mode
            return (RectTransform)b.transform;
        }

        // A sprite when there is one; the UI font has no dingbats, so a typed
        // glyph is only ever the fallback for a missing asset.
        void CardIcon(Transform host, string iconName, Sprite icon, Color fg, bool shown)
        {
            if (icon == null)
                return;

            var glyphGo = NewUI(iconName, host);
            Place((RectTransform)glyphGo.transform, new Vector2(0.5f, 0.5f), Vector2.zero,
                  new Vector2(12f, 12f), new Vector2(0.5f, 0.5f));
            var img = glyphGo.AddComponent<UnityEngine.UI.Image>();
            img.sprite = icon;
            img.color = fg;
            img.preserveAspect = true;
            img.raycastTarget = false;
            glyphGo.SetActive(shown);
        }
    }

    /// <summary>
    /// The card that captures a new block. An outline rather than a fill, so
    /// it reads as a slot waiting to be filled rather than a block that is
    /// already there — the mockup's own treatment for it.
    /// </summary>
    static GameObject AddBlockCardTemplate(RectTransform parent)
    {
        RectTransform card = Panel("AddBlockCardTemplate", parent, Color.clear, 9f);
        card.sizeDelta = new Vector2(BlockCardWidth, BlockCardHeight);

        // The outline spans the WHOLE card, not just the thumbnail area. A
        // block card is thumbnail plus two lines of text, so an outline that
        // stopped at the thumbnail made the empty slot look shorter than the
        // cards it sits beside, and the row read as ragged.
        RectTransform outline = Panel("Outline", card, Seam, 9f);
        Stretch(outline);
        outline.offsetMin = Vector2.zero;
        outline.offsetMax = Vector2.zero;
        var outlineImg = outline.GetComponent<Image>();
        if (_outlineSprite != null)
        {
            outlineImg.sprite = _outlineSprite;
            outlineImg.color = new Color(1f, 1f, 1f, 0.28f);
        }
        outlineImg.raycastTarget = false;

        var plus = Text("Plus", outline, "+", _regular, 30f, UIChrome.DockText, TextAlignmentOptions.Center);
        Place(plus.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 14f),
              new Vector2(44f, 38f), new Vector2(0.5f, 0.5f));
        plus.raycastTarget = false;

        var caption = Text("Caption", outline, "Add a Block", _regular, 13f,
                           UIChrome.DockText, TextAlignmentOptions.Center);
        Place(caption.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -18f),
              new Vector2(BlockCardWidth, 20f), new Vector2(0.5f, 0.5f));
        caption.raycastTarget = false;

        var button = Undo.AddComponent<Button>(card.gameObject);
        button.targetGraphic = card.GetComponent<Image>();
        Undo.AddComponent<UIHoverStatus>(card.gameObject).hint =
            "Add a block from a module, or from a code?";

        card.gameObject.SetActive(false);
        return card.gameObject;
    }

    /// <summary>The in-place rename field on a block card.</summary>
    static void BuildNameField(RectTransform box)
    {
        var areaGo = NewUI("TextArea", box);
        var area = (RectTransform)areaGo.transform;
        Stretch(area);
        area.offsetMin = new Vector2(6f, 2f);
        area.offsetMax = new Vector2(-6f, -2f);
        areaGo.AddComponent<RectMask2D>();

        var value = Text("Text", area, string.Empty, _semiBold, 12f, Ink, TextAlignmentOptions.MidlineLeft);
        Stretch(value.rectTransform);

        var input = Undo.AddComponent<TMP_InputField>(box.gameObject);
        input.targetGraphic = box.GetComponent<Image>();
        input.textViewport = area;
        input.textComponent = value;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 48;
        input.fontAsset = _semiBold;
        input.caretColor = Ink;
        input.customCaretColor = true;
        input.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.35f);
    }

    static void SectionSeam(RectTransform parent, float x)
    {
        RectTransform line = Panel("SectionSeam", parent, Seam, 0f);
        SetAnchors(line, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
        line.sizeDelta = new Vector2(1f, -32f);
        line.anchoredPosition = new Vector2(x, 0f);
        line.GetComponent<Image>().sprite = null;
    }

    /// <summary>
    /// The right-hand utility block from the NEOSPACE web UI mockup: one
    /// translucent card holding dimensions, undo, redo, saved designs,
    /// settings and fullscreen, replacing six buttons spread across three
    /// different anchors.
    ///
    /// All six are baked here now, in order. Dimensions and fullscreen used to
    /// build themselves into this rail, because each script attached its
    /// onClick only on the path where it created its own button — so baking
    /// them would have produced controls that looked right and did nothing.
    /// Both bootstraps now wire whatever they find instead, which is what the
    /// other four already did.
    ///
    /// Two differences from the rest, both deliberate:
    ///   - Btn_Dimensions gets NO RailButtonVisual. It is lit by whether
    ///     dimensions are pinned, not by an open panel, and its bootstrap
    ///     already paints that from the same colour pair. Adding the visual
    ///     would give those colours a second owner.
    ///   - its Icon is baked empty. UIIcons draws that glyph procedurally at
    ///     runtime, so there is no asset for the scene to reference; the
    ///     bootstrap fills the sprite in.
    /// </summary>
    /// <summary>
    /// The open project's name, at the top left, on the mockup's own metrics.
    ///
    /// It was briefly on the right, between the bar and the rail, because
    /// that is where it was asked for — and it was wrong there: cramped
    /// between two things, and right-aligned text under a left-aligned
    /// wordmark reads as a second heading rather than a label for the scene.
    /// The mockup's .scene-label sits top left and so does this.
    ///
    ///     .eyebrow        10px, letter-spacing 1.8px, dim
    ///     .scene-label h1 24px, weight 400, letter-spacing -0.9px
    ///
    /// TMP measures character spacing as a percentage of the font size, so a
    /// CSS pixel value converts as spacing * 100 / fontSize: 1.8px at 10 is
    /// 18, and -0.9px at 24 is -3.75.
    ///
    /// Weight 400 is why this uses Manrope MEDIUM rather than the SemiBold
    /// display face: at 24 units the difference is the whole character of the
    /// line, and the mockup's is light.
    /// </summary>
    static void BuildProjectName(Transform canvas)
    {
        var host = NewUI("ProjectName", canvas);
        var rt = (RectTransform)host.transform;
        SetAnchors(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        rt.anchoredPosition = new Vector2(UIChrome.DockInset, -104f);
        rt.sizeDelta = new Vector2(460f, 56f);

        var eyebrow = Text("Eyebrow", rt, "PROJECT", _medium, 10f, Muted, TextAlignmentOptions.TopLeft);
        Place(eyebrow.rectTransform, new Vector2(0f, 1f), Vector2.zero, new Vector2(460f, 13f), new Vector2(0f, 1f));
        eyebrow.characterSpacing = 18f;
        eyebrow.raycastTarget = false;
        _theme.mutedTexts.Add(eyebrow);

        var name = Text("Txt_ProjectName", rt, CurrentProject.UntitledLabel,
                        _displayMedium != null ? _displayMedium : _display, 24f, Ink,
                        TextAlignmentOptions.TopLeft);
        // 36, not 32. A 24-unit line wants about 33 units of height, and with
        // an Ellipsis overflow mode TMP renders NOTHING at all when the line
        // does not fit — not a clipped line, not an ellipsis, nothing. One
        // unit short and the label was simply absent.
        Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -19f),
              new Vector2(460f, 36f), new Vector2(0f, 1f));
        name.characterSpacing = -3.75f;
        name.overflowMode = TextOverflowModes.Ellipsis;
        name.raycastTarget = false;
        _theme.inkTexts.Add(name);

        Undo.AddComponent<ProjectNameDisplay>(host).nameText = name;
    }

    static void BuildUtilityRail(Transform canvas, BuildController buildController)
    {
        // Height covers six 32px rows, two 7px separators, 1px gaps and 5px
        // padding: 6*32 + 2*7 + 7*1 + 10 = 223.
        RectTransform rail = Panel(UIChrome.RailName, canvas, new Color(1f, 1f, 1f, 0.63f), 11f);
        Place(rail, new Vector2(1f, 1f), new Vector2(-24f, -104f), new Vector2(44f, 223f), new Vector2(1f, 1f));
        var railShadow = rail.gameObject.AddComponent<Shadow>();
        railShadow.effectColor = new Color(0f, 0f, 0f, .13f);
        railShadow.effectDistance = new Vector2(2f, -3f);

        var layout = rail.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(5, 5, 5, 5);
        layout.spacing = 1f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        RailButton("Btn_Dimensions", RailIcon("Design/Spacing Horizontal"), "Dimensions", emptyIcon: true);
        Separator("Sep_History");
        RailButton("Btn_Undo", _historyIcon, "Undo", mirrorX: true);
        RailButton("Btn_Redo", _historyIcon, "Redo");
        Separator("Sep_Actions");
        // No Btn_Pieces. The block library was a rail panel; it is the dock's
        // Blocks tab now, and a folder here would be a second door to the
        // same library — which is what it had become, each door with its own
        // idea of renaming, deleting, and which collection a block was in.
        Button projects = RailButton("Btn_Projects", RailIcon("File/Folder"), "My Projects", watchedPanel: "ProjectsPanel");
        Button gear = RailButton("Btn_Settings", RailIcon("System/Eye"), "View controls", watchedPanel: "ControlSettingsPanel");
        RailButton("Btn_Fullscreen", RailIcon("Device/Scan"), "Fullscreen");

        // Both cards hang off the button that opens them, wherever it lives.
        BuildProjectsPanel(canvas, projects, buildController);
        BuildControlSettingsPanel(canvas, gear);

        void Separator(string name)
        {
            var slot = NewUI(name, rail);
            var slotRt = (RectTransform)slot.transform;
            slotRt.sizeDelta = new Vector2(24f, 7f);
            AddRow(slot, 7f);

            RectTransform line = Panel("Line", slot.transform, Hex("DADADA"), 1f);
            Place(line, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 1f), new Vector2(0.5f, 0.5f));
        }

        Button RailButton(string name, Sprite icon, string fallbackLabel,
                          bool mirrorX = false, string watchedPanel = null,
                          bool withVisual = true, bool emptyIcon = false)
        {
            // Idle is transparent; RailButtonVisual owns both colours
            // from here on, so the button is NOT registered with the theme
            // (that would fight it every time the palette is applied).
            Button b = SolidButton(name, rail, icon == null && !emptyIcon ? fallbackLabel : string.Empty,
                                   Color.clear, Ink, 9f);
            var rt = (RectTransform)b.transform;
            rt.sizeDelta = new Vector2(34f, UIChrome.RowHeight);
            AddRow(b.gameObject, UIChrome.RowHeight);
            _theme.surfaceImages.Remove(b.GetComponent<Image>());

            var lbl = b.GetComponentInChildren<TextMeshProUGUI>(true);
            if (lbl != null)
                _theme.mutedTexts.Add(lbl);

            Image iconImg = null;
            if (icon != null || emptyIcon)
            {
                var iconGo = NewUI("Icon", b.transform);
                var iconRt = (RectTransform)iconGo.transform;
                Place(iconRt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f), new Vector2(0.5f, 0.5f));
                if (mirrorX)
                    iconRt.localScale = new Vector3(-1f, 1f, 1f);
                iconImg = iconGo.AddComponent<Image>();
                iconImg.sprite = icon;   // null when the sprite is drawn at runtime
                iconImg.color = Muted;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
            }

            if (!withVisual)
                return b;

            var visual = Undo.AddComponent<RailButtonVisual>(b.gameObject);
            visual.icon = iconImg;
            visual.watchedPanelName = watchedPanel;
            visual.hint = fallbackLabel;
            b.transition = Selectable.Transition.None;
            visual.ConfigureGlyph();
            visual.RefreshVisual();

            // Momentary buttons acknowledge the click with a brief flash;
            // toggles stay lit from their panel's state instead.
            if (string.IsNullOrEmpty(watchedPanel))
                UnityEventTools.AddPersistentListener(b.onClick, new UnityAction(visual.Flash));

            return b;
        }

        void AddRow(GameObject go, float height)
        {
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            le.flexibleHeight = 0f;
        }
    }

    static Sprite RailIcon(string name) =>
        AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Evo/Evo UI/Sprites/Icons/" + name + ".png");

    /// <summary>
    /// Create a tab page that fills the dock body, falling back to the old
    /// left-hand card when no dock exists.
    ///
    /// The page keeps its Image and its rounded sprite even inside the dock:
    /// six runtime scripts copy that sprite to style cards of their own
    /// (ApplyCardSprite / StyleCard), so removing it would silently change how
    /// those cards look. It is just made transparent — the dock body already
    /// provides the surface — and for the same reason it is NOT registered
    /// with the theme, which would repaint it opaque on every Apply().
    /// </summary>
    static RectTransform DockPage(string name, Transform canvas)
    {
        Transform body = canvas.Find(UIChrome.DockBodyPath);
        RectTransform page = Panel(name, body != null ? body : canvas, Card, 20f);

        if (body != null)
        {
            Stretch(page);
            page.offsetMin = Vector2.zero;
            page.offsetMax = Vector2.zero;

            var img = page.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f);

            // Invisible, and it must be intangible too. A fully transparent
            // Graphic still absorbs raycasts in uGUI, and these pages stretch
            // across the WHOLE body — over the shared Tools/Parts column,
            // which is an earlier sibling. That made both column rows
            // unclickable while looking perfectly normal. The page image only
            // exists because six scripts clone its sprite as a style source;
            // it has no business swallowing clicks. The dock's own Ink fill
            // still stops clicks reaching the scene behind it.
            img.raycastTarget = false;
            return page;
        }

        SetAnchors(page, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
        page.offsetMin = new Vector2(24f, 24f);
        page.offsetMax = new Vector2(24f + 336f, -108f);
        AddShadow(page);
        _theme.cardImages.Add(page.GetComponent<Image>());
        return page;
    }

    /// <summary>
    /// Soft drop shadow behind a panel. The default spread is authored for
    /// large surfaces — the dock, a panel, a card. On something small it
    /// has to be scaled down: at full size it put a 584x82 halo behind the
    /// 540x38 status pill, more than twice the pill's own height, and being
    /// bottom-heavy (28 below, 16 above) it made the pill read as sitting
    /// high inside a grey block rather than casting a shadow.
    /// </summary>
    static void AddShadow(RectTransform target, float scale = 1f)
    {
        var go = NewUI("Shadow", target);
        var rt = (RectTransform)go.transform;
        Stretch(rt);
        rt.offsetMin = new Vector2(-22f, -28f) * scale;
        rt.offsetMax = new Vector2(22f, 16f) * scale;
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
    /// <summary>
    /// A guided tool card, built to the SAME spec as a part card in the Parts
    /// gallery: same size, same icon position, same type sizes, same hover.
    /// The two sit a tab apart in one dock and used to differ in all four —
    /// 304x56 against 304x96, a vertically centred icon against a top-left
    /// one, 15pt against 16pt.
    ///
    /// The numbers come from UIChrome.PartCardSize and from
    /// UIPartsPalette.MakeCard, which restyles the part cards at runtime; a
    /// comment in both points here so the pair stay together.
    /// </summary>
    static Button TemplateToolButton(RectTransform panel, string name, string title, string caption, float y,
        Sprite icon = null)
    {
        RectTransform rt = Panel(name, panel, Surface, 14f);
        Place(rt, new Vector2(0.5f, 1f), new Vector2(0f, y), UIChrome.PartCardSize, new Vector2(0.5f, 1f));
        _theme.surfaceImages.Add(rt.GetComponent<Image>());

        float textX = 16f;
        if (icon != null)
        {
            var iconGo = NewUI("Icon", rt);
            var iconRt = (RectTransform)iconGo.transform;
            // Top-left, as on a part card — not vertically centred.
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 1f);
            iconRt.pivot = new Vector2(0f, 1f);
            iconRt.anchoredPosition = new Vector2(14f, -11f);
            iconRt.sizeDelta = new Vector2(26f, 26f);

            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            iconImg.color = Ink;
            _theme.inkIcons.Add(iconImg);

            textX = 52f;
        }

        var titleText = Text("Label", rt, title, _semiBold, 16f, Ink, TextAlignmentOptions.MidlineLeft);
        SetAnchors(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
        titleText.rectTransform.anchoredPosition = new Vector2(textX, -10f);
        titleText.rectTransform.sizeDelta = new Vector2(-(textX + 12f), 28f);
        titleText.raycastTarget = false;
        _theme.inkTexts.Add(titleText);

        var captionText = Text("Caption", rt, caption, _regular, 11.5f, Muted, TextAlignmentOptions.TopLeft);
        SetAnchors(captionText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        captionText.rectTransform.offsetMin = new Vector2(14f, 8f);
        captionText.rectTransform.offsetMax = new Vector2(-12f, -44f);
        captionText.textWrappingMode = TextWrappingModes.Normal;
        captionText.overflowMode = TextOverflowModes.Ellipsis;
        captionText.raycastTarget = false;
        _theme.mutedTexts.Add(captionText);

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = rt.GetComponent<Image>();

        // The same ColorBlock the part cards carry. Unity's default
        // highlighted colour is 0.96 grey, which against the Surface fill is
        // a change of about four levels out of 255 — a hover you cannot see.
        var colors = button.colors;
        colors.highlightedColor = new Color(0.94f, 0.94f, 0.94f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        button.colors = colors;

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
