using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Single-palette theme service for the whole configurator: UI surfaces and
/// text, the toolbar/palette highlight colors, and the 3D environment (camera
/// background, sun, ambient trilight and floor tint). The UI builder fills the
/// reference lists. The light/dark toggle was removed — there is one palette,
/// taken from the NEOSPACE web UI mockup.
/// </summary>
public class UIThemeController : MonoBehaviour
{
    [System.Serializable]
    public class Palette
    {
        [Header("UI")]
        public Color card;
        public Color surface;
        public Color ink;
        public Color muted;
        public Color accent;
        public Color hintBg;

        // No Environment block at all any more.
        //
        // The backdrop is a gradient of four corners, not one colour, and it
        // belongs to SceneBackdrop. The floor tint, the sun and the ambient
        // trilight belong to StageLighting. Every one of them was also
        // authored by ConfiguratorEnvironmentStyler, and a palette that
        // repainted them on Start silently won that argument — which is how
        // the floor came to be #DAD3C7 in the editor and #E7E7E7 in Play mode.
        //
        // This palette is the UI's colours now, and only those.
    }

    /// <summary>
    /// The single NEOSPACE palette. Luminance comes from the web UI mockup,
    /// but the hue does not: the mockup's greys lead green by 1-6 points per
    /// channel (#202622, #3B423C, #E1E3DF), which reads as a colour cast once
    /// it fills full-height panels. These are the same greys neutralised to
    /// zero chroma, so lightness and contrast ratios are unchanged.
    ///
    /// The sun, the ambient and the floor have left this palette entirely —
    /// they are StageLighting's, which is also where the exposure that stopped
    /// the ground clipping to white lives.
    /// </summary>
    public Palette light = new Palette
    {
        card = Hex("FFFFFF"),
        surface = Hex("E2E2E2"),
        ink = Hex("242424"),
        muted = Hex("7D7D7D"),
        accent = Hex("3F3F3F"),
        // Opaque, matching card: the hint pill sits in the band beside the
        // status pill and must read as the same object. At 85% the shadow
        // behind it showed through the fill. HintPillBootstrap force-syncs
        // this colour to the status pill's every frame anyway, so a different
        // value here only ever produced a flicker at startup.
        hintBg = Hex("FFFFFF"),
    };

    /// <summary>
    /// Alias of <see cref="light"/>. The dark palette and its toggle were
    /// removed; this keeps the panels that read
    /// <c>IsDark ? theme.dark : theme.light</c> compiling untouched.
    /// </summary>
    public Palette dark => light;

    [Header("Scene")]
    public Camera targetCamera;
    public Light sun;
    // No floorRenderer. The ground is a shadow catcher with no colour of its
    // own, so there is nothing here to repaint.

    [Header("UI Elements")]
    public List<Image> cardImages = new List<Image>();
    public List<Image> surfaceImages = new List<Image>();
    public List<Image> accentImages = new List<Image>();
    public List<TMP_Text> inkTexts = new List<TMP_Text>();
    public List<TMP_Text> mutedTexts = new List<TMP_Text>();
    /// <summary>Icon images tinted with the ink color (e.g. undo/redo arrows).</summary>
    public List<Image> inkIcons = new List<Image>();
    /// <summary>Icon images tinted with the muted color (e.g. gear, fullscreen).</summary>
    public List<Image> mutedIcons = new List<Image>();
    public Image hintImage;

    [Header("Controllers")]
    public UIToolbarController toolbar;
    public UIPartsPalette palette;

    /// <summary>Always false — the dark theme was removed. Kept so callers compile.</summary>
    public bool IsDark => false;

    // ------------------------------------------------------------------
    // Design tokens for world-space graphics (guides, markers, marquee).
    // Statics so the in-scene drawing code can stay theme-correct every
    // frame without holding a reference; defaults match the light palette
    // so graphics look right even before a theme controller exists.
    // ------------------------------------------------------------------
    /// <summary>Always false — the dark theme was removed. Kept so callers compile.</summary>
    public static bool IsDarkTheme => false;
    public static Color InkColor { get; private set; } = Hex("242424");
    public static Color MutedColor { get; private set; } = Hex("7D7D7D");
    public static Color AccentColor { get; private set; } = Hex("3F3F3F");
    public static Color CardColor { get; private set; } = Hex("FFFFFF");
    public static Color SurfaceColor { get; private set; } = Hex("E2E2E2");

    /// <summary>Blocked/invalid signal, tuned per theme to sit in the warm palette.</summary>
    public static Color DangerColor { get; private set; } = Hex("BF4A40");

    /// <summary>
    /// The ONE colour that means "this is the thing you are pointing at" —
    /// a valid placement ghost, a selected part, a module under the block
    /// picker. Anything the scene lights up to answer "which one?" uses this
    /// and nothing else, so the answer always looks the same.
    ///
    /// The scene previously answered in three different colours: green for a
    /// valid ghost, orange for a selection, and whatever the UI accent
    /// happened to be for the picker. Three highlights are three things to
    /// learn for one idea.
    ///
    /// Red is NOT part of this and stays as it is: it does not mean "here",
    /// it means "no", and a refusal that looked like a highlight would be the
    /// one genuinely dangerous confusion in the set.
    ///
    /// The ghost and selection MATERIALS are written from this value by
    /// ConfiguratorUIBuilder on a rebuild, so the constant is the only place
    /// it is decided.
    /// </summary>
    public static Color HighlightColor { get; private set; } = Hex("2776EA");

    /// <summary>Raised after a theme is applied, so self-styling UI can restyle live.</summary>
    public static event System.Action ThemeChanged;


    void Start()
    {
        // Runtime-injected chrome (fullscreen, gear glyph, tool icons) is
        // created in AfterSceneLoad, before this Start. Collect it so the
        // first Apply — and every toggle after — tints those buttons too.
        CollectRuntimeChrome();
        Apply();
    }

    public void Apply()
    {
        Palette p = light;

        InkColor = p.ink;
        MutedColor = p.muted;
        AccentColor = p.accent;
        CardColor = p.card;
        SurfaceColor = p.surface;
        DangerColor = Hex("BF4A40");

        // --- UI surfaces & text ---
        foreach (Image img in cardImages)
            if (img != null) img.color = p.card;

        foreach (Image img in surfaceImages)
            if (img != null) img.color = p.surface;

        foreach (Image img in accentImages)
            if (img != null) img.color = p.accent;

        foreach (TMP_Text text in inkTexts)
            if (text != null) text.color = p.ink;

        foreach (TMP_Text text in mutedTexts)
            if (text != null) text.color = p.muted;

        foreach (Image img in inkIcons)
            if (img != null) img.color = p.ink;

        foreach (Image img in mutedIcons)
            if (img != null) img.color = p.muted;

        if (hintImage != null)
            hintImage.color = p.hintBg;

        // --- Highlight colors (toolbar refreshes now; palette repaints itself) ---
        if (toolbar != null)
        {
            toolbar.activeBgColor = p.ink;
            toolbar.activeTextColor = p.card;
            toolbar.inactiveBgColor = Color.clear;
            toolbar.inactiveTextColor = p.muted;
            toolbar.RefreshHighlights();
        }

        if (palette != null)
        {
            palette.normalBgColor = p.surface;
            palette.selectedBgColor = p.accent;
            palette.normalTextColor = p.ink;
            palette.selectedTextColor = Color.white;
            palette.RefreshHighlights();
        }

        // --- Environment ---
        if (targetCamera != null)
        {
            // NOT from the palette. The visible backdrop is a gradient quad on
            // the camera (SceneBackdrop), and this clear colour is only the
            // floor underneath it — so it has to be the gradient's own middle
            // stop or the two disagree wherever the quad is absent. The
            // palette used to carry a second, different value here, which is
            // why the background changed colour on entering Play mode.
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = SceneBackdrop.ClearColor;
        }

        // Sun and ambient come from StageLighting rather than from the
        // palette. Both used to exist twice — authored by the editor styler
        // and overwritten here — which is how the floor came to be baked
        // #DAD3C7 and painted #E7E7E7, changing shade on entering Play mode
        // with only one of the two ever seen.
        StageLighting.ApplySun(sun);
        StageLighting.ApplyAmbient();

        // The floor is not tinted here any more, because the floor has no
        // colour: it is a shadow catcher, invisible except where the key
        // light is blocked. Repainting a surface nobody can see was a knob
        // that would have been turned and turned with nothing happening.

        ThemeChanged?.Invoke();
    }

    public void RegisterCard(Image img) => Add(cardImages, img, CardColor);
    public void RegisterSurface(Image img) => Add(surfaceImages, img, SurfaceColor);
    public void RegisterInkIcon(Image img) => Add(inkIcons, img, InkColor);
    public void RegisterMutedIcon(Image img) => Add(mutedIcons, img, MutedColor);
    public void RegisterInkText(TMP_Text text) => AddText(inkTexts, text, InkColor);
    public void RegisterMutedText(TMP_Text text) => AddText(mutedTexts, text, MutedColor);

    /// <summary>
    /// Buttons built at runtime (or baked without a theme slot) would otherwise
    /// keep the light-mode fill they were created with.
    /// </summary>
    void CollectRuntimeChrome()
    {
        RegisterSurface(FindImg("Btn_Fullscreen"));
        RegisterMutedIcon(FindImg("Btn_Fullscreen/Icon"));
        // No Btn_Settings entries. The gear moved to the utility rail, where
        // RailButtonVisual owns both its colours — registering it here would
        // repaint over that on every Apply(). These two paths had been dead
        // since the move, and the top bar they named is now gone entirely.

        RegisterCard(FindImg("GuidedToolsPanel"));
        // No HintBox entry: it is retired, and GuidedBootstrap destroys any
        // left over from an older scene.
        RegisterSurface(FindImg("GuidedToolsPanel/Btn_T1_Posts"));
        RegisterSurface(FindImg("GuidedToolsPanel/Btn_T3_PanelBay"));
        RegisterInkIcon(FindImg("GuidedToolsPanel/Btn_T1_Posts/Icon"));
        RegisterInkIcon(FindImg("GuidedToolsPanel/Btn_T3_PanelBay/Icon"));
        // No Title/Subtitle entries: the dock's shared Tools/Parts column
        // names the page and the footer carries its description.
        RegisterInkText(FindTmp("GuidedToolsPanel/Btn_T1_Posts/Label"));
        RegisterMutedText(FindTmp("GuidedToolsPanel/Btn_T1_Posts/Caption"));
        RegisterInkText(FindTmp("GuidedToolsPanel/Btn_T3_PanelBay/Label"));
        RegisterMutedText(FindTmp("GuidedToolsPanel/Btn_T3_PanelBay/Caption"));

        RegisterCard(FindImg("ControlSettingsPanel"));
        RegisterInkText(FindTmp("ControlSettingsPanel/Title"));
        RegisterMutedText(FindTmp("ControlSettingsPanel/Txt_Legend"));
    }

    Image FindImg(string path)
    {
        Transform t = transform.Find(path);
        return t != null ? t.GetComponent<Image>() : null;
    }

    TMP_Text FindTmp(string path)
    {
        Transform t = transform.Find(path);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }

    static void Add(List<Image> list, Image img, Color color)
    {
        if (img == null || list.Contains(img))
            return;
        list.Add(img);
        img.color = color;
    }

    static void AddText(List<TMP_Text> list, TMP_Text text, Color color)
    {
        if (text == null || list.Contains(text))
            return;
        list.Add(text);
        text.color = color;
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }
}
