using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(RadarChart))]
    public class RadarChartEditor : Editor
    {
        // Target
        RadarChart radarTarget;

        // Chart Data
        SerializedProperty dataPoints;

        // Chart Settings
        SerializedProperty scaleMultiplier;
        SerializedProperty gridLevels;
        SerializedProperty angleOffset;

        // Fill Settings
        SerializedProperty showFill;

        // Line Settings
        SerializedProperty showLine;
        SerializedProperty lineThickness;

        // Point Settings
        SerializedProperty showPoints;
        SerializedProperty pointSize;

        // Grid Settings
        SerializedProperty showGrid;
        SerializedProperty gridLineThickness;

        // Axis Settings
        SerializedProperty axisThickness;

        // Label Settings
        SerializedProperty showLabels;
        SerializedProperty labelFontSize;
        SerializedProperty labelOffset;
        SerializedProperty drawLabelBackground;
        SerializedProperty labelBackgroundSprite;
        SerializedProperty labelBackgroundPPU;
        SerializedProperty labelPadding;

        void OnEnable()
        {
            radarTarget = (RadarChart)target;

            dataPoints = serializedObject.FindProperty("dataPoints");

            scaleMultiplier = serializedObject.FindProperty("scaleMultiplier");
            gridLevels = serializedObject.FindProperty("gridLevels");
            angleOffset = serializedObject.FindProperty("angleOffset");

            showFill = serializedObject.FindProperty("showFill");

            showLine = serializedObject.FindProperty("showLine");
            lineThickness = serializedObject.FindProperty("lineThickness");

            showPoints = serializedObject.FindProperty("showPoints");
            pointSize = serializedObject.FindProperty("pointSize");

            showGrid = serializedObject.FindProperty("showGrid");
            gridLineThickness = serializedObject.FindProperty("gridLineThickness");

            axisThickness = serializedObject.FindProperty("axisThickness");

            showLabels = serializedObject.FindProperty("showLabels");
            labelFontSize = serializedObject.FindProperty("labelFontSize");
            labelOffset = serializedObject.FindProperty("labelOffset");
            drawLabelBackground = serializedObject.FindProperty("drawLabelBackground");
            labelBackgroundSprite = serializedObject.FindProperty("labelBackgroundSprite");
            labelBackgroundPPU = serializedObject.FindProperty("labelBackgroundPPU");
            labelPadding = serializedObject.FindProperty("labelPadding");

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

            DrawDataSection();
            DrawSettingsSection();
            DrawLabelSection();
            DrawStyleSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawDataSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref radarTarget.dataFoldout, "Data", EvoEditorGUI.GetIcon("UI_Object")))
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

            if (EvoEditorGUI.DrawFoldout(ref radarTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(scaleMultiplier, "Scale Multiplier", "Scaling offset to make room for labels.", true, true, true);
                    EvoEditorGUI.DrawProperty(angleOffset, "Angle Offset", null, true, true, true);
                    EvoEditorGUI.DrawProperty(axisThickness, "Axis Thickness", null, true, true, true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawToggle(showFill, "Show Fill", null, false, true, true, bypassNormalBackground: true);
                    EvoEditorGUI.EndVerticalBackground(true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawToggle(showLine, "Show Line", null, false, true, true, bypassNormalBackground: true);
                    if (showLine.boolValue)
                    {
                        EvoEditorGUI.BeginContainer(3);
                        EvoEditorGUI.DrawProperty(lineThickness, "Thickness", null, false, true);
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndVerticalBackground(true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawToggle(showPoints, "Show Points", null, false, true, true, bypassNormalBackground: true);
                    if (showPoints.boolValue)
                    {
                        EvoEditorGUI.BeginContainer(3);
                        EvoEditorGUI.DrawProperty(pointSize, "Size", null, false, true);
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndVerticalBackground(true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawToggle(showGrid, "Show Grid", null, false, true, true, bypassNormalBackground: true);
                    if (showGrid.boolValue)
                    {
                        EvoEditorGUI.BeginContainer(3);
                        {
                            EvoEditorGUI.DrawProperty(gridLevels, "Grid Levels", null, true, true);
                            EvoEditorGUI.DrawProperty(gridLineThickness, "Line Thickness", null, false, true);
                        }
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndVerticalBackground();
#if EVO_LOCALIZATION
                    EvoEditorGUI.AddLayoutSpace();
                    Localization.ExternalEditor.DrawLocalizationContainer(serializedObject, radarTarget.gameObject, addSpace: false);
#endif
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawLabelSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref radarTarget.labelFoldout, "Label", EvoEditorGUI.GetIcon("UI_Text")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawToggle(showLabels, "Show Labels", null, true, true, true);
                    if (!showLabels.boolValue) { GUI.enabled = false; }
                    EvoEditorGUI.DrawProperty(labelOffset, "Offset", null, true, true, true);
                    EvoEditorGUI.DrawProperty(labelFontSize, "Font Size", null, true, true, true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawToggle(drawLabelBackground, "Draw Background", null, false, true, true, bypassNormalBackground: true);
                    if (drawLabelBackground.boolValue)
                    {
                        EvoEditorGUI.BeginContainer(3);
                        EvoEditorGUI.DrawProperty(labelBackgroundSprite, "Sprite", null, true, true);
                        EvoEditorGUI.DrawProperty(labelBackgroundPPU, "PPU", null, true, true);
                        EvoEditorGUI.DrawProperty(labelPadding, "Padding", null, false, true);
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndVerticalBackground(false);
                    GUI.enabled = true;
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawStyleSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref radarTarget.stylingFoldout, "Style", EvoEditorGUI.GetIcon("UI_Style")))
            {
                EvoEditorGUI.BeginContainer();
                StylerEditor.DrawStylingSourceSection(serializedObject, RadarChart.GetColorFields(), RadarChart.GetFontFields(), false);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }
    }
}