using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(OffScreenIndicator))]
    public class OffScreenIndicatorEditor : Editor
    {
        // Target
        OffScreenIndicator ofiTarget;

        // Customization
        SerializedProperty indicatorPreset;
        SerializedProperty indicatorIcon;

        // Settings
        SerializedProperty trackUIElement;
        SerializedProperty hideWhenOnScreen;
        SerializedProperty checkDistance;
        SerializedProperty borderOffset;
        SerializedProperty fadeDuration;
        SerializedProperty transitionEase;
        SerializedProperty distanceUnit;
        SerializedProperty distanceFormat;
        SerializedProperty distanceSource;
        SerializedProperty enableDistanceFade;
        SerializedProperty fadeStartDistance;
        SerializedProperty fadeEndDistance;

        // References
        SerializedProperty targetTransform;
        SerializedProperty targetCamera;
        SerializedProperty targetCanvas;
        SerializedProperty rectBoundary;

        void OnEnable()
        {
            ofiTarget = (OffScreenIndicator)target;

            indicatorPreset = serializedObject.FindProperty("indicatorPreset");
            indicatorIcon = serializedObject.FindProperty("indicatorIcon");

            trackUIElement = serializedObject.FindProperty("trackUIElement");
            hideWhenOnScreen = serializedObject.FindProperty("hideWhenOnScreen");
            checkDistance = serializedObject.FindProperty("checkDistance");
            borderOffset = serializedObject.FindProperty("borderOffset");
            fadeDuration = serializedObject.FindProperty("fadeDuration");
            transitionEase = serializedObject.FindProperty("transitionEase");
            distanceUnit = serializedObject.FindProperty("distanceUnit");
            distanceFormat = serializedObject.FindProperty("distanceFormat");
            distanceSource = serializedObject.FindProperty("distanceSource");
            enableDistanceFade = serializedObject.FindProperty("enableDistanceFade");
            fadeStartDistance = serializedObject.FindProperty("fadeStartDistance");
            fadeEndDistance = serializedObject.FindProperty("fadeEndDistance");

            targetTransform = serializedObject.FindProperty("targetTransform");
            targetCamera = serializedObject.FindProperty("targetCamera");
            targetCanvas = serializedObject.FindProperty("targetCanvas");
            rectBoundary = serializedObject.FindProperty("rectBoundary");

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

            DrawObjectSection();
            DrawSettingsSection();
            DrawReferencesSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawObjectSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref ofiTarget.objectFoldout, "Indicator", EvoEditorGUI.GetIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawProperty(indicatorPreset, "Indicator Preset", null, true, true, true);
                EvoEditorGUI.DrawProperty(indicatorIcon, "Indicator Icon", null, false, true, true);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawSettingsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref ofiTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();

                EvoEditorGUI.DrawToggle(trackUIElement, "Track UI Element", "Enable if tracking a UI element (RectTransform) instead of a 3D object.", true, true, true);
                EvoEditorGUI.DrawToggle(hideWhenOnScreen, "Hide When On Screen", "Hide the indicator when the target transform is visible on the screen. Disables distance options when enabled.", true, true, true);
                EvoEditorGUI.DrawProperty(checkDistance, "Check Distance (m)", "Check the object within the specified distance in metric (m) units.", true, true, true);
                EvoEditorGUI.DrawProperty(borderOffset, "Border Offset", null, true, true, true);
                EvoEditorGUI.DrawProperty(fadeDuration, "Fade Duration", "Set the fade transition duration.", true, true, true);
                EvoEditorGUI.DrawProperty(transitionEase, "Transition Ease", "Set the transition ease of damping.", true, true, true);

                EvoEditorGUI.BeginVerticalBackground(true);
                EvoEditorGUI.DrawToggle(enableDistanceFade, "Distance Fade", "Controls the visibility of the indicator based on the start and end distances.", false, true, true, bypassNormalBackground: true);
                if (enableDistanceFade.boolValue == true)
                {
                    EvoEditorGUI.BeginContainer(3);
                    EvoEditorGUI.DrawProperty(fadeStartDistance, "Start Distance (m)", "Calculated in metric (m) units.", true, true);
                    EvoEditorGUI.DrawProperty(fadeEndDistance, "End Distance (m)", "Calculated in metric (m) units.", false, true);
                    EvoEditorGUI.EndContainer();
                }
                EvoEditorGUI.EndVerticalBackground(true);

                if (hideWhenOnScreen.boolValue == false) 
                {
                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawProperty(distanceUnit, "Distance Unit", null, false, false, true);
                    if (distanceUnit.enumValueIndex != 0)
                    {
                        EvoEditorGUI.BeginContainer(3);
                        EvoEditorGUI.DrawProperty(distanceFormat, "Distance Format", "{0} = distance, {1} = unit", true, true);
                        EvoEditorGUI.DrawProperty(distanceSource, "Distance Source", "The transform used to calculate the distance to the target transform. If not set, the target camera will be assigned automatically.", false, true);
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndVerticalBackground();
                }

                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawReferencesSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref ofiTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawInfoBox("These references are optional, you don't have to assign them.", revertBackgroundColor: true);
                GUILayout.Space(4);
                EvoEditorGUI.DrawProperty(targetTransform, "Target Transform", "Transform which the indicator will based on.", true, true, true);
                EvoEditorGUI.DrawProperty(targetCamera, "Target Camera", "Camera which the indicator calculations will made on.", true, true, true);
                EvoEditorGUI.DrawProperty(targetCanvas, "Target Canvas", "Canvas which  the indicator will be rendered on.", true, true, true);
                EvoEditorGUI.DrawProperty(rectBoundary, "Rect Boundary", "Use a custom RectTransform boundary instead of screen edges.", false, true, true);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }
    }
}