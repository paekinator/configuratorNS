using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Runtime light/dark theme switch for the whole configurator: UI surfaces and
/// text, the toolbar/palette highlight colors, and the 3D environment (camera
/// background, sun, ambient trilight and floor tint). The UI builder fills the
/// reference lists; the toggle button calls <see cref="Toggle"/>.
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

        [Header("Environment")]
        public Color background;
        public Color floorTint;
        public Color sunColor;
        public float sunIntensity;
        public Color ambientSky;
        public Color ambientEquator;
        public Color ambientGround;
    }

    public Palette light = new Palette
    {
        card = Hex("FFFFFF"),
        surface = Hex("F3EFE9"),
        ink = Hex("26221E"),
        muted = Hex("8F8880"),
        accent = Hex("D96C47"),
        hintBg = new Color(1f, 1f, 1f, 0.85f),
        background = Hex("EAE5DD"),
        floorTint = Hex("DAD3C7"),
        sunColor = Hex("FFF5E8"),
        sunIntensity = 1.1f,
        ambientSky = Hex("F2EEE7"),
        ambientEquator = Hex("D8D2C7"),
        ambientGround = Hex("B5AC9D")
    };

    public Palette dark = new Palette
    {
        card = Hex("2C2925"),
        surface = Hex("3B372F"),
        ink = Hex("EDE8E1"),
        muted = Hex("9A938A"),
        accent = Hex("E07A52"),
        hintBg = new Color(0.17f, 0.16f, 0.14f, 0.88f),
        background = Hex("211E1A"),
        floorTint = Hex("35312B"),
        sunColor = Hex("FFEBD2"),
        sunIntensity = 0.85f,
        ambientSky = Hex("47423A"),
        ambientEquator = Hex("332F29"),
        ambientGround = Hex("221F1B")
    };

    [Header("Scene")]
    public Camera targetCamera;
    public Light sun;
    public Renderer floorRenderer;

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

    [Header("Toggle Button")]
    public Image themeIcon;
    public Sprite sunSprite;
    public Sprite moonSprite;

    [Header("Startup")]
    [Tooltip("Legacy baked default; the saved preference (dark unless the user switched) wins at runtime.")]
    public bool startDark;

    const string ThemePrefKey = "Neospace.DarkTheme";

    public bool IsDark { get; private set; }

    // ------------------------------------------------------------------
    // Design tokens for world-space graphics (guides, markers, marquee).
    // Statics so the in-scene drawing code can stay theme-correct every
    // frame without holding a reference; defaults match the light palette
    // so graphics look right even before a theme controller exists.
    // ------------------------------------------------------------------
    public static bool IsDarkTheme { get; private set; }
    public static Color InkColor { get; private set; } = Hex("26221E");
    public static Color MutedColor { get; private set; } = Hex("8F8880");
    public static Color AccentColor { get; private set; } = Hex("D96C47");
    public static Color CardColor { get; private set; } = Hex("FFFFFF");
    public static Color SurfaceColor { get; private set; } = Hex("F3EFE9");

    /// <summary>Blocked/invalid signal, tuned per theme to sit in the warm palette.</summary>
    public static Color DangerColor { get; private set; } = Hex("BF4A40");

    /// <summary>Raised after a theme is applied, so self-styling UI can restyle live.</summary>
    public static event System.Action ThemeChanged;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    MaterialPropertyBlock _floorBlock;

    void Start()
    {
        // Runtime-injected chrome (fullscreen, gear glyph, tool icons) is
        // created in AfterSceneLoad, before this Start. Collect it so the
        // first Apply — and every toggle after — tints those buttons too.
        CollectRuntimeChrome();
        // Dark is the default; a user's explicit toggle is remembered.
        Apply(PlayerPrefs.GetInt(ThemePrefKey, 1) == 1);
    }

    public void Toggle()
    {
        Apply(!IsDark);
        PlayerPrefs.SetInt(ThemePrefKey, IsDark ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void Apply(bool darkMode)
    {
        IsDark = darkMode;
        Palette p = darkMode ? dark : light;

        IsDarkTheme = darkMode;
        InkColor = p.ink;
        MutedColor = p.muted;
        AccentColor = p.accent;
        CardColor = p.card;
        SurfaceColor = p.surface;
        DangerColor = darkMode ? Hex("E0685C") : Hex("BF4A40");

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

        // --- Toggle icon shows the mode you would switch TO ---
        if (themeIcon != null)
        {
            Sprite icon = darkMode ? sunSprite : moonSprite;
            if (icon != null) themeIcon.sprite = icon;
            themeIcon.color = p.muted;
        }

        // --- Environment ---
        if (targetCamera != null)
        {
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = p.background;
        }

        if (sun != null)
        {
            sun.color = p.sunColor;
            sun.intensity = p.sunIntensity;
        }

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = p.ambientSky;
        RenderSettings.ambientEquatorColor = p.ambientEquator;
        RenderSettings.ambientGroundColor = p.ambientGround;

        if (floorRenderer != null)
        {
            _floorBlock ??= new MaterialPropertyBlock();
            floorRenderer.GetPropertyBlock(_floorBlock);
            _floorBlock.SetColor(BaseColorId, p.floorTint);
            _floorBlock.SetColor(ColorId, p.floorTint);
            floorRenderer.SetPropertyBlock(_floorBlock);
        }

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
        RegisterSurface(FindImg("TopBar/Btn_Settings"));
        RegisterMutedIcon(FindImg("TopBar/Btn_Settings/Icon"));

        RegisterCard(FindImg("GuidedToolsPanel"));
        RegisterSurface(FindImg("GuidedToolsPanel/HintBox"));
        RegisterSurface(FindImg("GuidedToolsPanel/Btn_T1_Posts"));
        RegisterSurface(FindImg("GuidedToolsPanel/Btn_T3_PanelBay"));
        RegisterInkIcon(FindImg("GuidedToolsPanel/Btn_T1_Posts/Icon"));
        RegisterInkIcon(FindImg("GuidedToolsPanel/Btn_T3_PanelBay/Icon"));
        RegisterInkText(FindTmp("GuidedToolsPanel/Title"));
        RegisterMutedText(FindTmp("GuidedToolsPanel/Subtitle"));
        RegisterInkText(FindTmp("GuidedToolsPanel/Btn_T1_Posts/Label"));
        RegisterMutedText(FindTmp("GuidedToolsPanel/Btn_T1_Posts/Caption"));
        RegisterInkText(FindTmp("GuidedToolsPanel/Btn_T3_PanelBay/Label"));
        RegisterMutedText(FindTmp("GuidedToolsPanel/Btn_T3_PanelBay/Caption"));
        RegisterMutedText(FindTmp("GuidedToolsPanel/HintBox/Txt_GuidedHint"));
        RegisterMutedText(FindTmp("GuidedToolsPanel/Txt_GuidedHint"));

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
