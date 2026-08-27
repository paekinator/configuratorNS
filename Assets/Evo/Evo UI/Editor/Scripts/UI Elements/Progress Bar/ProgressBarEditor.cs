using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(ProgressBar))]
    [CanEditMultipleObjects]
    public class ProgressBarEditor : Editor
    {
        // Target
        ProgressBar pbTarget;

        // Object
        SerializedProperty value;
        SerializedProperty minValue;
        SerializedProperty maxValue;

        // Settings
        SerializedProperty invokeAtStart;
        SerializedProperty isVertical;
        SerializedProperty enableSmoothing;
        SerializedProperty smoothingDuration;
        SerializedProperty smoothingCurve;
        SerializedProperty displayMultiplier;
        SerializedProperty displayFormat;
        SerializedProperty textFormat;

        // References
        SerializedProperty fillRect;
        SerializedProperty valueText;

        // Events
        SerializedProperty onValueChanged;

        void OnEnable()
        {
            pbTarget = (ProgressBar)target;

            value = serializedObject.FindProperty("value");
            minValue = serializedObject.FindProperty("minValue");
            maxValue = serializedObject.FindProperty("maxValue");

            invokeAtStart = serializedObject.FindProperty("invokeAtStart");
            isVertical = serializedObject.FindProperty("isVertical");
            enableSmoothing = serializedObject.FindProperty("enableSmoothing");
            smoothingDuration = serializedObject.FindProperty("smoothingDuration");
            smoothingCurve = serializedObject.FindProperty("smoothingCurve");
            displayMultiplier = serializedObject.FindProperty("displayMultiplier");
            displayFormat = serializedObject.FindProperty("displayFormat");
            textFormat = serializedObject.FindProperty("textFormat");

            fillRect = serializedObject.FindProperty("fillRect");
            valueText = serializedObject.FindProperty("valueText");

            onValueChanged = serializedObject.FindProperty("onValueChanged");

            // Register this editor for hover repaints
            EvoEditorGUI.RegisterEditor(this);
        }

        void OnDisable()
        {
            // Unregister from hover repaints
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

            DrawObjectSection();
            DrawSettingsSection();
            DrawReferencesSection();
            DrawEventsSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawObjectSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref pbTarget.objectFoldout, "Object", EvoEditorGUI.GetIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();

                EditorGUI.BeginChangeCheck();
                EvoEditorGUI.DrawSlider(value, minValue.floatValue, maxValue.floatValue, "Value", true, true, true);
                if (EditorGUI.EndChangeCheck())
                {
                    // Apply the change before sending the event
                    serializedObject.ApplyModifiedProperties();

                    foreach (var t in targets)
                    {
                        if (t is Slider slider)
                        {
                            slider.onValueChanged?.Invoke(slider.value);
                        }
                    }
                }

                EvoEditorGUI.DrawProperty(minValue, "Min Value", null, true, true, true);
                EvoEditorGUI.DrawProperty(maxValue, "Max Value", null, false, true, true);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawSettingsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref pbTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawToggle(invokeAtStart, "Invoke At Start", "Process OnValueChanged events at runtime start.", true, true, true);
                EvoEditorGUI.DrawToggle(isVertical, "Is Vertical", "Set whetever if this bar is vertical or not.", true, true, true);
                EvoEditorGUI.BeginVerticalBackground(true);
                {
                    EvoEditorGUI.DrawToggle(enableSmoothing, "Smoothing", addSpace: false, customBackground: true, revertColor: true, bypassNormalBackground: true);
                    if (enableSmoothing.boolValue)
                    {
                        EvoEditorGUI.BeginContainer(3);
                        EvoEditorGUI.DrawProperty(smoothingDuration, "Duration", null, true, true);
                        EvoEditorGUI.DrawProperty(smoothingCurve, "Curve", null, false, true);
                        EvoEditorGUI.EndContainer();
                    }
                }
                EvoEditorGUI.EndVerticalBackground(true);
                if (valueText.objectReferenceValue != null)
                {
                    EvoEditorGUI.DrawProperty(displayMultiplier, "Display Multiplier", null, true, true, true);
                    EvoEditorGUI.DrawProperty(displayFormat, "Display Format", null, true, true, true);
                    EvoEditorGUI.DrawProperty(textFormat, "Text Format", null, false, true, true);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawReferencesSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref pbTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawProperty(fillRect, "Fill Rect", null, true, true, true);
                EvoEditorGUI.DrawProperty(valueText, "Value Text", null, false, true, true);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawEventsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref pbTarget.eventsFoldout, "Events", EvoEditorGUI.GetIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawProperty(onValueChanged, "On Value Changed", null, false, false);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }
    }
}