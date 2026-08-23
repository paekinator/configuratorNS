using System.IO;
using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(StylerPreset))]
    public class StylerPresetEditor : Editor
    {
        StylerPreset spTarget;

        // Properties
        SerializedProperty audioItems;
        SerializedProperty colorItems;
        SerializedProperty fontItems;
        SerializedProperty textStyleItems;
        SerializedProperty gradientItems;
        SerializedProperty spriteItems;
        SerializedProperty updateMode;

        // Cache
        bool isDefaultPreset;
        bool isFallbackPreset;

        void OnEnable()
        {
            spTarget = (StylerPreset)target;

            audioItems = serializedObject.FindProperty("audioItems");
            colorItems = serializedObject.FindProperty("colorItems");
            fontItems = serializedObject.FindProperty("fontItems");
            textStyleItems = serializedObject.FindProperty("textStyleItems");
            gradientItems = serializedObject.FindProperty("gradientItems");
            spriteItems = serializedObject.FindProperty("spriteItems");
            updateMode = serializedObject.FindProperty("updateMode");

            // Register this editor for hover repaints
            EvoEditorGUI.RegisterEditor(this);

            // Check default status once when enabled
            CheckDefaultStatus();
            CheckFallbackStatus();

            string currentPath = AssetDatabase.GetAssetPath(spTarget);
            string resourcePath = GetResourcePath(currentPath);
            isFallbackPreset = !string.IsNullOrEmpty(resourcePath) && resourcePath.Replace('\\', '/') == Constants.StylerFallbackPath;
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
            EvoEditorGUI.BeginCenteredInspector(true);

            DrawAudioItems();
            DrawColorItems();
            DrawFontItems();
            DrawTextStyleItems();
            DrawGradientItems();
            DrawSpriteItems();
            DrawSettings();

            if (!isFallbackPreset)
                DrawSetDefault();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawAudioItems()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref spTarget.audioFoldout, "Audio", EvoEditorGUI.GetIcon("UI_Audio")))
            {
                EvoEditorGUI.BeginContainer();
                
                DrawItemList(audioItems, Styler.ItemType.Audio);
                GUILayout.Space(2);
               
                if (EvoEditorGUI.DrawButton("New Audio", "Add", height: 20, iconSize: 8, revertBackgroundColor: true))
                {
                    audioItems.arraySize++;
                    var newItem = audioItems.GetArrayElementAtIndex(audioItems.arraySize - 1);
                    newItem.FindPropertyRelative("itemID").stringValue = "New Audio";
                    newItem.FindPropertyRelative("audioAsset").objectReferenceValue = null;
                    newItem.isExpanded = true;
                    EditorUtility.SetDirty(spTarget);
                }

                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawColorItems()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref spTarget.colorFoldout, "Color", EvoEditorGUI.GetIcon("UI_Style")))
            {
                EvoEditorGUI.BeginContainer();
                
                DrawItemList(colorItems, Styler.ItemType.Color);
                GUILayout.Space(2);
                
                if (EvoEditorGUI.DrawButton("New Color", "Add", height: 20, iconSize: 8, revertBackgroundColor: true))
                {
                    colorItems.arraySize++;
                    var newItem = colorItems.GetArrayElementAtIndex(colorItems.arraySize - 1);
                    newItem.FindPropertyRelative("itemID").stringValue = $"Color {colorItems.arraySize}";
                    newItem.FindPropertyRelative("colorValue").colorValue = Color.white;
                    newItem.isExpanded = true;
                    EditorUtility.SetDirty(spTarget);
                }

                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawFontItems()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref spTarget.fontFoldout, "Font", EvoEditorGUI.GetIcon("UI_Text")))
            {
                EvoEditorGUI.BeginContainer();
                
                DrawItemList(fontItems, Styler.ItemType.Font);
                GUILayout.Space(2);
                
                if (EvoEditorGUI.DrawButton("New Font", "Add", height: 20, iconSize: 8, revertBackgroundColor: true))
                {
                    fontItems.arraySize++;
                    var newItem = fontItems.GetArrayElementAtIndex(fontItems.arraySize - 1);
                    newItem.FindPropertyRelative("itemID").stringValue = "New Font";
                    newItem.FindPropertyRelative("fontAsset").objectReferenceValue = null;
                    newItem.isExpanded = true;
                    EditorUtility.SetDirty(spTarget);
                }

                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawTextStyleItems()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref spTarget.textStyleFoldout, "Text Style", EvoEditorGUI.GetIcon("UI_TextStyle")))
            {
                EvoEditorGUI.BeginContainer();
                
                DrawItemList(textStyleItems, Styler.ItemType.TextStyle);
                GUILayout.Space(2);
                
                if (EvoEditorGUI.DrawButton("New Text Style", "Add", height: 20, iconSize: 8, revertBackgroundColor: true))
                {
                    textStyleItems.arraySize++;
                    var newItem = textStyleItems.GetArrayElementAtIndex(textStyleItems.arraySize - 1);
                    newItem.FindPropertyRelative("itemID").stringValue = $"Text Style {textStyleItems.arraySize}";
                    newItem.FindPropertyRelative("applySize").boolValue = false;
                    newItem.FindPropertyRelative("applyAlignment").boolValue = false;
                    newItem.FindPropertyRelative("applyWrappingAndOverflow").boolValue = false;
                    newItem.FindPropertyRelative("applySpacing").boolValue = false;
                    newItem.FindPropertyRelative("applyMargin").boolValue = false;
                    newItem.FindPropertyRelative("applyFontStyle").boolValue = false;
                    newItem.isExpanded = true;
                    EditorUtility.SetDirty(spTarget);
                }

                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawSpriteItems()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref spTarget.spriteFoldout, "Sprite", EvoEditorGUI.GetIcon("UI_Sprite")))
            {
                EvoEditorGUI.BeginContainer();
                
                DrawItemList(spriteItems, Styler.ItemType.Sprite);
                GUILayout.Space(2);
               
                if (EvoEditorGUI.DrawButton("New Sprite", "Add", height: 20, iconSize: 8, revertBackgroundColor: true))
                {
                    spriteItems.arraySize++;
                    var newItem = spriteItems.GetArrayElementAtIndex(spriteItems.arraySize - 1);
                    newItem.FindPropertyRelative("itemID").stringValue = "New Sprite";
                    newItem.FindPropertyRelative("spriteAsset").objectReferenceValue = null;
                    newItem.isExpanded = true;
                    EditorUtility.SetDirty(spTarget);
                }

                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawGradientItems()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref spTarget.gradientFoldout, "Gradient", EvoEditorGUI.GetIcon("UI_Gradient")))
            {
                EvoEditorGUI.BeginContainer();
               
                DrawItemList(gradientItems, Styler.ItemType.Gradient);
                GUILayout.Space(2);
                
                if (EvoEditorGUI.DrawButton("New Gradient", "Add", height: 20, iconSize: 8, revertBackgroundColor: true))
                {
                    gradientItems.arraySize++;
                    var newItem = gradientItems.GetArrayElementAtIndex(gradientItems.arraySize - 1);
                    newItem.FindPropertyRelative("itemID").stringValue = $"Gradient {gradientItems.arraySize}";
                    newItem.isExpanded = true;
                    EditorUtility.SetDirty(spTarget);
                }

                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawSettings()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref spTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawProperty(updateMode, "Update Mode", updateMode.tooltip, false, false);
                    EvoEditorGUI.BeginContainer(4);

                    string description = updateMode.enumValueIndex == 0
                        ? "Styler objects are updated in the editor and on every change at runtime."
                        : "Styler objects are always updated in the editor and whenever the object is enabled at runtime.";
                    
                    GUILayout.Space(2);
                    EvoEditorGUI.DrawInfoBox(description);
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground();
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawItemList(SerializedProperty listProperty, Styler.ItemType itemType)
        {
            for (int i = 0; i < listProperty.arraySize; i++)
            {
                SerializedProperty item = listProperty.GetArrayElementAtIndex(i);
                DrawListItem(item, i, itemType, () =>
                {
                    listProperty.DeleteArrayElementAtIndex(i);
                    EditorUtility.SetDirty(spTarget);
                });
            }
        }

        void DrawListItem(SerializedProperty item, int index, Styler.ItemType itemType, System.Action deleteCallback)
        {
            SerializedProperty itemID = item.FindPropertyRelative("itemID");
            EvoEditorGUI.BeginVerticalBackground(true);

            GUILayout.BeginHorizontal();
            {
                string displayName = string.IsNullOrEmpty(itemID.stringValue) ? $"Item {index}" : itemID.stringValue;

                // Draw color preview box for color items
                if (itemType == Styler.ItemType.Color)
                {
                    SerializedProperty colorValue = item.FindPropertyRelative("colorValue");

                    Rect colorRect = GUILayoutUtility.GetRect(3, 9.5f, GUILayout.ExpandWidth(false));
                    colorRect.x += 8;
                    colorRect.y += 7;
                    EditorGUI.DrawRect(colorRect, colorValue.colorValue);

                    GUILayout.Space(-3);
                    displayName = $"   {displayName}";
                }
                else if (itemType == Styler.ItemType.Gradient)
                {
                    SerializedProperty gradientValue = item.FindPropertyRelative("gradientValue");

                    Rect gradRect = GUILayoutUtility.GetRect(20, 9.5f, GUILayout.ExpandWidth(false));
                    gradRect.x += 8;
                    gradRect.y += 7;

                    // Natively draw the gradient box similar to how normal property fields do, but read-only
                    GUI.enabled = false;
                    EditorGUI.PropertyField(gradRect, gradientValue, GUIContent.none);
                    GUI.enabled = true;

                    // Use negative space to rewind the layout cursor
                    GUILayout.Space(-20);
                    displayName = $"        {displayName}";
                }

                if (EvoEditorGUI.DrawButton(displayName, item.isExpanded ? "Minimize" : "Expand", height: 24, normalColor: Color.clear,
                    iconSize: 8, textAlignment: TextAnchor.MiddleLeft, iconAlignment: EvoEditorGUI.ButtonAlignment.Right))
                {
                    item.isExpanded = !item.isExpanded;
                }

                if (EvoEditorGUI.DrawButton(null, "Delete", "Delete item", iconSize: 8, width: 24, height: 24, normalColor: Color.clear))
                {
                    string itmName = string.IsNullOrEmpty(itemID.stringValue) ? $"Item {index}" : itemID.stringValue;
                    if (EditorUtility.DisplayDialog("Delete Item",
                        $"Are you sure you want to delete '{itmName}'?", "Delete", "Cancel"))
                    {
                        deleteCallback?.Invoke();
                        return;
                    }
                }
            }
            GUILayout.EndHorizontal();

            // Only draw the content if expanded
            if (item.isExpanded)
            {
                EvoEditorGUI.BeginContainer(3);
                {
                    EvoEditorGUI.DrawProperty(itemID, "ID", "Unique identifier for this item.");

                    if (itemType == Styler.ItemType.Audio)
                    {
                        SerializedProperty audioAsset = item.FindPropertyRelative("audioAsset");
                        EvoEditorGUI.DrawProperty(audioAsset, "Audio Clip", "The audio clip for this item.", false);
                    }
                    else if (itemType == Styler.ItemType.Color)
                    {
                        SerializedProperty colorValue = item.FindPropertyRelative("colorValue");
                        EvoEditorGUI.DrawProperty(colorValue, "Color", "The color value for this item.", false);
                    }
                    else if (itemType == Styler.ItemType.Font)
                    {
                        SerializedProperty fontAsset = item.FindPropertyRelative("fontAsset");
                        EvoEditorGUI.DrawProperty(fontAsset, "Font Asset", "The font asset for this item.", false);
                    }
                    else if (itemType == Styler.ItemType.TextStyle)
                    {
                        SerializedProperty applyFontStyle = item.FindPropertyRelative("applyFontStyle");
                        EvoEditorGUI.BeginVerticalBackground();
                        {
                            EvoEditorGUI.DrawToggle(applyFontStyle, "Apply Font Style", applyFontStyle.tooltip, false, revertColor: true, bypassNormalBackground: true);
                            if (applyFontStyle.boolValue)
                            {
                                EvoEditorGUI.BeginContainer(4, compactHeader: true);
                                {
                                    GUILayout.Space(1);
                                    EvoEditorGUI.DrawProperty(item.FindPropertyRelative("fontStyle"), "Font Style", null, false, true, true);
                                }
                                EvoEditorGUI.EndContainer();
                            }
                        }
                        EvoEditorGUI.EndVerticalBackground();
                        EvoEditorGUI.AddLayoutSpace();

                        SerializedProperty applySize = item.FindPropertyRelative("applySize");
                        EvoEditorGUI.BeginVerticalBackground();
                        { 
                            EvoEditorGUI.DrawToggle(applySize, "Apply Size", applySize.tooltip, false, revertColor: true, bypassNormalBackground: true);
                            if (applySize.boolValue)
                            {
                                EvoEditorGUI.BeginContainer(4, compactHeader: true);
                                {
                                    GUILayout.Space(1);
                                    SerializedProperty enableAutoSizing = item.FindPropertyRelative("enableAutoSizing");
                                    EvoEditorGUI.DrawToggle(enableAutoSizing, "Auto Size", null, true, true, true);

                                    if (enableAutoSizing.boolValue)
                                    {
                                        EvoEditorGUI.DrawProperty(item.FindPropertyRelative("fontSizeMin"), "Min Size", null, true, true, true);
                                        EvoEditorGUI.DrawProperty(item.FindPropertyRelative("fontSizeMax"), "Max Size", null, true, true, true);
                                        EvoEditorGUI.DrawProperty(item.FindPropertyRelative("characterWidthAdjustment"), "WD%", null, true, true, true);
                                        EvoEditorGUI.DrawProperty(item.FindPropertyRelative("lineSpacingAdjustment"), "Line", null, false, true, true);
                                    }
                                    else
                                    {
                                        EvoEditorGUI.DrawProperty(item.FindPropertyRelative("fontSize"), "Font Size", null, false, true, true);
                                    }
                                }
                                EvoEditorGUI.EndContainer();
                            }
                        }
                        EvoEditorGUI.EndVerticalBackground();
                        EvoEditorGUI.AddLayoutSpace();

                        SerializedProperty applyAlignment = item.FindPropertyRelative("applyAlignment");
                        EvoEditorGUI.BeginVerticalBackground();
                        {
                            EvoEditorGUI.DrawToggle(applyAlignment, "Apply Alignment", applyAlignment.tooltip, false, revertColor: true, bypassNormalBackground: true);
                            if (applyAlignment.boolValue)
                            {
                                EvoEditorGUI.BeginContainer(4, compactHeader: true);
                                {
                                    GUILayout.Space(1);
                                    EvoEditorGUI.BeginVerticalBackground(true);
                                    EvoEditorGUI.AddLayoutSpace();
                                    EditorGUILayout.PropertyField(item.FindPropertyRelative("alignment"), new GUIContent(""));
                                    EvoEditorGUI.EndVerticalBackground();

                                }
                                EvoEditorGUI.EndContainer();
                            }
                        }
                        EvoEditorGUI.EndVerticalBackground();
                        EvoEditorGUI.AddLayoutSpace();

                                                SerializedProperty applySpacing = item.FindPropertyRelative("applySpacing");
                        EvoEditorGUI.BeginVerticalBackground();
                        {
                            EvoEditorGUI.DrawToggle(applySpacing, "Apply Spacing", applySpacing.tooltip, false, revertColor: true, bypassNormalBackground: true);
                            if (applySpacing.boolValue)
                            {
                                EvoEditorGUI.BeginContainer(4,compactHeader: true);
                                {
                                    GUILayout.Space(1);
                                    EvoEditorGUI.DrawProperty(item.FindPropertyRelative("characterSpacing"), "Character", null, true, true, true);
                                    EvoEditorGUI.DrawProperty(item.FindPropertyRelative("wordSpacing"), "Word", null, true, true, true);
                                    EvoEditorGUI.DrawProperty(item.FindPropertyRelative("lineSpacing"), "Line", null, true, true, true);
                                    EvoEditorGUI.DrawProperty(item.FindPropertyRelative("paragraphSpacing"), "Paragraph", null, false, true, true);
                                }
                                EvoEditorGUI.EndContainer();
                            }
                        }
                        EvoEditorGUI.EndVerticalBackground();
                        EvoEditorGUI.AddLayoutSpace();

                        SerializedProperty applyWrappingAndOverflow = item.FindPropertyRelative("applyWrappingAndOverflow");
                        EvoEditorGUI.BeginVerticalBackground();
                        {
                            EvoEditorGUI.DrawToggle(applyWrappingAndOverflow, "Apply Wrapping & Overflow", applyWrappingAndOverflow.tooltip, false, revertColor: true, bypassNormalBackground: true);
                            if (applyWrappingAndOverflow.boolValue)
                            {
                                EvoEditorGUI.BeginContainer(4, compactHeader: true);
                                {
                                    GUILayout.Space(1);
                                    EvoEditorGUI.DrawToggle(item.FindPropertyRelative("enableWordWrapping"), "Word Wrapping", null, true, true, true);
                                    EvoEditorGUI.DrawProperty(item.FindPropertyRelative("overflowMode"), "Overflow", null, false, true, true);
                                }
                                EvoEditorGUI.EndContainer();
                            }
                        }
                        EvoEditorGUI.EndVerticalBackground();
                        EvoEditorGUI.AddLayoutSpace();

                        SerializedProperty applyMargin = item.FindPropertyRelative("applyMargin");
                        EvoEditorGUI.BeginVerticalBackground();
                        {
                            EvoEditorGUI.DrawToggle(applyMargin, "Apply Margin", applyMargin.tooltip, false, revertColor: true, bypassNormalBackground: true);
                            if (applyMargin.boolValue)
                            {
                                EvoEditorGUI.BeginContainer(4, compactHeader: true);
                                {
                                    GUILayout.Space(1);
                                    EvoEditorGUI.DrawArrayProperty(item.FindPropertyRelative("margin"), "Margin", null, false, true, true);
                                }
                                EvoEditorGUI.EndContainer();
                            }
                        }
                        EvoEditorGUI.EndVerticalBackground();
                    }
                    else if (itemType == Styler.ItemType.Gradient)
                    {
                        SerializedProperty gradientValue = item.FindPropertyRelative("gradientValue");
                        EvoEditorGUI.DrawProperty(gradientValue, "Gradient", "The gradient value for this item.", false);
                    }
                    else if (itemType == Styler.ItemType.Sprite)
                    {
                        SerializedProperty spriteAsset = item.FindPropertyRelative("spriteAsset");
                        EvoEditorGUI.DrawProperty(spriteAsset, "Sprite Asset", "The sprite asset for this item.", false);

                        EvoEditorGUI.AddLayoutSpace();
                        SerializedProperty applySettings = item.FindPropertyRelative("applyImageSettings");

                        EvoEditorGUI.BeginVerticalBackground();
                        {
                            EvoEditorGUI.DrawToggle(applySettings, "Apply Image Settings", applySettings.tooltip, false, revertColor: true, bypassNormalBackground: true);

                            if (applySettings.boolValue)
                            {
                                EvoEditorGUI.BeginContainer(4, compactHeader: true);
                                {
                                    GUILayout.Space(1);

                                    SerializedProperty imageType = item.FindPropertyRelative("imageType");
                                    EvoEditorGUI.DrawProperty(imageType, "Image Type", null, true, true, true);

                                    int typeIndex = imageType.enumValueIndex;

                                    if (typeIndex == 0 || typeIndex == 3) // Simple or Filled
                                    {
                                        EvoEditorGUI.DrawToggle(item.FindPropertyRelative("preserveAspect"), "Preserve Aspect", null, false, true, true);
                                    }
                                    if (typeIndex == 1 || typeIndex == 2) // Sliced or Tiled
                                    {
                                        EvoEditorGUI.DrawProperty(item.FindPropertyRelative("pixelsPerUnitMultiplier"), "Pixels Per Unit Multiplier", null, false, true, true);
                                    }
                                    if (typeIndex == 3) // Filled
                                    {
                                        EvoEditorGUI.AddLayoutSpace();

                                        SerializedProperty fillMethod = item.FindPropertyRelative("fillMethod");
                                        EvoEditorGUI.DrawProperty(fillMethod, "Fill Method", null, true, true, true);

                                        SerializedProperty fillOrigin = item.FindPropertyRelative("fillOrigin");

                                        string[] originOptions = GetFillOriginOptions((UnityEngine.UI.Image.FillMethod)fillMethod.enumValueIndex);

                                        // Ensure out of bounds selection doesn't break enum mappings
                                        if (fillOrigin.intValue < 0 || fillOrigin.intValue >= originOptions.Length) { fillOrigin.intValue = 0; }

                                        EditorGUI.BeginChangeCheck();
                                        int newOrigin = EvoEditorGUI.DrawDropdown(fillOrigin.intValue, originOptions, "Fill Origin", true, true, true);
                                        if (EditorGUI.EndChangeCheck()) { fillOrigin.intValue = newOrigin; }

                                        SerializedProperty fillAmount = item.FindPropertyRelative("fillAmount");
                                        EditorGUI.BeginChangeCheck();
                                        float newAmount = EvoEditorGUI.DrawSlider(fillAmount.floatValue, 0f, 1f, "Fill Amount", false, true, true);
                                        if (EditorGUI.EndChangeCheck()) { fillAmount.floatValue = newAmount; }

                                        if (fillMethod.enumValueIndex > 1) // Radial methods
                                        {
                                            EvoEditorGUI.AddLayoutSpace();
                                            EvoEditorGUI.DrawToggle(item.FindPropertyRelative("fillClockwise"), "Clockwise", null, false, true, true);
                                        }
                                    }
                                }
                                EvoEditorGUI.EndContainer();
                            }
                        }
                        EvoEditorGUI.EndVerticalBackground();
                    }
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground(true);
        }

        string[] GetFillOriginOptions(UnityEngine.UI.Image.FillMethod method)
        {
            return method switch
            {
                UnityEngine.UI.Image.FillMethod.Horizontal => new[] { "Left", "Right" },
                UnityEngine.UI.Image.FillMethod.Vertical => new[] { "Bottom", "Top" },
                UnityEngine.UI.Image.FillMethod.Radial90 => new[] { "BottomLeft", "TopLeft", "TopRight", "BottomRight" },
                UnityEngine.UI.Image.FillMethod.Radial180 => new[] { "Bottom", "Left", "Top", "Right" },
                UnityEngine.UI.Image.FillMethod.Radial360 => new[] { "Bottom", "Right", "Top", "Left" },
                _ => new[] { "Bottom" }
            };
        }

        void DrawSetDefault()
        {
            GUI.enabled = !isDefaultPreset;
            string btnText = isDefaultPreset ? "Currently Default" : "Set as Default Preset";
            if (EvoEditorGUI.DrawButton(btnText, isDefaultPreset ? "UI_DefaultStylerCheck" : null,
                "Sets this preset as the global default. Preset must be in a Resources folder.",
                height: 28, iconSize: 11, revertBackgroundColor: isDefaultPreset))
            {
                SetAsDefault();
            }
            GUI.enabled = true;
        }

        void SetAsDefault()
        {
            string assetPath = AssetDatabase.GetAssetPath(spTarget);
            string resourcePath = GetResourcePath(assetPath);

            // Validate Resources path
            if (resourcePath == null)
            {
                EditorUtility.DisplayDialog("Invalid Location", "To set this as the default preset, " +
                    "it must be located inside a 'Resources' folder.", "OK");
                return;
            }

            // Determine target path based on Styler.cs location
            string stylerScriptPath = FindStylerScriptPath();
            if (string.IsNullOrEmpty(stylerScriptPath))
            {
                EditorUtility.DisplayDialog("Error", "Could not locate 'Styler.cs' to determine config save location.", "OK");
                return;
            }

            int scriptsIndex = stylerScriptPath.LastIndexOf("/Scripts/");

            // Find "Scripts" and strip it to get the root "Evo UI" folder
            // Path: .../Evo UI/Scripts/Styler.cs
            string evoUiRoot;
            if (scriptsIndex != -1)
            {
                // Take everything before "/Scripts/"
                evoUiRoot = stylerScriptPath[..scriptsIndex];
            }
            else
            {
                // Fallback: If not in a "Scripts" folder, assume Styler.cs is in the root or deeper custom structure.
                // We'll just go up one level from the file to be safe.
                evoUiRoot = Path.GetDirectoryName(stylerScriptPath);
            }

            // Construct Resources path
            string resourcesDir = Path.Combine(evoUiRoot, "Resources");
            if (!Directory.Exists(resourcesDir)) { Directory.CreateDirectory(resourcesDir); }

            // Construct Config path using the Constant constant: "Styler Presets/Config"
            // This ensures we save it exactly where Styler.cs looks for it
            string fullPath = Path.Combine(resourcesDir, Constants.StylerConfigPath + ".txt");

            // Normalize path separators for Unity
            fullPath = fullPath.Replace('\\', '/');

            // Ensure subdirectories exist (e.g. "Styler Presets")
            string configDir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(configDir)) { Directory.CreateDirectory(configDir); }

            // Write Config
            try
            {
                File.WriteAllText(fullPath, resourcePath);
                Styler.UpdateCachedDefaultPreset(spTarget);
                AssetDatabase.Refresh();
                CheckDefaultStatus();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Styler] Failed to save config file: {e.Message}");
            }
        }

        void CheckDefaultStatus()
        {
            TextAsset config = Resources.Load<TextAsset>(Constants.StylerConfigPath);
            if (config == null)
            {
                isDefaultPreset = false;
                return;
            }

            string path = AssetDatabase.GetAssetPath(spTarget);
            string resourcePath = GetResourcePath(path);

            // Trim to handle potential whitespace or line endings in the text file
            isDefaultPreset = config.text.Trim() == resourcePath;
        }

        void CheckFallbackStatus()
        {
            string currentPath = AssetDatabase.GetAssetPath(spTarget);
            string resourcePath = GetResourcePath(currentPath);
            isFallbackPreset = !string.IsNullOrEmpty(resourcePath) && resourcePath.Replace('\\', '/') == Constants.StylerFallbackPath;
        }

        string GetResourcePath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return null;

            // Need the path relative to Resources for the config file content
            int resourcesIndex = assetPath.LastIndexOf("/Resources/");
            if (resourcesIndex == -1) { return null; }

            string relativePath = assetPath[(resourcesIndex + 11)..]; // Length of "/Resources/"
            int extensionIndex = relativePath.LastIndexOf(".");
            if (extensionIndex != -1) { relativePath = relativePath[..extensionIndex]; }
            return relativePath;
        }

        string FindStylerScriptPath()
        {
            string[] guids = AssetDatabase.FindAssets("Styler t:Script");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path) == "Styler.cs") { return path; }
            }
            return null;
        }
    }
}