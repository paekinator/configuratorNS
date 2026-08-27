using UnityEditor;
using UnityEditor.UI;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(RadialSlider))]
    [CanEditMultipleObjects]
    public class RadialSliderEditor : SelectableEditor
    {
        // Target
        RadialSlider sTarget;

        // Object
        SerializedProperty wholeNumbers;
        SerializedProperty value;
        SerializedProperty minValue;
        SerializedProperty maxValue;

        // Settings
        SerializedProperty clockwise;
        SerializedProperty allowContinuousDrag;
        SerializedProperty invokeAtStart;
        SerializedProperty angleRange;
        SerializedProperty startAngle;
        SerializedProperty handleOffset;
        SerializedProperty displayMultiplier;
        SerializedProperty displayFormat;
        SerializedProperty textFormat;

        // Navigation
        SerializedProperty navigation;

        // References
        SerializedProperty fillImage;
        SerializedProperty handleRect;
        SerializedProperty valueText;
        SerializedProperty valueInput;

        // Events
        SerializedProperty onValueChanged;

        protected override void OnEnable()
        {
            sTarget = (RadialSlider)target;

            wholeNumbers = serializedObject.FindProperty("wholeNumbers");
            value = serializedObject.FindProperty("value");
            minValue = serializedObject.FindProperty("minValue");
            maxValue = serializedObject.FindProperty("maxValue");

            invokeAtStart = serializedObject.FindProperty("invokeAtStart");
            allowContinuousDrag = serializedObject.FindProperty("allowContinuousDrag");
            clockwise = serializedObject.FindProperty("clockwise");
            angleRange = serializedObject.FindProperty("angleRange");
            startAngle = serializedObject.FindProperty("startAngle");
            handleOffset = serializedObject.FindProperty("handleOffset");
            displayMultiplier = serializedObject.FindProperty("displayMultiplier");
            displayFormat = serializedObject.FindProperty("displayFormat");
            textFormat = serializedObject.FindProperty("textFormat");

            navigation = serializedObject.FindProperty("m_Navigation");

            fillImage = serializedObject.FindProperty("fillImage");
            handleRect = serializedObject.FindProperty("handleRect");
            valueText = serializedObject.FindProperty("valueText");
            valueInput = serializedObject.FindProperty("valueInput");

            onValueChanged = serializedObject.FindProperty("onValueChanged");

            // Register this editor for hover repaints
            EvoEditorGUI.RegisterEditor(this);

            // Prepare for UI navigation flow
            UINavigationEditor.PrepareForVisualize(this);
        }

        protected override void OnDisable()
        {
            // Unregister from hover repaints
            EvoEditorGUI.UnregisterEditor(this);

            // Remove from UI navigation flow
            UINavigationEditor.RemoveFromVisualize(this);
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
            DrawNavigationSection();
            DrawReferencesSection();
            DrawEventsSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawObjectSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref sTarget.objectFoldout, "Object", EvoEditorGUI.GetIcon("UI_Object")))
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

            if (EvoEditorGUI.DrawFoldout(ref sTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawToggle(invokeAtStart, "Invoke At Start", "Process OnValueChanged events at runtime start.", true, true, true);
                EvoEditorGUI.DrawToggle(allowContinuousDrag, "Continuous Drag", "Allow jumping between min and max values.", true, true, true);
                EvoEditorGUI.DrawToggle(clockwise, "Clockwise", null, true, true, true);
                EvoEditorGUI.DrawToggle(wholeNumbers, "Whole Numbers", null, true, true, true);
                EvoEditorGUI.DrawProperty(angleRange, "Angle Range", null, true, true, true);
                EvoEditorGUI.DrawProperty(startAngle, "Start Angle", null, true, true, true);
                EvoEditorGUI.DrawProperty(handleOffset, "Handle Offset", null, true, true, true);
                if (valueText.objectReferenceValue != null || valueInput.objectReferenceValue != null)
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

        void DrawNavigationSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref sTarget.navigationFoldout, "Navigation", EvoEditorGUI.GetIcon("UI_Navigation")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.BeginVerticalBackground(true);
                EvoEditorGUI.DrawProperty(navigation, "Navigation Mode", null, false, false);
                UINavigationEditor.DrawVisualizeButton();
                EvoEditorGUI.EndVerticalBackground();
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawReferencesSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref sTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawProperty(fillImage, "Fill Image", null, true, true, true);
                EvoEditorGUI.DrawProperty(handleRect, "Handle Rect", null, true, true, true);
                EvoEditorGUI.DrawProperty(valueText, "Value Text", null, true, true, true);
                EvoEditorGUI.DrawProperty(valueInput, "Value Input", null, false, true, true);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawEventsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref sTarget.eventsFoldout, "Events", EvoEditorGUI.GetIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawProperty(onValueChanged, "On Value Changed", null, false, false);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }
    }
}