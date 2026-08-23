using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(RadialLayoutGroup), true)]
    public class RadialLayoutGroupEditor : Editor
    {
        // Properties
        SerializedProperty radiusScale;
        SerializedProperty centerOffset;
        SerializedProperty padding;
        SerializedProperty startAngle;
        SerializedProperty endAngle;
        SerializedProperty clockwise;
        SerializedProperty evenDistribution;
        SerializedProperty customSpacing;
        SerializedProperty faceCenter;
        SerializedProperty controlChildSize;
        SerializedProperty childSize;

        // Helpers
        bool settingsFoldout = true;
        bool presetsFoldout = true;

        void OnEnable()
        {
            radiusScale = serializedObject.FindProperty("radiusScale");
            centerOffset = serializedObject.FindProperty("centerOffset");
            padding = serializedObject.FindProperty("m_Padding");
            startAngle = serializedObject.FindProperty("startAngle");
            endAngle = serializedObject.FindProperty("endAngle");
            clockwise = serializedObject.FindProperty("clockwise");
            evenDistribution = serializedObject.FindProperty("evenDistribution");
            customSpacing = serializedObject.FindProperty("customSpacing");
            faceCenter = serializedObject.FindProperty("faceCenter");
            controlChildSize = serializedObject.FindProperty("controlChildSize");
            childSize = serializedObject.FindProperty("childSize");

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
            DrawPresetsSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawSettingsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.BeginContainer("Layout", 3);
                    {
                        EvoEditorGUI.DrawProperty(radiusScale, "Radius Scale", "Adjust the scale of the layout.", true);
                        EvoEditorGUI.DrawProperty(centerOffset, "Center Offset", null, true);
                        EvoEditorGUI.DrawProperty(padding, "Padding", null, false, hasFoldout: true);
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground(true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.BeginContainer("Angle", 3);
                    {
                        EvoEditorGUI.DrawToggle(clockwise, "Clockwise", "Sets the alignment of objects in a clockwise direction.", true);
                        EvoEditorGUI.DrawSlider(startAngle, 0f, endAngle.floatValue, "Start Angle", true);
                        GUI.enabled = evenDistribution.boolValue;
                        EvoEditorGUI.DrawSlider(endAngle, 0f, 360f, "End Angle", false);
                        GUI.enabled = true;
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground(true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.BeginContainer("Spacing", 3);
                    {
                        EvoEditorGUI.DrawToggle(evenDistribution, "Even Distribution", "Evenly spaces child objects.", true);
                        GUI.enabled = !evenDistribution.boolValue;
                        EvoEditorGUI.DrawProperty(customSpacing, "Custom Spacing", null, false);
                        GUI.enabled = true;
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground(true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.BeginContainer("Child Objects", 3);
                    {
                        EvoEditorGUI.DrawToggle(faceCenter, "Face Center", "Sets each child object's rotation to face the center.", true);
                        EvoEditorGUI.DrawToggle(controlChildSize, "Control Child Size", null, controlChildSize.boolValue);
                        if (controlChildSize.boolValue) { EvoEditorGUI.DrawProperty(childSize, "Child Size", null, false); }
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground();
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawPresetsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref presetsFoldout, "Presets", EvoEditorGUI.GetIcon("UI_Style")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EditorGUILayout.BeginHorizontal();
                    if (EvoEditorGUI.DrawButton("Full Circle", revertBackgroundColor: true))
                    {
                        startAngle.floatValue = 0f;
                        endAngle.floatValue = 360f;
                        evenDistribution.boolValue = true;
                    }
                    GUILayout.Space(4);
                    if (EvoEditorGUI.DrawButton("Half Circle", revertBackgroundColor: true))
                    {
                        startAngle.floatValue = 0f;
                        endAngle.floatValue = 180f;
                        evenDistribution.boolValue = true;
                    }
                    GUILayout.Space(4);
                    if (EvoEditorGUI.DrawButton("Quarter Circle", revertBackgroundColor: true))
                    {
                        startAngle.floatValue = 0f;
                        endAngle.floatValue = 90f;
                        evenDistribution.boolValue = true;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }
    }
}