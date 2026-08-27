using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Live build summary: counts the placed parts in the scene and shows a running
/// price in AUD. Every beam is priced from the official component price list
/// (editable in the Inspector); twist beams use the price of the matching H
/// size. Panels are included in the part count but are free for now.
/// The scene is rescanned on a small interval, which keeps the readout correct
/// no matter how parts are added or removed (build tool, delete key, undo...).
/// </summary>
public class UIBuildStats : MonoBehaviour
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

    [Tooltip("Panels are counted as parts but not priced (for now).")]
    public float panelPrice = 0f;

    [Header("Refresh")]
    [Tooltip("Seconds between scene rescans.")]
    public float refreshInterval = 0.25f;

    public int PartCount { get; private set; }
    public float TotalPrice { get; private set; }

    Dictionary<string, float> _priceById;
    int _seenStructureVersion;
    int _seenPanelVersion;

    void Start()
    {
        Refresh();
    }

    void Update()
    {
        // Count and price depend only on WHICH parts exist — and every beam
        // spawn/destroy bumps the structure version, every panel the panel
        // version. Idle frames cost two compares instead of a scene rescan
        // every refresh interval.
        if (_seenStructureVersion == AttachmentPoint.StructureVersion &&
            _seenPanelVersion == PanelInstance.Version)
            return;
        _seenStructureVersion = AttachmentPoint.StructureVersion;
        _seenPanelVersion = PanelInstance.Version;
        Refresh();
    }

    void Refresh()
    {
        RebuildPriceLookup();

        int count = 0;
        float total = 0f;

        foreach (SelectableBeam beam in
                 Object.FindObjectsByType<SelectableBeam>(FindObjectsSortMode.None))
        {
            Transform root = beam.transform.root;
            string rootName = root.name;

            // Skip the placement ghost and anything that isn't a placed beam.
            if (rootName.Contains("_GhostInstance") || !BeamPartUtility.IsBeam(rootName))
                continue;

            count++;
            total += PriceForPart(ParsePartId(rootName));
        }

        foreach (PanelInstance panel in
                 Object.FindObjectsByType<PanelInstance>(FindObjectsSortMode.None))
        {
            count++;
            total += panelPrice;
        }

        PartCount = count;
        TotalPrice = total;

        if (partCountText != null)
            partCountText.text = count == 1 ? "1 part" : $"{count} parts";

        if (priceText != null)
            priceText.text = "$" + total.ToString("N0");
    }

    void RebuildPriceLookup()
    {
        if (_priceById != null && _priceById.Count == partPrices.Count)
            return;

        _priceById = new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase);
        foreach (PartPrice entry in partPrices)
        {
            if (entry != null && !string.IsNullOrEmpty(entry.partId))
                _priceById[entry.partId.Trim()] = entry.price;
        }
    }

    /// <summary>Catalogue price of one part (also used by Space Mode dedup).</summary>
    public float PriceForPart(string partId)
    {
        if (string.IsNullOrEmpty(partId))
            return 0f;

        if (_priceById.TryGetValue(partId, out float price))
            return price;

        // Twist beams are priced like the H beam of the same size.
        if (BeamPartUtility.IsTwist(partId))
        {
            int digitStart = 0;
            while (digitStart < partId.Length && !char.IsDigit(partId[digitStart]))
                digitStart++;

            if (digitStart < partId.Length &&
                _priceById.TryGetValue("H" + partId.Substring(digitStart), out float hPrice))
                return hPrice;
        }

        return 0f;
    }

    /// <summary>Leading letters + first digit run, e.g. "H9(Clone)" -> "H9".</summary>
    static string ParsePartId(string name)
    {
        int end = 0;
        while (end < name.Length && char.IsLetter(name[end]))
            end++;
        while (end < name.Length && char.IsDigit(name[end]))
            end++;

        return name.Substring(0, end);
    }
}
