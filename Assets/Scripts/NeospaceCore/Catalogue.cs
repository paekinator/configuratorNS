using System.Collections.Generic;

/// <summary>Catalogue queries and snap tables over <see cref="CatalogueData"/>.</summary>
public static class Catalogue
{
    static HashSet<long> _panelPairSet;

    static long PackPair(int a, int b)
    {
        int hi = a >= b ? a : b;
        int lo = a >= b ? b : a;
        return ((long)hi << 32) | (uint)lo;
    }

    static HashSet<long> PanelPairSet()
    {
        if (_panelPairSet != null)
            return _panelPairSet;

        _panelPairSet = new HashSet<long>();
        for (int i = 0; i < CatalogueData.PanelPairs.Length; i++)
        {
            var p = CatalogueData.PanelPairs[i];
            _panelPairSet.Add(PackPair(p.A, p.B));
        }
        return _panelPairSet;
    }

    public static bool IsValidV(int size, bool cableHole = false)
    {
        return Contains(cableHole ? CatalogueData.VCableHoleSizes : CatalogueData.VSizes, size);
    }

    public static bool IsValidH(int size, bool cableHole = false)
    {
        return Contains(cableHole ? CatalogueData.HCableHoleSizes : CatalogueData.HSizes, size);
    }

    public static bool IsValidHt(int size, bool cableHole = false)
    {
        return Contains(cableHole ? CatalogueData.HtCableHoleSizes : CatalogueData.HtSizes, size);
    }

    public static bool IsValidPanelPair(int dimA, int dimB)
    {
        return PanelPairSet().Contains(PackPair(dimA, dimB));
    }

    public static bool IsValidVeneer(string veneerType, int size)
    {
        return ContainsString(CatalogueData.VeneerTypes, veneerType) &&
               Contains(CatalogueData.VeneerLengths, size);
    }

    public static bool IsValidLoadBearingBar(int size)
    {
        return Contains(CatalogueData.LoadBearingBarSizes, size);
    }

    public static List<int> PartnerEdges(int edge)
    {
        var partners = new SortedSet<int>();
        foreach (long packed in PanelPairSet())
        {
            int a = (int)(packed >> 32);
            int b = (int)(packed & 0xffffffff);
            if (a == edge) partners.Add(b);
            if (b == edge) partners.Add(a);
        }
        return new List<int>(partners);
    }

    public static int? MaxPartnerEdge(int edge)
    {
        int? best = null;
        foreach (long packed in PanelPairSet())
        {
            int a = (int)(packed >> 32);
            int b = (int)(packed & 0xffffffff);
            int? partner = null;
            if (a == edge) partner = b;
            else if (b == edge) partner = a;
            if (partner.HasValue && (!best.HasValue || partner.Value > best.Value))
                best = partner;
        }
        return best;
    }

    public static bool IsInCatalogue(Naming.ParsedName parsed)
    {
        if (parsed == null)
            return false;

        bool cable = parsed.Variation == "Cable Hole";
        if (parsed.Family == Naming.Frame)
        {
            if (parsed.Type == "V") return IsValidV(parsed.SizeInt, cable);
            if (parsed.Type == "H") return IsValidH(parsed.SizeInt, cable);
            if (parsed.Type == "HT") return IsValidHt(parsed.SizeInt, cable);
            return false;
        }

        if (parsed.Family == Naming.Panel)
            return IsValidPanelPair(parsed.SizeA, parsed.SizeB);

        if (parsed.Family == Naming.Veneer)
            return IsValidVeneer(parsed.Type, parsed.SizeInt);

        if (parsed.Family == Naming.Cap)
            return ContainsString(CatalogueData.CapTypes, parsed.Type);

        if (parsed.Family == Naming.Foot)
            return true;

        if (parsed.Family == Naming.LoadBearingBar)
            return IsValidLoadBearingBar(parsed.SizeInt);

        return false;
    }

    public readonly struct SnapEntry
    {
        public readonly float DistanceMm;
        public readonly int Size;

        public SnapEntry(float distanceMm, int size)
        {
            DistanceMm = distanceMm;
            Size = size;
        }
    }

    public static List<SnapEntry> HSpanSnapTable()
    {
        var table = new List<SnapEntry>(CatalogueData.HSizes.Length);
        int[] sizes = (int[])CatalogueData.HSizes.Clone();
        System.Array.Sort(sizes);
        for (int i = 0; i < sizes.Length; i++)
            table.Add(new SnapEntry(Skeleton.HSkeletonLength(sizes[i]), sizes[i]));
        return table;
    }

    public static List<SnapEntry> VHeightSnapTable()
    {
        var table = new List<SnapEntry>(CatalogueData.VSizes.Length);
        int[] sizes = (int[])CatalogueData.VSizes.Clone();
        System.Array.Sort(sizes);
        for (int i = 0; i < sizes.Length; i++)
            table.Add(new SnapEntry(Skeleton.VSkeletonLength(sizes[i]), sizes[i]));
        return table;
    }

    public static SnapEntry? SnapToTable(float distanceMm, List<SnapEntry> table, float toleranceMm)
    {
        SnapEntry? best = null;
        float bestDiff = float.PositiveInfinity;
        for (int i = 0; i < table.Count; i++)
        {
            float diff = System.Math.Abs(distanceMm - table[i].DistanceMm);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                best = table[i];
            }
        }

        if (best.HasValue && bestDiff <= toleranceMm)
            return best;
        return null;
    }

    /// <summary>Snap a picked distance to the nearest H span (or 0).</summary>
    public static void SnapSpan(float pickedMm, out float snappedMm, out int? hSize)
    {
        snappedMm = 0f;
        hSize = null;
        float bestDiff = System.Math.Abs(pickedMm);
        List<SnapEntry> table = HSpanSnapTable();
        for (int i = 0; i < table.Count; i++)
        {
            float diff = System.Math.Abs(System.Math.Abs(pickedMm) - table[i].DistanceMm);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                snappedMm = table[i].DistanceMm;
                hSize = table[i].Size;
            }
        }
    }

    /// <summary>Snap a picked height to the nearest V skeleton length.</summary>
    public static void SnapHeight(float pickedMm, out float snappedMm, out int vSize)
    {
        snappedMm = 0f;
        vSize = 1;
        float bestDiff = float.PositiveInfinity;
        List<SnapEntry> table = VHeightSnapTable();
        for (int i = 0; i < table.Count; i++)
        {
            float diff = System.Math.Abs(pickedMm - table[i].DistanceMm);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                snappedMm = table[i].DistanceMm;
                vSize = table[i].Size;
            }
        }
    }

    public static List<string> AllBlockNames()
    {
        var names = new List<string>();
        foreach (int n in CatalogueData.VSizes)
            names.Add(Naming.FrameName("V", n));
        foreach (int n in CatalogueData.VCableHoleSizes)
            names.Add(Naming.FrameName("V", n, true));
        foreach (int n in CatalogueData.HSizes)
            names.Add(Naming.FrameName("H", n));
        foreach (int n in CatalogueData.HCableHoleSizes)
            names.Add(Naming.FrameName("H", n, true));
        foreach (int n in CatalogueData.HtSizes)
            names.Add(Naming.FrameName("HT", n));
        foreach (int n in CatalogueData.HtCableHoleSizes)
            names.Add(Naming.FrameName("HT", n, true));
        foreach (var p in CatalogueData.PanelPairs)
            names.Add(Naming.PanelName(p.A, p.B));
        foreach (string t in CatalogueData.VeneerTypes)
        {
            foreach (int n in CatalogueData.VeneerLengths)
                names.Add(Naming.VeneerName(t, n));
        }
        foreach (string t in CatalogueData.CapTypes)
            names.Add("Cap " + t);
        names.Add("Foot");
        foreach (int n in CatalogueData.LoadBearingBarSizes)
            names.Add(Naming.LoadBearingBarName(n));
        return names;
    }

    static bool Contains(int[] arr, int value)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i] == value)
                return true;
        }
        return false;
    }

    static bool ContainsString(string[] arr, string value)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i] == value)
                return true;
        }
        return false;
    }
}
