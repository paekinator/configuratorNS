using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(HorizontalChart))]
    public class HorizontalChartEditor : Editor
    {
        // Target
        HorizontalChart chartTarget;

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
        SerializedProperty verticalGridLines;
        SerializedProperty gridLineThickness;

        // Axis Settings
        SerializedProperty showAxis;
        SerializedProperty axisThickness;

        // Value Settings
        SerializedProperty showValues;
        SerializedProperty valueWidth;
        SerializedProperty valueFontSize;

        // Style
        SerializedProperty stylingSource;
        SerializedProperty stylerPreset;
        SerializedProperty colorMapping;
        SerializedProperty fontMapping;
        SerializedProperty labelWidth;
        SerializedProperty labelFontSize;

        void OnEnable()
        {
            chartTarget = (HorizontalChart)target;

            dataPoints = serializedObject.FindProperty("dataPoints");

            padding = serializedObject.FindProperty("padding");
            gridToBarsOffset = serializedObject.FindProperty("gridToBarsOffset");
            barSpacing = serializedObject.FindProperty("barSpacing");
            barCornerRadius = serializedObject.FindProperty("barCornerRadius");
            barSprite = serializedObject.FindProperty("barSprite");

            showGrid = serializedObject.FindProperty("showGrid");
            verticalGridLines = serializedObject.FindProperty("verticalGridLines");
            gridLineThickness = serializedObject.FindProperty("gridLineThickness");

            showAxis = serializedObject.FindProperty("showAxis");
            axisThickness = serializedObject.FindProperty("axisThickness");

            showValues = serializedObject.FindProperty("showValues");
            valueWidth = serializedObject.FindProperty("valueWidth");
            valueFontSize = serializedObject.FindProperty("valueFontSize");

            stylingSource = serializedObject.FindProperty("stylingSource");
            stylerPreset = serializedObject.FindProperty("stylerPreset");
            colorMapping = serializedObject.FindProperty("colorMapping");
            fontMapping = serializedObject.FindProperty("fontMapping");
            labelWidth = serializedObject.FindProperty("labelWidth");
            labelFontSize = serializedObject.FindProperty("labelFontSize");

            // Register to hover repaints
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
                            EvoEditorGUI.DrawProperty(verticalGridLines, "Vertical Lines", null, true, true);
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
                            EvoEditorGUI.DrawProperty(valueWidth, "Width", null, true, true);
                            EvoEditorGUI.DrawProperty(valueFontSize, "Font Size", null, false, true);
                        }
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndVerticalBackground();
#if EVO_LOCALIZATION
                    EvoEditorGUI.AddLayoutSpace();
                    Localization.ExternalEditor.DrawLocalizationContainer(serializedObject, chartTarget.gameObject, addSpace: false);
#endif
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
                    StylerEditor.DrawStylingSourceSection(serializedObject, HorizontalChart.GetColorFields(), HorizontalChart.GetFontFields());
                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.BeginContainer("Others", 3);
                    {
                        EvoEditorGUI.DrawProperty(labelFontSize, "Font Size", null, true);
                        EvoEditorGUI.DrawProperty(labelWidth, "Width", null, false);
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