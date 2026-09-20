#if UNITY_EDITOR
using System;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Applies one typography pairing to the open configurator scene. The rules
/// preserve the existing three weight levels, reserve mono for measured or
/// system-generated data, and keep the current Manrope-style display role.
/// </summary>
public sealed class NeospaceTypographyTesterWindow : EditorWindow
{
    const string SetFolder = "Assets/Fonts/Sets";
    static readonly Regex TechnicalText = new(
        @"(^|\s)(A\$\s?\d|NS\d?[-/]|\d+(?:\.\d+)?\s*(?:mm|cm|m)\b|\d+\s*[×x]\s*\d+|STATUS\s*/|SYSTEM\s*/|CONFIGURATION\s+\d)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    Vector2 _scroll;

    [MenuItem("NEOSPACE/Typography/Font Set Tester")]
    static void Open() => GetWindow<NeospaceTypographyTesterWindow>("Typography Sets");

    void OnGUI()
    {
        EditorGUILayout.LabelField("NEOSPACE Typography Sets", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Applies a set to all TextMesh Pro text in the open scene. Technical mono is limited to dimensions, prices, IDs, counts, codes and system states. The operation is undoable.",
            MessageType.Info);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (NeospaceTypographySet set in LoadSets())
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(set.displayName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(set.character, EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button("Apply to open configurator"))
                    Apply(set);
            }
        }
        EditorGUILayout.EndScrollView();
    }

    static NeospaceTypographySet[] LoadSets()
    {
        return AssetDatabase.FindAssets("t:NeospaceTypographySet", new[] { SetFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<NeospaceTypographySet>)
            .Where(set => set != null)
            .OrderBy(set => set.name)
            .ToArray();
    }

    public static void Apply(NeospaceTypographySet set)
    {
        if (set == null) return;

        TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>()
            .Where(text => text != null && text.gameObject.scene.IsValid() && text.gameObject.scene.isLoaded)
            .ToArray();
        Undo.RecordObjects(texts, "Apply NEOSPACE typography set");

        int technical = 0;
        int display = 0;
        foreach (TMP_Text text in texts)
        {
            string path = HierarchyPath(text.transform);
            NeospaceTypographyWeight weight = WeightOf(text);
            TMP_FontAsset target;

            if (IsTechnical(path, text.text))
            {
                target = set.Technical(weight);
                technical++;
            }
            else if (IsDisplay(path, text.text))
            {
                target = set.Display(weight == NeospaceTypographyWeight.Regular
                    ? NeospaceTypographyWeight.Medium
                    : weight);
                display++;
            }
            else
            {
                target = set.Primary(weight);
            }

            if (target != null)
            {
                text.font = target;
                EditorUtility.SetDirty(text);
            }
        }

        foreach (TMP_InputField input in Resources.FindObjectsOfTypeAll<TMP_InputField>()
                     .Where(input => input != null && input.gameObject.scene.IsValid() && input.gameObject.scene.isLoaded))
        {
            if (input.textComponent != null)
                input.fontAsset = input.textComponent.font;
            EditorUtility.SetDirty(input);
        }

        foreach (var dimensions in Resources.FindObjectsOfTypeAll<StructureDimensionsController>()
                     .Where(item => item != null && item.gameObject.scene.IsValid() && item.gameObject.scene.isLoaded))
        {
            Undo.RecordObject(dimensions, "Apply NEOSPACE technical font");
            dimensions.typographyFont = set.Technical(NeospaceTypographyWeight.Regular);
            EditorUtility.SetDirty(dimensions);
        }

        foreach (var column in Resources.FindObjectsOfTypeAll<UIDockColumnStyle>())
        {
            if (!column.gameObject.scene.IsValid()) continue;
            Undo.RecordObject(column, "Apply sidebar typography");
            column.regularFont = set.Primary(NeospaceTypographyWeight.Regular);
            column.emphasisFont = set.Primary(NeospaceTypographyWeight.SemiBold);
            column.Apply();
            EditorUtility.SetDirty(column);
        }

        if (texts.Length > 0)
            EditorSceneManager.MarkSceneDirty(texts[0].gameObject.scene);

        Debug.Log($"[Typography] Applied '{set.displayName}' to {texts.Length} texts ({display} display, {technical} technical).");
    }

    static NeospaceTypographyWeight WeightOf(TMP_Text text)
    {
        string fontName = text.font != null ? text.font.name : string.Empty;
        if (fontName.IndexOf("SemiBold", StringComparison.OrdinalIgnoreCase) >= 0 ||
            fontName.IndexOf("Bold", StringComparison.OrdinalIgnoreCase) >= 0 ||
            (text.fontStyle & FontStyles.Bold) != 0)
            return NeospaceTypographyWeight.SemiBold;
        if (fontName.IndexOf("Medium", StringComparison.OrdinalIgnoreCase) >= 0)
            return NeospaceTypographyWeight.Medium;
        return NeospaceTypographyWeight.Regular;
    }

    static bool IsTechnical(string path, string value)
    {
        string[] pathSignals =
        {
            "/Price", "/Size", "/Count", "/Code", "/Meta", "/Txt_Status",
            "/Badge_Cost/", "/PartCount", "/Cost", "Dimension", "Coordinate"
        };
        return pathSignals.Any(signal => path.IndexOf(signal, StringComparison.OrdinalIgnoreCase) >= 0) ||
               (!string.IsNullOrWhiteSpace(value) && TechnicalText.IsMatch(value));
    }

    static bool IsDisplay(string path, string value)
    {
        return (path.IndexOf("/Dock/Nav/Tabs/", StringComparison.OrdinalIgnoreCase) >= 0 &&
                !string.IsNullOrWhiteSpace(value)) ||
               path.EndsWith("/ProjectName/Txt_ProjectName", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith("/AddBlockDialog/Card/Title", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith("/ConfirmDialog/Card/Title", StringComparison.OrdinalIgnoreCase) ||
               path.IndexOf("/CheckoutPage/Title", StringComparison.OrdinalIgnoreCase) >= 0 ||
               path.IndexOf("/LiteCheckoutPage/Title", StringComparison.OrdinalIgnoreCase) >= 0 ||
               path.IndexOf("/LiteStylePage/Title", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static string HierarchyPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }
}
#endif
