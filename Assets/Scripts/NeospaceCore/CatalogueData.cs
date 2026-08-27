using System.Collections.Generic;

/// <summary>
/// Single source of truth for NEOSPACE sizes and constants.
/// Ported from Rhino neospace/catalogue_data.py. Lengths are in millimetres;
/// convert with <see cref="NeospaceUnits"/> for Unity scene work.
/// </summary>
public static class CatalogueData
{
    public const float ModuleMm = 88f;
    public const float ProfileMm = 41f;
    public const float HalfProfileMm = 20.5f;

    public const float PanelInwardFaceOffsetMm = 14f;
    public const float PanelBoardThicknessMm = 8f;
    public const float PanelBasepointOffsetMm =
        PanelInwardFaceOffsetMm + PanelBoardThicknessMm * 0.5f; // 18 mm

    public const float GroundOffsetMm = 30.5f; // half-profile + 10 mm foot

    // V14 and V22 are not produced, so they are excluded even though older
    // Rhino data listed them.
    public static readonly int[] VSizes =
    {
        1, 3, 5, 7, 9, 11, 13, 15, 17, 21, 25, 27, 29
    };

    public static readonly int[] VCableHoleSizes =
    {
        3, 5, 7, 9, 11, 13, 15, 17, 21, 25, 27, 29
    };

    public static readonly int[] HSizes =
    {
        1, 3, 5, 7, 9, 11, 15, 19, 23
    };

    public static readonly int[] HCableHoleSizes =
    {
        3, 5, 7, 9, 11, 15, 19, 23
    };

    public static readonly int[] HtSizes =
    {
        1, 3, 5, 7, 9, 11, 15, 19, 23
    };

    public static readonly int[] HtCableHoleSizes =
    {
        3, 5, 7, 9, 11, 15, 19, 23
    };

    /// <summary>
    /// The 19 produced panel boards, stored as (A, B) with A &gt;= B.
    /// Edges come only from {1, 3, 5, 7, 11, 15}; the old Rhino data also
    /// listed 9-, 12- and 19-edge boards which are no longer produced.
    /// Board edge length: (n + 1) * 88 - 41.283 mm (see <see cref="PanelFill"/>).
    /// </summary>
    public static readonly IntPair[] PanelPairs =
    {
        new IntPair(3, 1), new IntPair(3, 3),
        new IntPair(5, 1), new IntPair(5, 3), new IntPair(5, 5),
        new IntPair(7, 1), new IntPair(7, 3), new IntPair(7, 5), new IntPair(7, 7),
        new IntPair(11, 1), new IntPair(11, 3), new IntPair(11, 5), new IntPair(11, 7),
        new IntPair(11, 11),
        new IntPair(15, 1), new IntPair(15, 3), new IntPair(15, 5), new IntPair(15, 7),
        new IntPair(15, 11),
    };

    public static readonly string[] VeneerTypes = { "Outer", "Inner", "In/Out" };

    /// <summary>
    /// Every produced veneer size. The original range (1, 3, 5, 7, 9, 11,
    /// 12, 15) left odd channel sections uncoverable; the owner filled in
    /// the missing lengths so any section of 2+ intervals now tiles exactly.
    /// </summary>
    public static readonly int[] VeneerLengths =
    {
        1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15
    };
    public static readonly string[] CapTypes = { "Side", "End" };
    public static readonly int[] LoadBearingBarSizes = { 3, 5, 7, 9, 11, 15 };

    public readonly struct IntPair
    {
        public readonly int A;
        public readonly int B;

        public IntPair(int a, int b)
        {
            A = a;
            B = b;
        }
    }
}
