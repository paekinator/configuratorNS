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

    private static Mode _currentMode = Mode.Build;
    private static Tab _currentTab = Tab.Vertical;

    public static event Action<Mode> OnModeChanged;
    public static event Action<Tab> OnTabChanged;

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

    public static void ResetDefaults()
    {
        _currentMode = Mode.Build;
        _currentTab = Tab.Vertical;
        OnModeChanged?.Invoke(_currentMode);
        OnTabChanged?.Invoke(_currentTab);
    }
}