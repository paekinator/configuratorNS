using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(RadioButtonGroup))]
    public class RadioButtonGroupEditor : Editor
    {
        // Target
        RadioButtonGroup rbgTarget;

        // Settings
        SerializedProperty selectedIndex;
        SerializedProperty allowDeselection;
        SerializedProperty useUnscaledTime;
        SerializedProperty stateChangeDelay;
        SerializedProperty targetParent;

        // Indicator
        SerializedProperty indicatorObject;
        SerializedProperty indicatorDirection;
        SerializedProperty indicatorAutoSize;
        SerializedProperty indicatorStretch;
        SerializedProperty indicatorCurve;
        SerializedProperty indicatorDuration;

        // Events
        SerializedProperty onSelectionChanged;

        // Foldout states
        bool objectFoldout = true;
        bool settingsFoldout = true;
        bool eventsFoldout = false;

        void OnEnable()
        {
            rbgTarget = (RadioButtonGroup)target;

            selectedIndex = serializedObject.FindProperty("selectedIndex");
            allowDeselection = serializedObject.FindProperty("allowDeselection");
            useUnscaledTime = serializedObject.FindProperty("useUnscaledTime");
            stateChangeDelay = serializedObject.FindProperty("stateChangeDelay");
            targetParent = serializedObject.FindProperty("targetParent");

            indicatorObject = serializedObject.FindProperty("indicatorObject");
            indicatorDirection = serializedObject.FindProperty("indicatorDirection");
            indicatorAutoSize = serializedObject.FindProperty("indicatorAutoSize");
            indicatorStretch = serializedObject.FindProperty("indicatorStretch");
            indicatorCurve = serializedObject.FindProperty("indicatorCurve");
            indicatorDuration = serializedObject.FindProperty("indicatorDuration");

            onSelectionChanged = serializedObject.FindProperty("onSelectionChanged");

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

            DrawObject();
            DrawSettings();
            DrawEvents();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawObject()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref objectFoldout, "Object", EvoEditorGUI.GetIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        Transform targetParent = rbgTarget.targetParent != null ? rbgTarget.targetParent : rbgTarget.transform;
                        if (rbgTarget.selectedIndex > targetParent.childCount - 1) { rbgTarget.selectedIndex = -1; }
                        if (rbgTarget.selectedIndex >= 0 && targetParent.childCount > 0) { EvoEditorGUI.DrawLabel($" Selected Button: {targetParent.GetChild(rbgTarget.selectedIndex).name}"); }
                        else { EvoEditorGUI.DrawLabel(" Selected Button: None"); }
                        EditorGUI.BeginChangeCheck();
                        EvoEditorGUI.DrawSlider(selectedIndex, -1, targetParent.childCount > 0 ? targetParent.childCount - 1 : 0, "Index:", false, false, labelWidth: 40);
                        if (EditorGUI.EndChangeCheck()) { serializedObject.ApplyModifiedProperties(); }
                    }
                    EvoEditorGUI.EndVerticalBackground();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawSettings()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawToggle(allowDeselection, "Allow Deselection", "Allow deselection of buttons.", true, true, true);
                    EvoEditorGUI.DrawToggle(useUnscaledTime, "Use Unscaled Time", null, true, true, true);
                    EvoEditorGUI.DrawProperty(targetParent, "Custom Parent", "If not set, attached transform will be used as button parent.", true, true, true);

                    // Indicator
                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        EvoEditorGUI.DrawProperty(indicatorObject, "Indicator Object", null, false, false);
                        if (indicatorObject.objectReferenceValue)
                        {
                            EvoEditorGUI.BeginContainer(3);
                            EvoEditorGUI.DrawToggle(indicatorAutoSize, "Auto Size", null, true, true);
                            EvoEditorGUI.DrawProperty(indicatorDirection, "Direction", null, true, true);
                            EvoEditorGUI.DrawProperty(indicatorStretch, "Elastic Stretch", "Amount of stretch effect during animation.", true, true);
                            EvoEditorGUI.DrawProperty(indicatorCurve, "Animation Curve", null, true, true);
                            EvoEditorGUI.DrawProperty(indicatorDuration, "Animation Duration", null, true, true);
                            EvoEditorGUI.DrawProperty(stateChangeDelay, "State Change Delay", "Delay the visual state change (e.g. text color) to sync with indicator arrival.", false, true);
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndVerticalBackground();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawEvents()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref eventsFoldout, "Events", EvoEditorGUI.GetIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(onSelectionChanged, "On Selection Changed", null, true, false);
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
        }
    }
}