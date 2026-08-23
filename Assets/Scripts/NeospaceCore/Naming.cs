using System.Text.RegularExpressions;

/// <summary>Parse and format official NEOSPACE block names. HT before H.</summary>
public static class Naming
{
    public const string Frame = "Frame";
    public const string Panel = "Panel";
    public const string Veneer = "Veneer";
    public const string Cap = "Cap";
    public const string Foot = "Foot";
    public const string LoadBearingBar = "Load Bearing Bar";

    static readonly Regex ReLbb = new Regex(@"^Load Bearing Bar H(\d+)$", RegexOptions.Compiled);
    static readonly Regex ReVeneer = new Regex(@"^Veneer (Inner|Outer|In/Out) H(\d+)$", RegexOptions.Compiled);
    static readonly Regex RePanel = new Regex(@"^Panel H(\d+)xH(\d+)$", RegexOptions.Compiled);
    static readonly Regex ReCap = new Regex(@"^Cap (Side|End)$", RegexOptions.Compiled);
    static readonly Regex ReFoot = new Regex(@"^Foot$", RegexOptions.Compiled);
    static readonly Regex ReFrame = new Regex(@"^(HT|H|V)(\d+)( Cable Hole)?$", RegexOptions.Compiled);

    public sealed class ParsedName
    {
        public string Family;
        public string Type;
        public object Size; // int, IntPair-like via SizeA/SizeB, or null
        public int SizeInt;
        public int SizeA;
        public int SizeB;
        public string Variation;
        public string Name;
    }

    public static ParsedName Parse(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        Match m = ReLbb.Match(name);
        if (m.Success)
        {
            return new ParsedName
            {
                Family = LoadBearingBar,
                Type = LoadBearingBar,
                SizeInt = int.Parse(m.Groups[1].Value),
                Variation = null,
                Name = name
            };
        }

        m = ReVeneer.Match(name);
        if (m.Success)
        {
            return new ParsedName
            {
                Family = Veneer,
                Type = m.Groups[1].Value,
                SizeInt = int.Parse(m.Groups[2].Value),
                Variation = null,
                Name = name
            };
        }

        m = RePanel.Match(name);
        if (m.Success)
        {
            int a = int.Parse(m.Groups[1].Value);
            int b = int.Parse(m.Groups[2].Value);
            return new ParsedName
            {
                Family = Panel,
                Type = "Rectangular",
                SizeA = a,
                SizeB = b,
                Variation = null,
                Name = name
            };
        }

        m = ReCap.Match(name);
        if (m.Success)
        {
            return new ParsedName
            {
                Family = Cap,
                Type = m.Groups[1].Value,
                Variation = null,
                Name = name
            };
        }

        m = ReFoot.Match(name);
        if (m.Success)
        {
            return new ParsedName
            {
                Family = Foot,
                Type = Foot,
                Variation = null,
                Name = name
            };
        }

        m = ReFrame.Match(name);
        if (m.Success)
        {
            return new ParsedName
            {
                Family = Frame,
                Type = m.Groups[1].Value,
                SizeInt = int.Parse(m.Groups[2].Value),
                Variation = m.Groups[3].Success ? "Cable Hole" : null,
                Name = name
            };
        }

        return null;
    }

    public static string FrameName(string frameType, int size, bool cableHole = false)
    {
        string name = frameType + size;
        if (cableHole)
            name += " Cable Hole";
        return name;
    }

    public static string PanelName(int dimA, int dimB)
    {
        int a = dimA >= dimB ? dimA : dimB;
        int b = dimA >= dimB ? dimB : dimA;
        return "Panel H" + a + "xH" + b;
    }

    public static string VeneerName(string veneerType, int size)
    {
        return "Veneer " + veneerType + " H" + size;
    }

    public static string LoadBearingBarName(int size)
    {
        return "Load Bearing Bar H" + size;
    }

    /// <summary>Unity twist prefabs use T* while the catalogue names HT*.</summary>
    public static string ToUnityPartId(string catalogueName)
    {
        ParsedName parsed = Parse(catalogueName);
        if (parsed == null)
            return catalogueName;
        if (parsed.Family == Frame && parsed.Type == "HT")
            return "T" + parsed.SizeInt;
        if (parsed.Family == Frame)
            return parsed.Type + parsed.SizeInt;
        return catalogueName;
    }

    public static string FromUnityPartId(string partId)
    {
        if (string.IsNullOrEmpty(partId))
            return partId;
        string trimmed = partId.Trim();
        if (trimmed.Length >= 2 && (trimmed[0] == 'T' || trimmed[0] == 't') && char.IsDigit(trimmed[1]))
            return "HT" + trimmed.Substring(1);
        return trimmed;
    }
}
