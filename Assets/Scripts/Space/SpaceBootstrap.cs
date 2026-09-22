using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime wiring for Lite (Space) mode: puts the factory, history,
/// interaction, mode controller and the piece panel on a "SpaceTools" host,
/// and wires the Pro | Lite switch the builder baked.
///
/// It only WIRES that switch now. It used to fall back to creating one, which
/// is a thing no bootstrap should do: a control built here has no styling the
/// builder knows about, and lands wherever this file guesses.
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
        if (canvas == null)
            return;

        Transform root = canvas.transform;

        // The Pro | Lite switch, located by UIChrome rather than by parent: it
        // used to be a top-bar child and now sits in the band above the dock,
        // and a parent-relative Find would return null after the move, leaving
        // a switch that does nothing.
        //
        // There is no fallback any more. Two used to follow — wire a single
        // "Space mode" pill from an older builder, or failing that CLONE a
        // history button into the top bar and call it Btn_SpaceMode. Neither
        // could fire: the builder has not produced a Btn_SpaceMode for a long
        // time, and the clone would have dropped a stray pill into a top bar
        // that no longer exists at all.
        Transform modeSwitch = UIChrome.ModeSwitch(root);
        if (modeSwitch == null)
            return;

        if (modeSwitch.Find(UIChrome.ProSegmentName).TryGetComponent(out Button proBtn) &&
            modeSwitch.Find(UIChrome.LiteSegmentName).TryGetComponent(out Button liteBtn))
            controller.RegisterModeButtons(proBtn, liteBtn);
    }
}
