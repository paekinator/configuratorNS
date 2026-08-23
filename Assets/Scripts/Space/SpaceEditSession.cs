using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Edit Piece" from Space Mode: select copies of a piece in the space,
/// press Edit, and the piece's DEFINITION opens in Piece Mode with all the
/// normal build tools. A bar at the top offers the two ways to save:
///
///  - UPDATE ALL COPIES — every instance in the space that uses this
///    definition (not just the selected ones) gets the edited version, and
///    the matching piece in the library is updated too.
///  - MAKE UNIQUE — only the instances that were selected when editing
///    began switch to the edited version (as a new definition named
///    "… (unique)"); all other copies and the library stay untouched.
///
/// Cancel (or the mode button) returns to the space unchanged. Applying is
/// ONE space-history step, so Ctrl/Cmd+Z rolls the whole edit back.
/// </summary>
public class SpaceEditSession : MonoBehaviour
{
    public BuildController buildController;
    public SpaceModeController modeController;
    public SpaceInteractionController interaction;
    public SpaceHistory history;

    public bool IsEditing => _editing;

    bool _editing;
    bool _busy;
    string _originalCode;
    string _pieceName;
    readonly List<SpaceInstance> _targets = new List<SpaceInstance>();

    // Editing bar UI
    RectTransform _bar;
    TextMeshProUGUI _barLabel;
    TextMeshProUGUI _updateLabel;
    Sprite _cardSprite;
    float _cardPpu = 1f;
    TMP_FontAsset _font;

    static Color Accent => UIThemeController.AccentColor;
    static readonly Color Ink = new Color(0.149f, 0.133f, 0.118f);
    static readonly Color Surface = new Color(0.953f, 0.937f, 0.914f);

    // ------------------------------------------------------------------
    // Pure edit semantics (unit-tested by SpaceCodeSelfTest)
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns a new state list with the edit applied.
    /// UPDATE ALL: every instance whose code equals <paramref name="originalCode"/>
    /// switches to <paramref name="newCode"/> / <paramref name="newPrice"/>.
    /// MAKE UNIQUE: only the instances at <paramref name="targetIndices"/>
    /// switch, and they take <paramref name="uniqueName"/>; everything else
    /// is untouched.
    /// </summary>
    public static List<SpaceHistory.InstanceState> ApplyEdit(
        List<SpaceHistory.InstanceState> states,
        HashSet<int> targetIndices,
        string originalCode,
        string newCode,
        float newPrice,
        string uniqueName,
        bool updateAll)
    {
        var result = new List<SpaceHistory.InstanceState>(states.Count);
        for (int i = 0; i < states.Count; i++)
        {
            SpaceHistory.InstanceState s = states[i];
            bool affected = updateAll
                ? s.Code == originalCode
                : targetIndices != null && targetIndices.Contains(i);

            if (affected)
            {
                s.Code = newCode;
                s.Price = newPrice;
                if (!updateAll && !string.IsNullOrEmpty(uniqueName))
                    s.PieceName = uniqueName;
            }
            result.Add(s);
        }
        return result;
    }

    // ------------------------------------------------------------------
    // Session lifecycle
    // ------------------------------------------------------------------

    /// <summary>All selected instances must share one code (caller checks).</summary>
    public void BeginEdit(List<SpaceInstance> selected)
    {
        if (_editing || _busy || selected == null || selected.Count == 0)
            return;

        _originalCode = selected[0].code;
        _pieceName = selected[0].pieceName;
        _targets.Clear();
        _targets.AddRange(selected);

        StartCoroutine(BeginRoutine());
    }

    IEnumerator BeginRoutine()
    {
        _busy = true;

        ConfigurationCodeValidation check = ConfigurationCode.Validate(_originalCode);
        if (!check.IsValid)
        {
            SelectionStatus.Set("This piece can't be edited: " + check.Error, 6f);
            _busy = false;
            yield break;
        }

        modeController.ExitSpaceMode();
        yield return null;   // builder systems wake up

        var restorer = ConfigurationCode.GetOrCreateRestorer(buildController);
        bool done = false;
        restorer.Restore(check.Model, _ => done = true);
        while (!done)
            yield return null;

        _editing = true;
        _busy = false;
        ShowBar();
        SelectionStatus.Set(
            $"Editing \"{_pieceName}\" · change it with the normal tools, " +
            "then choose above how to save it back to your space.", 8f);
    }

    public void ApplyUpdateAll() => Apply(updateAll: true);
    public void ApplyMakeUnique() => Apply(updateAll: false);

    void Apply(bool updateAll)
    {
        if (!_editing || _busy)
            return;

        ConfigurationModel model = ConfigurationCapture.Capture(buildController);
        if (model.Beams.Count == 0 && model.Panels.Count == 0)
        {
            SelectionStatus.Set("The grid is empty · the edited piece needs at least one part.", 4f);
            return;
        }

        string newCode = ConfigurationCode.Encode(model);
        var stats = FindFirstObjectByType<UIBuildStats>();
        float newPrice = stats != null ? stats.TotalPrice : 0f;

        // Map target instance refs to indices in the live instance list.
        var targetIndices = new HashSet<int>();
        IReadOnlyList<SpaceInstance> all = interaction.Instances;
        for (int i = 0; i < all.Count; i++)
            if (_targets.Contains(all[i]))
                targetIndices.Add(i);

        List<SpaceHistory.InstanceState> newStates = ApplyEdit(
            interaction.CurrentStates(), targetIndices, _originalCode,
            newCode, newPrice, _pieceName + " (unique)", updateAll);

        int affected = updateAll
            ? CountByCode(interaction.CurrentStates(), _originalCode)
            : targetIndices.Count;

        if (updateAll)
            UpdateLibraryPiece(newCode, model, newPrice);

        _editing = false;
        HideBar();
        StartCoroutine(ReturnRoutine(newStates, affected, updateAll));
    }

    IEnumerator ReturnRoutine(List<SpaceHistory.InstanceState> newStates,
        int affected, bool updateAll)
    {
        _busy = true;

        modeController.EnterSpaceMode();
        float deadline = Time.unscaledTime + 3f;
        while (!SpaceModeController.Active && Time.unscaledTime < deadline)
            yield return null;

        bool done = false;
        interaction.RebuildFromStates(newStates, () => done = true);
        while (!done)
            yield return null;

        history.Record(interaction.CurrentStates());
        _busy = false;

        SelectionStatus.Set(updateAll
            ? $"Updated {affected} cop{(affected == 1 ? "y" : "ies")} of \"{_pieceName}\" · Ctrl+Z (Cmd+Z) undoes the edit."
            : $"Made {affected} cop{(affected == 1 ? "y" : "ies")} unique · the other copies kept the old design.", 7f);
    }

    public void CancelEdit()
    {
        if (!_editing || _busy)
            return;
        _editing = false;
        HideBar();
        StartCoroutine(CancelRoutine());
    }

    IEnumerator CancelRoutine()
    {
        _busy = true;
        modeController.EnterSpaceMode();
        float deadline = Time.unscaledTime + 3f;
        while (!SpaceModeController.Active && Time.unscaledTime < deadline)
            yield return null;
        _busy = false;
        SelectionStatus.Set("Back to your space · nothing was changed.", 4f);
    }

    static int CountByCode(List<SpaceHistory.InstanceState> states, string code)
    {
        int n = 0;
        foreach (SpaceHistory.InstanceState s in states)
            if (s.Code == code)
                n++;
        return n;
    }

    /// <summary>Keep the library in step: the piece with the old code takes the edit.</summary>
    void UpdateLibraryPiece(string newCode, ConfigurationModel model, float newPrice)
    {
        foreach (PieceLibrary.PieceRecord record in PieceLibrary.LoadAll())
        {
            if (record.code != _originalCode)
                continue;

            record.code = newCode;
            record.modifiedUtc = PieceLibrary.NowUtc();
            record.beamCount = model.Beams.Count;
            record.panelCount = model.Panels.Count;
            record.price = newPrice;

            if (StructureBounds.TryCompute(buildController, out StructureBounds.Info info))
            {
                record.widthMm = Mathf.RoundToInt(info.WidthMm);
                record.depthMm = Mathf.RoundToInt(info.DepthMm);
                record.heightMm = Mathf.RoundToInt(info.HeightMm);
            }

            Camera cam = buildController != null && buildController.cam != null
                ? buildController.cam : Camera.main;
            Texture2D thumb = PieceLibrary.CaptureThumbnail(cam);
            if (thumb != null)
            {
                PieceLibrary.SaveThumbnail(record.id, thumb);
                record.hasThumbnail = true;
                Destroy(thumb);
            }

            PieceLibrary.Save(record);
            break;
        }
    }

    // ------------------------------------------------------------------
    // Editing bar (top-centre card in Piece Mode)
    // ------------------------------------------------------------------

    void ShowBar()
    {
        if (_bar == null)
            BuildBar();
        if (_bar == null)
            return;

        int copies = CountByCode(interaction.CurrentStates(), _originalCode);
        _barLabel.text = $"Editing \"{_pieceName}\" · {copies} cop{(copies == 1 ? "y" : "ies")} in your space";
        _updateLabel.text = copies == 1 ? "Update it" : $"Update all {copies}";
        _bar.gameObject.SetActive(true);
        _bar.SetAsLastSibling();
    }

    void HideBar()
    {
        if (_bar != null)
            _bar.gameObject.SetActive(false);
    }

    void BuildBar()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        AdoptCardStyle(canvas);

        var go = new GameObject("PieceEditBar", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        _bar = (RectTransform)go.transform;
        _bar.anchorMin = _bar.anchorMax = new Vector2(0.5f, 1f);
        _bar.pivot = new Vector2(0.5f, 1f);
        _bar.anchoredPosition = new Vector2(0f, -96f);
        _bar.sizeDelta = new Vector2(620f, 64f);

        var img = go.GetComponent<Image>();
        img.color = Surface;
        StyleCard(img, 1.4f);

        UiPolish.SoftShadow(_bar);

        _barLabel = CreateText(_bar, "Label", "", 13f, Ink, true);
        var labelRt = _barLabel.rectTransform;
        labelRt.anchorMin = new Vector2(0f, 0f);
        labelRt.anchorMax = new Vector2(0f, 1f);
        labelRt.pivot = new Vector2(0f, 0.5f);
        labelRt.anchoredPosition = new Vector2(18f, 0f);
        labelRt.sizeDelta = new Vector2(280f, 0f);
        _barLabel.alignment = TextAlignmentOptions.MidlineLeft;
        _barLabel.overflowMode = TextOverflowModes.Ellipsis;

        _updateLabel = BarButton("Btn_UpdateAll", "Update all", Accent, Color.white, -238f, 118f, ApplyUpdateAll);
        BarButton("Btn_MakeUnique", "Make unique", Surface, Ink, -120f, 112f, ApplyMakeUnique);
        BarButton("Btn_CancelEdit", "Cancel", Ink, Surface, -14f, 74f, CancelEdit);

        _bar.gameObject.SetActive(false);
    }

    TextMeshProUGUI BarButton(string name, string label, Color bg, Color fg,
        float right, float width, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(_bar, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(right, 0f);
        rt.sizeDelta = new Vector2(width, 40f);

        var img = go.GetComponent<Image>();
        img.color = bg;
        StyleCard(img, 1.8f);

        Button button = go.GetComponent<Button>();
        UiPolish.HoverTint(button);
        button.onClick.AddListener(onClick);

        TextMeshProUGUI text = CreateText(rt, "Text", label, 12.5f, fg, true);
        var textRt = text.rectTransform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        text.alignment = TextAlignmentOptions.Center;
        return text;
    }

    TextMeshProUGUI CreateText(RectTransform parent, string name, string value,
        float size, Color color, bool bold)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = value;
        if (_font != null)
            tmp.font = _font;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.raycastTarget = false;
        if (bold)
            tmp.fontStyle = FontStyles.Bold;
        return tmp;
    }

    void StyleCard(Image img, float ppu)
    {
        if (_cardSprite == null)
            return;
        img.sprite = _cardSprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = _cardPpu * ppu;
    }

    void AdoptCardStyle(Canvas canvas)
    {
        Transform partsPanel = canvas.transform.Find("PartsPanel");
        if (partsPanel == null)
            return;

        var img = partsPanel.GetComponent<Image>();
        if (img != null && img.sprite != null)
        {
            _cardSprite = img.sprite;
            _cardPpu = img.pixelsPerUnitMultiplier;
        }

        var text = partsPanel.GetComponentInChildren<TMP_Text>(true);
        if (text != null && text.font != null)
            _font = text.font;
    }
}
