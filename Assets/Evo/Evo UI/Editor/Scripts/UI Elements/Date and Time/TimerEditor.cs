using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(Timer))]
    public class TimerEditor : Editor
    {
        // Target
        Timer tTarget;

        // Timer
        SerializedProperty duration;
        SerializedProperty currentTime;
        SerializedProperty timeMultiplier;
        SerializedProperty textFormat;
        SerializedProperty displayFormat;

        // Settings
        SerializedProperty autoStart;
        SerializedProperty countDown;
        SerializedProperty loop;
        SerializedProperty isVertical;
        SerializedProperty enableSmoothing;
        SerializedProperty updateBarOnSecondsOnly;
        SerializedProperty smoothingDuration;

        // References
        SerializedProperty fillRect;
        SerializedProperty valueText;

        // Events
        SerializedProperty onValueChanged;
        SerializedProperty onTimerComplete;
        SerializedProperty onTimerStart;
        SerializedProperty onTimerStop;
        SerializedProperty onTimerReset;

        void OnEnable()
        {
            tTarget = (Timer)target;

            duration = serializedObject.FindProperty("duration");
            currentTime = serializedObject.FindProperty("currentTime");
            timeMultiplier = serializedObject.FindProperty("timeMultiplier");
            textFormat = serializedObject.FindProperty("textFormat");
            displayFormat = serializedObject.FindProperty("displayFormat");

            autoStart = serializedObject.FindProperty("autoStart");
            countDown = serializedObject.FindProperty("countDown");
            loop = serializedObject.FindProperty("loop");
            isVertical = serializedObject.FindProperty("isVertical");
            enableSmoothing = serializedObject.FindProperty("enableSmoothing");
            updateBarOnSecondsOnly = serializedObject.FindProperty("updateBarOnSecondsOnly");
            smoothingDuration = serializedObject.FindProperty("smoothingDuration");

            fillRect = serializedObject.FindProperty("fillRect");
            valueText = serializedObject.FindProperty("valueText");

            onValueChanged = serializedObject.FindProperty("onValueChanged");
            onTimerComplete = serializedObject.FindProperty("onTimerComplete");
            onTimerStart = serializedObject.FindProperty("onTimerStart");
            onTimerStop = serializedObject.FindProperty("onTimerStop");
            onTimerReset = serializedObject.FindProperty("onTimerReset");

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

            DrawTimerSection();
            DrawSettingsSection();
            DrawReferencesSection();
            DrawEventsSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawTimerSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref tTarget.objectFoldout, "Timer", EvoEditorGUI.GetIcon("UI_Time")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawSlider(currentTime, 0, duration.floatValue, "Current Time", true, true, true);
                    EvoEditorGUI.DrawProperty(duration, "Duration", null, true, true, true);
                    EvoEditorGUI.DrawProperty(timeMultiplier, "Time Multiplier", "Controls the speed of time flow.", true, true, true);
                    EvoEditorGUI.DrawProperty(textFormat, "Text Format", null, true, true, true);
                    EvoEditorGUI.DrawProperty(displayFormat, "Display Format", null, false, true, true);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawSettingsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref tTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawToggle(autoStart, "Auto Start", "Automatically start the timer at runtime.", true, true, true);
                    EvoEditorGUI.DrawToggle(countDown, "Count Down", "Counts down to the 0 when enabled.", true, true, true);
                    EvoEditorGUI.DrawToggle(loop, "Loop", null, true, true, true);
                    EvoEditorGUI.DrawToggle(isVertical, "Is Vertical", null, true, true, true);
                    EvoEditorGUI.DrawToggle(updateBarOnSecondsOnly, "Update Bar On Seconds Only", null, true, true, true);
                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawToggle(enableSmoothing, "Smoothing", addSpace: false, customBackground: true, revertColor: true, bypassNormalBackground: true);
                    if (enableSmoothing.boolValue)
                    {
                        EvoEditorGUI.BeginContainer(3);
                        EvoEditorGUI.DrawProperty(smoothingDuration, "Duration", null, false, true);
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

            if (EvoEditorGUI.DrawFoldout(ref tTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(fillRect, "Fill Rect", null, true, true, true);
                    EvoEditorGUI.DrawProperty(valueText, "Value Text", null, false, true, true);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawEventsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref tTarget.eventsFoldout, "Events", EvoEditorGUI.GetIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(onValueChanged, "On Value Changed", null, true, false);
                    EvoEditorGUI.DrawProperty(onTimerComplete, "On Timer Complete", null, true, false);
                    EvoEditorGUI.DrawProperty(onTimerStart, "On Timer Start", null, true, false);
                    EvoEditorGUI.DrawProperty(onTimerStop, "On Timer Stop", null, true, false);
                    EvoEditorGUI.DrawProperty(onTimerReset, "On Timer Reset", null, false, false);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }
    }
}