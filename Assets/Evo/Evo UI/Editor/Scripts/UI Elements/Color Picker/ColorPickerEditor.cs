using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(ColorPicker))]
    public class ColorPickerEditor : Editor
    {
        // Target
        ColorPicker cpTarget;

        // Settings
        SerializedProperty resetOnRightClick;
        SerializedProperty startColor;
        SerializedProperty pickerType;
        SerializedProperty wheelSize;

        // References
        SerializedProperty colorWheel;
        SerializedProperty colorPreview;
        SerializedProperty colorSelector;
        SerializedProperty hexInput;
        SerializedProperty hueSlider;
        SerializedProperty hueGradient;
        SerializedProperty saturationSlider;
        SerializedProperty saturationGradient;
        SerializedProperty brightnessSlider;
        SerializedProperty brightnessGradient;
        SerializedProperty opacitySlider;
        SerializedProperty opacityGradient;

        // Events
        SerializedProperty onColorChanged;

        void OnEnable()
        {
            cpTarget = (ColorPicker)target;

            resetOnRightClick = serializedObject.FindProperty("resetOnRightClick");
            startColor = serializedObject.FindProperty("startColor");
            pickerType = serializedObject.FindProperty("pickerType");
            wheelSize = serializedObject.FindProperty("wheelSize");

            colorWheel = serializedObject.FindProperty("colorWheel");
            colorPreview = serializedObject.FindProperty("colorPreview");
            colorSelector = serializedObject.FindProperty("colorSelector");
            hexInput = serializedObject.FindProperty("hexInput");
            hueSlider = serializedObject.FindProperty("hueSlider");
            hueGradient = serializedObject.FindProperty("hueGradient");
            saturationSlider = serializedObject.FindProperty("saturationSlider");
            saturationGradient = serializedObject.FindProperty("saturationGradient");
            brightnessSlider = serializedObject.FindProperty("brightnessSlider");
            brightnessGradient = serializedObject.FindProperty("brightnessGradient");
            opacitySlider = serializedObject.FindProperty("opacitySlider");
            opacityGradient = serializedObject.FindProperty("opacityGradient");

            onColorChanged = serializedObject.FindProperty("onColorChanged");

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

            DrawSettingsSection();
            DrawReferencesSection();
            DrawEventsSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawSettingsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref cpTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawToggle(resetOnRightClick, "Reset On Right Click", null, true, true, true);
                    EvoEditorGUI.DrawProperty(startColor, "Start Color", null, true, true, true);
                    EvoEditorGUI.DrawProperty(pickerType, "Picker Type", null, true, true, true);
                    EvoEditorGUI.DrawProperty(wheelSize, "Wheel Size", null, false, true, true);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawReferencesSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref cpTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(colorWheel, "Color Wheel", null, true, true, true);
                    EvoEditorGUI.DrawProperty(colorPreview, "Color Preview", null, true, true, true);
                    EvoEditorGUI.DrawProperty(colorSelector, "Color Selector", null, true, true, true);
                    EvoEditorGUI.DrawProperty(hexInput, "Hex Input", null, true, true, true);
                    EvoEditorGUI.DrawProperty(hueSlider, "Hue Slider", null, true, true, true);
                    EvoEditorGUI.DrawProperty(hueGradient, "Hue Gradient", null, true, true, true);
                    EvoEditorGUI.DrawProperty(saturationSlider, "Saturation Slider", null, true, true, true);
                    EvoEditorGUI.DrawProperty(saturationGradient, "Saturation Gradient", null, true, true, true);
                    EvoEditorGUI.DrawProperty(brightnessSlider, "Brightness Slider", null, true, true, true);
                    EvoEditorGUI.DrawProperty(brightnessGradient, "Brightness Gradient", null, true, true, true);
                    EvoEditorGUI.DrawProperty(opacitySlider, "Opacity Slider", null, true, true, true);
                    EvoEditorGUI.DrawProperty(opacityGradient, "Opacity Gradient", null, false, true, true);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawEventsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref cpTarget.eventsFoldout, "Events", EvoEditorGUI.GetIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawProperty(onColorChanged, "On Color Changed", null, false, false);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }
    }
}