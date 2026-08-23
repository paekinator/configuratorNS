using UnityEngine;
using UnityEditor;

namespace Evo.EditorTools
{
    public static class EvoEditorSettings
    {
        public static bool IsCustomEditorEnabled(string packageName = "")
        {
            string key = string.IsNullOrEmpty(packageName) ? "Evo_CustomEditor_Enabled" : $"Evo_CustomEditor_{packageName}_Enabled";
            return EditorPrefs.GetBool(key, true);
        }

        public static void SetCustomEditorEnabled(string packageName, bool enabled)
        {
            string key = string.IsNullOrEmpty(packageName) ? "Evo_CustomEditor_Enabled" : $"Evo_CustomEditor_{packageName}_Enabled";
            EditorPrefs.SetBool(key, enabled);

            // Force repaint all inspectors
            EditorApplication.RepaintHierarchyWindow();
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>()) { window.Repaint(); }
        }
    }
}