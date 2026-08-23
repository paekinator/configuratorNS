using UnityEngine;
using UnityEditor;
using UnityEditor.UI;
using Evo.EditorTools;

namespace Evo.UI
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(Switch))]
    public class SwitchEditor : SelectableEditor
    {
        // Target
        Switch switchTarget;

        // Settings
        SerializedProperty isOn;
        SerializedProperty interactable;
        SerializedProperty invokeAtStart;
        SerializedProperty transitionDuration;
        SerializedProperty handleDuration;
        SerializedProperty handleCurve;

        // References
        SerializedProperty switchHandle;
        SerializedProperty disabledCG;
        SerializedProperty normalCG;
        SerializedProperty highlightedCG;
        SerializedProperty pressedCG;
        SerializedProperty offCG;
        SerializedProperty onCG;

        // Navigation
        SerializedProperty navigation;

        // Events
        SerializedProperty onValueChanged;
        SerializedProperty onSwitchOn;
        SerializedProperty onSwitchOff;

        protected override void OnEnable()
        {
            switchTarget = (Switch)target;

            isOn = serializedObject.FindProperty("isOn");
            interactable = serializedObject.FindProperty("m_Interactable");
            invokeAtStart = serializedObject.FindProperty("invokeAtStart");
            transitionDuration = serializedObject.FindProperty("transitionDuration");
            handleDuration = serializedObject.FindProperty("handleDuration");
            handleCurve = serializedObject.FindProperty("handleCurve");

            switchHandle = serializedObject.FindProperty("switchHandle");
            disabledCG = serializedObject.FindProperty("disabledCG");
            normalCG = serializedObject.FindProperty("normalCG");
            highlightedCG = serializedObject.FindProperty("highlightedCG");
            pressedCG = serializedObject.FindProperty("pressedCG");
            offCG = serializedObject.FindProperty("offCG");
            onCG = serializedObject.FindProperty("onCG");

            navigation = serializedObject.FindProperty("m_Navigation");

            onValueChanged = serializedObject.FindProperty("onValueChanged");
            onSwitchOn = serializedObject.FindProperty("onSwitchOn");
            onSwitchOff = serializedObject.FindProperty("onSwitchOff");

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

            DrawSettingsSection();
            DrawStyleSection();
            DrawNavigationSection();
            DrawReferencesSection();
            DrawEventsSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawSettingsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref switchTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawToggle(isOn, "Is On", "Sets whether the switch is on or not.", true, true, true);
                    EvoEditorGUI.DrawToggle(interactable, "Interactable", "Sets whether the switch is interactable or not.", true, true, true);
                    EvoEditorGUI.DrawToggle(invokeAtStart, "Invoke At Start", "Process OnValueChanged events at runtime start.", true, true, true);
                    EvoEditorGUI.DrawProperty(transitionDuration, "Transition Duration", "Sets the fade transition duration.", true, true, true);
                    EvoEditorGUI.DrawProperty(handleDuration, "Handle Duration", "Sets the handle animation duration.", true, true, true);
                    EvoEditorGUI.DrawProperty(handleCurve, "Handle Curve", "Sets the handle animation curve.", false, true, true);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawStyleSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref switchTarget.styleFoldout, "Style", EvoEditorGUI.GetIcon("UI_Style")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    InteractiveEditor.DrawRippleEffect(serializedObject);
                    InteractiveEditor.DrawTrailEffect(serializedObject);
                    InteractiveEditor.DrawSoundEffects(serializedObject, Interactive.GetSFXFields());
                    InteractiveEditor.DrawCursorType(serializedObject, false);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawNavigationSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref switchTarget.navigationFoldout, "Navigation", EvoEditorGUI.GetIcon("UI_Navigation")))
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

            if (EvoEditorGUI.DrawFoldout(ref switchTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawProperty(switchHandle, "Switch Handle", null, true, true, true);
                EvoEditorGUI.DrawProperty(disabledCG, "Disabled CG", null, true, true, true);
                EvoEditorGUI.DrawProperty(normalCG, "Normal CG", null, true, true, true);
                EvoEditorGUI.DrawProperty(highlightedCG, "Highlighted CG", null, true, true, true);
                EvoEditorGUI.DrawProperty(pressedCG, "Pressed CG", null, true, true, true);
                EvoEditorGUI.DrawArrayProperty(offCG, "Off CG", null, true, true, true);
                EvoEditorGUI.DrawArrayProperty(onCG, "On CG", null, false, true, true);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawEventsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref switchTarget.eventsFoldout, "Events", EvoEditorGUI.GetIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawProperty(onValueChanged, "On Value Changed", null, true, false);
                EvoEditorGUI.DrawProperty(onSwitchOn, "On Switch On", null, true, false);
                EvoEditorGUI.DrawProperty(onSwitchOff, "On Switch Off", null, false, false);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }
    }
}