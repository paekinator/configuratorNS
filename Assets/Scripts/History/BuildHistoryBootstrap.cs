using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime wiring for <see cref="BuildHistory"/>: creates the component in any
/// scene and injects Undo / Redo / Clear all buttons into the top bar (styled
/// by cloning an existing top-bar button so old scenes match the current look).
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

        InjectTopBarButtons(history);
    }

    static void InjectTopBarButtons(BuildHistory history)
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        Transform bar = canvas != null ? canvas.transform.Find("TopBar") : null;
        if (bar == null)
            return;

        // Scenes rebuilt with the current builder bake the buttons (without
        // listeners — BuildHistory only exists at runtime). Just wire them.
        Transform baked = bar.Find("Btn_Undo");
        if (baked != null)
        {
            Wire("Btn_Undo", history.Undo);
            Wire("Btn_Redo", history.Redo);
            // Clear all sits in the ⋯ overflow menu (old scenes: top bar).
            Wire("MoreMenu/Btn_ClearAll", history.ClearAll);
            Wire("Btn_ClearAll", history.ClearAll);
            RemoveLegacyVeneerButtons(bar);
            return;

            void Wire(string name, UnityEngine.Events.UnityAction action)
            {
                Transform t = bar.Find(name);
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

        // Legacy scene: clone an existing surface-style button so fonts,
        // sprites and colors match.
        Transform template = bar.Find("Btn_ClearVeneer");
        if (template == null || template.GetComponent<Button>() == null)
            return;

        var theme = Object.FindFirstObjectByType<UIThemeController>();

        MakeButton("Btn_Undo", "Undo", 356f, 72f, history.Undo);
        MakeButton("Btn_Redo", "Redo", 434f, 72f, history.Redo);
        MakeButton("Btn_ClearAll", "Clear all", 512f, 104f, history.ClearAll);

        RemoveLegacyVeneerButtons(bar);

        void MakeButton(string name, string label, float x, float width, UnityEngine.Events.UnityAction action)
        {
            GameObject go = Object.Instantiate(template.gameObject, bar);
            go.name = name;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(width, 40f);

            var text = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
                text.text = label;

            var btn = go.GetComponent<Button>();
            btn.onClick = new Button.ButtonClickedEvent(); // drop cloned listeners
            btn.onClick.AddListener(action);

            if (theme != null)
            {
                var img = go.GetComponent<Image>();
                if (img != null)
                    theme.surfaceImages.Add(img);
                if (text != null)
                    theme.inkTexts.Add(text);
            }
        }
    }

    /// <summary>
    /// The top-bar veneer actions are gone — veneers ("Finish") live as a
    /// placeholder card in the Parts list now. Strip the baked buttons from
    /// old scenes and let the stats pill take their place.
    /// </summary>
    static void RemoveLegacyVeneerButtons(Transform bar)
    {
        bool removed = false;

        foreach (string name in new[] { "Btn_ApplyVeneer", "Btn_ClearVeneer" })
        {
            Transform t = bar.Find(name);
            if (t != null)
            {
                Object.Destroy(t.gameObject);
                removed = true;
            }
        }

        if (!removed)
            return;

        Transform stats = bar.Find("BuildStats");
        if (stats is RectTransform statsRt && statsRt.anchoredPosition.x < -300f)
            statsRt.anchoredPosition = new Vector2(-120f, statsRt.anchoredPosition.y);
    }
}
