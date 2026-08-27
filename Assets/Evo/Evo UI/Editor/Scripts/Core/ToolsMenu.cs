using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI
{
    public static class ToolsMenu
    {
        [MenuItem("Tools/Evo UI/Open Default Styler %#M", false, 0)]
        static void OpenDefaultStyler() => Selection.activeObject = Styler.GetDefaultPreset();

        [MenuItem("Tools/Evo UI/Enable Custom Editor", false, 22)]
        static void EnableCustomEditor()
        {
            bool current = EvoEditorSettings.IsCustomEditorEnabled(Constants.CustomEditorID);

            if (!current) { EvoEditorSettings.SetCustomEditorEnabled(Constants.CustomEditorID, true); }
            else if (EditorUtility.DisplayDialog("Evo UI - Custom Editor",
                    "Are you sure you want to disable the custom editor layout?\n\n" +
                    "Some components that depend on custom editor functionality will ignore this option.",
                    "Confirm", "Cancel"))
            {
                EvoEditorSettings.SetCustomEditorEnabled(Constants.CustomEditorID, false);
            }
        }

        [MenuItem("Tools/Evo UI/Enable Custom Editor", true, 22)]
        static bool EnableCustomEditorValidate()
        {
            Menu.SetChecked("Tools/Evo UI/Enable Custom Editor", EvoEditorSettings.IsCustomEditorEnabled(Constants.CustomEditorID));
            return true;
        }

        [MenuItem("Tools/Evo UI/Online Documentation", false, 44)]
        static void OpenDocs() => UnityEngine.Application.OpenURL(Constants.HelpUrl);

        [MenuItem("Tools/Evo UI/Discord Server", false, 45)]
        static void JoinDiscord() => UnityEngine.Application.OpenURL("https://discord.gg/VXpHyUt");
    }
}
