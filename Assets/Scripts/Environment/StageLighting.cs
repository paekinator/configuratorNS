using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The one place the studio lighting is decided: key light, ambient, floor
/// tint, and how dark a shadow falls.
///
/// WHY IT EXISTS. These values were written down twice — once in
/// ConfiguratorEnvironmentStyler, which bakes them into the scene, and once in
/// UIThemeController.Palette, which re-applies them the moment you press Play.
/// The two had already drifted apart: the floor was authored at #DAD3C7 and
/// repainted at #E7E7E7, so the ground changed shade on entering Play mode.
/// The camera's background had drifted the same way and was collapsed into
/// SceneBackdrop for the same reason; this is the rest of that job.
///
/// EXPOSURE. The authored colours are kept exactly as they were and scaled by
/// one number. The scene was running about a third too bright — an up-facing
/// surface received roughly 1.32x its own albedo, so the floor clipped to
/// white and took every shadow on it with it. That had been invisible because
/// post-processing was compressing the highlights back down, and it only
/// became visible once that was switched off.
///
/// Scaling rather than re-authoring is deliberate: it changes the exposure and
/// NOTHING else. Every hue, and the balance between key and ambient, is
/// exactly as it was, so this is a correction and not a re-design.
/// </summary>
public static class StageLighting
{
    /// <summary>
    /// Everything below is multiplied by this, in linear light. 1.0 was the
    /// setting that clipped; the measurement said an up-facing surface was
    /// receiving about 1.32x its albedo, and 1/1.32 is 0.76.
    ///
    /// Tools > Configurator > Report Backdrop Colours prints the camera's view
    /// and marks any channel that has clipped, so this can be checked rather
    /// than believed.
    /// </summary>
    public const float Exposure = 0.75f;

    /// <summary>
    /// How much of the authored warmth to keep. 1 is the studio palette as
    /// written below; 0 is perfectly neutral light of the same brightness.
    ///
    /// All four colours below lean warm, and together they were delivering
    /// red about 19% stronger than blue to an up-facing surface — measured, on
    /// a floor whose own colour is a neutral grey. Against a backdrop that was
    /// deliberately neutralised to zero chroma, and a UI that was neutralised
    /// with it, the ground read as orange.
    ///
    /// 0.2 keeps a trace of warmth — a little over 3% red-to-blue, which is
    /// barely nameable — so the light is not clinically colourless without
    /// being a colour. Turn it up for a warmer stage.
    ///
    /// The pull is toward each colour's OWN luminance, so changing this does
    /// not change how bright anything is. Exposure alone decides that.
    /// </summary>
    public const float Saturation = 0.2f;

    // --- The key light, as authored -----------------------------------
    public static readonly Color SunColor = Hex("FFF5E8");   // warm white
    public const float SunIntensity = 1.1f;
    public static readonly Vector3 SunAngles = new Vector3(50f, -32f, 0f);

    /// <summary>
    /// How dark a shadow goes: 1 removes all of the key light, 0 removes none.
    ///
    /// Half, not all. The key and the ambient are close to an even split, so a
    /// shadow at full strength lands about 50% down in linear light — a heavy,
    /// graphic shadow. The reference is product photography on a light ground,
    /// where the shadow reads as a soft grey that says "this object is
    /// standing here", not as a shape of its own.
    ///
    /// This is the ONE number to turn if shadows read too heavy or too faint.
    /// </summary>
    public const float ShadowStrength = 0.5f;

    // --- Ambient, as authored ------------------------------------------
    public static readonly Color AmbientSky = Hex("F2EEE7");
    public static readonly Color AmbientEquator = Hex("D8D2C7");
    public static readonly Color AmbientGround = Hex("B5AC9D");

    // --- The ground ----------------------------------------------------
    //
    // There is no FloorTint any more. The floor draws nothing of its own: it
    // is a shadow catcher, invisible except where the key light is blocked,
    // and the grid is a separate surface above it. A colour for a surface
    // that is never seen is a number people would keep adjusting and never
    // see change.

    /// <summary>The colour a shadow tends toward on the ground.</summary>
    public static readonly Color GroundShadowColor = Hex("202020");

    /// <summary>
    /// How dark the ground goes where the light is fully blocked.
    ///
    /// NOT the whole story on its own: URP has already applied
    /// <see cref="ShadowStrength"/> by the time this multiplies, so the
    /// darkest the ground reaches is the two together — 0.5 x 0.4, a 20%
    /// darkening at the core of a shadow. That is a soft grey that says
    /// "this is standing here", which is what a product photograph on a light
    /// ground looks like.
    ///
    /// The two are deliberately separate: ShadowStrength governs shadows
    /// everywhere, including one part shading another; this governs only how
    /// heavily they land on the ground.
    /// </summary>
    public const float GroundShadowOpacity = 0.4f;

    /// <summary>Key light intensity after exposure.</summary>
    public static float ExposedSunIntensity => SunIntensity * Exposure;

    /// <summary>
    /// An authored colour with <see cref="Saturation"/> applied: pulled toward
    /// its own luminance, so the colour drains without the brightness moving.
    /// Rec.709 weights, in linear light, which is where "the same brightness"
    /// actually means the same brightness.
    /// </summary>
    public static Color Desaturated(Color authored)
    {
        Color c = authored.linear;
        float luma = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
        return new Color(Mathf.Lerp(luma, c.r, Saturation),
                         Mathf.Lerp(luma, c.g, Saturation),
                         Mathf.Lerp(luma, c.b, Saturation),
                         1f).gamma;
    }

    /// <summary>
    /// An authored colour at the scene's saturation AND exposure. Both happen
    /// in LINEAR light — which is what "a fifth of the colour" and "three
    /// quarters of the light" actually mean — and the result comes back as a
    /// gamma-space colour, because that is what RenderSettings and Light.color
    /// expect to be given.
    /// </summary>
    public static Color Exposed(Color authored)
    {
        Color linear = Desaturated(authored).linear;
        return new Color(linear.r * Exposure, linear.g * Exposure, linear.b * Exposure, 1f).gamma;
    }

    /// <summary>
    /// Point a directional light at the scene. Called by the editor styler
    /// when it bakes, and by UIThemeController when the theme is applied, so
    /// the two cannot disagree about what the sun is.
    /// </summary>
    public static void ApplySun(Light sun)
    {
        if (sun == null)
            return;

        // Desaturated but NOT exposed: a light carries colour and intensity
        // separately, so the exposure goes through the intensity alone.
        // Scaling both would apply it twice and land at 0.56 rather than 0.75.
        // Ambient has no intensity of its own, which is why its colours carry
        // the exposure instead.
        sun.color = Desaturated(SunColor);
        sun.intensity = ExposedSunIntensity;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = ShadowStrength;
    }

    /// <summary>The ambient trilight, at the scene's exposure.</summary>
    public static void ApplyAmbient()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Exposed(AmbientSky);
        RenderSettings.ambientEquatorColor = Exposed(AmbientEquator);
        RenderSettings.ambientGroundColor = Exposed(AmbientGround);
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }
}
