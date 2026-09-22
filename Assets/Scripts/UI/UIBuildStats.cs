using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Live build summary: counts the placed parts in the scene and shows a running
/// estimated price in AUD from the local, Inspector-editable price list.
/// Includes frames, panels and the finish parts actually installed. Twist
/// beams use the price of the matching H size. Unpriced parts are
/// disclosed instead of being presented as free. Changes are refreshed after
/// deferred destruction completes, including undo/clear to an empty build.
/// </summary>
public class UIBuildStats : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [System.Serializable]
    public class PartPrice
    {
        public string partId;
        public float price;

        public PartPrice(string partId, float price)
        {
            this.partId = partId;
            this.price = price;
        }
    }

    [Header("UI")]
    public TextMeshProUGUI partCountText;
    public TextMeshProUGUI priceText;

    [Header("Pricing (AUD)")]
    public List<PartPrice> partPrices = new List<PartPrice>
    {
        // Vertical beams (V-series)
        new PartPrice("V1", 5f),
        new PartPrice("V3", 10f),
        new PartPrice("V5", 20f),
        new PartPrice("V7", 25f),
        new PartPrice("V9", 35f),
        new PartPrice("V13", 45f),
        new PartPrice("V15", 55f),
        new PartPrice("V17", 60f),
        new PartPrice("V21", 80f),
        new PartPrice("V25", 85f),
        new PartPrice("V27", 90f),
        new PartPrice("V29", 95f),
        // Horizontal beams (H-series, integrated joints)
        new PartPrice("H1", 55f),
        new PartPrice("H3", 60f),
        new PartPrice("H5", 65f),
        new PartPrice("H7", 70f),
        new PartPrice("H9", 75f),
        new PartPrice("H11", 85f),
        new PartPrice("H15", 100f),
        new PartPrice("H19", 105f),
        new PartPrice("H23", 120f),
    };

    [Header("Temporary unit estimates (AUD)")]
    [Tooltip("Default for each panel, any size. Add a Panel H3xH1 entry above to override a size. Negative means unavailable; zero is explicitly free.")]
    public float panelPrice = 25f;
    [Tooltip("Default for each installed veneer, any size. Add e.g. Veneer H7 above to override a size.")]
    public float veneerPrice = 5f;
    public float capSidePrice = 2f;
    public float capEndPrice = 2f;
    public float footPrice = 5f;

    [Header("Refresh")]
    [Tooltip("Seconds between scene rescans.")]
    public float refreshInterval = 0.25f;

    public int PartCount { get; private set; }
    public float TotalPrice { get; private set; }
    public int UnpricedPartCount { get; private set; }
    public bool HasIncompletePricing => UnpricedPartCount > 0;
    public BuildPartSummary Summary { get; private set; } = new BuildPartSummary(new BuildPartSummary.Line[0]);
    public string PriceNote => HasIncompletePricing
        ? $"AUD placeholder estimate · {UnpricedPartCount} unpriced parts · Click for parts & prices"
        : "AUD placeholder estimate · Includes panels and installed finish · Click for parts & prices";

    Dictionary<string, float> _priceById;
    int _seenStructureVersion;
    int _seenPanelVersion;
    int _seenFinishVersion;
    float _nextRefresh;
    int _refreshAtFrame = -1;
    bool _needsRefresh = true;
    bool _hovered;

    void OnEnable()
    {
        BuildHistory.Changed += OnBuildChanged;
        _needsRefresh = true;
    }

    void OnDisable()
    {
        BuildHistory.Changed -= OnBuildChanged;
        _hovered = false;
        CursorTooltip.Hide(this);
    }

    void OnBuildChanged()
    {
        _needsRefresh = true;
        _refreshAtFrame = Time.frameCount + 1;
    }

    void Start()
    {
        // Existing baked scenes use a decorative pill. Enable pointer access
        // to the itemized list without requiring a UI rebuild.
        var background = GetComponent<Graphic>();
        if (background != null)
            background.raycastTarget = true;
        if (priceText != null)
        {
            priceText.enableAutoSizing = true;
            priceText.fontSizeMin = 10f;
            priceText.fontSizeMax = priceText.fontSize;
        }
        RefreshNow();
    }

    void Update()
    {
        // SpaceModeController owns its refresh cadence. Keep this component
        // enabled there so the pill remains clickable and hoverable.
        if (SpaceModeController.Active) return;
        // Change notifications cover restores even if their parts have no
        // active attachment points. The versions also catch direct spawn or
        // removal. Never read an intermediate multi-frame restore.
        if ((BuildHistory.Instance != null && BuildHistory.Instance.IsRestoring) ||
            Time.frameCount < _refreshAtFrame)
            return;
        if (!_needsRefresh && _seenStructureVersion == AttachmentPoint.StructureVersion &&
            _seenPanelVersion == PanelInstance.Version && _seenFinishVersion == FinishController.PartsVersion &&
            Time.unscaledTime < _nextRefresh)
            return;
        RefreshNow();
    }

    /// <summary>Read the current stable scene, also before saving its metadata.</summary>
    public void RefreshNow()
    {
        if (BuildHistory.Instance != null && BuildHistory.Instance.IsRestoring) return;
        // Saving/quoting may happen before FinishController.Update this frame.
        // Synchronize the installed dressing before taking a single snapshot.
        if (FinishController.Instance != null) FinishController.Instance.RefreshNow();
        RebuildPriceLookup();
        _seenStructureVersion = AttachmentPoint.StructureVersion;
        _seenPanelVersion = PanelInstance.Version;
        _seenFinishVersion = FinishController.PartsVersion;
        _nextRefresh = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
        _needsRefresh = false;
        _refreshAtFrame = -1;
        Summary = BuildPartSummaryCollector.Capture(this);
        PartCount = Summary.PartCount;
        TotalPrice = (float)Summary.TotalPrice;
        UnpricedPartCount = Summary.UnpricedPartCount;

        if (partCountText != null)
            partCountText.text = PartCount == 1 ? "1 part" : $"{PartCount} parts";

        if (priceText != null)
            priceText.text = "Est. $" + Summary.TotalPrice.ToString("N0") + (HasIncompletePricing ? "*" : "");
        if (_hovered)
            CursorTooltip.Show(this, PriceNote);
    }

    void RebuildPriceLookup()
    {
        _priceById = new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase);
        if (partPrices == null)
            return;
        foreach (PartPrice entry in partPrices)
        {
            // Keep invalid overrides, so a removed price cannot silently fall
            // back to a plausible-looking placeholder.
            if (entry != null && !string.IsNullOrWhiteSpace(entry.partId))
                _priceById[entry.partId.Trim()] = entry.price;
        }
    }

    /// <summary>Catalogue price of one part (also used by Space Mode dedup).</summary>
    public float PriceForPart(string partId)
    {
        return TryPriceForPart(partId, out float price) ? price : 0f;
    }

    /// <summary>False means no local price exists; zero can be an explicit price.</summary>
    public bool TryPriceForPart(string partId, out float price)
    {
        price = 0f;
        if (_priceById == null)
            RebuildPriceLookup();
        if (string.IsNullOrEmpty(partId))
            return false;
        partId = partId.Trim();

        if (_priceById.TryGetValue(partId, out price))
            return BuildPartSummary.IsValidPrice(price);

        if (partId.StartsWith("Panel ", System.StringComparison.OrdinalIgnoreCase))
            price = panelPrice;
        else if (partId.StartsWith("Veneer H", System.StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(partId.Substring(8), out int size) && System.Array.IndexOf(CatalogueData.VeneerLengths, size) >= 0)
            price = veneerPrice;
        else if (partId.Equals("Cap Side", System.StringComparison.OrdinalIgnoreCase)) price = capSidePrice;
        else if (partId.Equals("Cap End", System.StringComparison.OrdinalIgnoreCase)) price = capEndPrice;
        else if (partId.Equals("Foot", System.StringComparison.OrdinalIgnoreCase)) price = footPrice;
        else price = float.NaN;
        if (!float.IsNaN(price)) return BuildPartSummary.IsValidPrice(price);

        // Twist beams are priced like the H beam of the same size.
        if (BeamPartUtility.IsTwist(partId))
        {
            int digitStart = 0;
            while (digitStart < partId.Length && !char.IsDigit(partId[digitStart]))
                digitStart++;

            if (digitStart < partId.Length &&
                _priceById.TryGetValue("H" + partId.Substring(digitStart), out price))
                return BuildPartSummary.IsValidPrice(price);
        }

        return false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        CursorTooltip.Show(this, PriceNote);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        CursorTooltip.Hide(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        CursorTooltip.Hide(this);
        FindFirstObjectByType<WorkflowReviewUI>()?.ShowParts();
    }
}
