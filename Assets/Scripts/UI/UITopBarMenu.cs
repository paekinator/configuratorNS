using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The top bar's "⋯" overflow menu: secondary actions (Load code, Clear all)
/// live here so the bar itself only carries primary controls. The menu
/// closes on Escape, on any click outside it, and after an item acts.
/// Items keep their own Button/UIConfirmingButton wiring — this component
/// only owns open/close behavior.
/// </summary>
public class UITopBarMenu : MonoBehaviour
{
    [Tooltip("The dropdown card that opens under the ⋯ button.")]
    public GameObject panel;

    [Tooltip("The ⋯ button itself (clicks on it must not count as 'outside').")]
    public RectTransform toggleButton;

    public void Toggle()
    {
        if (panel != null)
            panel.SetActive(!panel.activeSelf);
    }

    public void Close()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    void Start()
    {
        if (panel == null)
            return;

        // Plain items close the menu when they act. Confirming items only
        // close once CONFIRMED (the "Sure?" step must stay visible).
        foreach (Button item in panel.GetComponentsInChildren<Button>(true))
        {
            if (item.TryGetComponent(out UIConfirmingButton confirming))
                confirming.onConfirmed.AddListener(Close);
            else
                item.onClick.AddListener(Close);
        }
    }

    void Update()
    {
        if (panel == null || !panel.activeSelf)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            Vector2 pos = Input.mousePosition;
            Camera cam = null; // screen-space overlay canvas
            bool insidePanel = RectTransformUtility.RectangleContainsScreenPoint(
                (RectTransform)panel.transform, pos, cam);
            bool onToggle = toggleButton != null &&
                RectTransformUtility.RectangleContainsScreenPoint(toggleButton, pos, cam);
            if (!insidePanel && !onToggle)
                Close();
        }
    }
}
