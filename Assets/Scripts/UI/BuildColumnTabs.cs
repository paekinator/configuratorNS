using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Tools / Parts list in the dock's left section, under the Build tab.
///
/// This is the control that used to be the dock's own nav row. The dock's tabs
/// are now Build / Blocks / Checkout, so the choice between the guided tools
/// and the parts palette moved down here, laid out like the mockup's left-hand
/// list: a stack of rows, the selected one filled.
///
/// It drives the same static <see cref="UIInteractionState.CurrentExperience"/>
/// as the per-page ExperienceTabs rows and listens to the same event, so every
/// control stays in sync no matter which is visible. Those per-page rows are
/// still present but inactive — GuidedBootstrap rebuilds a missing one AND
/// shifts its siblings down 56px to make room, which would break this layout.
/// </summary>
public class BuildColumnTabs : MonoBehaviour
{
    [System.Serializable]
    public class Row
    {
        public Button button;
        public TextMeshProUGUI label;
        public Image background;
        public UIInteractionState.Experience experience;
    }

    public List<Row> rows = new List<Row>();

    void Awake()
    {
        foreach (Row row in rows)
        {
            if (row.button == null)
                continue;

            Row captured = row;
            row.button.onClick.AddListener(() =>
                UIInteractionState.CurrentExperience = captured.experience);
        }
    }

    void OnEnable()
    {
        UIInteractionState.OnExperienceChanged += Refresh;
        UIThemeController.ThemeChanged += RefreshCurrent;
        Refresh(UIInteractionState.CurrentExperience);
    }

    void OnDisable()
    {
        UIInteractionState.OnExperienceChanged -= Refresh;
        UIThemeController.ThemeChanged -= RefreshCurrent;
    }

    /// <summary>
    /// Re-apply the rows' appearance from the current experience. Public
    /// because nothing else can drive it outside play mode: the subscription
    /// to OnExperienceChanged is made in OnEnable, so in the editor the rows
    /// never repaint and any check of their state reads whatever the builder
    /// happened to bake.
    /// </summary>
    public void RefreshCurrent() => Refresh(UIInteractionState.CurrentExperience);

    void Refresh(UIInteractionState.Experience current)
    {
        foreach (Row row in rows)
        {
            bool active = row.experience == current;

            // On the dock's black body: the selected row lifts to a lighter
            // fill with white text, the rest stay flat and muted.
            if (row.background != null)
                row.background.color = active
                    ? new Color(1f, 1f, 1f, 0.10f)
                    : new Color(1f, 1f, 1f, 0f);

            // Not MutedColor: that grey is tuned for the light chrome and
            // reads at about 3.9:1 on this near-black body. See UIChrome.
            if (row.label != null)
                row.label.color = active
                    ? UIThemeController.CardColor
                    : UIChrome.DockText;

            // The selected row does not offer a hover outline: its fill
            // already says it is selected.
            if (row.button != null &&
                row.button.TryGetComponent(out UIHoverReveal hover))
                hover.Suppressed = active;
        }
    }
}
