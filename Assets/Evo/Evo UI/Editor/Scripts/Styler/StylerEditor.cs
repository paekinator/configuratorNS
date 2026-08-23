using System.Linq;
using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI
{
    public static class StylerEditor
    {
        public static void DrawItemDropdown(SerializedProperty preset, SerializedProperty stylerProperty, Styler.ItemType itemType, string label,
            bool addSpace = true, bool customBackground = true, bool revertColor = false, int labelWidth = 0)
        {
            var stylerPresetValue = preset.objectReferenceValue as StylerPreset;
            if (stylerPresetValue == null)
            {
                EvoEditorGUI.DrawProperty(stylerProperty, label, null, addSpace, customBackground, revertColor, false, labelWidth);
                return;
            }

            string[] baseOptions = null;
            string currentValue = stylerProperty.stringValue;
            int selectedIndex = 0;

            switch (itemType)
            {
                case Styler.ItemType.Audio:
                    baseOptions = stylerPresetValue.audioItems.Select(item => item.itemID).ToArray();
                    break;
                case Styler.ItemType.Color:
                    baseOptions = stylerPresetValue.colorItems.Select(item => item.itemID).ToArray();
                    break;
                case Styler.ItemType.Font:
                    baseOptions = stylerPresetValue.fontItems.Select(item => item.itemID).ToArray();
                    break;
                case Styler.ItemType.TextStyle:
                    baseOptions = stylerPresetValue.textStyleItems.Select(item => item.itemID).ToArray();
                    break;
                case Styler.ItemType.Sprite:
                    baseOptions = stylerPresetValue.spriteItems.Select(item => item.itemID).ToArray();
                    break;
                case Styler.ItemType.Gradient:
                    baseOptions = stylerPresetValue.gradientItems.Select(item => item.itemID).ToArray();
                    break;
            }

            if (baseOptions != null)
            {
                string[] options;

                // Add "None" option for specific items
                if (itemType == Styler.ItemType.Audio || itemType == Styler.ItemType.Color || itemType == Styler.ItemType.TextStyle 
                    || itemType == Styler.ItemType.Sprite)
                {
                    string firstOption = itemType == Styler.ItemType.Color ? "None (Transparent)" : "None";

                    // Create options array with special first option
                    options = new string[baseOptions.Length + 1];
                    options[0] = firstOption;
                    System.Array.Copy(baseOptions, 0, options, 1, baseOptions.Length);

                    // Find current selection index
                    if (string.IsNullOrEmpty(currentValue)) { selectedIndex = 0; } // Empty string = Transparent/None option       
                    else
                    {
                        bool found = false;
                        for (int i = 0; i < baseOptions.Length; i++)
                        {
                            if (baseOptions[i] == currentValue)
                            {
                                selectedIndex = i + 1; // +1 because special option is at index 0
                                found = true;
                                break;
                            }
                        }

                        // If current value is not found in options, show it as missing
                        if (!found)
                        {
                            var tempOptions = new string[options.Length + 1];
                            tempOptions[0] = firstOption;
                            tempOptions[1] = currentValue + " (Missing)";
                            System.Array.Copy(baseOptions, 0, tempOptions, 2, baseOptions.Length);
                            options = tempOptions;
                            selectedIndex = 1;
                        }
                    }
                }
                else
                {
                    // For others, use original logic without special first option
                    options = baseOptions;

                    // Find current selection index
                    for (int i = 0; i < options.Length; i++)
                    {
                        if (options[i] == currentValue)
                        {
                            selectedIndex = i;
                            break;
                        }
                    }

                    // If current value is not found in options, show it as missing
                    if (selectedIndex == 0 && !string.IsNullOrEmpty(currentValue) && !options.Contains(currentValue))
                    {
                        var tempOptions = new string[options.Length + 1];
                        tempOptions[0] = currentValue + " (Missing)";
                        System.Array.Copy(options, 0, tempOptions, 1, options.Length);
                        options = tempOptions;
                    }
                }

                EditorGUI.BeginChangeCheck();
                int newSelectedIndex = EvoEditorGUI.DrawDropdown(selectedIndex, options, label, addSpace, customBackground, revertColor, labelWidth);
                if (EditorGUI.EndChangeCheck())
                {
                    if ((itemType == Styler.ItemType.Audio || itemType == Styler.ItemType.Color || itemType == Styler.ItemType.TextStyle 
                        || itemType == Styler.ItemType.Sprite) && newSelectedIndex == 0)
                    {
                        stylerProperty.stringValue = "";
                    }
                    else if (newSelectedIndex < options.Length)
                    {
                        string newValue = options[newSelectedIndex];

                        if (newValue.EndsWith(" (Missing)"))
                            newValue = newValue.Replace(" (Missing)", "");

                        stylerProperty.stringValue = newValue;
                    }
                }
            }
        }

        public static void DrawStylingSourceSection(SerializedObject obj, string[] colorFields, string[] fontFields, bool addSpace = true)
        {
            var stylingSource = obj.FindProperty("stylingSource");
            var stylerPreset = obj.FindProperty("stylerPreset");

            EvoEditorGUI.BeginVerticalBackground(true);
            EvoEditorGUI.DrawProperty(stylingSource, "Styling Source", "Controls the source for colors and fonts.", false, false);
            if (stylingSource.enumValueIndex == 2)
            {
                EvoEditorGUI.BeginContainer(3);
                EvoEditorGUI.DrawProperty(stylerPreset, "Preset", null, false);
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();

            if (stylingSource.enumValueIndex != 0)
            {
                EvoEditorGUI.AddLayoutSpace();

                if (colorFields != null)
                {
                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.BeginContainer("Colors", 3);
                    {
                        for (int i = 0; i < colorFields.Length; i++)
                        {
                            SerializedProperty mapping = obj.FindProperty(colorFields[i]);
                            SerializedProperty color = mapping.FindPropertyRelative("color");
                            SerializedProperty stylerID = mapping.FindPropertyRelative("stylerID");

                            if (stylingSource.enumValueIndex == 2 && stylerPreset.objectReferenceValue != null)
                                DrawItemDropdown(stylerPreset, stylerID, Styler.ItemType.Color, mapping.displayName, i < colorFields.Length - 1);
                            else
                                EvoEditorGUI.DrawProperty(color, mapping.displayName, null, i < colorFields.Length - 1, true);
                        }
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground(fontFields != null);
                }

                if (fontFields != null)
                {
                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.BeginContainer("Fonts", 3);
                    {
                        for (int i = 0; i < fontFields.Length; i++)
                        {
                            SerializedProperty mapping = obj.FindProperty(fontFields[i]);
                            SerializedProperty font = mapping.FindPropertyRelative("font");
                            SerializedProperty stylerID = mapping.FindPropertyRelative("stylerID");

                            if (stylingSource.enumValueIndex == 2 && stylerPreset.objectReferenceValue != null)
                                DrawItemDropdown(stylerPreset, stylerID, Styler.ItemType.Font, mapping.displayName, i < fontFields.Length - 1);
                            else
                                EvoEditorGUI.DrawProperty(font, mapping.displayName, null, i < fontFields.Length - 1, true);
                        }
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndVerticalBackground();
                }
            }

            if (addSpace)
                EvoEditorGUI.AddLayoutSpace();
        }
    }
}