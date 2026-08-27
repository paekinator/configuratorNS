using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(Slider))]
    [CanEditMultipleObjects]
    public class SliderEditor : SelectableEditor
    {
        // Target
        Slider sTarget;

        // Object
        SerializedProperty wholeNumbers;
        SerializedProperty value;
        SerializedProperty minValue;
        SerializedProperty maxValue;
        SerializedProperty direction;

        // Settings
        SerializedProperty interactable;
        SerializedProperty invokeAtStart;
        SerializedProperty displayMultiplier;
        SerializedProperty displayFormat;
        SerializedProperty textFormat;

        // Animation
        SerializedProperty animationCurve;
        SerializedProperty highlightedScale;
        SerializedProperty pressedScale;
        SerializedProperty transitionDuration;

        // Navigation
        SerializedProperty navigation;

        // References
        SerializedProperty fillRect;
        SerializedProperty handleRect;
        SerializedProperty valueText;
        SerializedProperty valueInput;
        SerializedProperty highlightedCG;

        // Events
        SerializedProperty onValueChanged;

        protected override void OnEnable()
        {
            sTarget = (Slider)target;
 
            wholeNumbers = serializedObject.FindProperty("m_WholeNumbers");
            value = serializedObject.FindProperty("m_Value");
            minValue = serializedObject.FindProperty("m_MinValue");
            maxValue = serializedObject.FindProperty("m_MaxValue");
            direction = serializedObject.FindProperty("m_Direction");

            interactable = serializedObject.FindProperty("m_Interactable");
            invokeAtStart = serializedObject.FindProperty("invokeAtStart");
            displayMultiplier = serializedObject.FindProperty("displayMultiplier");
            displayFormat = serializedObject.FindProperty("displayFormat");
            textFormat = serializedObject.FindProperty("textFormat");

            animationCurve = serializedObject.FindProperty("animationCurve");
            highlightedScale = serializedObject.FindProperty("highlightedScale");
            pressedScale = serializedObject.FindProperty("pressedScale");
            transitionDuration = serializedObject.FindProperty("transitionDuration");

            navigation = serializedObject.FindProperty("m_Navigation");

            fillRect = serializedObject.FindProperty("m_FillRect");
            handleRect = serializedObject.FindProperty("m_HandleRect");
            valueText = serializedObject.FindProperty("valueText");
            valueInput = serializedObject.FindProperty("valueInput");
            highlightedCG = serializedObject.FindProperty("highlightedCG");

            onValueChanged = serializedObject.FindProperty("m_OnValueChanged");

			// Register this editor for hover repaints
			EvoEditorGUI.RegisterEditor(this);

			// Prepare for UI navigation flow
			UINavigationEditor.PrepareForVisualize(this);
		}

        protected override void OnDisable()
        {
			// Unregister from hover repaints
			EvoEditorGUI.UnregisterEditor(this);

			// Remove from UI navigation flow
			UINavigationEditor.RemoveFromVisualize(this);
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
            DrawNavigationSection();
            DrawReferencesSection();
            DrawEventsSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawObjectSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref sTarget.objectFoldout, "Object", EvoEditorGUI.GetIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();

                EditorGUI.BeginChangeCheck();
                EvoEditorGUI.DrawSlider(value, minValue.floatValue, maxValue.floatValue, "Value", true, true, true);
                if (EditorGUI.EndChangeCheck())
                {
                    // Apply the change before sending the event
                    serializedObject.ApplyModifiedProperties();

                    foreach (var t in targets)
                    {
                        if (t is Slider slider)
                        {
                            slider.onValueChanged?.Invoke(slider.value);
                        }
                    }
                }

                EvoEditorGUI.DrawProperty(minValue, "Min Value", null, true, true, true);
                EvoEditorGUI.DrawProperty(maxValue, "Max Value", null, false, true, true);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawSettingsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref sTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawToggle(interactable, "Interactable", "Sets whether the slider is interactable or not.", true, true, true);
                EvoEditorGUI.DrawToggle(invokeAtStart, "Invoke At Start", "Process OnValueChanged events at runtime start.", true, true, true);
                EvoEditorGUI.DrawToggle(wholeNumbers, "Whole Numbers", null, true, true, true);
                EvoEditorGUI.BeginVerticalBackground(true);
                GUILayout.Space(-6);
                EvoEditorGUI.DrawProperty(direction, "Direction", null, false, false);
                EvoEditorGUI.EndVerticalBackground(true);
                if (valueText.objectReferenceValue != null || valueInput.objectReferenceValue != null) 
                {
                    EvoEditorGUI.AddLayoutSpace();
                    EvoEditorGUI.DrawProperty(displayMultiplier, "Display Multiplier", null, true, true, true);
                    EvoEditorGUI.DrawProperty(displayFormat, "Display Format", null, true, true, true);
                    EvoEditorGUI.DrawProperty(textFormat, "Text Format", null, true, true, true);
                }
                EvoEditorGUI.BeginVerticalBackground(true);
                EvoEditorGUI.BeginContainer("Handle Animation", 3);
                {
                    EvoEditorGUI.DrawProperty(transitionDuration, "Transition Duration", null, true);
                    EvoEditorGUI.DrawProperty(highlightedScale, "Highlighted Scale", null, true);
                    EvoEditorGUI.DrawProperty(pressedScale, "Pressed Scale", null, true);
                    EvoEditorGUI.DrawProperty(animationCurve, "Animation Curve", null, false);
                }
                EvoEditorGUI.EndContainer();
                EvoEditorGUI.EndVerticalBackground();
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawNavigationSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref sTarget.navigationFoldout, "Navigation", EvoEditorGUI.GetIcon("UI_Navigation")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.BeginVerticalBackground(true);
                EvoEditorGUI.DrawProperty(navigation, "Navigation Mode", null, false, false);
                UINavigationEditor.DrawVisualizeButton();
                EvoEditorGUI.EndVerticalBackground();
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawReferencesSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref sTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawProperty(fillRect, "Fill Rect", null, true, true, true);
                EvoEditorGUI.DrawProperty(handleRect, "Handle Rect", null, true, true, true);
                EvoEditorGUI.DrawProperty(valueText, "Value Text", null, true, true, true);
                EvoEditorGUI.DrawProperty(valueInput, "Value Input", null, true, true, true);
                EvoEditorGUI.DrawProperty(highlightedCG, "Highlighted CG", null, false, true, true);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawEventsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref sTarget.eventsFoldout, "Events", EvoEditorGUI.GetIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawProperty(onValueChanged, "On Value Changed", null, false, false);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }
    }
}