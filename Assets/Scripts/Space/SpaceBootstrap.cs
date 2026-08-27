using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime wiring for Space Mode: puts the factory, history, interaction,
/// mode controller and the "My Pieces" panel on a "SpaceTools" host, and
/// injects the Piece/Space mode button into the top bar (wiring the baked
/// one when present, cloning a history button otherwise). Same injection
/// pattern as the other bootstraps — existing scenes need no rebuild.
/// </summary>
public static class SpaceBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBootstrap()
    {
        var build = Object.FindFirstObjectByType<BuildController>();
        if (build == null)
            return;

        var host = GameObject.Find("SpaceTools");
        if (host == null)
            host = new GameObject("SpaceTools");

        var factory = Ensure<PieceInstanceFactory>(host);
        factory.buildController = build;

        var history = Ensure<SpaceHistory>(host);

        var interaction = Ensure<SpaceInteractionController>(host);
        interaction.buildController = build;
        interaction.factory = factory;
        interaction.history = history;

        var panel = Ensure<SpacePanelUI>(host);
        panel.interaction = interaction;

        var controller = Ensure<SpaceModeController>(host);
        controller.buildController = build;
        controller.interaction = interaction;
        controller.history = history;
        controller.spacePanel = panel;

        var editSession = Ensure<SpaceEditSession>(host);
        editSession.buildController = build;
        editSession.modeController = controller;
        editSession.interaction = interaction;
        editSession.history = history;

        interaction.editSession = editSession;
        controller.editSession = editSession;

        InjectModeButton(controller);
    }

    static T Ensure<T>(GameObject host) where T : Component
    {
        var component = host.GetComponent<T>();
        if (component == null)
            component = host.AddComponent<T>();
        return component;
    }

    static void InjectModeButton(SpaceModeController controller)
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        Transform bar = canvas != null ? canvas.transform.Find("TopBar") : null;
        if (bar == null)
            return;

        // Current builder: segmented Build | Space switch.
        Transform segBuild = bar.Find("ModeSwitch/Btn_ModeBuild");
        Transform segSpace = bar.Find("ModeSwitch/Btn_ModeSpace");
        if (segBuild != null && segSpace != null &&
            segBuild.TryGetComponent(out Button buildBtn) &&
            segSpace.TryGetComponent(out Button spaceBtn))
        {
            controller.RegisterModeButtons(buildBtn, spaceBtn);
            return;
        }

        // Previous builder: single "Space mode" pill.
        Transform baked = bar.Find("Btn_SpaceMode");
        if (baked != null && baked.TryGetComponent(out Button bakedBtn))
        {
            controller.RegisterModeButton(bakedBtn);
            return;
        }

        // Older scene: clone a history button for a matching look.
        Transform template = bar.Find("Btn_ClearAll");
        if (template == null)
            template = bar.Find("Btn_Undo");
        if (template == null || template.GetComponent<Button>() == null)
            return;

        GameObject go = Object.Instantiate(template.gameObject, bar);
        go.name = "Btn_SpaceMode";

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(944f, 0f);
        rt.sizeDelta = new Vector2(110f, 40f);

        var text = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
            text.text = "Space mode";

        var btn = go.GetComponent<Button>();
        btn.onClick = new Button.ButtonClickedEvent();

        var theme = Object.FindFirstObjectByType<UIThemeController>();
        if (theme != null)
        {
            var img = go.GetComponent<Image>();
            if (img != null)
                theme.surfaceImages.Add(img);
            if (text != null)
                theme.inkTexts.Add(text);
        }

        controller.RegisterModeButton(btn);
    }
}
