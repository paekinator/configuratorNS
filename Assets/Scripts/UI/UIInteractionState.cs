using System;

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
