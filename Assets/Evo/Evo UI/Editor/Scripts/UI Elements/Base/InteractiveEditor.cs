using UnityEngine;
using UnityEditor;
using UnityEditor.UI;
using Evo.EditorTools;

namespace Evo.UI
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(Interactive))]
    public class InteractiveEditor : SelectableEditor
    {
        // Target
        Interactive iTarget;

        // References
        SerializedProperty disabledCG;
        SerializedProperty normalCG;
        SerializedProperty highlightedCG;
        SerializedProperty pressedCG;
        SerializedProperty selectedCG;

        // Settings
        SerializedProperty interactable;
        SerializedProperty transitionDuration;
        SerializedProperty interactionState;

        // Navigation
        SerializedProperty navigation;

        // Events
        SerializedProperty onClick;
        SerializedProperty onPointerEnter;
        SerializedProperty onPointerExit;

        protected override void OnEnable()
        {
            iTarget = (Interactive)target;

            disabledCG = serializedObject.FindProperty("disabledCG");
            normalCG = serializedObject.FindProperty("normalCG");
            highlightedCG = serializedObject.FindProperty("highlightedCG");
            pressedCG = serializedObject.FindProperty("pressedCG");
            selectedCG = serializedObject.FindProperty("selectedCG");

            interactable = serializedObject.FindProperty("m_Interactable");
            transitionDuration = serializedObject.FindProperty("transitionDuration");
            interactionState = serializedObject.FindProperty("interactionState");

            navigation = serializedObject.FindProperty("m_Navigation");

            onClick = serializedObject.FindProperty("onClick");
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

            DrawSettingsSection();
            DrawStyleSection();
            DrawNavigationSection();
            DrawReferencesSection();
            DrawEventsSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawSettingsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref iTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawToggle(interactable, "Interactable", "Sets whether the button is interactable or not.", true, true, true);
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

            if (EvoEditorGUI.DrawFoldout(ref iTarget.navigationFoldout, "Navigation", EvoEditorGUI.GetIcon("UI_Navigation")))
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

            if (EvoEditorGUI.DrawFoldout(ref iTarget.styleFoldout, "Style", EvoEditorGUI.GetIcon("UI_Style")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    DrawRippleEffect(serializedObject);
                    DrawTrailEffect(serializedObject);
                    DrawSoundEffects(serializedObject, Interactive.GetSFXFields());
                    DrawCursorType(serializedObject, false);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawReferencesSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref iTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                {
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

            if (EvoEditorGUI.DrawFoldout(ref iTarget.eventsFoldout, "Events", EvoEditorGUI.GetIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(onClick, "On Click", null, true, false);
                    EvoEditorGUI.DrawProperty(onPointerEnter, "On Pointer Enter", null, true, false);
                    EvoEditorGUI.DrawProperty(onPointerExit, "On Pointer Leave", null, false, false);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }

        #region Shared Methods
        public static void DrawRippleEffect(SerializedObject obj, bool addSpace = true)
        {
            var enableRipple = obj.FindProperty("enableRipple");
            var rippleParent = obj.FindProperty("rippleParent");
            var ripplePreset = obj.FindProperty("ripplePreset");

            EvoEditorGUI.BeginVerticalBackground(true);
            EvoEditorGUI.DrawToggle(enableRipple, "Ripple Effect", enableRipple.tooltip, false, true, true, bypassNormalBackground: true);
            if (enableRipple.boolValue)
            {
                EvoEditorGUI.BeginContainer(3);
                EvoEditorGUI.DrawProperty(rippleParent, "Target Parent", null, true, true);
                EvoEditorGUI.DrawProperty(ripplePreset, "Preset Settings", null, false, true, false, hasFoldout: true);
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground(addSpace);
        }

        public static void DrawTrailEffect(SerializedObject obj, bool addSpace = true)
        {
            var enableTrail = obj.FindProperty("enableTrail");
            var trailParent = obj.FindProperty("trailParent");
            var trailPreset = obj.FindProperty("trailPreset");

            EvoEditorGUI.BeginVerticalBackground(true);
            EvoEditorGUI.DrawToggle(enableTrail, "Trail Effect", enableTrail.tooltip, false, true, true, bypassNormalBackground: true);
            if (enableTrail.boolValue)
            {
                EvoEditorGUI.BeginContainer(3);
                EvoEditorGUI.DrawProperty(trailParent, "Target Parent", null, true, true);
                EvoEditorGUI.DrawProperty(trailPreset, "Preset Settings", null, false, true, false, hasFoldout: true);
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground(addSpace);
        }

        public static void DrawSoundEffects(SerializedObject obj, string[] fieldNames, bool addSpace = true)
        {
            var sfxSource = obj.FindProperty("sfxSource");
            var stylerPreset = obj.FindProperty("stylerPreset");

            EvoEditorGUI.BeginVerticalBackground(true);
            EvoEditorGUI.DrawProperty(sfxSource, "Sound Effects", "Controls styling source for sound effects.", false, false);
            if (sfxSource.enumValueIndex == 0) { EvoEditorGUI.EndVerticalBackground(addSpace); }
            else
            {
                EvoEditorGUI.BeginContainer(3);
                if (sfxSource.enumValueIndex == 2) { EvoEditorGUI.DrawProperty(stylerPreset, "Preset", null, true); }
                if (sfxSource.enumValueIndex != 0)
                {
                    for (int i = 0; i < fieldNames.Length; i++)
                    {
                        SerializedProperty mapping = obj.FindProperty(fieldNames[i]);
                        SerializedProperty audioClip = mapping.FindPropertyRelative("audioClip");
                        SerializedProperty stylerID = mapping.FindPropertyRelative("stylerID");

                        if (sfxSource.enumValueIndex == 2 && stylerPreset.objectReferenceValue != null)
                        {
                            StylerEditor.DrawItemDropdown(stylerPreset, stylerID, Styler.ItemType.Audio, mapping.displayName, i < fieldNames.Length - 1);
                        }
                        else
                        {
                            EvoEditorGUI.DrawProperty(audioClip, mapping.displayName, null, i < fieldNames.Length - 1, true, false);
                        }
                    }
                }
                EvoEditorGUI.EndContainer();
                EvoEditorGUI.EndVerticalBackground(addSpace);
            }
        }

        public static void DrawCursorType(SerializedObject obj, bool addSpace = true)
        {
            var cursorType = obj.FindProperty("cursorType");
            var cursorTexture = obj.FindProperty("cursorTexture");
            var cursorHotspot = obj.FindProperty("cursorHotspot");

            EvoEditorGUI.BeginVerticalBackground(true);
            EvoEditorGUI.DrawProperty(cursorType, "Cursor Type", null, false, false);

            if (cursorType.enumValueIndex == 1) // Custom
            {
                EvoEditorGUI.BeginContainer(3);
                EvoEditorGUI.DrawProperty(cursorTexture, "Cursor Texture", null, true, true);
                EvoEditorGUI.DrawProperty(cursorHotspot, "Hotspot", null, false, true);

                if (cursorTexture.objectReferenceValue != null)
                {
                    Texture2D tex = cursorTexture.objectReferenceValue as Texture2D;

                    if (tex != null)
                    {
                        // Check if the texture violates any of Unity's custom cursor requirements
                        bool needsFix = !tex.isReadable || tex.mipmapCount > 1 || tex.format != TextureFormat.RGBA32 || !tex.alphaIsTransparency;
                        if (needsFix)
                        {
                            EvoEditorGUI.AddLayoutSpace();
                            EvoEditorGUI.DrawInfoBox("Cursor texture is missing required settings! It must be RGBA32, " +
                                "readable, have alphaIsTransparency enabled, and have no mip chain.", EvoEditorGUI.InfoBoxType.Error);
                            EvoEditorGUI.AddLayoutSpace();

                            if (EvoEditorGUI.DrawButton("Fix Cursor Texture Settings"))
                            {
                                // Show a confirmation popup before modifying files on disk
                                bool confirm = EditorUtility.DisplayDialog(
                                    "Fix Cursor Texture",
                                    "This will modify the texture's import settings on disk and trigger a reimport.",
                                    "Proceed", "Cancel"
                                );

                                if (confirm)
                                {
                                    string assetPath = AssetDatabase.GetAssetPath(tex);
                                    TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                                    if (importer != null)
                                    {
                                        // Respect Sprite type to allow UI asset reuse, otherwise force to Cursor
                                        if (importer.textureType != TextureImporterType.Sprite)
                                            importer.textureType = TextureImporterType.Cursor;

                                        importer.isReadable = true; // Must be CPU Accessible
                                        importer.alphaIsTransparency = true; // Alpha is Transparency must be true
                                        importer.mipmapEnabled = false; // No Mip Maps

                                        // Must be RGBA32 format
                                        TextureImporterPlatformSettings platformSettings = importer.GetDefaultPlatformTextureSettings();
                                        platformSettings.format = TextureImporterFormat.RGBA32;
                                        importer.SetPlatformTextureSettings(platformSettings);

                                        // Apply all changes
                                        importer.SaveAndReimport();
                                    }
                                }
                            }
                        }
                    }
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground(addSpace);
        }
        #endregion
    }
}