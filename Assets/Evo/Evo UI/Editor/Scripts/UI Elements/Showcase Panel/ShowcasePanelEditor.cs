using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(ShowcasePanel))]
    public class ShowcasePanelEditor : Editor
    {
        // Target
        ShowcasePanel spTarget;

        // Content
        SerializedProperty currentIndex;
        SerializedProperty items;

        // Settings
        SerializedProperty useUnscaledTime;
        SerializedProperty setWithTimer;
        SerializedProperty timer;
        SerializedProperty animationDuration;
        SerializedProperty slideOffset;

#if EVO_LOCALIZATION
        // Localization
        SerializedProperty enableLocalization;
        SerializedProperty localizedObject;
#endif

        // References
        SerializedProperty buttonParent;
        SerializedProperty buttonPreset;
        SerializedProperty textDisplay;
        SerializedProperty backgroundImage;
        SerializedProperty backgroundShadow;

        void OnEnable()
        {
            spTarget = (ShowcasePanel)target;

            currentIndex = serializedObject.FindProperty("currentIndex");
            items = serializedObject.FindProperty("items");

            useUnscaledTime = serializedObject.FindProperty("useUnscaledTime");
            setWithTimer = serializedObject.FindProperty("setWithTimer");
            timer = serializedObject.FindProperty("timer");
            animationDuration = serializedObject.FindProperty("animationDuration");
            slideOffset = serializedObject.FindProperty("slideOffset");

#if EVO_LOCALIZATION
            enableLocalization = serializedObject.FindProperty("enableLocalization");
            localizedObject = serializedObject.FindProperty("localizedObject");
#endif

            buttonParent = serializedObject.FindProperty("buttonParent");
            buttonPreset = serializedObject.FindProperty("buttonPreset");
            textDisplay = serializedObject.FindProperty("textDisplay");
            backgroundImage = serializedObject.FindProperty("backgroundImage");
            backgroundShadow = serializedObject.FindProperty("backgroundShadow");

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

            DrawContentSection();
            DrawSettingsSection();
            DrawReferencesSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawContentSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref spTarget.objectFoldout, "Content", EvoEditorGUI.GetIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        if (spTarget.currentIndex < 0 || spTarget.items.Count <= 0) { EvoEditorGUI.DrawLabel(" Selected Item: None"); }
                        else if (spTarget.items.Count > 0 && !string.IsNullOrEmpty(spTarget.items[spTarget.currentIndex].title)) { EvoEditorGUI.DrawLabel($" Selected Item: {spTarget.items[spTarget.currentIndex].title}"); }
                        else if (spTarget.items.Count > 0 && string.IsNullOrEmpty(spTarget.items[spTarget.currentIndex].title)) { EvoEditorGUI.DrawLabel($" Selected Item: #{spTarget.currentIndex} (no label)"); }
                        EditorGUI.BeginChangeCheck();
                        EvoEditorGUI.DrawSlider(currentIndex, 0, spTarget.items.Count > 0 ? spTarget.items.Count - 1 : 0, "Index:", false, false, labelWidth: 40);
                        if (EditorGUI.EndChangeCheck()) { serializedObject.ApplyModifiedProperties(); }
                    }
                    EvoEditorGUI.EndHorizontalBackground();
                    EvoEditorGUI.DrawArrayProperty(items, "Items", null, true, true, true);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawSettingsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref spTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawToggle(useUnscaledTime, "Use Unscaled Time", null, true, true, true);
                    EvoEditorGUI.BeginVerticalBackground(true);
                    {
                        EvoEditorGUI.DrawToggle(setWithTimer, "Set With Timer", addSpace: false, customBackground: true, revertColor: true, bypassNormalBackground: true);
                        if (setWithTimer.boolValue)
                        {
                            EvoEditorGUI.BeginContainer(3);
                            EvoEditorGUI.DrawProperty(timer, "Timer", null, false, true);
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndVerticalBackground(true);
#if EVO_LOCALIZATION
                    EvoEditorGUI.AddLayoutSpace();
                    Localization.ExternalEditor.DrawLocalizationContainer(serializedObject, spTarget.gameObject, addSpace: true);
#endif
                    EvoEditorGUI.DrawProperty(animationDuration, "Animation Duration", null, true, true, true);
                    EvoEditorGUI.DrawProperty(slideOffset, "Slide Offset", null, false, true, true);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawReferencesSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref spTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawProperty(buttonParent, "Button Parent", null, true, true, true);
                EvoEditorGUI.DrawProperty(buttonPreset, "Button Preset", null, true, true, true);
                EvoEditorGUI.DrawProperty(textDisplay, "Text Display", null, true, true, true);
                EvoEditorGUI.DrawProperty(backgroundImage, "Background Image", null, true, true, true);
                EvoEditorGUI.DrawProperty(backgroundShadow, "Background Shadow", null, false, true, true);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }
    }
}