using System;
using UnityEngine;

public static class UIInteractionState
{
    public enum Mode
    {
        Build,
        Select
    }

    public enum Tab
    {
        Vertical,
        Horizontal,
        Twist
    }

    /// <summary>
    /// Expert = piece-by-piece Build/Select (default).
    /// Guided = NST-style template tools for faster layout.
    /// </summary>
    public enum Experience
    {
        Expert,
        Guided
    }

    private static Mode _currentMode = Mode.Build;
    private static Tab _currentTab = Tab.Vertical;
    private static Experience _currentExperience = Experience.Expert;

    public static event Action<Mode> OnModeChanged;
    public static event Action<Tab> OnTabChanged;
    public static event Action<Experience> OnExperienceChanged;

    public static Mode CurrentMode
    {
        get => _currentMode;
        set
        {
            if (_currentMode == value) return;
            _currentMode = value;
            OnModeChanged?.Invoke(_currentMode);
        }
    }

    public static Tab CurrentTab
    {
        get => _currentTab;
        set
        {
            if (_currentTab == value) return;
            _currentTab = value;
            OnTabChanged?.Invoke(_currentTab);
        }
    }

    public static Experience CurrentExperience
    {
        get => _currentExperience;
        set
        {
            if (_currentExperience == value) return;
            ActiveInteraction.Exit();
            _currentExperience = value;
            OnExperienceChanged?.Invoke(_currentExperience);
        }
    }

    public static void ResetDefaults()
    {
        _currentMode = Mode.Build;
        _currentTab = Tab.Vertical;
        _currentExperience = Experience.Expert;
        OnModeChanged?.Invoke(_currentMode);
        OnTabChanged?.Invoke(_currentTab);
        OnExperienceChanged?.Invoke(_currentExperience);
    }
}

/// <summary>
/// One exit path for every cursor-owned action. Navigation and tool buttons
/// call this before changing context so a hidden page cannot keep placing
/// parts or blocks into the scene.
/// </summary>
public static class ActiveInteraction
{
    public static void Exit()
    {
        ModulePickSession picker = FindSceneObject<ModulePickSession>();
        if (ModulePickSession.Armed && picker != null)
            picker.Cancel();

        TemplateSession guided = FindSceneObject<TemplateSession>();
        if (guided != null && guided.ActiveTool != GuidedTemplateTool.None)
            guided.SoftReset();

        FreePartSession freePart = FindSceneObject<FreePartSession>();
        if (freePart != null && freePart.ActiveKind != FreePartKind.None)
            freePart.SetKind(FreePartKind.None);

        PanelGhostController panel = FindSceneObject<PanelGhostController>();
        if (panel != null && panel.panelToolEnabled)
            panel.DisablePanelTool();

        BuildController build = FindSceneObject<BuildController>();
        if (build != null && !string.IsNullOrEmpty(build.currentPartId))
            build.SetCurrentPart(null);

        if (StructureClipboard.Active != null && StructureClipboard.Active.IsActive)
            StructureClipboard.Active.Clear();

        // Always call this: a Lite block may still be loading and have no
        // armed record yet. DisarmPlacement invalidates that pending callback.
        SpaceInteractionController space = FindSceneObject<SpaceInteractionController>();
        if (space != null)
            space.DisarmPlacement();

        if (BeamResizeSession.Instance != null && BeamResizeSession.Instance.IsActive)
            BeamResizeSession.Instance.Cancel();
    }

    static T FindSceneObject<T>() where T : MonoBehaviour
    {
        foreach (T candidate in Resources.FindObjectsOfTypeAll<T>())
        {
            if (candidate != null && candidate.gameObject.scene.IsValid() &&
                candidate.gameObject.scene.isLoaded)
                return candidate;
        }
        return null;
    }
}
