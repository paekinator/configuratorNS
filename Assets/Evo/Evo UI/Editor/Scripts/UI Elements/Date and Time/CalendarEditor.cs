using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(Calendar))]
    public class CalendarEditor : Editor
    {
        // Target
        Calendar dpTarget;

        // Settings
        SerializedProperty highlightToday;
        SerializedProperty yearRangeFromCurrent;

        // Initial Date
        SerializedProperty initialDateMode;
        SerializedProperty customYear;
        SerializedProperty customMonth;
        SerializedProperty customDay;

        // References
        SerializedProperty dateLabel;
        SerializedProperty monthDropdown;
        SerializedProperty yearDropdown;
        SerializedProperty previousMonthButton;
        SerializedProperty nextMonthButton;
        SerializedProperty daysContainer;
        SerializedProperty dayButtonPrefab;

        // Month Data
        SerializedProperty january;
        SerializedProperty february;
        SerializedProperty march;
        SerializedProperty april;
        SerializedProperty may;
        SerializedProperty june;
        SerializedProperty july;
        SerializedProperty august;
        SerializedProperty september;
        SerializedProperty october;
        SerializedProperty november;
        SerializedProperty december;

        // Events
        SerializedProperty onDateSelected;

        void OnEnable()
        {
            dpTarget = (Calendar)target;

            highlightToday = serializedObject.FindProperty("highlightToday");
            yearRangeFromCurrent = serializedObject.FindProperty("yearRangeFromCurrent");

            initialDateMode = serializedObject.FindProperty("initialDateMode");
            customYear = serializedObject.FindProperty("customYear");
            customMonth = serializedObject.FindProperty("customMonth");
            customDay = serializedObject.FindProperty("customDay");

            dateLabel = serializedObject.FindProperty("dateLabel");
            monthDropdown = serializedObject.FindProperty("monthDropdown");
            yearDropdown = serializedObject.FindProperty("yearDropdown");
            previousMonthButton = serializedObject.FindProperty("previousMonthButton");
            nextMonthButton = serializedObject.FindProperty("nextMonthButton");
            daysContainer = serializedObject.FindProperty("daysContainer");
            dayButtonPrefab = serializedObject.FindProperty("dayButtonPrefab");

            january = serializedObject.FindProperty("january");
            february = serializedObject.FindProperty("february");
            march = serializedObject.FindProperty("march");
            april = serializedObject.FindProperty("april");
            may = serializedObject.FindProperty("may");
            june = serializedObject.FindProperty("june");
            july = serializedObject.FindProperty("july");
            august = serializedObject.FindProperty("august");
            september = serializedObject.FindProperty("september");
            october = serializedObject.FindProperty("october");
            november = serializedObject.FindProperty("november");
            december = serializedObject.FindProperty("december");

            onDateSelected = serializedObject.FindProperty("onDateSelected");

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

            DrawSettingsSection();
            DrawReferencesSection();
            DrawMonthDataSection();
            DrawEventsSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawSettingsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref dpTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawToggle(highlightToday, "Highlight Today", null, true, true, true);
                    EvoEditorGUI.DrawProperty(yearRangeFromCurrent, "Year Range From Current", null, true, true, true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawProperty(initialDateMode, "Initial Date Mode", null, false, false);
                    if (initialDateMode.enumValueIndex == 2)
                    {
                        EvoEditorGUI.BeginContainer(3);
                        EvoEditorGUI.DrawProperty(customYear, "Custom Year", null, true, true);
                        EvoEditorGUI.DrawProperty(customMonth, "Custom Month", null, true, true);
                        EvoEditorGUI.DrawProperty(customDay, "Custom Day", null, false, true);
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndVerticalBackground(false);

#if EVO_LOCALIZATION
                    EvoEditorGUI.AddLayoutSpace();
                    Localization.ExternalEditor.DrawLocalizationContainer(serializedObject, dpTarget.gameObject, addSpace: false);
#endif
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawReferencesSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref dpTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(dateLabel, "Date Label", null, true, true, true);
                    EvoEditorGUI.DrawProperty(monthDropdown, "Month Dropdown", null, true, true, true);
                    EvoEditorGUI.DrawProperty(yearDropdown, "Year Dropdown", null, true, true, true);
                    EvoEditorGUI.DrawProperty(previousMonthButton, "Prev Month Button", null, true, true, true);
                    EvoEditorGUI.DrawProperty(nextMonthButton, "Next Month Button", null, true, true, true);
                    EvoEditorGUI.DrawProperty(daysContainer, "Days Container", null, true, true, true);
                    EvoEditorGUI.DrawProperty(dayButtonPrefab, "Day Button Prefab", null, false, true, true);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawMonthDataSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref dpTarget.monthDataFoldout, "Month Data", EvoEditorGUI.GetIcon("UI_Time")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(january, "January", null, true, true, true, hasFoldout: true);
                    EvoEditorGUI.DrawProperty(february, "February", null, true, true, true, hasFoldout: true);
                    EvoEditorGUI.DrawProperty(march, "March", null, true, true, true, hasFoldout: true);
                    EvoEditorGUI.DrawProperty(april, "April", null, true, true, true, hasFoldout: true);
                    EvoEditorGUI.DrawProperty(may, "May", null, true, true, true, hasFoldout: true);
                    EvoEditorGUI.DrawProperty(june, "June", null, true, true, true, hasFoldout: true);
                    EvoEditorGUI.DrawProperty(july, "July", null, true, true, true, hasFoldout: true);
                    EvoEditorGUI.DrawProperty(august, "August", null, true, true, true, hasFoldout: true);
                    EvoEditorGUI.DrawProperty(september, "September", null, true, true, true, hasFoldout: true);
                    EvoEditorGUI.DrawProperty(october, "October", null, true, true, true, hasFoldout: true);
                    EvoEditorGUI.DrawProperty(november, "November", null, true, true, true, hasFoldout: true);
                    EvoEditorGUI.DrawProperty(december, "December", null, false, true, true, hasFoldout: true);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawEventsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref dpTarget.eventsFoldout, "Events", EvoEditorGUI.GetIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawProperty(onDateSelected, "On Date Selected", null, false, false);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }
    }
}