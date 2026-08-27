using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(LineChart))]
    public class LineChartEditor : Editor
    {
        // Target
        LineChart chartTarget;

        // Chart Data
        SerializedProperty dataPoints;

        // Settings
        SerializedProperty padding;
        SerializedProperty labelPadding;
        SerializedProperty valuePadding;
        SerializedProperty horizontalGridLines;
        SerializedProperty verticalGridLines;
        SerializedProperty pointSprite;
        SerializedProperty lineThickness;
        SerializedProperty pointSize;
        SerializedProperty gridLineThickness;

        // Style
        SerializedProperty stylingSource;
        SerializedProperty stylerPreset;
        SerializedProperty colorMapping;
        SerializedProperty fontMapping;
        SerializedProperty labelFontSize;

        void OnEnable()
        {
            chartTarget = (LineChart)target;

            dataPoints = serializedObject.FindProperty("dataPoints");

            padding = serializedObject.FindProperty("padding");
            labelPadding = serializedObject.FindProperty("labelPadding");
            valuePadding = serializedObject.FindProperty("valuePadding");
            lineThickness = serializedObject.FindProperty("lineThickness");
            horizontalGridLines = serializedObject.FindProperty("horizontalGridLines");
            verticalGridLines = serializedObject.FindProperty("verticalGridLines");
            gridLineThickness = serializedObject.FindProperty("gridLineThickness");
            pointSize = serializedObject.FindProperty("pointSize");
            pointSprite = serializedObject.FindProperty("pointSprite");

            stylingSource = serializedObject.FindProperty("stylingSource");
            stylerPreset = serializedObject.FindProperty("stylerPreset");
            colorMapping = serializedObject.FindProperty("colorMapping");
            fontMapping = serializedObject.FindProperty("fontMapping");
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
                EvoEditorGUI.DrawProperty(padding, "Padding", "Set the content padding.", true, true, true, hasFoldout: true);
                EvoEditorGUI.DrawProperty(labelPadding, "Label Padding", null, true, true, true);
                EvoEditorGUI.DrawProperty(valuePadding, "Value Padding", null, true, true, true);
                EvoEditorGUI.DrawProperty(lineThickness, "Line Thickness", null, true, true, true);
                EvoEditorGUI.DrawProperty(horizontalGridLines, "Hor. Grid Lines", null, true, true, true);
                EvoEditorGUI.DrawProperty(verticalGridLines, "Ver. Grid Lines", null, true, true, true);
                EvoEditorGUI.DrawProperty(gridLineThickness, "Grid Line Thickness", null, true, true, true);
                EvoEditorGUI.DrawProperty(pointSize, "Point Size", null, true, true, true);
                EvoEditorGUI.DrawProperty(pointSprite, "Point Sprite", null, false, true, true);
#if EVO_LOCALIZATION
                EvoEditorGUI.AddLayoutSpace();
                Localization.ExternalEditor.DrawLocalizationContainer(serializedObject, chartTarget.gameObject, addSpace: false);
#endif
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawStyleSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref chartTarget.styleFoldout, "Style", EvoEditorGUI.GetIcon("UI_Style")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    StylerEditor.DrawStylingSourceSection(serializedObject, LineChart.GetColorFields(), LineChart.GetFontFields());
                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.BeginContainer("Others", 3);
                    {
                        EvoEditorGUI.DrawProperty(labelFontSize, "Font Size", null, false);
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground();
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }
    }
}