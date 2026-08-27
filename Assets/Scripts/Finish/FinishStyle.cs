using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The colour system of the Finish: a small set of named swatches, curated
/// themes that pair them, and the current selection.
///
/// The roles come from the owner's reference designs: PANELS carry the
/// colour field, VENEERS AND CAPS outline it — a theme is a coordinated
/// pair, never one colour painted on everything. Both slots stay
/// individually adjustable on top of the theme presets.
///
/// One palette governs the whole space (v1): the live build, saved piece
/// instances in Space Mode, and the merge-derived visuals all read the same
/// selection, which persists across play sessions.
///
/// Materials are created once per swatch at runtime from the URP Lit shader
/// (matte, near-zero gloss, like the physical powder-coated plates). The
/// "Frost" swatch is the semi-translucent matte polycarbonate board from
/// the reference images — panels only.
/// </summary>
public static class FinishStyle
{
    public struct Swatch
    {
        public string Id;
        public string Label;
        public Color Color;
        public bool Frosted;    // translucent matte polycarbonate (panels only)
        public bool PanelOnly;
    }

    public struct Theme
    {
        public string Id;
        public string Label;
        public string PanelSwatch;
        public string DressingSwatch;
    }

    // ------------------------------------------------------------------
    // Catalogue (owner's exact colours first, the rest from the reference
    // designs: green lockers, blush bunk room, USM silver, black frames)
    // ------------------------------------------------------------------

    public static readonly Swatch[] Swatches =
    {
        new Swatch { Id = "ivory",    Label = "Ivory",    Color = Hex(0xFF, 0xFF, 0xF4) },
        new Swatch { Id = "frost",    Label = "Frost",    Color = new Color(0.906f, 0.937f, 0.953f, 0.45f), Frosted = true, PanelOnly = true },
        new Swatch { Id = "sky",      Label = "Sky",      Color = Hex(0xC4, 0xE7, 0xF7) },
        new Swatch { Id = "sage",     Label = "Sage",     Color = Hex(0xAB, 0xC4, 0xB2) },
        new Swatch { Id = "blush",    Label = "Blush",    Color = Hex(0xE9, 0xCF, 0xC7) },
        new Swatch { Id = "cognac",   Label = "Cognac",   Color = Hex(0x83, 0x43, 0x33) },
        new Swatch { Id = "silver",   Label = "Silver",   Color = Hex(0xC8, 0xCC, 0xD0) },
        new Swatch { Id = "charcoal", Label = "Charcoal", Color = Hex(0x33, 0x30, 0x2D) },
    };

    public static readonly Theme[] Themes =
    {
        new Theme { Id = "kiosk",   Label = "Kiosk",   PanelSwatch = "ivory", DressingSwatch = "cognac" },
        new Theme { Id = "sky",     Label = "Sky",     PanelSwatch = "sky",   DressingSwatch = "ivory" },
        new Theme { Id = "archive", Label = "Archive", PanelSwatch = "frost", DressingSwatch = "silver" },
        new Theme { Id = "clinic",  Label = "Clinic",  PanelSwatch = "sage",  DressingSwatch = "ivory" },
        new Theme { Id = "suite",   Label = "Suite",   PanelSwatch = "blush", DressingSwatch = "charcoal" },
        new Theme { Id = "gallery", Label = "Gallery", PanelSwatch = "ivory", DressingSwatch = "silver" },
    };

    const string DefaultPanel = "ivory";
    const string DefaultDressing = "cognac";

    const string PanelPrefKey = "Finish.PanelSwatch";
    const string DressingPrefKey = "Finish.DressingSwatch";

    static Color Hex(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);

    // ------------------------------------------------------------------
    // Selection
    // ------------------------------------------------------------------

    /// <summary>Raised after either slot of the palette changes.</summary>
    public static event Action Changed;

    static string _panelId;
    static string _dressingId;

    public static string PanelSwatchId
    {
        get
        {
            if (_panelId == null)
                _panelId = PlayerPrefs.GetString(PanelPrefKey, DefaultPanel);
            return _panelId;
        }
    }

    public static string DressingSwatchId
    {
        get
        {
            if (_dressingId == null)
                _dressingId = PlayerPrefs.GetString(DressingPrefKey, DefaultDressing);
            return _dressingId;
        }
    }

    public static Swatch PanelSwatch => Find(PanelSwatchId, DefaultPanel);
    public static Swatch DressingSwatch => Find(DressingSwatchId, DefaultDressing);

    static Swatch Find(string id, string fallback)
    {
        foreach (Swatch s in Swatches)
            if (s.Id == id)
                return s;
        foreach (Swatch s in Swatches)
            if (s.Id == fallback)
                return s;
        return Swatches[0];
    }

    public static void SetPanelSwatch(string id)
    {
        if (PanelSwatchId == id)
            return;
        _panelId = id;
        PlayerPrefs.SetString(PanelPrefKey, id);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static void SetDressingSwatch(string id)
    {
        if (DressingSwatchId == id)
            return;
        _dressingId = id;
        PlayerPrefs.SetString(DressingPrefKey, id);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static void SetTheme(Theme theme)
    {
        if (PanelSwatchId == theme.PanelSwatch && DressingSwatchId == theme.DressingSwatch)
            return;
        _panelId = theme.PanelSwatch;
        _dressingId = theme.DressingSwatch;
        PlayerPrefs.SetString(PanelPrefKey, _panelId);
        PlayerPrefs.SetString(DressingPrefKey, _dressingId);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    /// <summary>The theme matching the current pair, if any.</summary>
    public static bool TryCurrentTheme(out Theme theme)
    {
        foreach (Theme t in Themes)
            if (t.PanelSwatch == PanelSwatchId && t.DressingSwatch == DressingSwatchId)
            {
                theme = t;
                return true;
            }
        theme = default;
        return false;
    }

    // ------------------------------------------------------------------
    // Materials (one runtime material per swatch and use)
    // ------------------------------------------------------------------

    static readonly Dictionary<string, Material> _panelMats = new Dictionary<string, Material>();
    static readonly Dictionary<string, Material> _dressingMats = new Dictionary<string, Material>();

    public static Material PanelMaterial => PanelMaterialFor(PanelSwatch);
    public static Material DressingMaterial => DressingMaterialFor(DressingSwatch);

    static Material _footMat;

    /// <summary>
    /// The Foot never follows the palette: it is universally black in the
    /// physical system, like a plinth under any colour scheme.
    /// </summary>
    public static Material FootMaterial
    {
        get
        {
            if (_footMat == null)
            {
                _footMat = CreateMatte(new Color(0.09f, 0.088f, 0.085f), 0.28f);
                _footMat.name = "FinishFoot_Black";
            }
            return _footMat;
        }
    }

    static Material PanelMaterialFor(in Swatch s)
    {
        if (_panelMats.TryGetValue(s.Id, out Material mat) && mat != null)
            return mat;
        mat = s.Frosted ? CreateFrosted(s) : CreateMatte(s.Color, 0.22f);
        mat.name = $"FinishPanel_{s.Id}";
        _panelMats[s.Id] = mat;
        return mat;
    }

    static Material DressingMaterialFor(in Swatch s)
    {
        if (_dressingMats.TryGetValue(s.Id, out Material mat) && mat != null)
            return mat;
        mat = CreateMatte(s.Color, 0.3f);
        mat.name = $"FinishDressing_{s.Id}";
        _dressingMats[s.Id] = mat;
        return mat;
    }

    static Material CreateMatte(Color color, float smoothness)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        color.a = 1f;
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", 0f);
        return mat;
    }

    /// <summary>
    /// Semi-translucent matte polycarbonate, soft sheen, ~45% opacity.
    ///
    /// The transparent surface setup ships as a SAVED material asset
    /// (Resources/Finish/FrostedPanel) and runtime only tints a copy. This is
    /// load-bearing for WebGL: builds strip every shader variant that no
    /// serialized asset references, so a transparent URP Lit material
    /// assembled purely from code renders opaque in the build — while the
    /// editor, which keeps all variants, shows it correctly.
    /// </summary>
    static Material CreateFrosted(in Swatch s)
    {
        Material template = Resources.Load<Material>("Finish/FrostedPanel");
        if (template != null)
        {
            var tinted = new Material(template);
            tinted.SetColor("_BaseColor", s.Color);
            return tinted;
        }

        // Fallback (asset missing): same setup from code — fine in the
        // editor, opaque on WebGL.
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", s.Color);
        mat.SetFloat("_Smoothness", 0.38f);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_AlphaClip", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
        return mat;
    }
}
