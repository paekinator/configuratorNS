using TMPro;
using UnityEngine;

/// <summary>
/// The dock footer's standing description of the open page.
///
/// This line used to sit in the dock's left column under a "Tools"/"Parts"
/// heading. That column now holds the Tools/Parts list itself, so the
/// description moved down here where there is a full-width line for it.
///
/// It is deliberately NOT the live step guidance. That belongs to the status
/// pill above the dock, which also carries selection counts and placement
/// errors; showing it here as well put the same sentence on screen twice.
/// This line changes only when the page does.
///
/// It followed the Tools/Parts switch alone at first, which was right while
/// Build was the only page with anything on it. Once Blocks had a gallery,
/// the footer went on saying "Pick a tool, then follow the steps in the bar
/// above" underneath a shelf of blocks — describing a page that was not on
/// screen, which is worse than describing nothing.
/// </summary>
public class DockFooterCaption : MonoBehaviour
{
    public TextMeshProUGUI caption;

    [Tooltip("Assigned by the builder; found at runtime if it is missing.")]
    public DockTabs dockTabs;

    public string toolsCaption = "Pick a tool, then follow the steps in the bar above";
    public string partsCaption = "Pick a part type — the size is chosen while placing";

    /// <summary>
    /// Pro's Blocks tab can make a block as well as place one, so it says so.
    /// Lite's can only place, and borrows the mockup's own wording.
    /// </summary>
    public string blocksProCaption = "Click a block to place it, or add one from a module in your scene";
    public string blocksLiteCaption = "Pick a block to add it to your scene";
    public string styleCaption = "Colour themes for panels and veneers";
    public string checkoutCaption = "Your parts list and order summary";

    void OnEnable()
    {
        if (dockTabs == null)
            dockTabs = FindFirstObjectByType<DockTabs>();

        UIInteractionState.OnExperienceChanged += OnExperienceChanged;
        if (dockTabs != null)
            dockTabs.Changed += Refresh;

        Refresh();
    }

    void OnDisable()
    {
        UIInteractionState.OnExperienceChanged -= OnExperienceChanged;
        if (dockTabs != null)
            dockTabs.Changed -= Refresh;
    }

    void OnExperienceChanged(UIInteractionState.Experience _) => Refresh();

    void Refresh()
    {
        if (caption == null)
            return;

        // Without the dock there is only one page worth describing, and it is
        // the Build one.
        if (dockTabs == null)
        {
            caption.text = BuildCaption();
            return;
        }

        switch (dockTabs.CurrentPage)
        {
            case DockTabs.Page.Build:
                caption.text = BuildCaption();
                break;

            case DockTabs.Page.Blocks:
            case DockTabs.Page.LiteBlocks:
                caption.text = dockTabs.CurrentMode == DockTabs.Mode.Lite
                    ? blocksLiteCaption
                    : blocksProCaption;
                break;

            case DockTabs.Page.Style:
                caption.text = styleCaption;
                break;

            case DockTabs.Page.Checkout:
                caption.text = checkoutCaption;
                break;
        }
    }

    string BuildCaption() =>
        UIInteractionState.CurrentExperience == UIInteractionState.Experience.Guided
            ? toolsCaption
            : partsCaption;
}
