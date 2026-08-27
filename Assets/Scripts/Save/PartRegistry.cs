using System.Collections.Generic;

/// <summary>
/// Permanent numeric ids for every part a configuration code can contain.
///
/// RULES (these make old codes reproducible forever):
///  - ids are assigned once and NEVER reused or renumbered;
///  - parts that leave the catalogue are marked <c>retired</c> but keep their
///    id and entry (V14/V22 are the precedent — they shipped in old Rhino
///    data and may exist in old codes);
///  - new parts are appended with fresh ids inside their family range.
///
/// Ids are keyed by the Unity scene part id ("V13", "H7", "T5"). The T ↔ HT
/// duality is owned by <see cref="Naming.FromUnityPartId"/> — this registry
/// is the only other place allowed to know both spellings.
///
/// Ranges: 1–31 V frames · 32–63 H beams · 64–95 T (HT) twist beams ·
/// 96–127 reserved (Load Bearing Bar, Cap, Foot) · 128+ future families.
/// </summary>
public static class PartRegistry
{
    public readonly struct Entry
    {
        public readonly int Code;
        public readonly string PartId;
        public readonly bool Retired;

        public Entry(int code, string partId, bool retired)
        {
            Code = code;
            PartId = partId;
            Retired = retired;
        }
    }

    // Append-only. Do not reorder, renumber, or delete lines — mark retired.
    static readonly Entry[] Table =
    {
        // V frames (1–31)
        new Entry( 1, "V1",  false),
        new Entry( 2, "V3",  false),
        new Entry( 3, "V5",  false),
        new Entry( 4, "V7",  false),
        new Entry( 5, "V9",  false),
        new Entry( 6, "V11", false),
        new Entry( 7, "V13", false),
        new Entry( 8, "V15", false),
        new Entry( 9, "V17", false),
        new Entry(10, "V21", false),
        new Entry(11, "V25", false),
        new Entry(12, "V27", false),
        new Entry(13, "V29", false),
        new Entry(14, "V14", true),   // not produced; existed in old Rhino data
        new Entry(15, "V22", true),   // not produced; existed in old Rhino data

        // H beams (32–63)
        new Entry(32, "H1",  false),
        new Entry(33, "H3",  false),
        new Entry(34, "H5",  false),
        new Entry(35, "H7",  false),
        new Entry(36, "H9",  false),
        new Entry(37, "H11", false),
        new Entry(38, "H15", false),
        new Entry(39, "H19", false),
        new Entry(40, "H23", false),

        // T twist beams (64–95) — catalogue name "HT{n}"
        new Entry(64, "T1",  false),
        new Entry(65, "T3",  false),
        new Entry(66, "T5",  false),
        new Entry(67, "T7",  false),
        new Entry(68, "T9",  false),
        new Entry(69, "T11", false),
        new Entry(70, "T15", false),
        new Entry(71, "T19", false),
        new Entry(72, "T23", false),
    };

    static readonly Dictionary<string, Entry> ById = new Dictionary<string, Entry>();
    static readonly Dictionary<int, Entry> ByCode = new Dictionary<int, Entry>();

    static PartRegistry()
    {
        foreach (Entry e in Table)
        {
            ById.Add(e.PartId, e);
            ByCode.Add(e.Code, e);
        }
    }

    /// <summary>Permanent code for a scene part id ("V13" → 7). False for unknown parts.</summary>
    public static bool TryGetCode(string partId, out int code)
    {
        if (partId != null && ById.TryGetValue(partId.Trim(), out Entry e))
        {
            code = e.Code;
            return true;
        }
        code = 0;
        return false;
    }

    /// <summary>Scene part id for a permanent code. False for codes this build doesn't know.</summary>
    public static bool TryGetPart(int code, out string partId, out bool retired)
    {
        if (ByCode.TryGetValue(code, out Entry e))
        {
            partId = e.PartId;
            retired = e.Retired;
            return true;
        }
        partId = null;
        retired = false;
        return false;
    }

    public static bool IsRetired(int code) =>
        ByCode.TryGetValue(code, out Entry e) && e.Retired;
}
