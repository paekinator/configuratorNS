using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates the Guided (Tools tab) controllers at runtime and points them at
/// the panel the builder baked.
///
/// It no longer BUILDS any interface. It used to be able to construct the
/// whole tools panel from scratch — card, tab row, two tool buttons with
/// icons and captions, a title and a subtitle — and to "repair" a baked one by
/// renaming its title, rewriting its captions, moving its text columns and
/// deleting a retired Beams button. All of that was for scenes saved before
/// those things existed. Checked against the live scene before removing:
///
///     Btn_T1_Posts     label 'Frames', caption, icon, 1 persistent onClick
///     Btn_T3_PanelBay  label 'Panels', caption, icon, 1 persistent onClick
///     Btn_T2_Connectors / HintBox / Title / Subtitle / Btn_GuidedToggle
///                      all absent
///
/// The riskiest piece is gone with it: EnsureExperienceTabs rebuilt a missing
/// tab row AND shifted every top-anchored sibling down 56px to make space,
/// which would silently push a rebuilt dock out of place. The builder bakes
/// those rows, so it only ever re-styled them — but it was one missing object
/// away from rearranging the panel.
/// </summary>
[DefaultExecutionOrder(-50)]
public class GuidedBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBootstrap()
    {
        // Scenes rebuilt several times with "Rebuild UI" may contain duplicate
        // GuidedTemplates hosts; stale ones keep running the Posts tool in the
        // background even when another tool is selected. Remove them first.
        DestroyStaleGuidedHosts();

        // Scene already rebuilt with Guided wiring, or bootstrap already ran.
        if (FindFirstObjectByType<GuidedBootstrap>() != null ||
            FindFirstObjectByType<GuidedModeController>() != null)
            return;

        var host = new GameObject("GuidedTemplates");
        host.AddComponent<GuidedBootstrap>();
    }

    /// <summary>
    /// Keep exactly one Guided host: prefer the controller whose tools panel is
    /// still alive (the one wired to the current Canvas buttons) and destroy the
    /// rest. Stale hosts otherwise auto-arm the Posts tool and place posts on
    /// every ground click regardless of the selected template.
    /// </summary>
    static void DestroyStaleGuidedHosts()
    {
        var controllers = FindObjectsByType<GuidedModeController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (controllers.Length <= 1)
            return;

        GuidedModeController keep = null;
        foreach (var controller in controllers)
        {
            if (controller.guidedToolsPanel != null)
            {
                keep = controller;
                break;
            }
        }
        if (keep == null)
            keep = controllers[0];

        foreach (var controller in controllers)
        {
            if (controller != keep)
                DestroyImmediate(controller.gameObject);
        }
    }

    void Awake()
    {
        var build = FindFirstObjectByType<BuildController>();
        var session = GetComponent<TemplateSession>() ?? gameObject.AddComponent<TemplateSession>();
        session.buildController = build;
        if (build != null)
        {
            session.cam = build.cam != null ? build.cam : Camera.main;
            session.floorMask = build.floorMask;
        }

        var spawner = GetComponent<TemplateSpawner>() ?? gameObject.AddComponent<TemplateSpawner>();
        spawner.buildController = build;
        if (build != null)
            spawner.panelSlotManager = build.panelSlotManager;
        session.spawner = spawner;

        var guided = GetComponent<GuidedModeController>() ?? gameObject.AddComponent<GuidedModeController>();
        guided.buildController = build;
        guided.templateSession = session;
        guided.panelGhost = FindFirstObjectByType<PanelGhostController>();

        WireToBakedPanels(guided);
    }

    /// <summary>
    /// Point the controller at the panels and attach the tool actions. Nothing
    /// here creates or moves anything; the panels' visibility belongs to
    /// DockTabs, which swaps the dock's pages.
    /// </summary>
    static void WireToBakedPanels(GuidedModeController guided)
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        // PartsPanel is no longer wired here: GuidedModeController stopped
        // showing and hiding those panels, and DockTabs owns that alone.
        Transform tools = UIChrome.FindPanel(canvas.transform, "GuidedToolsPanel");
        if (tools == null)
            return;

        guided.guidedToolsPanel = tools.gameObject;

        BindClick(tools.Find("Btn_T1_Posts"), guided.SelectPostsTool);
        BindClick(tools.Find("Btn_T3_PanelBay"), guided.SelectPanelBayTool);
    }

    static void BindClick(Transform t, UnityEngine.Events.UnityAction action)
    {
        var button = t != null ? t.GetComponent<Button>() : null;
        if (button == null)
            return;

        // The builder persists SelectPostsTool on these buttons. A second
        // runtime listener would fire the toggle twice — select, then
        // immediately deselect. Skip only while a persistent target is still
        // alive; a destroyed host leaves a dead persistent call to replace.
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            if (button.onClick.GetPersistentTarget(i) != null)
                return;
        }

        button.onClick.AddListener(action);
    }
}
