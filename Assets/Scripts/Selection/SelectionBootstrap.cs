using UnityEngine;

/// <summary>
/// Runtime wiring for the selection tools (marquee select, structure
/// clipboard, panel layer mover).
///
/// It used to demolish leftovers from an older interface as well — the
/// top-bar Build/Select switch, a retired Copy button, the Frames/Beams/Twist
/// tab row, a bottom Panel-tool button — re-laying the parts gallery around
/// each one it removed. The builder stopped producing any of those some time
/// ago, so all of it was searching for things that no longer exist. Removed:
/// see the commit for the check that confirmed each target was already gone.
/// </summary>
public static class SelectionBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBootstrap()
    {
        var build = Object.FindFirstObjectByType<BuildController>();
        if (build == null)
            return;

        Camera cam = build.cam != null ? build.cam : Camera.main;

        GameObject host = GameObject.Find("SelectionTools");
        if (host == null)
            host = new GameObject("SelectionTools");

        var ghosts = host.GetComponent<TemplateGhostPreview>() ?? host.AddComponent<TemplateGhostPreview>();
        ghosts.buildController = build;

        var spawner = host.GetComponent<TemplateSpawner>() ?? host.AddComponent<TemplateSpawner>();
        spawner.buildController = build;
        spawner.panelSlotManager = build.panelSlotManager;

        var clipboard = host.GetComponent<StructureClipboard>() ?? host.AddComponent<StructureClipboard>();
        clipboard.buildController = build;
        clipboard.spawner = spawner;
        clipboard.panelSlotManager = build.panelSlotManager;
        clipboard.cam = cam;
        clipboard.ghostPreview = ghosts;

        // The Length action's height scale gets its own guide so it never
        // fights the guided session's or the free tool's measure lines.
        var resizeGuide = host.GetComponent<TemplatePreviewGuide>() ?? host.AddComponent<TemplatePreviewGuide>();

        var resize = host.GetComponent<BeamResizeSession>() ?? host.AddComponent<BeamResizeSession>();
        resize.buildController = build;
        resize.cam = cam;
        resize.guide = resizeGuide;
        resize.ghosts = ghosts;
        resize.panelSlotManager = build.panelSlotManager;

        var marquee = host.GetComponent<MarqueeSelectionController>() ?? host.AddComponent<MarqueeSelectionController>();
        marquee.cam = cam;
        marquee.buildController = build;
        marquee.panelSlotManager = build.panelSlotManager;
        marquee.clipboard = clipboard;
        marquee.resize = resize;

        var gizmo = host.GetComponent<MoveGizmoController>() ?? host.AddComponent<MoveGizmoController>();
        gizmo.cam = cam;
        gizmo.selection = marquee;
        gizmo.buildController = build;
        gizmo.panelSlotManager = build.panelSlotManager;

        var markers = host.GetComponent<AttachmentMarkerController>() ?? host.AddComponent<AttachmentMarkerController>();
        markers.cam = cam;
        markers.buildController = build;

        var mover = host.GetComponent<PanelLayerMover>() ?? host.AddComponent<PanelLayerMover>();
        mover.cam = cam;
        mover.buildController = build;
        mover.panelSlotManager = build.panelSlotManager;
        mover.panelGhost = Object.FindFirstObjectByType<PanelGhostController>();
        mover.templateSession = Object.FindFirstObjectByType<TemplateSession>();
        mover.ghostPreview = ghosts;

        // Category part tools (Upright / Crossbar / Twist bar) live on their
        // own host with a private guide + ghost preview, so they never fight
        // the clipboard's ghosts or the guided session's measure line.
        GameObject freeHost = GameObject.Find("FreePartTool");
        if (freeHost == null)
            freeHost = new GameObject("FreePartTool");

        var freeGuide = freeHost.GetComponent<TemplatePreviewGuide>() ?? freeHost.AddComponent<TemplatePreviewGuide>();
        var freeGhosts = freeHost.GetComponent<TemplateGhostPreview>() ?? freeHost.AddComponent<TemplateGhostPreview>();
        freeGhosts.buildController = build;

        var freeSession = freeHost.GetComponent<FreePartSession>() ?? freeHost.AddComponent<FreePartSession>();
        freeSession.buildController = build;
        freeSession.cam = cam;
        freeSession.guide = freeGuide;
        freeSession.ghosts = freeGhosts;
        freeSession.spawner = spawner;

        // Selection is not a mode — it is always available by dragging with
        // the left mouse button — so the session always starts in Build.
        UIInteractionState.CurrentMode = UIInteractionState.Mode.Build;
    }
}
