using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(Notification))]
    public class NotificationEditor : Editor
    {
        // Target
        Notification nTarget;

        // Content
        SerializedProperty icon;
        SerializedProperty title;
        SerializedProperty description;

        // Settings
        SerializedProperty enableStacking;
        SerializedProperty useUnscaledTime;
        SerializedProperty playOnEnable;
        SerializedProperty autoClose;
        SerializedProperty autoCloseDelay;
        SerializedProperty destroyAfter;

        // Animation
        SerializedProperty animationType;
        SerializedProperty animationCurve;
        SerializedProperty duration;
        SerializedProperty scaleFrom;
        SerializedProperty slideOffset;

        // References
        SerializedProperty iconImage;
        SerializedProperty titleText;
        SerializedProperty descriptionText;
        SerializedProperty canvasGroup;

        void OnEnable()
        {
            nTarget = (Notification)target;

            icon = serializedObject.FindProperty("icon");
            title = serializedObject.FindProperty("title");
            description = serializedObject.FindProperty("description");

            enableStacking = serializedObject.FindProperty("enableStacking");
            useUnscaledTime = serializedObject.FindProperty("useUnscaledTime");
            playOnEnable = serializedObject.FindProperty("playOnEnable");
            autoClose = serializedObject.FindProperty("autoClose");
            autoCloseDelay = serializedObject.FindProperty("autoCloseDelay");
            destroyAfter = serializedObject.FindProperty("destroyAfter");

            animationType = serializedObject.FindProperty("animationType");
            animationCurve = serializedObject.FindProperty("animationCurve");
            duration = serializedObject.FindProperty("duration");
            scaleFrom = serializedObject.FindProperty("scaleFrom");
            slideOffset = serializedObject.FindProperty("slideOffset");

            iconImage = serializedObject.FindProperty("iconImage");
            titleText = serializedObject.FindProperty("titleText");
            descriptionText = serializedObject.FindProperty("descriptionText");
            canvasGroup = serializedObject.FindProperty("canvasGroup");

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

            if (EvoEditorGUI.DrawFoldout(ref nTarget.contentFoldout, "Content", EvoEditorGUI.GetIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawProperty(icon, "Icon", null, true, true, true);
                EvoEditorGUI.DrawProperty(title, "Title", null, true, true, true);
                EvoEditorGUI.DrawProperty(description, "Description", null, false, true, true);
#if EVO_LOCALIZATION
                EvoEditorGUI.AddLayoutSpace();
                Localization.ExternalEditor.DrawLocalizationContainer(serializedObject, nTarget.gameObject, addSpace: false);
#endif
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawSettingsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref nTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawToggle(enableStacking, "Enable Stacking", "Check for other notifications under the same parent.", true, true, true);
                    EvoEditorGUI.DrawToggle(useUnscaledTime, "Use Unscaled Time", null, true, true, true);
                    EvoEditorGUI.DrawToggle(playOnEnable, "Play On Enable", null, true, true, true);
                    EvoEditorGUI.DrawToggle(destroyAfter, "Destroy After", "Destroy object after closing.", true, true, true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawToggle(autoClose, "Auto Close", null, false, true, true, bypassNormalBackground: true);
                    if (autoClose.boolValue)
                    {
                        EvoEditorGUI.BeginContainer(3);
                        EvoEditorGUI.DrawProperty(autoCloseDelay, "Delay", null, false, true, false);
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndVerticalBackground(true);

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawProperty(animationType, "Animation Type", null, false, false);
                    EvoEditorGUI.BeginContainer(3);
                    if (animationType.enumValueIndex != 0)
                    {
                        EvoEditorGUI.DrawProperty(animationCurve, "Curve", null, true, true);
                        EvoEditorGUI.DrawProperty(duration, "Duration", null, animationType.enumValueIndex != 1, true);
                    }
                    if (animationType.enumValueIndex == 2) { EvoEditorGUI.DrawProperty(scaleFrom, "Scale From", null, false, true); }
                    else if (animationType.enumValueIndex == 3) { EvoEditorGUI.DrawProperty(slideOffset, "Slide Offset", null, false, true); }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground(true);

                    InteractiveEditor.DrawSoundEffects(serializedObject, Notification.GetSFXFields(), false);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawReferencesSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref nTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.DrawProperty(iconImage, "Icon Image", null, true, true, true);
                EvoEditorGUI.DrawProperty(titleText, "Title Text", null, true, true, true);
                EvoEditorGUI.DrawProperty(descriptionText, "Description Text", null, true, true, true);
                EvoEditorGUI.DrawProperty(canvasGroup, "Canvas Group", null, false, true, true);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }
    }
}