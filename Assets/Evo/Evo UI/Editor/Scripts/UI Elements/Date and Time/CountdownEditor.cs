using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(Countdown))]
    public class CountdownEditor : Editor
    {
        // Target
        Countdown cTarget;

        // Timer
        SerializedProperty useSystemClock;
        SerializedProperty hours;
        SerializedProperty minutes;
        SerializedProperty seconds;
        SerializedProperty separator;

        // Settings
        SerializedProperty autoStart;
        SerializedProperty showHours;
        SerializedProperty showMinutes;
        SerializedProperty showSeconds;
        SerializedProperty useUnscaledTime;

        // Spacing
        SerializedProperty useDynamicSpacing;
        SerializedProperty separatorSpacing;
        SerializedProperty digitSpacing;

        // Animation
        SerializedProperty animationDuration;
        SerializedProperty slideDistance;
        SerializedProperty animationCurve;

        // Styling
        SerializedProperty stylingSource;
        SerializedProperty stylerPreset;
        SerializedProperty colorMapping;
        SerializedProperty fontMapping;

        // Font Settings
        SerializedProperty fontSize;
        SerializedProperty fontStyle;

        // Events
        SerializedProperty onTimerComplete;
        SerializedProperty onTimeChanged;

        void OnEnable()
        {
            cTarget = (Countdown)target;

            useSystemClock = serializedObject.FindProperty("useSystemClock");
            hours = serializedObject.FindProperty("hours");
            minutes = serializedObject.FindProperty("minutes");
            seconds = serializedObject.FindProperty("seconds");
            separator = serializedObject.FindProperty("separator");

            autoStart = serializedObject.FindProperty("autoStart");
            showHours = serializedObject.FindProperty("showHours");
            showMinutes = serializedObject.FindProperty("showMinutes");
            showSeconds = serializedObject.FindProperty("showSeconds");
            useUnscaledTime = serializedObject.FindProperty("useUnscaledTime");

            useDynamicSpacing = serializedObject.FindProperty("useDynamicSpacing");
            separatorSpacing = serializedObject.FindProperty("separatorSpacing");
            digitSpacing = serializedObject.FindProperty("digitSpacing");

            animationDuration = serializedObject.FindProperty("animationDuration");
            slideDistance = serializedObject.FindProperty("slideDistance");
            animationCurve = serializedObject.FindProperty("animationCurve");

            // Styling properties
            stylingSource = serializedObject.FindProperty("stylingSource");
            stylerPreset = serializedObject.FindProperty("stylerPreset");
            colorMapping = serializedObject.FindProperty("colorMapping");
            fontMapping = serializedObject.FindProperty("fontMapping");

            fontSize = serializedObject.FindProperty("fontSize");
            fontStyle = serializedObject.FindProperty("fontStyle");

            onTimerComplete = serializedObject.FindProperty("onTimerComplete");
            onTimeChanged = serializedObject.FindProperty("onTimeChanged");

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
            DrawStyleSection();
            DrawEventsSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawTimerSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref cTarget.objectFoldout, "Timer", EvoEditorGUI.GetIcon("UI_Time")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawToggle(useSystemClock, "Use System Clock", "Display current system time instead of a countdown.", true, true, true);

                    bool isRealTime = useSystemClock.boolValue;
                    if (isRealTime) { GUI.enabled = false; }

                    EvoEditorGUI.DrawProperty(hours, "Hours", null, true, true, true);
                    EvoEditorGUI.DrawProperty(minutes, "Minutes", null, true, true, true);
                    EvoEditorGUI.DrawProperty(seconds, "Seconds", null, true, true, true);

                    if (isRealTime) { GUI.enabled = true; }

                    EvoEditorGUI.DrawProperty(separator, "Separator", null, false, true, true);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawSettingsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref cTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.BeginContainer("Core", 3);
                    {
                        EvoEditorGUI.DrawToggle(autoStart, "Auto Start", null, true);
                        EvoEditorGUI.DrawToggle(useUnscaledTime, "Use Unscaled Time", null, false);
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground(true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.BeginContainer("Display", 3);
                    {
                        EvoEditorGUI.DrawToggle(showHours, "Show Hours", null, true);
                        EvoEditorGUI.DrawToggle(showMinutes, "Show Minutes", null, true);
                        EvoEditorGUI.DrawToggle(showSeconds, "Show Seconds", null, false);
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground(true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.BeginContainer("Spacing", 3);
                    {
                        EvoEditorGUI.DrawToggle(useDynamicSpacing, "Use Dynamic Spacing", "Change the spacing based on this transform's width.", true);
                        GUI.enabled = !useDynamicSpacing.boolValue;
                        EvoEditorGUI.DrawProperty(separatorSpacing, "Separator Spacing", null, true);
                        EvoEditorGUI.DrawProperty(digitSpacing, "Digit Spacing", null, false);
                        GUI.enabled = true;
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground(true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.BeginContainer("Animation", 3);
                    {
                        EvoEditorGUI.DrawProperty(animationDuration, "Duration", null, true);
                        EvoEditorGUI.DrawProperty(slideDistance, "Slide Distance", null, true);
                        EvoEditorGUI.DrawProperty(animationCurve, "Curve", null, false);
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground();
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawStyleSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref cTarget.stylingFoldout, "Style", EvoEditorGUI.GetIcon("UI_Style")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    StylerEditor.DrawStylingSourceSection(serializedObject, Countdown.GetColorFields(), Countdown.GetFontFields());
                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.BeginContainer("Others", 3);
                    {
                        EvoEditorGUI.DrawProperty(fontStyle, "Font Style", null, true);
                        EvoEditorGUI.DrawProperty(fontSize, "Font Size", null, false);
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground();
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawEventsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref cTarget.eventsFoldout, "Events", EvoEditorGUI.GetIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(onTimerComplete, "On Timer Complete", null, true, false);
                    EvoEditorGUI.DrawProperty(onTimeChanged, "On Time Changed", null, false, false);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }
    }
}