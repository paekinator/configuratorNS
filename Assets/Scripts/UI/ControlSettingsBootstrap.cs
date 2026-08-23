using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime migration for scenes saved before the camera-control settings
/// existed: adds the CAD camera controller + scheme manager to the camera and
/// injects the gear button + controls card into the top bar. Safe no-op when
/// the scene was rebuilt with the editor UI builder.
/// </summary>
public static class ControlSettingsBootstrap
{
    static readonly Color Ink = new Color(0.149f, 0.133f, 0.118f);
    static readonly Color Surface = new Color(0.953f, 0.937f, 0.914f);
    static readonly Color Muted = new Color(0.561f, 0.533f, 0.502f);

    static Sprite _cardSprite;
    static float _cardPpu = 1f;
    static TMP_FontAsset _font;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBootstrap()
    {
        CameraControlManager manager = EnsureCameraComponents();
        if (manager == null)
            return;

        EnsureUi(manager);
    }

    static CameraControlManager EnsureCameraComponents()
    {
        var fly = Object.FindFirstObjectByType<FlyCameraController>(FindObjectsInactive.Include);
        GameObject camGo = fly != null ? fly.gameObject
            : Camera.main != null ? Camera.main.gameObject : null;
        if (camGo == null)
            return null;

        var cad = camGo.GetComponent<CadCameraController>();
        if (cad == null)
        {
            cad = camGo.AddComponent<CadCameraController>();
            cad.enabled = false;
        }

        var manager = camGo.GetComponent<CameraControlManager>();
        if (manager == null)
        {
            manager = camGo.AddComponent<CameraControlManager>();
            manager.walkthroughController = fly;
            manager.cadController = cad;
        }
        return manager;
    }

    static void EnsureUi(CameraControlManager manager)
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        Transform bar = canvas.transform.Find("TopBar");
        if (bar == null || bar.Find("Btn_Settings") != null)
            return;

        AdoptCardStyle(canvas.transform.Find("PartsPanel"));

        // Make room at the right edge of the bar: everything that currently
        // hugs the right side moves 48px left so the gear can sit outermost.
        string[] shifted = { "Btn_Theme", "Btn_ApplyVeneer", "Btn_ClearVeneer", "BuildStats" };
        foreach (string name in shifted)
        {
            var rt = bar.Find(name) as RectTransform;
            if (rt != null)
                rt.anchoredPosition += new Vector2(-48f, 0f);
        }

        // Gear button
        Button gear = CreateButton(bar, "Btn_Settings", string.Empty, Surface, Ink, 1.7f);
        var gearRt = (RectTransform)gear.transform;
        gearRt.anchorMin = gearRt.anchorMax = new Vector2(1f, 0.5f);
        gearRt.pivot = new Vector2(1f, 0.5f);
        gearRt.anchoredPosition = new Vector2(-20f, 0f);
        gearRt.sizeDelta = new Vector2(40f, 40f);

        Sprite gearSprite = Resources.Load<Sprite>("UI/GearIcon");
        if (gearSprite != null)
        {
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(gear.transform, false);
            var iconRt = (RectTransform)iconGo.transform;
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta = new Vector2(20f, 20f);
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.sprite = gearSprite;
            iconImg.color = Muted;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
        }
        else
        {
            SetLabel(gear, "...");
        }

        // Controls card, top-right under the bar
        var panelGo = new GameObject("ControlSettingsPanel", typeof(RectTransform), typeof(Image));
        panelGo.transform.SetParent(canvas.transform, false);
        var panelRt = (RectTransform)panelGo.transform;
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(1f, 1f);
        panelRt.pivot = new Vector2(1f, 1f);
        panelRt.anchoredPosition = new Vector2(-24f, -96f);
        panelRt.sizeDelta = new Vector2(340f, 344f);
        var panelImg = panelGo.GetComponent<Image>();
        panelImg.color = Color.white;
        StyleAsCard(panelImg, 1f);

        CreateLabel(panelGo.transform, "Title", "Controls", 18f, Ink,
            new Vector2(20f, -18f), new Vector2(200f, 26f));

        Button walk = CreateButton(panelGo.transform, "Btn_SchemeWalkthrough", "Walkthrough", Surface, Ink, 1.7f);
        PlaceTop((RectTransform)walk.transform, -54f, new Vector2(300f, 44f));

        Button cad = CreateButton(panelGo.transform, "Btn_SchemeCad", "Professional (CAD)", Surface, Ink, 1.7f);
        PlaceTop((RectTransform)cad.transform, -104f, new Vector2(300f, 44f));

        TextMeshProUGUI legend = CreateLabel(panelGo.transform, "Txt_Legend", string.Empty, 13.5f, Muted,
            new Vector2(20f, -162f), new Vector2(300f, 166f));

        var ui = panelGo.AddComponent<UIControlSettings>();
        ui.manager = manager;
        ui.panel = panelGo;
        ui.walkthroughBg = walk.GetComponent<Image>();
        ui.cadBg = cad.GetComponent<Image>();
        ui.walkthroughLabel = walk.GetComponentInChildren<TextMeshProUGUI>(true);
        ui.cadLabel = cad.GetComponentInChildren<TextMeshProUGUI>(true);
        ui.legendText = legend;
        // Selection colors come from the live theme inside UIControlSettings.

        gear.onClick.AddListener(ui.TogglePanel);
        walk.onClick.AddListener(ui.SelectWalkthrough);
        cad.onClick.AddListener(ui.SelectCad);

        panelGo.SetActive(false);
    }

    // ------------------------------------------------------------------
    // Styling helpers (mirror the parts panel so injected UI blends in)
    // ------------------------------------------------------------------

    static void AdoptCardStyle(Transform panel)
    {
        if (panel == null)
            return;

        var img = panel.GetComponent<Image>();
        if (img != null && img.sprite != null)
        {
            _cardSprite = img.sprite;
            _cardPpu = img.pixelsPerUnitMultiplier;
        }

        var text = panel.GetComponentInChildren<TMP_Text>(true);
        if (text != null && text.font != null)
            _font = text.font;
    }

    static void StyleAsCard(Image img, float cornerScale)
    {
        if (img == null || _cardSprite == null)
            return;
        img.sprite = _cardSprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = _cardPpu * cornerScale;
    }

    static Button CreateButton(Transform parent, string name, string label,
        Color bg, Color textColor, float cornerScale)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = bg;
        StyleAsCard(img, cornerScale);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;

        if (!string.IsNullOrEmpty(label))
        {
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            if (_font != null)
                tmp.font = _font;
            tmp.fontSize = 14.5f;
            tmp.color = textColor;
            tmp.alignment = TextAlignmentOptions.Midline;
            tmp.raycastTarget = false;
        }

        return btn;
    }

    static void SetLabel(Button button, string label)
    {
        var tmp = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            tmp.text = label;
            return;
        }

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(button.transform, false);
        var textRt = (RectTransform)textGo.transform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        var created = textGo.AddComponent<TextMeshProUGUI>();
        created.text = label;
        if (_font != null)
            created.font = _font;
        created.fontSize = 14.5f;
        created.color = Ink;
        created.alignment = TextAlignmentOptions.Midline;
        created.raycastTarget = false;
    }

    static TextMeshProUGUI CreateLabel(Transform parent, string name, string text,
        float size, Color color, Vector2 anchoredPos, Vector2 sizeDelta)
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
        if (_font != null)
            tmp.font = _font;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.raycastTarget = false;
        return tmp;
    }

    static void PlaceTop(RectTransform rt, float y, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = size;
    }
}
