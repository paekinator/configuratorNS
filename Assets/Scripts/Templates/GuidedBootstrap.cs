using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ensures Guided template controllers exist at runtime even if the scene UI
/// was built before Guided tools existed. Safe no-op when already wired.
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

        // Always migrate scenes saved with the old top-bar "Mode" toggle to the
        // left-panel Templates/Parts tabs, even when Guided wiring already exists.
        MigrateLegacyExperienceUi();

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

    // Style adopted from the scene's existing parts panel so everything the
    // bootstrap creates at runtime matches the built design (rounded card
    // sprite, corner radius, Inter font) instead of falling back to the plain
    // square default UI look.
    static Sprite _cardSprite;
    static float _cardPpuMultiplier = 1f;
    static TMP_FontAsset _cardFont;

    static readonly Color Surface = new Color(0.953f, 0.937f, 0.914f, 1f);
    static readonly Color Ink = new Color(0.149f, 0.133f, 0.118f, 1f);
    static readonly Color Muted = new Color(0.561f, 0.533f, 0.502f, 1f);

    /// <summary>
    /// Scenes built before the tabs existed have a "Mode: Expert" button on the
    /// top bar and no tab row on the left panels. Remove the button and add the
    /// tabs at runtime so the scene works without a UI rebuild.
    /// </summary>
    static void MigrateLegacyExperienceUi()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        Transform legacyToggle = canvas.transform.Find("TopBar/Btn_GuidedToggle");
        if (legacyToggle == null)
            legacyToggle = canvas.transform.Find("Btn_GuidedToggle");
        if (legacyToggle != null)
            Destroy(legacyToggle.gameObject);

        Transform partsPanel = canvas.transform.Find("PartsPanel");
        AdoptCardStyle(partsPanel);
        EnsureExperienceTabs(partsPanel);
        EnsureExperienceTabs(canvas.transform.Find("GuidedToolsPanel"));
    }

    /// <summary>
    /// Remember the panel's rounded sprite and font so runtime-created UI can
    /// reuse them and blend in with the built design.
    /// </summary>
    static void AdoptCardStyle(Transform panel)
    {
        if (panel == null)
            return;

        var img = panel.GetComponent<Image>();
        if (img != null && img.sprite != null)
        {
            _cardSprite = img.sprite;
            _cardPpuMultiplier = img.pixelsPerUnitMultiplier;
        }

        var text = panel.GetComponentInChildren<TMP_Text>(true);
        if (text != null && text.font != null)
            _cardFont = text.font;
    }

    /// <summary>Apply the adopted rounded-card sprite to an image, if available.</summary>
    static void StyleAsCard(Image img, float cornerScale = 1f)
    {
        if (img == null || _cardSprite == null)
            return;

        img.sprite = _cardSprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = _cardPpuMultiplier * cornerScale;
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

        EnsureUi(guided, session);
    }

    static void EnsureUi(GuidedModeController guided, TemplateSession session)
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        // Find existing expert parts panel
        Transform partsPanel = canvas.transform.Find("PartsPanel");
        if (partsPanel != null)
            guided.expertPartsPanel = partsPanel.gameObject;
        AdoptCardStyle(partsPanel);

        Transform existingGuided = canvas.transform.Find("GuidedToolsPanel");
        if (existingGuided != null)
        {
            guided.guidedToolsPanel = existingGuided.gameObject;
            WireExistingButtons(existingGuided, guided);
            var hint = existingGuided.Find("HintBox/Txt_GuidedHint");
            if (hint == null)
                hint = existingGuided.Find("Txt_GuidedHint");
            if (hint != null)
                guided.guidedHintText = hint.GetComponent<TextMeshProUGUI>();
            existingGuided.gameObject.SetActive(false);

            // Scenes baked before the rename still use the old wording.
            // (The bottom-right shortcut pill is handled by HintPillBootstrap,
            // which runs in every scene regardless of guided wiring.)
            RenameGuidedTitle(existingGuided);
            PolishToolButtons(existingGuided);

            EnsureExperienceTabs(partsPanel);
            EnsureExperienceTabs(existingGuided);
            return;
        }

        // Build a lightweight Templates panel with the Templates/Parts tabs on top.
        // It clones the parts panel's card look (rounded sprite, color, rect,
        // shadow) so the two left panels are visually identical.
        GameObject panelGo = new GameObject("GuidedToolsPanel", typeof(RectTransform), typeof(Image));
        panelGo.transform.SetParent(canvas.transform, false);
        var panelRt = (RectTransform)panelGo.transform;
        panelRt.anchorMin = new Vector2(0f, 0f);
        panelRt.anchorMax = new Vector2(0f, 1f);
        panelRt.pivot = new Vector2(0f, 0.5f);
        panelRt.offsetMin = new Vector2(24f, 24f);
        panelRt.offsetMax = new Vector2(24f + 336f, -108f);

        var panelImg = panelGo.GetComponent<Image>();
        panelImg.color = Color.white;
        StyleAsCard(panelImg);

        if (partsPanel is RectTransform partsRt)
        {
            // Sit exactly where the parts panel sits.
            panelRt.anchorMin = partsRt.anchorMin;
            panelRt.anchorMax = partsRt.anchorMax;
            panelRt.pivot = partsRt.pivot;
            panelRt.offsetMin = partsRt.offsetMin;
            panelRt.offsetMax = partsRt.offsetMax;

            var partsImg = partsPanel.GetComponent<Image>();
            if (partsImg != null)
                panelImg.color = partsImg.color;

            Transform shadow = partsPanel.Find("Shadow");
            if (shadow != null)
            {
                Transform shadowClone = Instantiate(shadow, panelGo.transform);
                shadowClone.name = "Shadow";
                shadowClone.SetAsFirstSibling();
            }
        }

        // Add the tab row before the content: this panel's layout already
        // leaves room for it, so nothing must be shifted down afterwards.
        EnsureExperienceTabs(panelGo.transform);

        CreateLabel(panelGo.transform, "Title", "Tools", 20f, new Vector2(20f, -74f), new Vector2(280f, 26f));
        CreateLabel(panelGo.transform, "Subtitle", "Pick a tool, then follow the steps shown below", 12.5f,
            new Vector2(20f, -101f), new Vector2(300f, 18f), new Color(0.56f, 0.53f, 0.5f));

        // No Beams tool: the Parts tab's Horizontal beam card covers single
        // beam placement, so the guided panel keeps only Frames and Panels.
        Button posts = CreateToolButton(panelGo.transform, "Btn_T1_Posts",
            "Frames", "Stand frames on the grid · 4 clicks", -131f,
            Resources.Load<Sprite>("UI/FrameToolIcon"));
        Button bay = CreateToolButton(panelGo.transform, "Btn_T3_PanelBay",
            "Panels", "Add panels between frames · 3 clicks", -197f,
            Resources.Load<Sprite>("UI/PanelToolIcon"));

        posts.onClick.AddListener(guided.SelectPostsTool);
        bay.onClick.AddListener(guided.SelectPanelBayTool);

        // Hint in an inset box so live feedback reads as part of the design.
        var hintBoxGo = new GameObject("HintBox", typeof(RectTransform), typeof(Image));
        hintBoxGo.transform.SetParent(panelGo.transform, false);
        var hintBoxRt = (RectTransform)hintBoxGo.transform;
        hintBoxRt.anchorMin = new Vector2(0f, 0f);
        hintBoxRt.anchorMax = new Vector2(1f, 0f);
        hintBoxRt.pivot = new Vector2(0.5f, 0f);
        hintBoxRt.offsetMin = new Vector2(16f, 16f);
        hintBoxRt.offsetMax = new Vector2(-16f, 126f);
        var hintBoxImg = hintBoxGo.GetComponent<Image>();
        hintBoxImg.color = Surface;
        StyleAsCard(hintBoxImg, 1.4f);

        var hintGo = CreateLabel(hintBoxGo.transform, "Txt_GuidedHint", "Choose a tool.", 12.5f,
            new Vector2(14f, -12f), new Vector2(276f, 86f), new Color(0.35f, 0.32f, 0.28f));
        hintGo.textWrappingMode = TMPro.TextWrappingModes.Normal;
        guided.guidedHintText = hintGo;

        guided.guidedToolsPanel = panelGo;
        panelGo.SetActive(false);

        EnsureExperienceTabs(partsPanel);
    }

    static void WireExistingButtons(Transform root, GuidedModeController guided)
    {
        Button t1 = root.Find("Btn_T1_Posts")?.GetComponent<Button>();
        Button t3 = root.Find("Btn_T3_PanelBay")?.GetComponent<Button>();
        if (t1 != null) t1.onClick.AddListener(guided.SelectPostsTool);
        if (t3 != null) t3.onClick.AddListener(guided.SelectPanelBayTool);

        // The Beams tool is retired (redundant with the Horizontal beam card
        // in Parts): remove it from baked scenes and close the gap.
        Transform t2 = root.Find("Btn_T2_Connectors");
        if (t2 != null)
        {
            Object.Destroy(t2.gameObject);
            if (t3 != null && t3.transform is RectTransform t3Rt)
                t3Rt.anchoredPosition = new Vector2(t3Rt.anchoredPosition.x, -197f);
        }
    }

    /// <summary>
    /// Add a "Tools | Parts" tab row to the top of a left panel that was
    /// built before the tabs existed, pushing the old content down to fit.
    /// Existing rows are polished in place (rename + icons).
    /// </summary>
    static void EnsureExperienceTabs(Transform panel)
    {
        const float rowHeight = 56f;

        if (panel == null)
            return;

        Transform existingRow = panel.Find("ExperienceTabs");
        if (existingRow != null)
        {
            PolishTabRow(existingRow);
            return;
        }

        // Make room: shift top-anchored children down, shrink stretched ones.
        // The drop shadow hugs the whole card and must never be resized.
        foreach (Transform child in panel)
        {
            var rt = child as RectTransform;
            if (rt == null || child.name == "Shadow")
                continue;

            if (Mathf.Approximately(rt.anchorMin.y, 1f) && Mathf.Approximately(rt.anchorMax.y, 1f))
                rt.anchoredPosition += new Vector2(0f, -rowHeight);
            else if (Mathf.Approximately(rt.anchorMax.y, 1f) && rt.anchorMin.y < 1f)
                rt.offsetMax += new Vector2(0f, -rowHeight);
        }

        // Inset segmented track; the active tab is an ink pill (see ExperienceTabs).
        var rowGo = new GameObject("ExperienceTabs", typeof(RectTransform), typeof(Image));
        rowGo.transform.SetParent(panel, false);
        var row = (RectTransform)rowGo.transform;
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.offsetMin = new Vector2(16f, 0f);
        row.offsetMax = new Vector2(-16f, 0f);
        row.anchoredPosition = new Vector2(0f, -16f);
        row.sizeDelta = new Vector2(row.sizeDelta.x, 44f);

        var rowImg = rowGo.GetComponent<Image>();
        rowImg.color = Surface;
        StyleAsCard(rowImg, 0.9f);

        Button templates = CreateTabButton(row, "Btn_Tab_Templates", "Tools", true);
        Button parts = CreateTabButton(row, "Btn_Tab_Parts", "Parts", false);

        var tabs = rowGo.AddComponent<ExperienceTabs>();
        tabs.templatesButton = templates;
        tabs.partsButton = parts;
        tabs.templatesBg = templates.GetComponent<Image>();
        tabs.partsBg = parts.GetComponent<Image>();
        tabs.templatesLabel = templates.GetComponentInChildren<TextMeshProUGUI>(true);
        tabs.partsLabel = parts.GetComponentInChildren<TextMeshProUGUI>(true);

        PolishTabRow(row);
    }

    /// <summary>
    /// Bring a tab row (fresh or baked by an older UI build) up to the current
    /// design: "Tools" wording plus tool/parts icons, correctly tinted.
    /// </summary>
    static void PolishTabRow(Transform row)
    {
        Button templates = row.Find("Btn_Tab_Templates")?.GetComponent<Button>();
        Button parts = row.Find("Btn_Tab_Parts")?.GetComponent<Button>();

        Sprite toolIcon = Resources.Load<Sprite>("UI/ToolIcon");
        Sprite partsIcon = Resources.Load<Sprite>("UI/PartsIcon");

        Image toolImg = ExperienceTabs.ApplyTabStyle(templates, "Tools", toolIcon);
        Image partsImg = ExperienceTabs.ApplyTabStyle(parts, "Parts", partsIcon);

        var tabs = row.GetComponent<ExperienceTabs>();
        if (tabs != null)
        {
            if (toolImg != null) tabs.templatesIcon = toolImg;
            if (partsImg != null) tabs.partsIcon = partsImg;
            tabs.RefreshVisuals();
        }
    }

    static void RenameGuidedTitle(Transform guidedPanel)
    {
        Transform title = guidedPanel != null ? guidedPanel.Find("Title") : null;
        var tmp = title != null ? title.GetComponent<TextMeshProUGUI>() : null;
        if (tmp != null && tmp.text == "Templates")
            tmp.text = "Tools";
    }

    /// <summary>Bring baked tool buttons up to the current wording and icons.</summary>
    static void PolishToolButtons(Transform guidedPanel)
    {
        SetLabelAndCaption(guidedPanel, "Btn_T1_Posts",
            "Frames", "Stand frames on the grid · 4 clicks", "UI/FrameToolIcon");
        SetLabelAndCaption(guidedPanel, "Btn_T3_PanelBay",
            "Panels", "Add panels between frames · 3 clicks", "UI/PanelToolIcon");

        var subtitle = guidedPanel.Find("Subtitle")?.GetComponent<TextMeshProUGUI>();
        if (subtitle != null)
            subtitle.text = "Pick a tool, then follow the steps shown below";
    }

    static void SetLabelAndCaption(Transform panel, string buttonName, string title, string caption,
        string iconResource = null)
    {
        Transform button = panel.Find(buttonName);
        if (button == null)
            return;

        float textX = 16f;
        if (!string.IsNullOrEmpty(iconResource))
        {
            Sprite icon = Resources.Load<Sprite>(iconResource);
            if (icon != null)
            {
                AddToolButtonIcon(button, icon);
                textX = 52f;
            }
        }

        var label = button.Find("Label")?.GetComponent<TextMeshProUGUI>();
        if (label != null)
        {
            label.text = title;
            MoveTextColumn(label.rectTransform, textX);
        }

        var cap = button.Find("Caption")?.GetComponent<TextMeshProUGUI>();
        if (cap != null)
        {
            cap.text = caption;
            MoveTextColumn(cap.rectTransform, textX);
        }
    }

    static void MoveTextColumn(RectTransform rt, float x)
    {
        Vector2 pos = rt.anchoredPosition;
        float shift = x - pos.x;
        if (Mathf.Approximately(shift, 0f))
            return;
        pos.x = x;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(rt.sizeDelta.x - shift, rt.sizeDelta.y);
    }

    static Button CreateTabButton(RectTransform row, string name, string label, bool leftHalf)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(row, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = leftHalf ? new Vector2(0f, 0f) : new Vector2(0.5f, 0f);
        rt.anchorMax = leftHalf ? new Vector2(0.5f, 1f) : new Vector2(1f, 1f);
        rt.offsetMin = leftHalf ? new Vector2(4f, 4f) : new Vector2(2f, 4f);
        rt.offsetMax = leftHalf ? new Vector2(-2f, -4f) : new Vector2(-4f, -4f);

        StyleAsCard(go.GetComponent<Image>(), 1.1f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = go.GetComponent<Image>();
        btn.transition = Selectable.Transition.None;

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var textRt = (RectTransform)textGo.transform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        if (_cardFont != null)
            tmp.font = _cardFont;
        tmp.fontSize = 14.5f;
        tmp.alignment = TextAlignmentOptions.Midline;
        tmp.raycastTarget = false;
        return btn;
    }

    static TextMeshProUGUI CreateLabel(
        Transform parent,
        string name,
        string text,
        float size,
        Vector2 anchoredPos,
        Vector2 sizeDelta,
        Color? color = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        if (_cardFont != null)
            tmp.font = _cardFont;
        tmp.fontSize = size;
        tmp.color = color ?? new Color(0.15f, 0.13f, 0.12f);
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.raycastTarget = false;
        return tmp;
    }

    static Button CreateToolButton(Transform parent, string name, string title, string caption, float y,
        Sprite icon = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.offsetMin = new Vector2(16f, y - 56f);
        rt.offsetMax = new Vector2(-16f, y);

        var img = go.GetComponent<Image>();
        img.color = Surface;
        StyleAsCard(img, 1.4f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;

        float textX = icon != null ? 52f : 16f;
        if (icon != null)
            AddToolButtonIcon(go.transform, icon);

        var titleGo = new GameObject("Label", typeof(RectTransform));
        titleGo.transform.SetParent(go.transform, false);
        var titleRt = (RectTransform)titleGo.transform;
        titleRt.anchorMin = titleRt.anchorMax = new Vector2(0f, 1f);
        titleRt.pivot = new Vector2(0f, 1f);
        titleRt.anchoredPosition = new Vector2(textX, -10f);
        titleRt.sizeDelta = new Vector2(260f - (textX - 16f), 20f);
        var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text = title;
        if (_cardFont != null)
            titleTmp.font = _cardFont;
        titleTmp.fontSize = 15f;
        titleTmp.color = Ink;
        titleTmp.alignment = TextAlignmentOptions.TopLeft;
        titleTmp.raycastTarget = false;

        var capGo = new GameObject("Caption", typeof(RectTransform));
        capGo.transform.SetParent(go.transform, false);
        var capRt = (RectTransform)capGo.transform;
        capRt.anchorMin = capRt.anchorMax = new Vector2(0f, 1f);
        capRt.pivot = new Vector2(0f, 1f);
        capRt.anchoredPosition = new Vector2(textX, -31f);
        capRt.sizeDelta = new Vector2(272f - (textX - 16f), 16f);
        var capTmp = capGo.AddComponent<TextMeshProUGUI>();
        capTmp.text = caption;
        if (_cardFont != null)
            capTmp.font = _cardFont;
        capTmp.fontSize = 11.5f;
        capTmp.color = Muted;
        capTmp.alignment = TextAlignmentOptions.TopLeft;
        capTmp.raycastTarget = false;

        return btn;
    }

    /// <summary>Left-aligned glyph on a tool card (idempotent for baked buttons).</summary>
    static void AddToolButtonIcon(Transform button, Sprite icon)
    {
        if (icon == null || button.Find("Icon") != null)
            return;

        var iconGo = new GameObject("Icon", typeof(RectTransform));
        iconGo.transform.SetParent(button, false);
        var iconRt = (RectTransform)iconGo.transform;
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.anchoredPosition = new Vector2(14f, 0f);
        iconRt.sizeDelta = new Vector2(26f, 26f);

        var iconImg = iconGo.AddComponent<Image>();
        iconImg.sprite = icon;
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        iconImg.color = Ink;
    }
}
