#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Debug copy/paste helper for configuration codes:
/// Tools → Configurator → Configuration Code.
///
/// Generate a code from the current build (play mode), copy it, paste one
/// back, validate it, and load it into the scene. No gameplay UI — this is
/// an editor-only tool.
/// </summary>
public class ConfigurationCodeWindow : EditorWindow
{
    string _generated = string.Empty;
    string _pasted = string.Empty;
    string _statusMessage = string.Empty;
    MessageType _statusType = MessageType.None;
    Vector2 _scroll;

    [MenuItem("Tools/Configurator/Configuration Code")]
    public static void Open()
    {
        var window = GetWindow<ConfigurationCodeWindow>("Config Code");
        window.minSize = new Vector2(420f, 320f);
    }

    void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        // ---------------- Current build → code ----------------
        EditorGUILayout.LabelField("Current build → code", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Generate code from current build"))
            {
                var build = FindFirstObjectByType<BuildController>();
                if (build == null)
                {
                    SetStatus("No BuildController in the scene.", MessageType.Error);
                }
                else
                {
                    var model = ConfigurationCapture.Capture(build);
                    if (model.Beams.Count == 0 && model.Panels.Count == 0)
                    {
                        SetStatus("The grid is empty — nothing to encode.", MessageType.Warning);
                        _generated = string.Empty;
                    }
                    else
                    {
                        _generated = ConfigurationCode.Encode(model);
                        SetStatus($"Encoded {model.Beams.Count} beams, {model.Panels.Count} panels " +
                                  $"({_generated.Length} characters).", MessageType.Info);
                    }
                }
            }
        }
        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Enter Play mode to capture or load builds.", MessageType.None);

        if (!string.IsNullOrEmpty(_generated))
        {
            EditorGUILayout.SelectableLabel(_generated, EditorStyles.textArea,
                GUILayout.MinHeight(48f));
            if (GUILayout.Button("Copy to clipboard"))
            {
                EditorGUIUtility.systemCopyBuffer = _generated;
                SetStatus("Code copied to clipboard.", MessageType.Info);
            }
        }

        EditorGUILayout.Space(12f);

        // ---------------- Code → build ----------------
        EditorGUILayout.LabelField("Code → build", EditorStyles.boldLabel);
        _pasted = EditorGUILayout.TextArea(_pasted, GUILayout.MinHeight(48f));

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Paste"))
                _pasted = EditorGUIUtility.systemCopyBuffer;

            if (GUILayout.Button("Validate"))
            {
                var check = ConfigurationCode.Validate(_pasted);
                if (!check.IsValid)
                {
                    SetStatus(check.Error, MessageType.Error);
                }
                else
                {
                    string warnings = check.Warnings.Count > 0
                        ? "\n" + string.Join("\n", check.Warnings)
                        : string.Empty;
                    SetStatus($"Valid: {check.Model.Beams.Count} beams, " +
                              $"{check.Model.Panels.Count} panels" +
                              (check.Model.FinishApplied ? ", finish applied" : "") +
                              warnings,
                        check.Warnings.Count > 0 ? MessageType.Warning : MessageType.Info);
                }
            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Load into scene"))
                    LoadPastedCode();
            }
        }

        if (!string.IsNullOrEmpty(_statusMessage))
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(_statusMessage, _statusType);
        }

        EditorGUILayout.EndScrollView();
    }

    void LoadPastedCode()
    {
        var build = FindFirstObjectByType<BuildController>();
        if (build == null)
        {
            SetStatus("No BuildController in the scene.", MessageType.Error);
            return;
        }

        try
        {
            ConfigurationCode.Load(_pasted, build, report =>
            {
                SetStatus(report.Summary, report.BeamsSkipped + report.PanelsSkipped > 0
                    ? MessageType.Warning
                    : MessageType.Info);
                Repaint();
            });
            SetStatus("Loading…", MessageType.Info);
        }
        catch (ConfigurationCodeException e)
        {
            SetStatus(e.Message, MessageType.Error);
        }
    }

    void SetStatus(string message, MessageType type)
    {
        _statusMessage = message;
        _statusType = type;
        Repaint();
    }
}
#endif
