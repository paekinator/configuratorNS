using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

/// <summary>
/// Configuration codes for whole SPACES ("NSS1-…"): a space code embeds the
/// full piece code of every distinct piece used, plus one entry per placed
/// instance (which piece, where on the grid, how many quarter turns). That
/// makes a space code self-contained — the receiver needs no piece library,
/// the pieces travel inside the code.
///
/// Same guarantees as piece codes: canonical (same space → same code, no
/// matter where it stands or in which order pieces were placed), compressed,
/// URL-safe, versioned, checksummed, and invalid codes fail with a clear
/// message instead of touching the scene.
/// </summary>
public static class SpaceCodec
{
    public const string Prefix = "NSS1-";
    const byte Version = 1;

    public static bool LooksLikeSpaceCode(string text) =>
        text != null && text.TrimStart().StartsWith(Prefix, StringComparison.Ordinal);

    static ConfigurationCodeException Bad(string message) =>
        new ConfigurationCodeException(ConfigurationCodeException.Kind.BadFormat, message);

    static ConfigurationCodeException Damaged(string message) =>
        new ConfigurationCodeException(ConfigurationCodeException.Kind.Corrupt, message);

    // ------------------------------------------------------------------
    // Encode
    // ------------------------------------------------------------------

    public static string Encode(List<SpaceHistory.InstanceState> states)
    {
        if (states == null || states.Count == 0)
            throw Bad("The space is empty · place at least one piece before sharing a code.");

        // Distinct pieces, sorted by code for determinism.
        var byCode = new Dictionary<string, (string name, int priceCents)>(StringComparer.Ordinal);
        foreach (SpaceHistory.InstanceState s in states)
        {
            if (string.IsNullOrEmpty(s.Code))
                throw Bad($"\"{s.PieceName}\" has no piece code and can't be shared.");
            if (!byCode.ContainsKey(s.Code))
                byCode[s.Code] = (CleanName(s.PieceName), Mathf.RoundToInt(s.Price * 100f));
        }

        var codes = new List<string>(byCode.Keys);
        codes.Sort(StringComparer.Ordinal);
        var indexByCode = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < codes.Count; i++)
            indexByCode[codes[i]] = i;

        // Instances in integer grid coordinates, normalised to min = 0.
        var instances = new List<(int piece, int xMm, int zMm, int quarter)>(states.Count);
        int minX = int.MaxValue, minZ = int.MaxValue;
        foreach (SpaceHistory.InstanceState s in states)
        {
            int x = Mathf.RoundToInt(NeospaceUnits.ToMm(s.Position.x));
            int z = Mathf.RoundToInt(NeospaceUnits.ToMm(s.Position.z));
            int q = ((Mathf.RoundToInt(s.YawDegrees / 90f) % 4) + 4) % 4;
            instances.Add((indexByCode[s.Code], x, z, q));
            minX = Mathf.Min(minX, x);
            minZ = Mathf.Min(minZ, z);
        }
        for (int i = 0; i < instances.Count; i++)
            instances[i] = (instances[i].piece,
                instances[i].xMm - minX, instances[i].zMm - minZ, instances[i].quarter);

        instances.Sort((a, b) =>
        {
            int c = a.piece.CompareTo(b.piece);
            if (c == 0) c = a.xMm.CompareTo(b.xMm);
            if (c == 0) c = a.zMm.CompareTo(b.zMm);
            return c != 0 ? c : a.quarter.CompareTo(b.quarter);
        });

        var sb = new StringBuilder(256);
        sb.Append("S1\n");
        foreach (string code in codes)
        {
            (string name, int priceCents) = byCode[code];
            sb.Append("P|").Append(name).Append('|').Append(priceCents)
              .Append('|').Append(code).Append('\n');
        }
        foreach ((int piece, int xMm, int zMm, int quarter) in instances)
            sb.Append("I|").Append(piece).Append('|').Append(xMm)
              .Append('|').Append(zMm).Append('|').Append(quarter).Append('\n');

        byte[] payload = Deflate(Encoding.UTF8.GetBytes(sb.ToString()));

        using var ms = new MemoryStream(payload.Length + 8);
        ms.WriteByte((byte)'N');
        ms.WriteByte((byte)'S');
        ms.WriteByte((byte)'S');
        ms.WriteByte(Version);
        ms.Write(payload, 0, payload.Length);

        byte[] body = ms.ToArray();
        uint crc = ConfigurationCodec.Crc32(body, 0, body.Length);
        ms.WriteByte((byte)(crc & 0xFF));
        ms.WriteByte((byte)((crc >> 8) & 0xFF));
        ms.WriteByte((byte)((crc >> 16) & 0xFF));
        ms.WriteByte((byte)((crc >> 24) & 0xFF));

        return Prefix + ToBase64Url(ms.ToArray());
    }

    // ------------------------------------------------------------------
    // Decode
    // ------------------------------------------------------------------

    /// <summary>Throws <see cref="ConfigurationCodeException"/> on any problem.</summary>
    public static List<SpaceHistory.InstanceState> Decode(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw Bad("The code is empty.");

        text = text.Trim();
        if (!text.StartsWith(Prefix, StringComparison.Ordinal))
            throw Bad("Not a space code (it should start with NSS1-).");

        byte[] bytes = FromBase64Url(text.Substring(Prefix.Length));
        if (bytes.Length < 9)
            throw Bad("The code is truncated.");

        if (bytes[0] != 'N' || bytes[1] != 'S' || bytes[2] != 'S')
            throw Bad("This is not a NEOSPACE space code.");
        if (bytes[3] > Version)
            throw new ConfigurationCodeException(
                ConfigurationCodeException.Kind.UnsupportedVersion,
                "This space code was made with a newer version of the configurator.");

        uint stored = (uint)(bytes[bytes.Length - 4] | (bytes[bytes.Length - 3] << 8) |
                             (bytes[bytes.Length - 2] << 16) | (bytes[bytes.Length - 1] << 24));
        if (ConfigurationCodec.Crc32(bytes, 0, bytes.Length - 4) != stored)
            throw new ConfigurationCodeException(
                ConfigurationCodeException.Kind.ChecksumMismatch,
                "The code is damaged (checksum mismatch) · copy it again in one piece.");

        string payload;
        try
        {
            byte[] packed = new byte[bytes.Length - 8];
            Array.Copy(bytes, 4, packed, 0, packed.Length);
            payload = Encoding.UTF8.GetString(Inflate(packed));
        }
        catch (Exception)
        {
            throw Damaged("The code could not be unpacked · it may be damaged.");
        }

        string[] lines = payload.Split('\n');
        if (lines.Length == 0 || lines[0] != "S1")
            throw Damaged("The code content is not recognised.");

        var pieces = new List<(string name, float price, string code)>();
        var states = new List<SpaceHistory.InstanceState>();

        foreach (string line in lines)
        {
            if (line.StartsWith("P|", StringComparison.Ordinal))
            {
                // P|name|priceCents|pieceCode — the piece code never contains '|'.
                string[] parts = line.Split('|');
                if (parts.Length != 4 || !int.TryParse(parts[2], out int cents))
                    throw Damaged("The code contains a malformed piece entry.");
                pieces.Add((parts[1], cents / 100f, parts[3]));
            }
            else if (line.StartsWith("I|", StringComparison.Ordinal))
            {
                string[] parts = line.Split('|');
                if (parts.Length != 5 ||
                    !int.TryParse(parts[1], out int pieceIndex) ||
                    !int.TryParse(parts[2], out int xMm) ||
                    !int.TryParse(parts[3], out int zMm) ||
                    !int.TryParse(parts[4], out int quarter))
                    throw Damaged("The code contains a malformed placement entry.");
                if (pieceIndex < 0 || pieceIndex >= pieces.Count)
                    throw Damaged("The code references a missing piece.");

                (string name, float price, string code) = pieces[pieceIndex];
                states.Add(new SpaceHistory.InstanceState
                {
                    PieceId = "shared",
                    PieceName = name,
                    Code = code,
                    Price = price,
                    Position = new Vector3(NeospaceUnits.Mm(xMm), 0f, NeospaceUnits.Mm(zMm)),
                    YawDegrees = ((quarter % 4) + 4) % 4 * 90f
                });
            }
        }

        if (states.Count == 0)
            throw Damaged("The code contains no placed pieces.");

        // Every embedded piece code must itself be loadable.
        foreach ((string name, float _, string code) in pieces)
        {
            ConfigurationCodeValidation check = ConfigurationCode.Validate(code);
            if (!check.IsValid)
                throw Damaged($"The piece \"{name}\" inside this space code is invalid: {check.Error}");
        }

        return states;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    static string CleanName(string name) =>
        string.IsNullOrEmpty(name)
            ? "Piece"
            : name.Replace('|', ' ').Replace('\n', ' ').Replace('\r', ' ');

    static byte[] Deflate(byte[] raw)
    {
        using var ms = new MemoryStream();
        using (var ds = new DeflateStream(
                   ms, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
            ds.Write(raw, 0, raw.Length);
        return ms.ToArray();
    }

    static byte[] Inflate(byte[] packed)
    {
        using var input = new MemoryStream(packed);
        using var ds = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        ds.CopyTo(output);
        return output.ToArray();
    }

    static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    static byte[] FromBase64Url(string text)
    {
        string s = text.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
            case 1: throw Bad("The code has an invalid length.");
        }
        try
        {
            return Convert.FromBase64String(s);
        }
        catch (FormatException)
        {
            throw Bad("The code contains invalid characters.");
        }
    }
}
