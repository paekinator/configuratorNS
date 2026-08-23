using UnityEngine;

/// <summary>
/// Runtime wiring for the selection tools (marquee select, structure
/// clipboard, panel layer mover). Also removes the legacy top-bar
/// Build/Select switch — selection is now always available by dragging with
/// the left mouse button, so a separate mode is no longer needed.
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

        RemoveLegacySelectUi();
    }

    static void RemoveLegacySelectUi()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        // The legacy Build/Select segmented control: selection no longer is
        // a mode. Careful: the CURRENT top bar has a "ModeSwitch" too (the
        // Build | Space segments) — only the old one (Btn_Build/Btn_Select
        // children) may be destroyed.
        Transform bar = canvas.transform.Find("TopBar");
        Transform modeSwitch = bar != null ? bar.Find("ModeSwitch") : null;
        if (modeSwitch != null && modeSwitch.Find("Btn_ModeBuild") == null)
            Object.Destroy(modeSwitch.gameObject);

        // The retired Copy template button (superseded by the selection card).
        Transform staleCopy = canvas.transform.Find("GuidedToolsPanel/Btn_T4_Copy");
        if (staleCopy != null)
            Object.Destroy(staleCopy.gameObject);

        // The Frames/Beams/Twist tab row: the three category cards ARE the
        // categories now, so the tab track is dead weight. Let the card grid
        // grow into its space.
        Transform partsTabs = canvas.transform.Find("PartsPanel/Tabs");
        if (partsTabs != null)
        {
            Object.Destroy(partsTabs.gameObject);

            Transform scroll = canvas.transform.Find("PartsPanel/PartsScroll");
            if (scroll is RectTransform scrollRt)
                scrollRt.offsetMax = new Vector2(scrollRt.offsetMax.x, -131f);

            Transform subtitle = canvas.transform.Find("PartsPanel/Subtitle");
            if (subtitle != null &&
                subtitle.TryGetComponent(out TMPro.TextMeshProUGUI subtitleText))
                subtitleText.text = "Pick a part type · the size is chosen while placing";
        }

        // The bottom "Panel tool" button: the Panel tool is a card in the
        // grid now, so drop the button and give the grid its space.
        Transform panelToolBtn = canvas.transform.Find("PartsPanel/Btn_PanelTool");
        if (panelToolBtn != null)
        {
            Object.Destroy(panelToolBtn.gameObject);

            Transform scroll = canvas.transform.Find("PartsPanel/PartsScroll");
            if (scroll is RectTransform scrollRt)
                scrollRt.offsetMin = new Vector2(scrollRt.offsetMin.x, 16f);
        }

        UIInteractionState.CurrentMode = UIInteractionState.Mode.Build;
    }
}
