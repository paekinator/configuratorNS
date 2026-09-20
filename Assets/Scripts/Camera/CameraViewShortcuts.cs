using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Reusable camera bookmarks on the existing orbit rig.</summary>
public sealed class CameraViewShortcuts : MonoBehaviour
{
    public CadCameraController orbit;
    public CameraControlManager controls;
    int _corner;
    bool _plan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        var canvas = GameObject.Find("UI_Canvas")?.GetComponent<Canvas>();
        if (canvas != null) Install(canvas);
    }

    public static CameraViewShortcuts Install(Canvas canvas)
    {
        var shortcuts = canvas.GetComponent<CameraViewShortcuts>();
        if (shortcuts == null) shortcuts = canvas.gameObject.AddComponent<CameraViewShortcuts>();
        shortcuts.BuildControls();
        return shortcuts;
    }

    void Start() => BuildControls();

    public void Previous() => Step(-1);
    public void Next() => Step(1);
    public void Plan()
    {
        if (!ResolveCamera()) return;
        if (!_plan) _corner = NearestCorner(orbit.RequestedYaw);
        _plan = !_plan;
        controls?.SetCad();
        orbit.ShowPreset(_plan ? 0f : 45f + 90f * _corner, _plan);
        UIStatusBar.FlashAction();
    }

    void Step(int direction)
    {
        if (!ResolveCamera()) return;
        // After free orbit, choose the next standard corner from the current
        // heading. Returning from plan continues the previous corner sequence.
        if (!_plan)
            _corner = NearestCorner(orbit.RequestedYaw);
        _corner = (_corner + direction + 4) % 4;
        _plan = false;
        controls?.SetCad();
        orbit.ShowPreset(45f + 90f * _corner);
        UIStatusBar.FlashAction();
    }

    static int NearestCorner(float yaw) => ((Mathf.RoundToInt((yaw - 45f) / 90f) % 4) + 4) % 4;

    bool ResolveCamera()
    {
        if (controls == null) controls = FindFirstObjectByType<CameraControlManager>();
        if (orbit == null) orbit = controls != null ? controls.cadController : FindFirstObjectByType<CadCameraController>();
        return orbit != null;
    }

    public void BuildControls()
    {
        var source = transform.Find("ModeSwitch/Btn_ModeBuild")?.GetComponent<Image>();
        if (source == null) return;
        TMP_FontAsset font = source.GetComponentInChildren<TMP_Text>(true)?.font;
        var root = transform.Find("CameraViews") as RectTransform;
        if (root == null)
        {
            root = new GameObject("CameraViews", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(transform, false);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            root.SetAsFirstSibling();
        }
        Make("Previous", new Vector2(0, .52f), new Vector2(24, 0), new Vector2(0, .5f), "‹", "90°", "Previous isometric view", Previous);
        Make("Next", new Vector2(1, .52f), new Vector2(-24, 0), new Vector2(1, .5f), "›", "90°", "Next isometric view", Next);
        Make("Plan", new Vector2(.5f, 1), new Vector2(0, -88), new Vector2(.5f, 1), "‹", "Plan", "Toggle plan / previous isometric view", Plan);

        void Make(string name, Vector2 anchor, Vector2 position, Vector2 pivot,
            string arrow, string caption, string hint, UnityEngine.Events.UnityAction action)
        {
            var rt = root.Find(name) as RectTransform;
            if (rt == null)
            {
                rt = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Shadow)).GetComponent<RectTransform>();
                rt.SetParent(root, false);
                rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot;
                rt.anchoredPosition = position; rt.sizeDelta = new Vector2(48, 66);
                var image = rt.GetComponent<Image>(); image.sprite = source.sprite; image.type = source.type;
                image.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier * .65f;
                image.color = new Color(1, 1, 1, .95f);
                var shadow = rt.GetComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, .12f);
                shadow.effectDistance = new Vector2(1.5f, -2.5f);
                Label("Arrow", arrow, 26, 8);
                Label("Caption", caption, 11, -17);

                void Label(string labelName, string value, float size, float y)
                {
                    var go = new GameObject(labelName, typeof(RectTransform), typeof(TextMeshProUGUI));
                    go.transform.SetParent(rt, false);
                    var text = go.GetComponent<TextMeshProUGUI>();
                    text.text = value; text.font = font; text.fontSize = size; text.enableAutoSizing = false;
                    text.alignment = TextAlignmentOptions.Center; text.color = new Color32(112, 112, 112, 255);
                    text.raycastTarget = false; text.rectTransform.sizeDelta = new Vector2(44, 30);
                    text.rectTransform.anchoredPosition = new Vector2(0, y);
                }
            }
            var arrowText = rt.Find("Arrow").GetComponent<TMP_Text>();
            arrowText.text = arrow;
            arrowText.rectTransform.localEulerAngles = name == "Plan" ? new Vector3(0, 0, -90) : Vector3.zero;
            if (name == "Plan")
            {
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, 72f);
                // Use symmetric graphics, not rotated text whose font metrics
                // can change when the typography system rebuilds its mesh.
                arrowText.gameObject.SetActive(false);
                rt.Find("DownArrow")?.gameObject.SetActive(false);
                var label = rt.Find("Caption").GetComponent<TMP_Text>();
                label.rectTransform.anchoredPosition = Vector2.zero;
                Chevron("UpChevron", UtilityLineIcon.Glyph.ChevronUp, 21);
                Chevron("DownChevron", UtilityLineIcon.Glyph.ChevronDown, -21);

                void Chevron(string childName, UtilityLineIcon.Glyph glyph, float y)
                {
                    var child = rt.Find(childName) as RectTransform;
                    if (child == null)
                    {
                        child = new GameObject(childName, typeof(RectTransform), typeof(UtilityLineIcon)).GetComponent<RectTransform>();
                        child.SetParent(rt, false);
                    }
                    child.anchorMin = child.anchorMax = child.pivot = new Vector2(.5f, .5f);
                    child.anchoredPosition = new Vector2(0, y);
                    child.sizeDelta = new Vector2(14, 14);
                    child.localRotation = Quaternion.identity;
                    child.localScale = Vector3.one;
                    var icon = child.GetComponent<UtilityLineIcon>();
                    icon.glyph = glyph; icon.stroke = 2.5f;
                    icon.color = new Color32(112, 112, 112, 255);
                    icon.raycastTarget = false;
                    icon.SetAllDirty();
                }
            }
            else
            {
                // Match the Plan control with the same centred procedural
                // chevron rather than a font-dependent arrow character.
                arrowText.gameObject.SetActive(false);
                var child = rt.Find("SideChevron") as RectTransform;
                if (child == null)
                {
                    child = new GameObject("SideChevron", typeof(RectTransform), typeof(UtilityLineIcon)).GetComponent<RectTransform>();
                    child.SetParent(rt, false);
                }
                child.anchorMin = child.anchorMax = child.pivot = new Vector2(.5f, .5f);
                child.anchoredPosition = new Vector2(0, 8);
                child.sizeDelta = new Vector2(14, 14);
                child.localRotation = Quaternion.identity;
                child.localScale = Vector3.one;
                var icon = child.GetComponent<UtilityLineIcon>();
                icon.glyph = name == "Previous" ? UtilityLineIcon.Glyph.ChevronLeft : UtilityLineIcon.Glyph.ChevronRight;
                icon.stroke = 2.5f;
                icon.color = new Color32(112, 112, 112, 255);
                icon.raycastTarget = false;
                icon.SetAllDirty();
            }
            var button = rt.GetComponent<Button>();
            button.targetGraphic = rt.GetComponent<Image>();
            var colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = new Color(.90f, .90f, .90f, 1);
            colors.pressedColor = new Color(.80f, .80f, .80f, 1);
            colors.selectedColor = Color.white; colors.fadeDuration = .12f; button.colors = colors;
            button.onClick.RemoveListener(action); button.onClick.AddListener(action);
            var events = rt.GetComponent<EventTrigger>();
            if (events == null) events = rt.gameObject.AddComponent<EventTrigger>();
            events.triggers.Clear();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => CursorTooltip.Show(button, hint)); events.triggers.Add(enter);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => CursorTooltip.Hide(button)); events.triggers.Add(exit);
        }
    }
}
