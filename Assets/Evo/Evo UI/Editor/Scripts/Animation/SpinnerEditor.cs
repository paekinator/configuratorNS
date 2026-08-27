using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(Spinner))]
    public class SpinnerEditor : Editor
    {
        // Properties
        SerializedProperty spinnerStyle;
        SerializedProperty rotationSpeed;
        SerializedProperty minFillAmount;
        SerializedProperty maxFillAmount;
        SerializedProperty spinnerImage;

        // Foldout states
        bool settingsFoldout = true;
        bool referencesFoldout = false;

        void OnEnable()
        {
            spinnerStyle = serializedObject.FindProperty("spinnerStyle");
            rotationSpeed = serializedObject.FindProperty("rotationSpeed");
            minFillAmount = serializedObject.FindProperty("minFillAmount");
            maxFillAmount = serializedObject.FindProperty("maxFillAmount");
            spinnerImage = serializedObject.FindProperty("spinnerImage");

            EvoEditorGUI.RegisterEditor(this);
        }

        void OnDisable()
        {
            EvoEditorGUI.UnregisterEditor(this);
        }

        public override void OnInspectorGUI()
        {
            if (!EvoEditorSettings.IsCustomEditorEnabled(Constants.CustomEditorID)) { DrawDefaultInspector(); }
            else
            {
                DrawCustomGUI();
                EvoEditorGUI.HandleInspectorGUI();
            }
        }

        void DrawCustomGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            DrawSettings();
            DrawReferences();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawSettings()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        EvoEditorGUI.DrawProperty(spinnerStyle, "Spinner Style", null, false, false);
                        EvoEditorGUI.BeginContainer(3);
                        {
                            EvoEditorGUI.DrawProperty(rotationSpeed, "Rotation Speed", null, true, true);
                            EvoEditorGUI.DrawProperty(minFillAmount, "Min Fill Amount", null, true, true);
                            EvoEditorGUI.DrawProperty(maxFillAmount, "Max Fill Amount", null, false, true);
                        }
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndVerticalBackground();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawReferences()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawProperty(spinnerImage, "Spinner Image", null, false, true, true);
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
        }
    }
}