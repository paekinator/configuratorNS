using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(Button))]
    public class ButtonEditor : SelectableEditor
    {
        // Target
        Button btnTarget;

        // Icon
        SerializedProperty enableIcon;
        SerializedProperty icon;
        SerializedProperty iconSize;

        // Text
        SerializedProperty enableText;
        SerializedProperty text;
        SerializedProperty textSize;

        // Layout
        SerializedProperty dynamicScale;
        SerializedProperty reverseArrangement;
        SerializedProperty spacing;
        SerializedProperty padding;

        // References
        SerializedProperty disabledCG;
        SerializedProperty normalCG;
        SerializedProperty highlightedCG;
        SerializedProperty pressedCG;
        SerializedProperty selectedCG;
        SerializedProperty textObject;
        SerializedProperty textContainer;
        SerializedProperty imageObject;
        SerializedProperty iconElement;
        SerializedProperty contentFitter;
        SerializedProperty contentLayout;

        // Settings
        SerializedProperty interactable;
        SerializedProperty customContent;
        SerializedProperty allowDoubleClick;
        SerializedProperty doubleClickDuration;
        SerializedProperty transitionDuration;
        SerializedProperty interactionState;

        // Navigation
        SerializedProperty navigation;

        // Events
        SerializedProperty onClick;
        SerializedProperty onDoubleClick;
        SerializedProperty onPointerEnter;
        SerializedProperty onPointerExit;

        protected override void OnEnable()
        {
            btnTarget = (Button)target;

            enableIcon = serializedObject.FindProperty("enableIcon");
            icon = serializedObject.FindProperty("icon");
            iconSize = serializedObject.FindProperty("iconSize");

            enableText = serializedObject.FindProperty("enableText");
            text = serializedObject.FindProperty("text");
            textSize = serializedObject.FindProperty("textSize");

            dynamicScale = serializedObject.FindProperty("dynamicScale");
            reverseArrangement = serializedObject.FindProperty("reverseArrangement");
            spacing = serializedObject.FindProperty("spacing");
            padding = serializedObject.FindProperty("padding");

            disabledCG = serializedObject.FindProperty("disabledCG");
            normalCG = serializedObject.FindProperty("normalCG");
            highlightedCG = serializedObject.FindProperty("highlightedCG");
            pressedCG = serializedObject.FindProperty("pressedCG");
            selectedCG = serializedObject.FindProperty("selectedCG");
            contentFitter = serializedObject.FindProperty("contentFitter");
            textObject = serializedObject.FindProperty("textObject");
            textContainer = serializedObject.FindProperty("textContainer");
            imageObject = serializedObject.FindProperty("imageObject");
            iconElement = serializedObject.FindProperty("iconElement");
            contentLayout = serializedObject.FindProperty("contentLayout");

            interactable = serializedObject.FindProperty("m_Interactable");
            customContent = serializedObject.FindProperty("customContent");
            allowDoubleClick = serializedObject.FindProperty("allowDoubleClick");
            transitionDuration = serializedObject.FindProperty("transitionDuration");
            doubleClickDuration = serializedObject.FindProperty("doubleClickDuration");
            interactionState = serializedObject.FindProperty("interactionState");

            navigation = serializedObject.FindProperty("m_Navigation");

            onClick = serializedObject.FindProperty("onClick");
            onDoubleClick = serializedObject.FindProperty("onDoubleClick");
            onPointerEnter = serializedObject.FindProperty("onPointerEnter");
            onPointerExit = serializedObject.FindProperty("onPointerExit");

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
            DrawStyleSection();
            DrawNavigationSection();
            DrawReferencesSection();
            DrawEventsSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawObjectSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref btnTarget.objectFoldout, "Content", EvoEditorGUI.GetIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    if (customContent.boolValue)
                    {
                        EvoEditorGUI.DrawInfoBox("'Custom Content' is enabled; content will not be managed by this component.", revertBackgroundColor: true);                    
                    }
                    else
                    {
                        if (imageObject.objectReferenceValue != null)
                        {
                            EvoEditorGUI.BeginVerticalBackground(true);
                            {
                                EvoEditorGUI.DrawToggle(enableIcon, "Icon", null, false, revertColor: true, bypassNormalBackground: true);
                                if (enableIcon.boolValue)
                                {
                                    EvoEditorGUI.BeginContainer(3);
                                    EvoEditorGUI.DrawProperty(icon, "Source Sprite", null, false, true);
                                    if (iconElement.objectReferenceValue != null)
                                    {
                                        EvoEditorGUI.AddLayoutSpace();
                                        EvoEditorGUI.DrawProperty(iconSize, "Icon Size", null, false, true); 
                                    }
                                    EvoEditorGUI.EndContainer();
                                }
                            }
                            EvoEditorGUI.EndVerticalBackground();
                        }

                        if (textObject.objectReferenceValue != null)
                        {
                            EvoEditorGUI.AddLayoutSpace();
                            EvoEditorGUI.BeginVerticalBackground(true);
                            {
                                EvoEditorGUI.DrawToggle(enableText, "Text", null, false, revertColor: true, bypassNormalBackground: true);
                                if (enableText.boolValue)
                                {
                                    EvoEditorGUI.BeginContainer(3);
                                    EvoEditorGUI.DrawProperty(text, "Button Text", null, true, true);
                                    EvoEditorGUI.DrawProperty(textSize, "Text Size", null, false, true);
#if EVO_LOCALIZATION
                                    if (btnTarget.enableLocalization && btnTarget.localizedObject)
                                    {
                                        EvoEditorGUI.AddLayoutSpace();
                                        EvoEditorGUI.DrawInfoBox("Localization is enabled; text will be managed through Localized Object.");
                                        GUILayout.Space(1);
                                    }
#endif
                                    EvoEditorGUI.EndContainer();
                                }
                            }
                            EvoEditorGUI.EndVerticalBackground();

#if EVO_LOCALIZATION
                            EvoEditorGUI.AddLayoutSpace();
                            Localization.ExternalEditor.DrawLocalizationContainer(serializedObject, btnTarget.gameObject, btnTarget.textObject, addSpace: false);
#endif
                        }
                    }

                    if (contentLayout.objectReferenceValue != null)
                    {
                        EvoEditorGUI.AddLayoutSpace();
                        EvoEditorGUI.BeginVerticalBackground(true);
                        EvoEditorGUI.BeginContainer("Layout", 3);
                        {
                            EvoEditorGUI.DrawToggle(dynamicScale, "Dynamic Scale", dynamicScale.tooltip, true, true);
                            if (enableIcon.boolValue && enableText.boolValue) { EvoEditorGUI.DrawToggle(reverseArrangement, "Reverse Arrangement", "Change the content arrangement.", true, true); }
                            EvoEditorGUI.DrawProperty(spacing, "Spacing", "Add spacing between the content.", true, true);
                            EvoEditorGUI.DrawProperty(padding, "Padding", "Set the content padding.", false, true, false, hasFoldout: true);
                        }
                        EvoEditorGUI.EndContainer();
                        EvoEditorGUI.EndVerticalBackground();
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

            if (EvoEditorGUI.DrawFoldout(ref btnTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawToggle(interactable, "Interactable", "Sets whether the button is interactable or not.", true, true, true);
                    EvoEditorGUI.DrawToggle(customContent, "Custom Content", customContent.tooltip, true, true, true);
                    EvoEditorGUI.DrawToggle(allowDoubleClick, "Allow Double Click", null, true, true, true);
                    if (allowDoubleClick.boolValue) { EvoEditorGUI.DrawProperty(doubleClickDuration, "Double Click Duration", null, true, true, true); }
                    EvoEditorGUI.DrawProperty(transitionDuration, "Transition Duration", "Sets the fade transition duration.", true, true, true);
                    EvoEditorGUI.DrawProperty(interactionState, "Interaction State", null, false, true, true);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawNavigationSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref btnTarget.navigationFoldout, "Navigation", EvoEditorGUI.GetIcon("UI_Navigation")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawProperty(navigation, "Navigation Mode", null, false, false);
                    UINavigationEditor.DrawVisualizeButton();
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

            if (EvoEditorGUI.DrawFoldout(ref btnTarget.styleFoldout, "Style", EvoEditorGUI.GetIcon("UI_Style")))
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

        void DrawReferencesSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref btnTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(imageObject, "Image Object", null, true, true, true);
                    EvoEditorGUI.DrawProperty(iconElement, "Icon Element", null, true, true, true);
                    EvoEditorGUI.DrawProperty(textObject, "Text Object", null, true, true, true);
                    EvoEditorGUI.DrawProperty(textContainer, "Text Container", null, true, true, true);
                    EvoEditorGUI.DrawProperty(contentFitter, "Content Fitter", null, true, true, true);
                    EvoEditorGUI.DrawProperty(contentLayout, "Content Layout", null, true, true, true);
                    EvoEditorGUI.DrawProperty(disabledCG, "Disabled CG", null, true, true, true);
                    EvoEditorGUI.DrawProperty(normalCG, "Normal CG", null, true, true, true);
                    EvoEditorGUI.DrawProperty(highlightedCG, "Highlighted CG", null, true, true, true);
                    EvoEditorGUI.DrawProperty(pressedCG, "Pressed CG", null, true, true, true);
                    EvoEditorGUI.DrawProperty(selectedCG, "Selected CG", null, false, true, true);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawEventsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref btnTarget.eventsFoldout, "Events", EvoEditorGUI.GetIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(onClick, "On Click", null, true, false);
                    if (btnTarget.allowDoubleClick) { EvoEditorGUI.DrawProperty(onDoubleClick, "On Double Click", null, true, false); }
                    EvoEditorGUI.DrawProperty(onPointerEnter, "On Pointer Enter", null, true, false);
                    EvoEditorGUI.DrawProperty(onPointerExit, "On Pointer Leave", null, false, false);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }
    }
}