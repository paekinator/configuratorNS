using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime wiring for <see cref="BuildHistory"/>: creates the component in any
/// scene and attaches Undo / Redo / Clear all to the buttons the builder baked.
///
/// It no longer BUILDS those buttons. It used to clone a top-bar veneer button
/// when it could not find them, and strip two other veneer buttons on the way
/// past — all of which the builder stopped producing long ago.
/// </summary>
public static class BuildHistoryBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBootstrap()
    {
        var build = Object.FindFirstObjectByType<BuildController>();
        if (build == null)
            return;

        GameObject host = GameObject.Find("SelectionTools");
        if (host == null)
            host = new GameObject("SelectionTools");

        var history = host.GetComponent<BuildHistory>() ?? host.AddComponent<BuildHistory>();
        history.buildController = build;
        history.panelSlotManager = build.panelSlotManager;

        WireHistoryButtons(history);
    }

    /// <summary>
    /// Attach the history actions to the buttons the builder baked. The
    /// builder cannot bake these listeners itself: BuildHistory is created at
    /// runtime, so there is no object for a saved reference to point at.
    /// </summary>
    static void WireHistoryButtons(BuildHistory history)
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        Transform root = canvas.transform;

        Wire("Btn_Undo", history.Undo);
        Wire("Btn_Redo", history.Redo);

        // No Clear all button to wire. It lived in the ⋯ menu, which is
        // retired; emptying the scene is "New project" in My Projects, which
        // calls ClearAll itself and also detaches the project — so the next
        // save cannot quietly overwrite it with nothing. ClearAll stays
        // public and undoable; only the button has gone.

        void Wire(string path, UnityEngine.Events.UnityAction action)
        {
            // Resolved from the canvas, never from the top bar. Undo and Redo
            // live in the utility rail; this method used to bail out entirely
            // if it could not find a TopBar, so dissolving that bar — which
            // has since happened — would have silently unwired both.
            Transform t = path.Contains("/") ? UIChrome.FindPanel(root, path)
                                             : UIChrome.FindButton(root, path);
            var b = t != null ? t.GetComponent<Button>() : null;
            if (b == null)
                return;

            // A confirming button ("Sure?" step) fires from onConfirmed;
            // its own onClick is the arming step.
            if (t.TryGetComponent(out UIConfirmingButton confirming))
            {
                confirming.onConfirmed.RemoveListener(action);
                confirming.onConfirmed.AddListener(action);
                return;
            }

            b.onClick.RemoveListener(action); // no doubles on domain reloads
            b.onClick.AddListener(action);
        }
    }
}
