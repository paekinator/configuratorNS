using TMPro;
using UnityEngine;

/// <summary>
/// A testable NEOSPACE typography pairing. Font files live once under
/// Assets/Fonts/Families; these assets describe how a family pairing is used.
/// </summary>
[CreateAssetMenu(fileName = "Typography Set", menuName = "NEOSPACE/Typography Set")]
public sealed class NeospaceTypographySet : ScriptableObject
{
    [Header("Identity")]
    public string displayName;
    [TextArea(2, 4)] public string character;

    [Header("Primary sans")]
    public TMP_FontAsset primaryRegular;
    public TMP_FontAsset primaryMedium;
    public TMP_FontAsset primarySemiBold;

    [Header("Technical mono")]
    public TMP_FontAsset technicalRegular;
    public TMP_FontAsset technicalMedium;
    public TMP_FontAsset technicalSemiBold;

    [Header("Optional display variation")]
    public TMP_FontAsset displayMedium;
    public TMP_FontAsset displaySemiBold;

    public TMP_FontAsset Primary(NeospaceTypographyWeight weight)
    {
        return weight switch
        {
            NeospaceTypographyWeight.SemiBold => primarySemiBold != null ? primarySemiBold : primaryMedium,
            NeospaceTypographyWeight.Medium => primaryMedium != null ? primaryMedium : primaryRegular,
            _ => primaryRegular != null ? primaryRegular : primaryMedium
        };
    }

    public TMP_FontAsset Technical(NeospaceTypographyWeight weight)
    {
        TMP_FontAsset selected = weight switch
        {
            NeospaceTypographyWeight.SemiBold => technicalSemiBold != null ? technicalSemiBold : technicalMedium,
            NeospaceTypographyWeight.Medium => technicalMedium != null ? technicalMedium : technicalRegular,
            _ => technicalRegular != null ? technicalRegular : technicalMedium
        };
        return selected != null ? selected : Primary(weight);
    }

    public TMP_FontAsset Display(NeospaceTypographyWeight weight)
    {
        TMP_FontAsset selected = weight == NeospaceTypographyWeight.SemiBold
            ? (displaySemiBold != null ? displaySemiBold : displayMedium)
            : (displayMedium != null ? displayMedium : displaySemiBold);
        return selected != null ? selected : Primary(weight);
    }
}

public enum NeospaceTypographyWeight
{
    Regular,
    Medium,
    SemiBold
}
