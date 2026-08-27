using UnityEngine;
using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI
{
	[CustomEditor(typeof(PieChart))]
	public class PieChartEditor : Editor
	{
		// Target
		PieChart pieTarget;

		// Chart Data
		SerializedProperty dataPoints;

		// Chart Settings
		SerializedProperty innerRadius;
		SerializedProperty startAngle;
		SerializedProperty segments;
        SerializedProperty enableAntiAliasing;
        SerializedProperty antiAliasingWidth;

        // Border Settings
        SerializedProperty drawBorder;
		SerializedProperty borderWidth;

		// Label Settings
		SerializedProperty showLabels;
		SerializedProperty showPercentages;
		SerializedProperty labelFontSize;
		SerializedProperty drawLabelBackground;
		SerializedProperty labelBackgroundSprite;
        SerializedProperty labelBackgroundPPU;
        SerializedProperty labelPadding;

		// Legend Settings
		SerializedProperty showLegend;
		SerializedProperty legendContainer;
		SerializedProperty legendItemHeight;
		SerializedProperty legendColorBoxSize;
		SerializedProperty legendColorBoxSprite;
		SerializedProperty legendFontSize;

        void OnEnable()
		{
			pieTarget = (PieChart)target;

			dataPoints = serializedObject.FindProperty("dataPoints");

			innerRadius = serializedObject.FindProperty("innerRadius");
			startAngle = serializedObject.FindProperty("startAngle");
			segments = serializedObject.FindProperty("segments");
            enableAntiAliasing = serializedObject.FindProperty("enableAntiAliasing");
            antiAliasingWidth = serializedObject.FindProperty("antiAliasingWidth");

            drawBorder = serializedObject.FindProperty("drawBorder");
			borderWidth = serializedObject.FindProperty("borderWidth");

			showLabels = serializedObject.FindProperty("showLabels");
			showPercentages = serializedObject.FindProperty("showPercentages");
			labelFontSize = serializedObject.FindProperty("labelFontSize");
			drawLabelBackground = serializedObject.FindProperty("drawLabelBackground");
			labelBackgroundSprite = serializedObject.FindProperty("labelBackgroundSprite");
            labelBackgroundPPU = serializedObject.FindProperty("labelBackgroundPPU");
            labelPadding = serializedObject.FindProperty("labelPadding");

			showLegend = serializedObject.FindProperty("showLegend");
			legendContainer = serializedObject.FindProperty("legendContainer");
			legendItemHeight = serializedObject.FindProperty("legendItemHeight");
			legendColorBoxSize = serializedObject.FindProperty("legendColorBoxSize");
			legendColorBoxSprite = serializedObject.FindProperty("legendColorBoxSprite");
			legendFontSize = serializedObject.FindProperty("legendFontSize");

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
			DrawLegendSection();
			DrawStyleSection();

			EvoEditorGUI.EndCenteredInspector();
			serializedObject.ApplyModifiedProperties();
		}

		void DrawDataSection()
		{
			EvoEditorGUI.BeginVerticalBackground();

			if (EvoEditorGUI.DrawFoldout(ref pieTarget.dataFoldout, "Data", EvoEditorGUI.GetIcon("UI_Object")))
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

			if (EvoEditorGUI.DrawFoldout(ref pieTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
			{
				EvoEditorGUI.BeginContainer();
				{
					EvoEditorGUI.DrawProperty(innerRadius, "Inner Radius", null, true, true, true);
					EvoEditorGUI.DrawProperty(startAngle, "Start Angle", null, true, true, true);
					EvoEditorGUI.DrawProperty(segments, "Segments", "Set the smoothness of the chart edges.", true, true, true);
              
					EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawToggle(enableAntiAliasing, "Anti-Aliasing", null, false, true, true, bypassNormalBackground: true);
                    if (enableAntiAliasing.boolValue)
                    {
                        EvoEditorGUI.BeginContainer(3);
                        EvoEditorGUI.DrawProperty(antiAliasingWidth, "AA Width", null, false, true);
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndVerticalBackground(true);

                    EvoEditorGUI.BeginVerticalBackground(true);
					EvoEditorGUI.DrawToggle(drawBorder, "Draw Border", null, false, true, true, bypassNormalBackground: true);
					if (drawBorder.boolValue)
					{
						EvoEditorGUI.BeginContainer(3);
						EvoEditorGUI.DrawProperty(borderWidth, "Border Width", null, false, true);
						EvoEditorGUI.EndContainer();
					}
					EvoEditorGUI.EndVerticalBackground();
				}
				EvoEditorGUI.EndContainer();
			}

			EvoEditorGUI.EndVerticalBackground();
			EvoEditorGUI.AddFoldoutSpace();
		}

		void DrawLabelSection()
		{
			EvoEditorGUI.BeginVerticalBackground();

			if (EvoEditorGUI.DrawFoldout(ref pieTarget.labelFoldout, "Label", EvoEditorGUI.GetIcon("UI_Text")))
			{
				EvoEditorGUI.BeginContainer();
				{
					EvoEditorGUI.DrawToggle(showLabels, "Show Labels", null, true, true, true);
					if (!showLabels.boolValue) { GUI.enabled = false; }
					EvoEditorGUI.DrawToggle(showPercentages, "Show Percentages", null, true, true, true);
					EvoEditorGUI.DrawProperty(labelFontSize, "Font Size", null, true, true, true);

					EvoEditorGUI.BeginVerticalBackground(true);
					EvoEditorGUI.DrawToggle(drawLabelBackground, "Draw Background", null, false, true, true, bypassNormalBackground: true);
					if (drawLabelBackground.boolValue)
					{
						EvoEditorGUI.BeginContainer(3);
						{
							EvoEditorGUI.DrawProperty(labelBackgroundSprite, "Sprite", null, true, true);
                            EvoEditorGUI.DrawProperty(labelBackgroundPPU, "PPU", null, true, true);
                            EvoEditorGUI.DrawProperty(labelPadding, "Padding", null, false, true);
						}
						EvoEditorGUI.EndContainer();
					}
					EvoEditorGUI.EndVerticalBackground();
					GUI.enabled = true;
				}
				EvoEditorGUI.EndContainer();
			}

			EvoEditorGUI.EndVerticalBackground();
			EvoEditorGUI.AddFoldoutSpace();
		}

		void DrawLegendSection()
		{
			EvoEditorGUI.BeginVerticalBackground();

			if (EvoEditorGUI.DrawFoldout(ref pieTarget.legendFoldout, "Legend", EvoEditorGUI.GetIcon("UI_Event")))
			{
				EvoEditorGUI.BeginContainer();
				{
					EvoEditorGUI.DrawToggle(showLegend, "Show Legend", null, true, true, true);
					if (!showLegend.boolValue) { GUI.enabled = false; }
					EvoEditorGUI.DrawProperty(legendContainer, "Container", null, true, true, true);
					EvoEditorGUI.DrawProperty(legendItemHeight, "Item Height", null, true, true, true);
					EvoEditorGUI.DrawProperty(legendColorBoxSize, "Color Box Size", null, true, true, true);
					EvoEditorGUI.DrawProperty(legendColorBoxSprite, "Color Box Sprite", null, true, true, true);
					EvoEditorGUI.DrawProperty(legendFontSize, "Font Size", null, false, true, true);
#if EVO_LOCALIZATION
                    EvoEditorGUI.AddLayoutSpace();
                    Localization.ExternalEditor.DrawLocalizationContainer(serializedObject, pieTarget.gameObject, addSpace: false);
#endif
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

			if (EvoEditorGUI.DrawFoldout(ref pieTarget.styleFoldout, "Style", EvoEditorGUI.GetIcon("UI_Style")))
			{
				EvoEditorGUI.BeginContainer();
				{
                    StylerEditor.DrawStylingSourceSection(serializedObject, PieChart.GetColorFields(), PieChart.GetFontFields());
                }
				EvoEditorGUI.EndContainer();
			}

			EvoEditorGUI.EndVerticalBackground();
		}
	}
}