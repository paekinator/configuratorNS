using Evo.EditorTools;
using UnityEditor;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace Evo.UI
{
    [CustomEditor(typeof(ModalWindow), true)]
    public class ModalWindowEditor : Editor
    {
        // Target
        ModalWindow mwTarget;

        // Content
        SerializedProperty icon;
        SerializedProperty title;
        SerializedProperty description;

        // Settings
        SerializedProperty useUnscaledTime;
        SerializedProperty customContent;
        SerializedProperty closeOnConfirm;
        SerializedProperty closeOnCancel;
        SerializedProperty startBehavior;
        SerializedProperty closeBehavior;
        SerializedProperty navigationMode;

        // Animation Settings
        SerializedProperty animationType;
        SerializedProperty animationDuration;
        SerializedProperty animationCurve;
        SerializedProperty scaleFrom;
        SerializedProperty slideOffset;

        // References
        SerializedProperty contentParent;
        SerializedProperty iconImage;
        SerializedProperty titleText;
        SerializedProperty descriptionText;
        SerializedProperty confirmButton;
        SerializedProperty cancelButton;

        // Events
        SerializedProperty onOpen;
        SerializedProperty onClose;
        SerializedProperty onConfirm;
        SerializedProperty onCancel;

        void OnEnable()
        {
            mwTarget = (ModalWindow)target;

            icon = serializedObject.FindProperty("icon");
            title = serializedObject.FindProperty("title");
            description = serializedObject.FindProperty("description");

            useUnscaledTime = serializedObject.FindProperty("useUnscaledTime");
            customContent = serializedObject.FindProperty("customContent");
            closeOnConfirm = serializedObject.FindProperty("closeOnConfirm");
            closeOnCancel = serializedObject.FindProperty("closeOnCancel");
            startBehavior = serializedObject.FindProperty("startBehavior");
            closeBehavior = serializedObject.FindProperty("closeBehavior");
            navigationMode = serializedObject.FindProperty("navigationMode");

            animationType = serializedObject.FindProperty("animationType");
            animationDuration = serializedObject.FindProperty("animationDuration");
            animationCurve = serializedObject.FindProperty("animationCurve");
            scaleFrom = serializedObject.FindProperty("scaleFrom");
            slideOffset = serializedObject.FindProperty("slideOffset");

            contentParent = serializedObject.FindProperty("contentParent");
            iconImage = serializedObject.FindProperty("iconImage");
            titleText = serializedObject.FindProperty("titleText");
            descriptionText = serializedObject.FindProperty("descriptionText");
            confirmButton = serializedObject.FindProperty("confirmButton");
            cancelButton = serializedObject.FindProperty("cancelButton");

            onOpen = serializedObject.FindProperty("onOpen");
            onClose = serializedObject.FindProperty("onClose");
            onConfirm = serializedObject.FindProperty("onConfirm");
            onCancel = serializedObject.FindProperty("onCancel");

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
            DrawEventsSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawContentSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref mwTarget.objectFoldout, "Content", EvoEditorGUI.GetIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    if (customContent.boolValue)
                    {
                        EvoEditorGUI.DrawInfoBox("'Custom Content' is enabled; content will not be managed by this component.", revertBackgroundColor: true);
                    }
                    else
                    {
                        EvoEditorGUI.BeginVerticalBackground(true);
                        EvoEditorGUI.BeginContainer("Window", 3);
                        {
                            if (iconImage.objectReferenceValue != null) { EvoEditorGUI.DrawProperty(icon, "Icon", null, true); }
                            EvoEditorGUI.DrawProperty(title, "Title", null, true);
                            EvoEditorGUI.DrawProperty(description, "Description", null, false);
                        }
                        EvoEditorGUI.EndContainer();
                        EvoEditorGUI.EndVerticalBackground();

#if EVO_LOCALIZATION
                        EvoEditorGUI.AddLayoutSpace();
                        string[] exProps = new string[] { "titleKey", "descriptionKey" };
                        Localization.ExternalEditor.DrawLocalizationContainer(serializedObject, mwTarget.gameObject, null, exProps, addSpace: false);
#endif

                        if (mwTarget.confirmButton != null || mwTarget.cancelButton != null)
                        {
                            EvoEditorGUI.AddLayoutSpace();
                            EvoEditorGUI.BeginVerticalBackground(true);
                            EvoEditorGUI.BeginContainer("Buttons", 3);
                            {
                                if (mwTarget.confirmButton != null && EvoEditorGUI.DrawButton("Edit Confirm Button"))
                                {
                                    Selection.activeGameObject = mwTarget.confirmButton.gameObject;
                                }
                                GUILayout.Space((mwTarget.confirmButton != null && mwTarget.cancelButton != null) ? 3 : 0);
                                if (mwTarget.cancelButton != null && EvoEditorGUI.DrawButton("Edit Cancel Button"))
                                {
                                    Selection.activeGameObject = mwTarget.cancelButton.gameObject;
                                }
                            }
                            EvoEditorGUI.EndContainer();
                            EvoEditorGUI.EndVerticalBackground();
                        }
                    }
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawSettingsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref mwTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawToggle(useUnscaledTime, "Use Unscaled Time", null, true, true, true);
                    EvoEditorGUI.DrawToggle(customContent, "Custom Content", "Allows manual editing of window content, including icon and text.", true, true, true);
                    EvoEditorGUI.DrawToggle(closeOnConfirm, "Close On Confirm", "Closes the window on confirm button press.", true, true, true);
                    EvoEditorGUI.DrawToggle(closeOnCancel, "Close On Cancel", "Closes the window on cancel button press.", true, true, true);
                    EvoEditorGUI.DrawProperty(startBehavior, "Start Behavior", null, true, true, true);
                    EvoEditorGUI.DrawProperty(closeBehavior, "Close Behavior", null, true, true, true);
                    EvoEditorGUI.DrawProperty(navigationMode, "Navigation Mode", "When set to Focused, UI navigation is restricted to only the Confirm and Cancel buttons while the window remains open.", true, true, true);
                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawProperty(animationType, "Animation Type", addSpace: false, customBackground: false);
                    if (animationType.enumValueIndex != 0)
                    {
                        EvoEditorGUI.BeginContainer(3);
                        EvoEditorGUI.DrawProperty(animationCurve, "Curve", null, true, true);
                        EvoEditorGUI.DrawProperty(animationDuration, "Duration", null, animationType.enumValueIndex != 1, true);
                        if (animationType.enumValueIndex == 2) { EvoEditorGUI.DrawProperty(scaleFrom, "Scale From", null, false, true); }
                        else if (animationType.enumValueIndex == 3) { EvoEditorGUI.DrawProperty(slideOffset, "Slide Offset", null, false, true); }
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

            if (EvoEditorGUI.DrawFoldout(ref mwTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(contentParent, "Content Parent", null, true, true, true);
                    EvoEditorGUI.DrawProperty(iconImage, "Icon Image", null, true, true, true);
                    EvoEditorGUI.DrawProperty(titleText, "Title Text", null, true, true, true);
                    EvoEditorGUI.DrawProperty(descriptionText, "Description Text", null, true, true, true);
                    EvoEditorGUI.DrawProperty(confirmButton, "Confirm Button", null, true, true, true);
                    EvoEditorGUI.DrawProperty(cancelButton, "Cancel Button", null, false, true, true);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawEventsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref mwTarget.eventsFoldout, "Events", EvoEditorGUI.GetIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(onOpen, "On Open", null, true, false);
                    EvoEditorGUI.DrawProperty(onClose, "On Close", null, true, false);
                    EvoEditorGUI.DrawProperty(onConfirm, "On Confirm", null, true, false);
                    EvoEditorGUI.DrawProperty(onCancel, "On Cancel", null, false, false);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }
    }
}