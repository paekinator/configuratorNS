using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(FlexibleGridLayout), true)]
    public class FlexibleGridLayoutEditor : Editor
    {
        // Layout Properties
        SerializedProperty padding;
        SerializedProperty childAlignment;

        SerializedProperty fitType;
        SerializedProperty rows;
        SerializedProperty columns;

        // Sizing Properties
        SerializedProperty spacing;
        SerializedProperty minCellSize;
        SerializedProperty masonry;

        // Constraint Properties
        SerializedProperty controlChildWidth;
        SerializedProperty controlChildHeight;
        SerializedProperty fitX;
        SerializedProperty fitY;
        SerializedProperty preserveAspectRatio;
        SerializedProperty aspectRatio;

        // UI Helpers
        bool settingsFoldout = true;

        void OnEnable()
        {
            // Base LayoutGroup properties (these remain m_ as they belong to Unity's native LayoutGroup)
            padding = serializedObject.FindProperty("m_Padding");
            childAlignment = serializedObject.FindProperty("m_ChildAlignment");

            // Custom FlexibleGrid properties
            fitType = serializedObject.FindProperty("fitType");
            rows = serializedObject.FindProperty("rows");
            columns = serializedObject.FindProperty("columns");

            spacing = serializedObject.FindProperty("spacing");
            minCellSize = serializedObject.FindProperty("minCellSize");
            masonry = serializedObject.FindProperty("masonry");

            controlChildWidth = serializedObject.FindProperty("controlChildWidth");
            controlChildHeight = serializedObject.FindProperty("controlChildHeight");
            fitX = serializedObject.FindProperty("fitX");
            fitY = serializedObject.FindProperty("fitY");
            preserveAspectRatio = serializedObject.FindProperty("preserveAspectRatio");
            aspectRatio = serializedObject.FindProperty("aspectRatio");

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

            EvoEditorGUI.BeginVerticalBackground();
            {
                if (EvoEditorGUI.DrawFoldout(ref settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
                {
                    EvoEditorGUI.BeginContainer();

                    DrawLayoutSection();
                    DrawSizingSection();
                    DrawConstraintsSection();

                    EvoEditorGUI.EndContainer();
                }
            }
            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawLayoutSection()
        {
            EvoEditorGUI.BeginVerticalBackground(true);
            EvoEditorGUI.BeginContainer("Grid Layout", 3);
            {
                EvoEditorGUI.DrawProperty(fitType, "Fit Type");

                int fitEnum = fitType.enumValueIndex;
                if (fitEnum == 1 || fitEnum == 4) { EvoEditorGUI.DrawProperty(columns, "Columns"); }
                else if (fitEnum == 2 || fitEnum == 3) { EvoEditorGUI.DrawProperty(rows, "Rows"); }

                EvoEditorGUI.DrawProperty(childAlignment, "Child Alignment");
                EvoEditorGUI.DrawToggle(masonry, "Masonry Layout", masonry.tooltip, false);

                if (masonry.boolValue && (preserveAspectRatio.boolValue || fitY.boolValue))
                {
                    EvoEditorGUI.AddLayoutSpace();
                    EvoEditorGUI.DrawInfoBox("'Preserve Aspect Ratio' and 'Fit Y' can mess with calculations when masonry enabled. " +
                        "Consider disabling them.");
                }
            }
            EvoEditorGUI.EndContainer();
            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddPropertySpace();
        }

        void DrawSizingSection()
        {
            EvoEditorGUI.BeginVerticalBackground(true);
            EvoEditorGUI.BeginContainer("Spacing & Sizing", 3);
            {
                EvoEditorGUI.DrawProperty(padding, "Padding", hasFoldout: true);
                EvoEditorGUI.DrawProperty(minCellSize, "Min Cell Size");
                EvoEditorGUI.DrawProperty(spacing, "Spacing", null, false);
            }
            EvoEditorGUI.EndContainer();
            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddPropertySpace();
        }

        void DrawConstraintsSection()
        {
            EvoEditorGUI.BeginVerticalBackground(true);
            EvoEditorGUI.BeginContainer("Child Constraints", 3);

            EvoEditorGUI.DrawToggle(controlChildWidth, "Control Child Width", controlChildWidth.tooltip);
            EvoEditorGUI.DrawToggle(controlChildHeight, "Control Child Height", controlChildHeight.tooltip);

            EvoEditorGUI.DrawToggle(fitX, "Fit X", fitX.tooltip);
            EvoEditorGUI.DrawToggle(fitY, "Fit Y", fitY.tooltip);

            EvoEditorGUI.DrawToggle(preserveAspectRatio, "Preserve Aspect Ratio", preserveAspectRatio.tooltip, false);
            if (preserveAspectRatio.boolValue)
            {
                EvoEditorGUI.AddLayoutSpace();
                EvoEditorGUI.DrawProperty(aspectRatio, "Aspect Ratio (W/H)", null, false);
            }

            EvoEditorGUI.EndContainer();
            EvoEditorGUI.EndVerticalBackground();
        }
    }
}