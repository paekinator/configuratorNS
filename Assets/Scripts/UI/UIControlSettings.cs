using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the control-settings card opened by the gear button: two scheme
/// options (Walkthrough / CAD) with a legend explaining the active mappings.
/// </summary>
public class UIControlSettings : MonoBehaviour
{
    public CameraControlManager manager;

    [Header("Panel")]
    public GameObject panel;

    [Header("Scheme buttons")]
    public Image walkthroughBg;
    public Image cadBg;
    public TMP_Text walkthroughLabel;
    public TMP_Text cadLabel;
    public TMP_Text legendText;

    [Header("Colors (legacy: selection colors now come from the theme)")]
    public Color activeBg = new Color(0.15f, 0.13f, 0.12f);
    public Color inactiveBg = new Color(0.95f, 0.94f, 0.91f);
    public Color activeText = Color.white;
    public Color inactiveText = new Color(0.15f, 0.13f, 0.12f);

    const string WalkthroughLegend =
        "W / A / S / D · move\n" +
        "Right-drag · look around\n" +
        "Q / E · up / down\n" +
        "Shift · sprint\n" +
        "Scroll · movement speed";

    const string CadLegend =
        "Right-drag · orbit\n" +
        "Shift + right-drag · pan\n" +
        "Middle-drag · pan\n" +
        "Ctrl + right-drag · zoom\n" +
        "Scroll · zoom to cursor\n" +
        "Double middle-click / F · zoom extents";

    void OnEnable()
    {
        CameraControlManager.OnSchemeChanged += HandleSchemeChanged;
        UIThemeController.ThemeChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        CameraControlManager.OnSchemeChanged -= HandleSchemeChanged;
        UIThemeController.ThemeChanged -= Refresh;
    }

    public void TogglePanel()
    {
        if (panel != null)
        {
            panel.SetActive(!panel.activeSelf);
            if (panel.activeSelf)
            {
                panel.transform.SetAsLastSibling();
                Refresh();
            }
        }
    }

    public void ClosePanel()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    public void SelectWalkthrough()
    {
        if (manager != null)
            manager.SetWalkthrough();
    }

    public void SelectCad()
    {
        if (manager != null)
            manager.SetCad();
    }

    void HandleSchemeChanged(CameraControlScheme scheme) => Refresh();

    void Refresh()
    {
        if (manager == null)
            manager = FindFirstObjectByType<CameraControlManager>();

        bool cad = manager != null && manager.Scheme == CameraControlScheme.Cad;

        // Selection language shared with the tool cards: the chosen option
        // fills with the accent color. The old ink-on-card scheme was
        // unreadable in dark mode (the "selected" ink chip vanished into the
        // dark panel, so the light idle chip looked selected instead).
        Color selectedBg = UIThemeController.AccentColor;
        Color idleBg = UIThemeController.SurfaceColor;
        Color idleText = UIThemeController.InkColor;

        if (walkthroughBg != null) walkthroughBg.color = cad ? idleBg : selectedBg;
        if (cadBg != null) cadBg.color = cad ? selectedBg : idleBg;
        if (walkthroughLabel != null) walkthroughLabel.color = cad ? idleText : Color.white;
        if (cadLabel != null) cadLabel.color = cad ? Color.white : idleText;

        if (legendText != null)
        {
            legendText.text = cad ? CadLegend : WalkthroughLegend;
            legendText.color = UIThemeController.MutedColor;
        }

        // The card itself: runtime-injected panels are not registered with the
        // theme lists, so they'd stay a light card in dark mode. Style the
        // panel and its title directly from the theme tokens.
        if (panel != null && panel.TryGetComponent(out Image panelImg))
            panelImg.color = UIThemeController.CardColor;
        var title = panel != null ? panel.transform.Find("Title") : null;
        if (title != null && title.TryGetComponent(out TMP_Text titleText))
            titleText.color = UIThemeController.InkColor;
    }
}
