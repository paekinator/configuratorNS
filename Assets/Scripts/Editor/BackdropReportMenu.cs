#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Measures the backdrop instead of arguing about it.
///
/// "The gradient looks darker than expected" has three quite different causes
/// — the wrong colours reaching the material, the right colours blended in the
/// wrong space, or the right image being darkened after the fact by
/// post-processing — and they are indistinguishable by eye. The first attempt
/// at this gradient was diagnosed by reasoning about it and the reasoning was
/// wrong twice over.
///
/// So: render the camera, read the actual pixels at the four corners and the
/// centre, and print them beside what SceneBackdrop says they should be. If
/// the two columns agree, the shader is doing as it is told and any remaining
/// complaint is about the numbers, which is a design decision. If they
/// disagree, the size and direction of the gap says which of the three causes
/// it is.
///
/// TWICE, though. The first version of this rendered the camera once and
/// reported a flat warm #C9C6BB at all four corners — which was not the
/// backdrop failing, it was the grid floor filling the view and being measured
/// instead. The backdrop is drawn before everything and painted over by
/// anything opaque, so a single shot answers "what is on screen", which is a
/// different question from "is the gradient right". Both are printed now.
///
/// Writes to Logs/BackdropReport.txt as well as the Console, because the
/// Console is cleared on entering Play mode.
/// </summary>
public static class BackdropReportMenu
{
    const string ReportPath = "Logs/BackdropReport.txt";
    const int ShotWidth = 320;
    const int ShotHeight = 200;

    /// <summary>How far in from the edge to sample, so no edge case is read.</summary>
    const int Inset = 3;

    [MenuItem("Tools/Configurator/Report Backdrop Colours")]
    public static void Report()
    {
        var sb = new StringBuilder();
        sb.AppendLine("isPlaying: " + EditorApplication.isPlaying);
        sb.AppendLine("colour space: " + PlayerSettings.colorSpace);

        Camera cam = Camera.main;
        if (cam == null)
            cam = Object.FindFirstObjectByType<Camera>();
        if (cam == null)
        {
            Write(sb.AppendLine("no Camera in the scene").ToString());
            return;
        }

        sb.AppendLine($"camera: {cam.name}  clearFlags={cam.clearFlags}  "
                      + $"clearColour={Describe(cam.backgroundColor)}");

        var backdrop = Object.FindFirstObjectByType<SceneBackdrop>();
        if (backdrop == null)
        {
            sb.AppendLine("SceneBackdrop: NOT IN THE SCENE — run "
                          + "Tools > Configurator > Style Environment. Everything measured "
                          + "below is the camera's flat clear colour.");
        }
        else
        {
            var rend = backdrop.GetComponent<Renderer>();
            Material mat = rend != null ? rend.sharedMaterial : null;
            sb.AppendLine($"SceneBackdrop: parent={(backdrop.transform.parent != null ? backdrop.transform.parent.name : "<none>")}  "
                          + $"active={backdrop.gameObject.activeInHierarchy}  "
                          + $"material={(mat != null ? mat.name : "<none>")}  "
                          + $"shader={(mat != null && mat.shader != null ? mat.shader.name : "<none>")}");

            if (mat != null && mat.shader != null && mat.shader.name.Contains("Screen Gradient"))
            {
                // What the MATERIAL carries, which can differ from what
                // SceneBackdrop says if the styler has not been re-run.
                sb.AppendLine("material corners: "
                              + $"TL={Describe(mat.GetColor("_CornerTL"))}  "
                              + $"TR={Describe(mat.GetColor("_CornerTR"))}  "
                              + $"BL={Describe(mat.GetColor("_CornerBL"))}  "
                              + $"BR={Describe(mat.GetColor("_CornerBR"))}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("intended (SceneBackdrop):");
        sb.AppendLine($"  TL {Describe(SceneBackdrop.TopLeft)}   TR {Describe(SceneBackdrop.TopRight)}");
        sb.AppendLine($"  BL {Describe(SceneBackdrop.BottomLeft)}   BR {Describe(SceneBackdrop.BottomRight)}");
        sb.AppendLine($"  centre {Describe(SceneBackdrop.ClearColor)}");
        sb.AppendLine();

        UniversalAdditionalCameraData urp = cam.GetUniversalAdditionalCameraData();
        sb.AppendLine("post-processing on this camera: "
                      + (urp != null ? urp.renderPostProcessing.ToString() : "<no URP camera data>")
                      + "   antialiasing: " + (urp != null ? urp.antialiasing.ToString() : "?"));
        sb.AppendLine();

        // FOUR shots. Reasoning about this has been wrong twice now, so each
        // shot exists to rule out exactly one explanation.
        //
        // The CONTROL comes first and matters most: with every renderer off,
        // the frame can only be the camera's clear colour, which is a value we
        // already know. If that reads back as anything else, the darkening is
        // in the pipeline or the capture and has nothing to do with the
        // gradient at all — and every other number below is shifted by the
        // same amount.
        MeasureFlat(sb, "CONTROL — the camera's clear colour, nothing drawn",
                    cam, cam.backgroundColor);

        // Both ways round, always, whichever way the camera is currently set.
        // An A/B that only runs when post-processing happens to be ON cannot
        // report the one thing it exists to report once it has been turned off.
        if (urp != null)
        {
            bool previous = urp.renderPostProcessing;
            try
            {
                urp.renderPostProcessing = false;
                Measure(sb, "the backdrop alone, post-processing OFF", cam, isolate: true);
                urp.renderPostProcessing = true;
                Measure(sb, "the backdrop alone, post-processing ON", cam, isolate: true);
            }
            finally
            {
                urp.renderPostProcessing = previous;
            }
        }
        else
        {
            Measure(sb, "the backdrop alone", cam, isolate: true);
        }

        Measure(sb, "as the camera sees it", cam, isolate: false);

        sb.AppendLine("READING THIS.");
        sb.AppendLine("Start with the CONTROL. A clear colour is not drawn by any shader, so if "
                      + "it does not read back as itself, nothing else in this report is being "
                      + "measured fairly and the cause is downstream of everything.");
        sb.AppendLine("Then judge the gradient by \"the backdrop alone, post-processing OFF\". "
                      + "Within about a point is exact — that is the rounding of an 8-bit "
                      + "channel. Compare it against the ON row directly below it.");
        sb.AppendLine("  · OFF exact, ON darker ................ post-processing, and only that");
        sb.AppendLine("  · the same gap at every corner ........ a flat multiply somewhere");
        sb.AppendLine("  · a bigger gap at the BRIGHT corners .. a curve compressing highlights");
        sb.AppendLine("  · corners darker than the centre ...... a vignette");
        sb.AppendLine("  · a gap that grows toward the middle ... blended in the wrong space");
        sb.AppendLine("  · a gap only at the dark corners ...... the numbers themselves");
        sb.AppendLine("  · flat, with no gradient at all ....... the quad is not drawing");
        sb.AppendLine("A measured centre BRIGHTER than the average of the measured corners is "
                      + "not something a four-corner blend can produce at all, so it is never "
                      + "the gradient's doing.");
        sb.AppendLine("If the backdrop alone is right and \"as the camera sees it\" is not, then "
                      + "something opaque is in front of it — the grid floor usually — and the "
                      + "backdrop is fine.");

        Write(sb.ToString());
    }

    /// <summary>
    /// Render with nothing drawn at all and report the one colour that comes
    /// back, against the colour the camera was told to clear to. The control
    /// group: it measures the capture and the pipeline, with no shader of ours
    /// anywhere in it.
    /// </summary>
    static void MeasureFlat(StringBuilder sb, string label, Camera cam, Color expected)
    {
        var backdrop = Object.FindFirstObjectByType<SceneBackdrop>();
        bool backdropWasEnabled = backdrop != null && backdrop.enabled;

        // Disabling the component first, because it re-enables its own renderer
        // on every camera render — which is exactly what it is for.
        if (backdrop != null)
            backdrop.enabled = false;

        var hidden = new System.Collections.Generic.List<Renderer>();
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (r == null || !r.enabled)
                continue;
            r.enabled = false;
            hidden.Add(r);
        }

        Texture2D shot;
        try
        {
            shot = ThumbnailCapture.Plain(cam, ShotWidth, ShotHeight);
        }
        finally
        {
            foreach (Renderer r in hidden)
                if (r != null)
                    r.enabled = true;
            if (backdrop != null)
                backdrop.enabled = backdropWasEnabled;
        }

        if (shot == null)
        {
            sb.AppendLine(label + ": the camera render failed");
            sb.AppendLine();
            return;
        }

        try
        {
            Color measured = shot.GetPixel(ShotWidth / 2, ShotHeight / 2);
            sb.AppendLine(label + ":");
            sb.AppendLine($"  told to clear to {Describe(expected)}");
            sb.AppendLine($"  actually read back {Describe(measured)}   difference {Gap(measured, expected)}");
            sb.AppendLine();
        }
        finally
        {
            Object.DestroyImmediate(shot);
        }
    }

    /// <summary>
    /// Render the camera and sample five points. With <paramref name="isolate"/>
    /// every renderer but the backdrop's is switched off for the render, so
    /// what comes back is the gradient and nothing standing in front of it.
    /// </summary>
    static void Measure(StringBuilder sb, string label, Camera cam, bool isolate)
    {
        var hidden = new System.Collections.Generic.List<Renderer>();
        if (isolate)
        {
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r == null || !r.enabled || SceneBackdrop.IsBackdrop(r))
                    continue;
                r.enabled = false;
                hidden.Add(r);
            }
        }

        Texture2D shot;
        try
        {
            shot = ThumbnailCapture.Plain(cam, ShotWidth, ShotHeight);
        }
        finally
        {
            // Only what WE hid, so a renderer already off for its own reasons
            // stays off. The backdrop re-enables itself on the next camera
            // render regardless — that is how it stays out of the Scene view.
            foreach (Renderer r in hidden)
                if (r != null)
                    r.enabled = true;
        }

        if (shot == null)
        {
            sb.AppendLine(label + ": the camera render failed; nothing to measure");
            sb.AppendLine();
            return;
        }

        try
        {
            // ReadPixels puts (0,0) at the BOTTOM-left, so "top" is the high y.
            int left = Inset;
            int right = ShotWidth - 1 - Inset;
            int bottom = Inset;
            int top = ShotHeight - 1 - Inset;

            Color mTL = shot.GetPixel(left, top);
            Color mTR = shot.GetPixel(right, top);
            Color mBL = shot.GetPixel(left, bottom);
            Color mBR = shot.GetPixel(right, bottom);
            Color mC = shot.GetPixel(ShotWidth / 2, ShotHeight / 2);

            sb.AppendLine("measured — " + label + ":");
            sb.AppendLine($"  TL {Describe(mTL)}   TR {Describe(mTR)}");
            sb.AppendLine($"  BL {Describe(mBL)}   BR {Describe(mBR)}");
            sb.AppendLine($"  centre {Describe(mC)}");
            sb.AppendLine("  difference from intended, in brightness points:");
            sb.AppendLine($"    TL {Gap(mTL, SceneBackdrop.TopLeft)}   TR {Gap(mTR, SceneBackdrop.TopRight)}");
            sb.AppendLine($"    BL {Gap(mBL, SceneBackdrop.BottomLeft)}   BR {Gap(mBR, SceneBackdrop.BottomRight)}");
            sb.AppendLine($"    centre {Gap(mC, SceneBackdrop.ClearColor)}");
            sb.AppendLine();
        }
        finally
        {
            Object.DestroyImmediate(shot);
        }
    }

    /// <summary>
    /// "#F7F7F7 (97.0%)" — hex plus the brightness a picker shows, and the
    /// word CLIPPED when a channel has hit the ceiling.
    ///
    /// Without that word, an over-exposed surface reads as a perfectly
    /// respectable "100.0%" and says nothing about the detail burned out of
    /// it — which is exactly how the blown-out floor went unnoticed.
    /// </summary>
    static string Describe(Color c)
    {
        float v = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        bool clipped = c.r >= 254.5f / 255f || c.g >= 254.5f / 255f || c.b >= 254.5f / 255f;
        return "#" + ColorUtility.ToHtmlStringRGB(c) + " (" + (v * 100f).ToString("F1") + "%)"
               + (clipped ? " CLIPPED" : string.Empty);
    }

    static string Gap(Color measured, Color intended)
    {
        float a = Mathf.Max(measured.r, Mathf.Max(measured.g, measured.b));
        float b = Mathf.Max(intended.r, Mathf.Max(intended.g, intended.b));
        float delta = (a - b) * 100f;
        return (delta >= 0f ? "+" : "") + delta.ToString("F1");
    }

    static void Write(string text)
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText(ReportPath, text);
        Debug.Log("[BackdropReport] written to " + ReportPath + "\n\n" + text);
    }
}
#endif
