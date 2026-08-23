using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(StylerObject))]
    public class StylerObjectEditor : Editor
    {
        // Target
        StylerObject soTarget;

        // Properties
        SerializedProperty presetSource;
        SerializedProperty preset;
        SerializedProperty targetGraphic;
        SerializedProperty targetText;
        SerializedProperty targetGradient;
        SerializedProperty objectType;
        SerializedProperty colorID;
        SerializedProperty fontID;
        SerializedProperty textStyleID;
        SerializedProperty spriteID;
        SerializedProperty gradientID;
        SerializedProperty useCustomColor;
        SerializedProperty overrideAlpha;
        SerializedProperty alphaOverride;

        // Interaction
        SerializedProperty enableInteraction;
        SerializedProperty interactableObject;

        // Color Interaction
        SerializedProperty enableColorInteraction;
        SerializedProperty disabledColor;
        SerializedProperty normalColor;
        SerializedProperty highlightedColor;
        SerializedProperty pressedColor;
        SerializedProperty selectedColor;

        // Font Interaction
        SerializedProperty enableFontInteraction;
        SerializedProperty disabledFont;
        SerializedProperty normalFont;
        SerializedProperty highlightedFont;
        SerializedProperty pressedFont;
        SerializedProperty selectedFont;

        void OnEnable()
        {
            soTarget = (StylerObject)target;

            presetSource = serializedObject.FindProperty("presetSource");
            preset = serializedObject.FindProperty("preset");
            targetGraphic = serializedObject.FindProperty("targetGraphic");
            targetText = serializedObject.FindProperty("targetText");
            targetGradient = serializedObject.FindProperty("targetGradient");
            objectType = serializedObject.FindProperty("objectType");
            colorID = serializedObject.FindProperty("colorID");
            fontID = serializedObject.FindProperty("fontID");
            textStyleID = serializedObject.FindProperty("textStyleID");
            spriteID = serializedObject.FindProperty("spriteID");
            gradientID = serializedObject.FindProperty("gradientID");
            useCustomColor = serializedObject.FindProperty("useCustomColor");
            overrideAlpha = serializedObject.FindProperty("overrideAlpha");
            alphaOverride = serializedObject.FindProperty("alphaOverride");

            enableInteraction = serializedObject.FindProperty("enableInteraction");
            interactableObject = serializedObject.FindProperty("interactableObject");

            enableColorInteraction = serializedObject.FindProperty("enableColorInteraction");
            disabledColor = serializedObject.FindProperty("disabledColor");
            normalColor = serializedObject.FindProperty("normalColor");
            highlightedColor = serializedObject.FindProperty("highlightedColor");
            pressedColor = serializedObject.FindProperty("pressedColor");
            selectedColor = serializedObject.FindProperty("selectedColor");

            enableFontInteraction = serializedObject.FindProperty("enableFontInteraction");
            disabledFont = serializedObject.FindProperty("disabledFont");
            normalFont = serializedObject.FindProperty("normalFont");
            highlightedFont = serializedObject.FindProperty("highlightedFont");
            pressedFont = serializedObject.FindProperty("pressedFont");
            selectedFont = serializedObject.FindProperty("selectedFont");

            EvoEditorGUI.RegisterEditor(this);
        }

        void OnDisable() => EvoEditorGUI.UnregisterEditor(this);

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

            DrawReferences();
            DrawSettings();
            DrawInteraction();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawReferences()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref soTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                {      
                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawProperty(presetSource, "Preset Source", null, false, customBackground: false);
                    EvoEditorGUI.BeginContainer(3);
                    {
                        GUI.enabled = presetSource.enumValueIndex == (int)StylerObject.PresetSource.UserDefined;
                        EvoEditorGUI.DrawProperty(preset, "Styler Preset", null, false, true);
                        GUI.enabled = true;
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground(true);
                 
                    if (objectType.enumValueIndex == (int)StylerObject.ObjectType.Graphic)
                        EvoEditorGUI.DrawProperty(targetGraphic, "Target Graphic", null, false, true, true);
                    else if (objectType.enumValueIndex == (int)StylerObject.ObjectType.TMPText)
                        EvoEditorGUI.DrawProperty(targetText, "Target Text", null, false, true, true);
                    else if (objectType.enumValueIndex == (int)StylerObject.ObjectType.Image)
                        EvoEditorGUI.DrawProperty(targetGraphic, "Target Image", null, false, true, true);
                    else if (objectType.enumValueIndex == (int)StylerObject.ObjectType.Gradient)
                        EvoEditorGUI.DrawProperty(targetGradient, "Target Gradient", "", false, true, true);
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawSettings()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref soTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    // Object Type
                    EvoEditorGUI.DrawProperty(objectType, "Object Type", null, true, true, true);

                    bool isGradient = objectType.enumValueIndex == (int)StylerObject.ObjectType.Gradient;
                    GUI.enabled = preset.objectReferenceValue;

                    // Draw fields
                    if (objectType.enumValueIndex == (int)StylerObject.ObjectType.TMPText)
                    {
                        StylerEditor.DrawItemDropdown(preset, fontID, Styler.ItemType.Font, "Font ID", true, true, true);
                        StylerEditor.DrawItemDropdown(preset, textStyleID, Styler.ItemType.TextStyle, "Text Style ID", true, true, true);
                    }
                    else if (objectType.enumValueIndex == (int)StylerObject.ObjectType.Image)
                    {
                        StylerEditor.DrawItemDropdown(preset, spriteID, Styler.ItemType.Sprite, "Sprite ID", true, true, true);
                    }
                    else if (isGradient)
                    {
                        StylerEditor.DrawItemDropdown(preset, gradientID, Styler.ItemType.Gradient, "Gradient ID", false, true, true);
                    }

                    GUI.enabled = !useCustomColor.boolValue && !enableInteraction.boolValue;

                    // Hide color-specific settings entirely if working with a gradient
                    if (!isGradient)
                    {
                        StylerEditor.DrawItemDropdown(preset, colorID, Styler.ItemType.Color, "Color ID", true, true, true);

                        // Override Alpha section
                        GUI.enabled = !useCustomColor.boolValue;
                        EvoEditorGUI.BeginVerticalBackground(true);
                        EvoEditorGUI.DrawToggle(overrideAlpha, "Override Alpha", overrideAlpha.tooltip, false, true, true, bypassNormalBackground: true);
                        if (overrideAlpha.boolValue)
                        {
                            EvoEditorGUI.BeginContainer(3);
                            EvoEditorGUI.DrawProperty(alphaOverride, "Alpha", alphaOverride.tooltip, false, true);
                            EvoEditorGUI.EndContainer();
                        }
                        EvoEditorGUI.EndVerticalBackground(true);
                        GUI.enabled = true;

                        EvoEditorGUI.DrawToggle(useCustomColor, "Use Custom Color", useCustomColor.tooltip, false, true, true);
                    }
                    else
                    {
                        // Reset GUI enabled state if skipping color fields
                        GUI.enabled = true;
                    }

                    // Info Boxes
                    if (!preset.objectReferenceValue && (!useCustomColor.boolValue || isGradient))
                    {
                        GUILayout.Space(4);
                        EvoEditorGUI.DrawInfoBox("No preset attached. Please assign a valid Styler Preset to use the Styler system.", null, true);
                    }
                    else if (enableInteraction.boolValue && interactableObject.objectReferenceValue && !isGradient)
                    {
                        GUILayout.Space(4);
                        EvoEditorGUI.DrawInfoBox("Interaction is enabled; visual states will be handled by the interaction system.", null, true);
                    }
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawInteraction()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref soTarget.interactionFoldout, "Interaction", EvoEditorGUI.GetIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    bool isGradient = objectType.enumValueIndex == (int)StylerObject.ObjectType.Gradient;

                    // Disable properties & inform the user if in Gradient Mode
                    if (isGradient)
                    {
                        EvoEditorGUI.DrawInfoBox("Interaction is currently not supported for the Gradient type.", null, true);
                        GUILayout.Space(4);
                        GUI.enabled = false;
                    }

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawToggle(enableInteraction, "Enable Interaction", enableInteraction.tooltip, false, true, true, bypassNormalBackground: true);

                    if (!enableInteraction.boolValue)
                    {
                        EvoEditorGUI.EndVerticalBackground();
                    }
                    else
                    {
                        EvoEditorGUI.BeginContainer(3);
                        EvoEditorGUI.DrawProperty(interactableObject, "Target Object", null, false, true);
                        EvoEditorGUI.EndContainer();
                        EvoEditorGUI.EndVerticalBackground();

                        if (soTarget.interactableObject)
                        {
                            bool isTMPText = objectType.enumValueIndex == (int)StylerObject.ObjectType.TMPText;

                            EvoEditorGUI.AddPropertySpace();

                            // Set Color
                            EvoEditorGUI.BeginVerticalBackground(true);
                            EvoEditorGUI.DrawToggle(enableColorInteraction, "Set Color", enableColorInteraction.tooltip, false, true, true, bypassNormalBackground: true);
                            if (enableColorInteraction.boolValue)
                            {
                                EvoEditorGUI.BeginContainer(3);
                                DrawInteractionColors();
                                EvoEditorGUI.EndContainer();
                            }
                            EvoEditorGUI.EndVerticalBackground();

                            // Set Font
                            if (isTMPText)
                            {
                                EvoEditorGUI.AddPropertySpace();
                                EvoEditorGUI.BeginVerticalBackground(true);
                                EvoEditorGUI.DrawToggle(enableFontInteraction, "Set Font", enableFontInteraction.tooltip, false, true, true, bypassNormalBackground: true);
                                if (enableFontInteraction.boolValue)
                                {
                                    EvoEditorGUI.BeginContainer(3);
                                    DrawInteractionFonts();
                                    EvoEditorGUI.EndContainer();
                                }
                                EvoEditorGUI.EndVerticalBackground();
                            }
                        }
                    }

                    if (isGradient)
                        GUI.enabled = true;
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }

        void DrawInteractionColors()
        {
            // Array of ColorMapping properties matching InteractionState enum order
            SerializedProperty[] colorMappings = new[]
            {
                disabledColor,
                normalColor,
                highlightedColor,
                pressedColor,
                selectedColor
            };

            string[] stateNames = Interactive.GetInteractionStateIDs();

            // Draw each state's color mapping
            for (int i = 0; i < stateNames.Length; i++)
            {
                SerializedProperty mapping = colorMappings[i];
                SerializedProperty color = mapping.FindPropertyRelative("color");
                SerializedProperty stylerID = mapping.FindPropertyRelative("stylerID");

                bool isLastItem = i >= stateNames.Length - 1;

                // If preset is assigned, use dropdown
                if (preset.objectReferenceValue != null && !useCustomColor.boolValue)
                    StylerEditor.DrawItemDropdown(preset, stylerID, Styler.ItemType.Color, stateNames[i], !isLastItem);
                else
                    EvoEditorGUI.DrawProperty(color, stateNames[i], null, !isLastItem, true);
            }
        }

        void DrawInteractionFonts()
        {
            // Array of FontMapping properties matching InteractionState enum order
            SerializedProperty[] fontMappings = new[]
            {
                disabledFont,
                normalFont,
                highlightedFont,
                pressedFont,
                selectedFont
            };

            string[] stateNames = Interactive.GetInteractionStateIDs();

            // Draw each state's font mapping
            for (int i = 0; i < stateNames.Length; i++)
            {
                SerializedProperty mapping = fontMappings[i];
                SerializedProperty font = mapping.FindPropertyRelative("font");
                SerializedProperty stylerID = mapping.FindPropertyRelative("stylerID");

                bool isLastItem = i >= stateNames.Length - 1;

                // If preset is assigned, use dropdown
                if (preset.objectReferenceValue != null)
                    StylerEditor.DrawItemDropdown(preset, stylerID, Styler.ItemType.Font, stateNames[i], !isLastItem);
                else
                    EvoEditorGUI.DrawProperty(font, stateNames[i], null, !isLastItem, true);
            }
        }
    }
}