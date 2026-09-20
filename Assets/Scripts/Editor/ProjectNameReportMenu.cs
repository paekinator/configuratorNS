#if UNITY_EDITOR
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reports everything about the project-name label and the eyebrow above it.
///
/// The two sit in the same parent, are built by the same helper one line
/// apart, and one of them renders. That makes the eyebrow a control group:
/// whatever differs between these two listings is the reason the name is not
/// on screen. Guessing at it from the source has cost two rounds already.
///
/// Writes to Logs/ProjectNameReport.txt as well as the Console, because the
/// Console is cleared on entering Play mode and this is only interesting
/// about a running scene.
/// </summary>
public static class ProjectNameReportMenu
{
    [MenuItem("Tools/Configurator/Report Project Name")]
    public static void Report()
    {
        var sb = new StringBuilder();
        sb.AppendLine("isPlaying: " + EditorApplication.isPlaying);

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Write(sb.AppendLine("no Canvas in the scene").ToString());
            return;
        }

        Transform host = UIChrome.FindPanel(canvas.transform, "ProjectName");
        if (host == null)
        {
            sb.AppendLine("ProjectName: NOT IN THE SCENE — the builder never made it, "
                          + "or the UI has not been rebuilt since it was added.");
            Write(sb.ToString());
            return;
        }

        sb.AppendLine($"ProjectName host: active={host.gameObject.activeInHierarchy} "
                      + $"rect={Describe(host as RectTransform)}");
        sb.AppendLine($"CurrentProject: open={CurrentProject.IsOpen} "
                      + $"id='{CurrentProject.Id}' name='{CurrentProject.Name}' "
                      + $"display='{CurrentProject.DisplayName}'");

        var display = host.GetComponent<ProjectNameDisplay>();
        sb.AppendLine("ProjectNameDisplay: "
                      + (display == null ? "MISSING"
                         : display.nameText == null ? "present, nameText UNASSIGNED"
                         : "present, nameText assigned"));

        Describe(sb, host, "Eyebrow", "the one that DOES render");
        Describe(sb, host, "Txt_ProjectName", "the one that does not");

        Write(sb.ToString());
    }

    static void Describe(StringBuilder sb, Transform host, string child, string note)
    {
        sb.AppendLine();
        sb.AppendLine($"--- {child}   ({note})");

        Transform t = host.Find(child);
        if (t == null)
        {
            sb.AppendLine("    NOT FOUND");
            return;
        }

        sb.AppendLine($"    active      {t.gameObject.activeInHierarchy}");
        sb.AppendLine($"    rect        {Describe(t as RectTransform)}");

        var tmp = t.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
        {
            sb.AppendLine("    no TextMeshProUGUI");
            return;
        }

        sb.AppendLine($"    text        '{tmp.text}'  (length {tmp.text?.Length ?? 0})");
        sb.AppendLine($"    fontSize    {tmp.fontSize}   spacing {tmp.characterSpacing}");
        sb.AppendLine($"    colour      {ColorUtility.ToHtmlStringRGBA(tmp.color)}  "
                      + $"alpha={tmp.alpha}  enabled={tmp.enabled}");
        sb.AppendLine($"    renderedChars {tmp.textInfo?.characterCount ?? -1}  "
                      + $"preferred {tmp.preferredWidth:F0}x{tmp.preferredHeight:F0}");

        TMP_FontAsset font = tmp.font;
        if (font == null)
        {
            sb.AppendLine("    font        NULL (TMP would fall back to its default)");
            return;
        }

        Texture atlas = font.atlasTexture;
        sb.AppendLine($"    font        {font.name}");
        sb.AppendLine($"    atlas       {(atlas == null ? "none" : atlas.width + "x" + atlas.height)}"
                      + $"  declared {font.atlasWidth}x{font.atlasHeight}"
                      + $"  mode {font.atlasPopulationMode}");
        sb.AppendLine($"    glyphs      {font.glyphTable?.Count ?? -1}  "
                      + $"characters {font.characterTable?.Count ?? -1}");
        sb.AppendLine($"    hasText     {(string.IsNullOrEmpty(tmp.text) ? "n/a" : font.HasCharacters(tmp.text).ToString())}");
        sb.AppendLine($"    material    {(font.material == null ? "none" : font.material.name)}");
    }

    static string Describe(RectTransform rt)
    {
        if (rt == null)
            return "none";

        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        return $"size={rt.rect.width:F0}x{rt.rect.height:F0} "
               + $"anchored={rt.anchoredPosition} "
               + $"world=({corners[0].x:F0},{corners[0].y:F0})-({corners[2].x:F0},{corners[2].y:F0})";
    }

    static void Write(string text)
    {
        Debug.Log(text);
        try
        {
            System.IO.Directory.CreateDirectory("Logs");
            System.IO.File.WriteAllText("Logs/ProjectNameReport.txt",
                System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n\n" + text);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Could not write Logs/ProjectNameReport.txt: " + e.Message);
        }
    }
}
#endif
