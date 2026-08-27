using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI
{
    [CustomEditor(typeof(UIAnimator))]
    public class UIAnimatorEditor : Editor
    {
        // Target
        UIAnimator animator;

        // Properties
        SerializedProperty animationGroups;
        SerializedProperty useUnscaledTime;
        SerializedProperty rectTransform;
        SerializedProperty canvasGroup;
        SerializedProperty image;
        SerializedProperty text;
        SerializedProperty onAnimationStart;
        SerializedProperty onAnimationEnd;

        // Helpers
        bool runtimeFoldout = true;
        const int LABEL_WIDTH = 80;

        void OnEnable()
        {
            animator = (UIAnimator)target;

            animationGroups = serializedObject.FindProperty("animationGroups");
            useUnscaledTime = serializedObject.FindProperty("useUnscaledTime");
            rectTransform = serializedObject.FindProperty("rectTransform");
            canvasGroup = serializedObject.FindProperty("canvasGroup");
            image = serializedObject.FindProperty("image");
            text = serializedObject.FindProperty("text");
            onAnimationStart = serializedObject.FindProperty("onAnimationStart");
            onAnimationEnd = serializedObject.FindProperty("onAnimationEnd");

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
            DrawCustomGUI();
            EvoEditorGUI.HandleInspectorGUI();
        }

        void DrawCustomGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            DrawRuntimeSection();
            DrawGroupsSection();
            DrawSettingsSection();
            DrawReferencesSection();
            DrawEventsSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawGroupsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref animator.groupFoldout, "Animation Groups", EvoEditorGUI.GetIcon("UI_Group")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    // Animation Groups
                    for (int i = 0; i < animator.animationGroups.Count; i++) { DrawAnimationGroup(i); }

                    // Add button
                    GUILayout.Space(1);
                    if (EvoEditorGUI.DrawButton("Add Animation Group", "Add", height: 22, iconSize: 8, revertBackgroundColor: true))
                    {
                        Undo.RecordObject(target, "Add Animation Group");
                        animator.animationGroups.Add(new UIAnimator.AnimationGroup());
                        EditorUtility.SetDirty(target);
                    }
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawEventsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref animator.eventsFoldout, "Events", EvoEditorGUI.GetIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EditorGUILayout.PropertyField(onAnimationStart, new GUIContent("On Animation Start", "Called when any animation group starts"));
                    EditorGUILayout.PropertyField(onAnimationEnd, new GUIContent("On Animation End", "Called when any animation group ends (all animations complete)"));
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }

        void DrawSettingsSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref animator.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawToggle(useUnscaledTime, "Use Unscaled Time", "Animations ignore Time.timeScale (useful for UI during pause).", false, true, true);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddLayoutSpace();
        }

        void DrawReferencesSection()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref animator.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(rectTransform, "Rect Transform", null, true, true, true);
                    EvoEditorGUI.DrawProperty(canvasGroup, "Canvas Group", null, true, true, true);
                    EvoEditorGUI.DrawProperty(image, "Image", null, true, true, true);
                    EvoEditorGUI.DrawProperty(text, "Text", null, false, true, true);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddLayoutSpace();
        }

        void DrawRuntimeSection()
        {
            if (!Application.isPlaying)
                return;

            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref runtimeFoldout, "Runtime Testing", EvoEditorGUI.GetIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    GUILayout.BeginVertical();
                    GUILayout.BeginHorizontal();
                    {
                        if (EvoEditorGUI.DrawButton("OnClick", revertBackgroundColor: true)) { animator.ExecuteAnimations(UIAnimator.TriggerType.OnClick); }
                        GUILayout.Space(4);
                        if (EvoEditorGUI.DrawButton("OnPointerDown", revertBackgroundColor: true)) { animator.ExecuteAnimations(UIAnimator.TriggerType.OnPointerDown); }
                        GUILayout.Space(4);
                        if (EvoEditorGUI.DrawButton("OnPointerUp", revertBackgroundColor: true)) { animator.ExecuteAnimations(UIAnimator.TriggerType.OnPointerUp); }
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Space(4);
                    GUILayout.BeginHorizontal();
                    {
                        if (EvoEditorGUI.DrawButton("OnPointerEnter", revertBackgroundColor: true)) { animator.ExecuteAnimations(UIAnimator.TriggerType.OnPointerEnter); }
                        GUILayout.Space(4);
                        if (EvoEditorGUI.DrawButton("OnPointerLeave", revertBackgroundColor: true)) { animator.ExecuteAnimations(UIAnimator.TriggerType.OnPointerLeave); }
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Space(4);
                    GUILayout.BeginHorizontal();
                    {
                        if (EvoEditorGUI.DrawButton("OnEnable", revertBackgroundColor: true)) { animator.ExecuteAnimations(UIAnimator.TriggerType.OnEnable); }
                        GUILayout.Space(4);
                        if (EvoEditorGUI.DrawButton("Manual", revertBackgroundColor: true)) { animator.ExecuteAnimations(UIAnimator.TriggerType.Manual); }
                        GUILayout.Space(4);
                        if (EvoEditorGUI.DrawButton("Reset", revertBackgroundColor: true)) { animator.ResetToOriginalValues(); }
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawAnimationGroup(int groupIndex)
        {
            UIAnimator.AnimationGroup group = animator.animationGroups[groupIndex];

            EvoEditorGUI.BeginVerticalBackground(true);

            GUILayout.BeginHorizontal();
            {
                string displayName = string.IsNullOrEmpty(group.label) ? $"{group.trigger}" : $"{group.label} ({group.trigger})";
                if (EvoEditorGUI.DrawButton(displayName, group.isExpanded ? "Minimize" : "Expand", height: 24, normalColor: Color.clear, iconSize: 8,
                   textAlignment: TextAnchor.MiddleLeft, iconAlignment: EvoEditorGUI.ButtonAlignment.Left))
                {
                    group.isExpanded = !group.isExpanded;
                }

                // Group controls
                if (!Application.isPlaying)
                {
                    GUI.enabled = groupIndex > 0;
                    if (EvoEditorGUI.DrawButton(text: "↑", tooltip: "Move group up", iconSize: 8, width: 24, height: 24, normalColor: Color.clear))
                    {
                        Undo.RecordObject(target, "Move Group Up");
                        animator.MoveGroupUp(groupIndex);
                        EditorUtility.SetDirty(target);
                    }
                    GUI.enabled = true;

                    GUI.enabled = groupIndex < animator.animationGroups.Count - 1;
                    if (EvoEditorGUI.DrawButton(text: "↓", tooltip: "Move group down", iconSize: 8, width: 24, height: 24, normalColor: Color.clear))
                    {
                        Undo.RecordObject(target, "Move Group Down");
                        animator.MoveGroupDown(groupIndex);
                        EditorUtility.SetDirty(target);
                    }
                    GUI.enabled = true;
                }

                // Remove button
                if (EvoEditorGUI.DrawButton(null, "Delete", "Delete group", iconSize: 8, width: 24, height: 24, normalColor: Color.clear))
                {
                    if (EditorUtility.DisplayDialog("Delete Animation Group",
                    $"Are you sure you want to delete '{group.label}'?", "Yes", "No"))
                    {
                        Undo.RecordObject(target, "Delete Animation Group");
                        animator.RemoveAnimationGroup(groupIndex);
                        EditorUtility.SetDirty(target);
                    }
                }
            }
            GUILayout.EndHorizontal();

            // Only draw the content if expanded
            if (group.isExpanded)
            {
                EvoEditorGUI.BeginContainer(3);
                {
                    SerializedProperty groupProp = animationGroups.GetArrayElementAtIndex(groupIndex);
                    SerializedProperty labelProp = groupProp.FindPropertyRelative("label");
                    EvoEditorGUI.DrawProperty(labelProp, "Label", labelWidth: LABEL_WIDTH);

                    SerializedProperty triggerProp = groupProp.FindPropertyRelative("trigger");
                    EvoEditorGUI.DrawProperty(triggerProp, "Trigger", labelWidth: LABEL_WIDTH);

                    // Draw animations
                    if (group.animations.Count == 0) { GUILayout.Space(1); }
                    else if (group.animations.Count > 0)
                    {
                        EvoEditorGUI.BeginVerticalBackground();
                        EvoEditorGUI.BeginContainer("Animations", 3);
                        SerializedProperty animationsProp = groupProp.FindPropertyRelative("animations");
                        for (int j = 0; j < group.animations.Count; j++)
                        {
                            SerializedProperty animProp = animationsProp.GetArrayElementAtIndex(j);
                            DrawAnimation(animProp, j, group);
                        }
                        EvoEditorGUI.EndContainer();
                        EvoEditorGUI.EndVerticalBackground();
                        GUILayout.Space(3);
                    }

                    // Draw local events section
                    DrawGroupEvents(groupProp, group);
                    GUILayout.Space(3);

                    // Add animation button
                    if (EvoEditorGUI.DrawButton("Add Animation", "Add", height: 22, iconSize: 8))
                    {
                        Undo.RecordObject(target, "Add Animation");
                        group.animations.Add(new UIAnimator.AnimationData());
                        EditorUtility.SetDirty(target);
                    }
                    GUILayout.Space(1);
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground(true);
        }

        void DrawGroupEvents(SerializedProperty groupProp, UIAnimator.AnimationGroup group)
        {
            EvoEditorGUI.BeginVerticalBackground();

            SerializedProperty eventsExpandedProp = groupProp.FindPropertyRelative("eventsExpanded");

            // Foldout button
            if (EvoEditorGUI.DrawButton("Local Events", group.eventsExpanded ? "Minimize" : "Expand", height: 22, normalColor: Color.clear,
                iconSize: 8, textAlignment: TextAnchor.MiddleLeft, iconAlignment: EvoEditorGUI.ButtonAlignment.Left))
            {
                eventsExpandedProp.boolValue = !eventsExpandedProp.boolValue;
            }

            if (group.eventsExpanded)
            {
                EvoEditorGUI.BeginContainer(3);
                {
                    SerializedProperty onGroupStartProp = groupProp.FindPropertyRelative("onGroupStart");
                    SerializedProperty onGroupEndProp = groupProp.FindPropertyRelative("onGroupEnd");

                    EditorGUILayout.PropertyField(onGroupStartProp, new GUIContent("On Group Start", "Called when this animation group starts"));
                    EditorGUILayout.PropertyField(onGroupEndProp, new GUIContent("On Group End", "Called when this animation group ends (all animations complete)"));
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }

        void DrawAnimation(SerializedProperty animProp, int animIndex, UIAnimator.AnimationGroup group)
        {
            UIAnimator.AnimationData animData = group.animations[animIndex];

            EvoEditorGUI.BeginVerticalBackground(true);

            GUILayout.BeginHorizontal();
            {
                // Enabled toggle
                GUILayout.Space(2);
                EditorGUILayout.BeginVertical(GUILayout.Width(16));
                EditorGUILayout.Space(1);
                EditorGUILayout.PropertyField(animProp.FindPropertyRelative("enabled"), GUIContent.none, GUILayout.Width(16));
                EditorGUILayout.EndVertical();

                // Animation type dropdown
                GUILayout.BeginVertical();
                GUILayout.Space(3);
                EditorGUILayout.PropertyField(animProp.FindPropertyRelative("type"), GUIContent.none);
                GUILayout.EndVertical();

                // Minimize/Expand button
                if (EvoEditorGUI.DrawButton(null, animData.isExpanded ? "Minimize" : "Expand", iconSize: 8, width: 24, height: 24, normalColor: Color.clear))
                {
                    Undo.RecordObject(target, "Toggle Animation Expand");
                    animData.isExpanded = !animData.isExpanded;
                    EditorUtility.SetDirty(target);
                }

                // Remove button
                if (EvoEditorGUI.DrawButton(null, "Delete", "Delete animation", iconSize: 8, width: 24, height: 24, normalColor: Color.clear))
                {
                    if (EditorUtility.DisplayDialog("Delete Animation",
                    $"Are you sure you want to delete this animation?", "Yes", "No"))
                    {
                        Undo.RecordObject(target, "Delete Animation");
                        group.animations.RemoveAt(animIndex);
                        EditorUtility.SetDirty(target);
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                    return;
                }
            }
            GUILayout.EndHorizontal();

            // Only show details if expanded
            if (animData.isExpanded)
            {
                EvoEditorGUI.BeginContainer(3);
                GUI.enabled = animData.enabled;

                // Duration and Delay
                GUILayout.BeginHorizontal();
                {
                    EvoEditorGUI.DrawProperty(animProp.FindPropertyRelative("duration"), "Duration", null, false, true, false, labelWidth: LABEL_WIDTH);
                    EvoEditorGUI.DrawProperty(animProp.FindPropertyRelative("delay"), "Delay", null, false, true, false, labelWidth: LABEL_WIDTH);
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(3);

                // Ease Type
                EvoEditorGUI.DrawProperty(animProp.FindPropertyRelative("easeType"), "Ease Type", null, true, true, false, labelWidth: LABEL_WIDTH);

                // Loop and Yoyo
                GUILayout.BeginHorizontal();
                EvoEditorGUI.DrawToggle(animProp.FindPropertyRelative("loop"), "Loop", null, false, true, false);
                GUILayout.Space(2);
                EvoEditorGUI.DrawToggle(animProp.FindPropertyRelative("yoyo"), "Yoyo", null, false, true, false);
                GUILayout.EndHorizontal();
                GUILayout.Space(3);
                // Use Current Value
                bool canUseCurrentValue = !animData.loop && !animData.yoyo;
                GUI.enabled = canUseCurrentValue && animData.enabled;
                SerializedProperty useCurrentProp = animProp.FindPropertyRelative("animateFromCurrentValue");
                EvoEditorGUI.DrawToggle(useCurrentProp, "Animate From Current Value", "Animate from based on the current value.", true, true, false);
                if (!canUseCurrentValue && animData.animateFromCurrentValue) { useCurrentProp.boolValue = false; }
                GUI.enabled = true;

                // Animation-specific properties
                DrawAnimationSpecificProperties(animProp, animData);

                GUI.enabled = true;
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground(animIndex < group.animations.Count - 1);
        }

        private void DrawAnimationSpecificProperties(SerializedProperty animProp, UIAnimator.AnimationData animData)
        {
            switch (animData.type)
            {
                case UIAnimator.AnimationType.Fade:
                    GUI.enabled = !animData.animateFromCurrentValue && animData.enabled;
                    EvoEditorGUI.DrawProperty(animProp.FindPropertyRelative("fadeFrom"), "From", null, true, true, false, labelWidth: LABEL_WIDTH);
                    GUI.enabled = animData.enabled;
                    EvoEditorGUI.DrawProperty(animProp.FindPropertyRelative("fadeTo"), "To", null, false, true, false, labelWidth: LABEL_WIDTH);
                    break;

                case UIAnimator.AnimationType.Scale:
                    GUI.enabled = !animData.animateFromCurrentValue && animData.enabled;
                    EvoEditorGUI.DrawProperty(animProp.FindPropertyRelative("scaleFrom"), "From", null, true, true, false, labelWidth: LABEL_WIDTH);
                    GUI.enabled = animData.enabled;
                    EvoEditorGUI.DrawProperty(animProp.FindPropertyRelative("scaleTo"), "To", null, false, true, false, labelWidth: LABEL_WIDTH);
                    break;

                case UIAnimator.AnimationType.Slide:
                    GUI.enabled = !animData.animateFromCurrentValue && animData.enabled;
                    EvoEditorGUI.DrawProperty(animProp.FindPropertyRelative("slideFrom"), "From Offset", null, true, true, false, labelWidth: LABEL_WIDTH);
                    GUI.enabled = animData.enabled;
                    EvoEditorGUI.DrawProperty(animProp.FindPropertyRelative("slideTo"), "To Offset", null, false, true, false, labelWidth: LABEL_WIDTH);
                    break;

                case UIAnimator.AnimationType.Rotate:
                    GUI.enabled = !animData.animateFromCurrentValue && animData.enabled;
                    EvoEditorGUI.DrawProperty(animProp.FindPropertyRelative("rotateFrom"), "From", null, true, true, false, labelWidth: LABEL_WIDTH);
                    GUI.enabled = animData.enabled;
                    EvoEditorGUI.DrawProperty(animProp.FindPropertyRelative("rotateTo"), "To", null, false, true, false, labelWidth: LABEL_WIDTH);
                    break;

                case UIAnimator.AnimationType.PunchScale:
                    EvoEditorGUI.DrawSlider(floatValue: animProp.FindPropertyRelative("punchIntensity"), 0, 50, "Intensity", false, true, false, labelWidth: LABEL_WIDTH);
                    break;

                case UIAnimator.AnimationType.Shake:
                    EvoEditorGUI.DrawSlider(floatValue: animProp.FindPropertyRelative("shakeIntensity"), 0, 50, "Intensity", false, true, false, labelWidth: LABEL_WIDTH);
                    break;

                case UIAnimator.AnimationType.Bounce:
                    EvoEditorGUI.DrawProperty(animProp.FindPropertyRelative("bounceHeight"), "Height", null, false, true, false, labelWidth: LABEL_WIDTH);
                    break;

                case UIAnimator.AnimationType.ColorTint:
                    GUI.enabled = !animData.animateFromCurrentValue && animData.enabled;
                    EvoEditorGUI.DrawProperty(animProp.FindPropertyRelative("colorFrom"), "From", null, true, true, false, labelWidth: LABEL_WIDTH);
                    GUI.enabled = animData.enabled;
                    EvoEditorGUI.DrawProperty(animProp.FindPropertyRelative("colorTo"), "To", null, false, true, false, labelWidth: LABEL_WIDTH);
                    break;
            }
        }
    }
}