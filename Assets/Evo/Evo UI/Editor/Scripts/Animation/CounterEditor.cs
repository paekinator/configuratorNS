using Evo.EditorTools;
using UnityEditor;

namespace Evo.UI
{
    [CustomEditor(typeof(Counter))]
    public class CounterEditor : Editor
    {
        // Properties
        SerializedProperty textObject;
        SerializedProperty value;
        SerializedProperty textFormat;
        SerializedProperty displayFormat;
        SerializedProperty animateOnEnable;
        SerializedProperty useUnscaledTime;
        SerializedProperty counterDuration;
        SerializedProperty delay;
        SerializedProperty animationCurve;

        // Foldout states
        bool settingsFoldout = true;
        bool referencesFoldout = true;

        void OnEnable()
        {
            textObject = serializedObject.FindProperty("textObject");
            value = serializedObject.FindProperty("value");
            textFormat = serializedObject.FindProperty("textFormat");
            displayFormat = serializedObject.FindProperty("displayFormat");
            animateOnEnable = serializedObject.FindProperty("animateOnEnable");
            useUnscaledTime = serializedObject.FindProperty("useUnscaledTime");
            counterDuration = serializedObject.FindProperty("counterDuration");
            delay = serializedObject.FindProperty("delay");
            animationCurve = serializedObject.FindProperty("animationCurve");

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
                    EvoEditorGUI.BeginContainer("Core", 3);
                    {
                        EvoEditorGUI.DrawProperty(value, "Value", null, true);
                        EvoEditorGUI.DrawProperty(textFormat, "Text Format", null, true);
                        EvoEditorGUI.DrawProperty(displayFormat, "Display Format", null, false);
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground(true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.BeginContainer("Animation", 3);
                    {
                        EvoEditorGUI.DrawToggle(animateOnEnable, "Animate On Enable", null, true);
                        EvoEditorGUI.DrawToggle(useUnscaledTime, "Use Unscaled Time", null, true);
                        EvoEditorGUI.DrawProperty(counterDuration, "Counter Duration", null, true);
                        EvoEditorGUI.DrawProperty(delay, "Delay", null, true);
                        EvoEditorGUI.DrawProperty(animationCurve, "Curve", null, false);
                    }
                    EvoEditorGUI.EndContainer();
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
                {
                    EvoEditorGUI.DrawProperty(textObject, "Text Object", null, false, true, true);
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
        }
    }
}