using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(VerticalChart))]
    public class VerticalChartEditor : Editor
    {
        // Target
        VerticalChart chartTarget;

        // Chart Data
        SerializedProperty dataPoints;

        // Settings
        SerializedProperty padding;
        SerializedProperty gridToBarsOffset;
        SerializedProperty barSpacing;
        SerializedProperty barCornerRadius;
        SerializedProperty barSprite;

        // Grid Settings
        SerializedProperty showGrid;
        SerializedProperty horizontalGridLines;
        SerializedProperty gridLineThickness;

        // Axis Settings
        SerializedProperty showAxis;
        SerializedProperty axisThickness;

        // Value Settings
        SerializedProperty showValues;
        SerializedProperty valueHeight;
        SerializedProperty valueFontSize;

        // Style
        SerializedProperty stylingSource;
        SerializedProperty stylerPreset;
        SerializedProperty colorMapping;
        SerializedProperty fontMapping;

        // Label Settings
        SerializedProperty labelHeight;
        SerializedProperty labelFontSize;

        void OnEnable()
        {
            chartTarget = (VerticalChart)target;

            dataPoints = serializedObject.FindProperty("dataPoints");

            padding = serializedObject.FindProperty("padding");
            gridToBarsOffset = serializedObject.FindProperty("gridToBarsOffset");
            barSpacing = serializedObject.FindProperty("barSpacing");
            barCornerRadius = serializedObject.FindProperty("barCornerRadius");
            barSprite = serializedObject.FindProperty("barSprite");

            showGrid = serializedObject.FindProperty("showGrid");
            horizontalGridLines = serializedObject.FindProperty("horizontalGridLines");
            gridLineThickness = serializedObject.FindProperty("gridLineThickness");

            showAxis = serializedObject.FindProperty("showAxis");
            axisThickness = serializedObject.FindProperty("axisThickness");

            showValues = serializedObject.FindProperty("showValues");
            valueHeight = serializedObject.FindProperty("valueHeight");
            valueFontSize = serializedObject.FindProperty("valueFontSize");

            // Styling properties
            stylingSource = serializedObject.FindProperty("stylingSource");
            stylerPreset = serializedObject.FindProperty("stylerPreset");
            colorMapping = serializedObject.FindProperty("colorMapping");
            fontMapping = serializedObject.FindProperty("fontMapping");

            labelHeight = serializedObject.FindProperty("labelHeight");
            labelFontSize = serializedObject.FindProperty("labelFontSize");

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

            DrawDataSection();
            DrawSettingsSection();
            DrawStyleSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawDataSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref chartTarget.dataFoldout, "Data", EvoEditorGUI.GetIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawArrayProperty(dataPoints, "Data Points", null, false, true, true);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawSettingsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref chartTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(padding, "Padding", "Set the content padding.", true, true, true, hasFoldout: true);
                    EvoEditorGUI.DrawProperty(gridToBarsOffset, "Grid To Bars Offset", null, true, true, true);
                    EvoEditorGUI.DrawProperty(barSpacing, "Bar Spacing", null, true, true, true);
                    EvoEditorGUI.DrawProperty(barCornerRadius, "Bar Corner Radius", null, true, true, true);
                    EvoEditorGUI.DrawProperty(barSprite, "Bar Sprite", null, true, true, true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawToggle(showGrid, "Show Grid", null, false, true, true, bypassNormalBackground: true);
                    if (showGrid.boolValue)
                    {
                        EvoEditorGUI.BeginContainer(3);
                        {
                            EvoEditorGUI.DrawProperty(horizontalGridLines, "Horizontal Lines", null, true, true);
                            EvoEditorGUI.DrawProperty(gridLineThickness, "Line Thickness", null, false, true);
                        }
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndVerticalBackground(true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawToggle(showAxis, "Show Axis", null, false, true, true, bypassNormalBackground: true);
                    if (showAxis.boolValue)
                    {
                        EvoEditorGUI.BeginContainer(3);
                        {
                            EvoEditorGUI.DrawProperty(axisThickness, "Thickness", null, false, true);
                        }
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndVerticalBackground(true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawToggle(showValues, "Show Values", null, false, true, true, bypassNormalBackground: true);
                    if (showValues.boolValue)
                    {
                        EvoEditorGUI.BeginContainer(3);
                        {
                            EvoEditorGUI.DrawProperty(valueHeight, "Height", null, true, true);
                            EvoEditorGUI.DrawProperty(valueFontSize, "Font Size", null, false, true);
                        }
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndVerticalBackground(false);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawStyleSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref chartTarget.stylingFoldout, "Style", EvoEditorGUI.GetIcon("UI_Style")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    StylerEditor.DrawStylingSourceSection(serializedObject, VerticalChart.GetColorFields(), VerticalChart.GetFontFields());
                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.BeginContainer("Others", 3);
                    {
                        EvoEditorGUI.DrawProperty(labelFontSize, "Font Size", null, true);
                        EvoEditorGUI.DrawProperty(labelHeight, "Label Height", null, false);
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground();
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }
    }
}