using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

/// <summary>
/// Configuration code v1: <see cref="ConfigurationModel"/> ↔ "NS1-…" string.
///
/// Byte layout (see CONFIG_CODE_SCHEMA.md):
///   "NSC" | version (1) | compression (1: 0 raw, 1 deflate) | body | CRC32 (4, LE)
/// Body (possibly deflated):
///   flags varuint | beamCount varuint | panelCount varuint |
///   beams  { partCode varuint | x,y,z zigzag-varint mm | rot byte [0xFF → 3 × zigzag deci-euler] } |
///   panels { x,y,z zigzag-varint mm | axisSide byte (bits 0–2 axis, bit 3 side) } |
///   extensionCount varuint { type varuint | length varuint | bytes } — v1 writes none, skips unknown
/// Text: "NS1-" + Base64Url (RFC 4648 §5, no padding).
///
/// Canonical: before writing, records are normalized (min X/Z → 0; Y is
/// floor-referenced and kept absolute) and sorted, so the same build always
/// yields the same code, byte for byte, wherever it stood on the grid.
///
/// All failures throw <see cref="ConfigurationCodeException"/> with a
/// human-readable message; <see cref="Validate"/> wraps that into a result.
/// </summary>
public static class ConfigurationCodec
{
    public const byte SchemaVersion = 1;
    public const string TextPrefix = "NS1-";

    const byte CompressionRaw = 0;
    const byte CompressionDeflate = 1;
    const byte RotEscape = 0xFF;

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    public static string Encode(ConfigurationModel model)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        ConfigurationModel canon = Canonicalize(model);
        byte[] body = WriteBody(canon);

        byte compression = CompressionRaw;
        byte[] deflated = Deflate(body);
        if (deflated.Length < body.Length)
        {
            body = deflated;
            compression = CompressionDeflate;
        }

        using var ms = new MemoryStream(body.Length + 9);
        ms.WriteByte((byte)'N');
        ms.WriteByte((byte)'S');
        ms.WriteByte((byte)'C');
        ms.WriteByte(SchemaVersion);
        ms.WriteByte(compression);
        ms.Write(body, 0, body.Length);

        byte[] payload = ms.ToArray();
        uint crc = Crc32(payload, 0, payload.Length);

        var final = new byte[payload.Length + 4];
        Buffer.BlockCopy(payload, 0, final, 0, payload.Length);
        final[payload.Length] = (byte)(crc & 0xFF);
        final[payload.Length + 1] = (byte)((crc >> 8) & 0xFF);
        final[payload.Length + 2] = (byte)((crc >> 16) & 0xFF);
        final[payload.Length + 3] = (byte)((crc >> 24) & 0xFF);

        return TextPrefix + ToBase64Url(final);
    }

    public static ConfigurationModel Decode(string code)
    {
        byte[] bytes = TextToBytes(code);

        // 3 magic + 1 version + 1 compression + 4 crc = minimum shell.
        if (bytes.Length < 9)
            throw new ConfigurationCodeException(
                ConfigurationCodeException.Kind.BadFormat,
                "This code is too short to be a configuration code.");

        if (bytes[0] != (byte)'N' || bytes[1] != (byte)'S' || bytes[2] != (byte)'C')
            throw new ConfigurationCodeException(
                ConfigurationCodeException.Kind.BadFormat,
                "This is not a NEOSPACE configuration code (bad header).");

        byte version = bytes[3];
        if (version == 0 || version > SchemaVersion)
            throw new ConfigurationCodeException(
                ConfigurationCodeException.Kind.UnsupportedVersion,
                $"This code was made with a newer version of the configurator " +
                $"(schema v{version}, this build reads up to v{SchemaVersion}). Please update.");

        uint stored = (uint)(bytes[bytes.Length - 4]
                     | bytes[bytes.Length - 3] << 8
                     | bytes[bytes.Length - 2] << 16
                     | bytes[bytes.Length - 1] << 24);
        uint actual = Crc32(bytes, 0, bytes.Length - 4);
        if (stored != actual)
            throw new ConfigurationCodeException(
                ConfigurationCodeException.Kind.ChecksumMismatch,
                "The code is damaged (checksum mismatch) · it was probably " +
                "mistyped or truncated. Copy it again from the source.");

        byte compression = bytes[4];
        byte[] body;
        try
        {
            body = compression switch
            {
                CompressionRaw => Slice(bytes, 5, bytes.Length - 9),
                CompressionDeflate => Inflate(bytes, 5, bytes.Length - 9),
                _ => throw new ConfigurationCodeException(
                    ConfigurationCodeException.Kind.Corrupt,
                    $"Unknown compression method ({compression}).")
            };
        }
        catch (ConfigurationCodeException) { throw; }
        catch (Exception e)
        {
            throw new ConfigurationCodeException(
                ConfigurationCodeException.Kind.Corrupt,
                "The code's payload could not be decompressed.", e);
        }

        return ReadBody(body);
    }

    public static ConfigurationCodeValidation Validate(string code)
    {
        var result = new ConfigurationCodeValidation { Warnings = new List<string>() };
        try
        {
            result.Model = Decode(code);
        }
        catch (ConfigurationCodeException e)
        {
            result.IsValid = false;
            result.Error = e.Message;
            return result;
        }

        result.IsValid = true;
        foreach (BeamRecord b in result.Model.Beams)
        {
            if (PartRegistry.IsRetired(b.PartCode) &&
                PartRegistry.TryGetPart(b.PartCode, out string partId, out _))
            {
                string warning = $"Part {partId} is retired · it may not be placeable in this build.";
                if (!result.Warnings.Contains(warning))
                    result.Warnings.Add(warning);
            }
        }
        return result;
    }

    // ------------------------------------------------------------------
    // Canonicalization — same build, same bytes, always
    // ------------------------------------------------------------------

    static ConfigurationModel Canonicalize(ConfigurationModel model)
    {
        var canon = new ConfigurationModel { FinishApplied = model.FinishApplied };
        canon.Beams.AddRange(model.Beams);
        canon.Panels.AddRange(model.Panels);

        // Position independence: shift so the minimum X/Z is 0. Y stays
        // absolute — it is floor-referenced and must survive round trips.
        int minX = int.MaxValue, minZ = int.MaxValue;
        foreach (BeamRecord b in canon.Beams) { minX = Math.Min(minX, b.XMm); minZ = Math.Min(minZ, b.ZMm); }
        foreach (PanelRecord p in canon.Panels) { minX = Math.Min(minX, p.XMm); minZ = Math.Min(minZ, p.ZMm); }

        if (minX != int.MaxValue)
        {
            for (int i = 0; i < canon.Beams.Count; i++)
            {
                BeamRecord b = canon.Beams[i];
                b.XMm -= minX;
                b.ZMm -= minZ;
                canon.Beams[i] = b;
            }
            for (int i = 0; i < canon.Panels.Count; i++)
            {
                PanelRecord p = canon.Panels[i];
                p.XMm -= minX;
                p.ZMm -= minZ;
                canon.Panels[i] = p;
            }
        }

        canon.Beams.Sort(CompareBeams);
        canon.Panels.Sort(ComparePanels);
        return canon;
    }

    static int CompareBeams(BeamRecord a, BeamRecord b)
    {
        int c = a.PartCode.CompareTo(b.PartCode); if (c != 0) return c;
        c = a.XMm.CompareTo(b.XMm); if (c != 0) return c;
        c = a.YMm.CompareTo(b.YMm); if (c != 0) return c;
        c = a.ZMm.CompareTo(b.ZMm); if (c != 0) return c;
        c = a.OrientIndex.CompareTo(b.OrientIndex); if (c != 0) return c;
        c = a.EulerXDeci.CompareTo(b.EulerXDeci); if (c != 0) return c;
        c = a.EulerYDeci.CompareTo(b.EulerYDeci); if (c != 0) return c;
        return a.EulerZDeci.CompareTo(b.EulerZDeci);
    }

    static int ComparePanels(PanelRecord a, PanelRecord b)
    {
        int c = a.XMm.CompareTo(b.XMm); if (c != 0) return c;
        c = a.YMm.CompareTo(b.YMm); if (c != 0) return c;
        c = a.ZMm.CompareTo(b.ZMm); if (c != 0) return c;
        c = ((byte)a.Axis).CompareTo((byte)b.Axis); if (c != 0) return c;
        return a.SideMinus.CompareTo(b.SideMinus);
    }

    // ------------------------------------------------------------------
    // Body
    // ------------------------------------------------------------------

    static byte[] WriteBody(ConfigurationModel model)
    {
        using var ms = new MemoryStream(16 + model.Beams.Count * 10 + model.Panels.Count * 8);

        WriteVarUInt(ms, model.FinishApplied ? 1u : 0u);   // flags, bit0 finish
        WriteVarUInt(ms, (uint)model.Beams.Count);
        WriteVarUInt(ms, (uint)model.Panels.Count);

        foreach (BeamRecord b in model.Beams)
        {
            if (!PartRegistry.TryGetPart(b.PartCode, out _, out _))
                throw new ArgumentException(
                    $"Beam has unregistered part code {b.PartCode} · add it to PartRegistry first.");

            WriteVarUInt(ms, (uint)b.PartCode);
            WriteVarInt(ms, b.XMm);
            WriteVarInt(ms, b.YMm);
            WriteVarInt(ms, b.ZMm);

            if (PoseQuantizer.IsValidOrientationIndex(b.OrientIndex))
            {
                ms.WriteByte((byte)b.OrientIndex);
            }
            else
            {
                ms.WriteByte(RotEscape);
                WriteVarInt(ms, b.EulerXDeci);
                WriteVarInt(ms, b.EulerYDeci);
                WriteVarInt(ms, b.EulerZDeci);
            }
        }

        foreach (PanelRecord p in model.Panels)
        {
            WriteVarInt(ms, p.XMm);
            WriteVarInt(ms, p.YMm);
            WriteVarInt(ms, p.ZMm);
            ms.WriteByte((byte)((byte)p.Axis | (p.SideMinus ? 0x08 : 0x00)));
        }

        WriteVarUInt(ms, 0u);   // extension count — none in v1
        return ms.ToArray();
    }

    static ConfigurationModel ReadBody(byte[] body)
    {
        var model = new ConfigurationModel();
        int pos = 0;

        uint flags = ReadVarUInt(body, ref pos);
        model.FinishApplied = (flags & 1u) != 0;

        uint beamCount = ReadVarUInt(body, ref pos);
        uint panelCount = ReadVarUInt(body, ref pos);
        if (beamCount > 100_000 || panelCount > 100_000)
            throw new ConfigurationCodeException(
                ConfigurationCodeException.Kind.Corrupt,
                "The code claims an implausible number of parts.");

        for (uint i = 0; i < beamCount; i++)
        {
            var b = new BeamRecord
            {
                PartCode = (int)ReadVarUInt(body, ref pos),
                XMm = ReadVarInt(body, ref pos),
                YMm = ReadVarInt(body, ref pos),
                ZMm = ReadVarInt(body, ref pos)
            };

            byte rot = ReadByte(body, ref pos);
            if (rot == RotEscape)
            {
                b.OrientIndex = -1;
                b.EulerXDeci = ReadVarInt(body, ref pos);
                b.EulerYDeci = ReadVarInt(body, ref pos);
                b.EulerZDeci = ReadVarInt(body, ref pos);
            }
            else if (PoseQuantizer.IsValidOrientationIndex(rot))
            {
                b.OrientIndex = rot;
            }
            else
            {
                throw new ConfigurationCodeException(
                    ConfigurationCodeException.Kind.Corrupt,
                    $"Beam {i + 1} has an invalid rotation code ({rot}).");
            }

            if (!PartRegistry.TryGetPart(b.PartCode, out _, out _))
                throw new ConfigurationCodeException(
                    ConfigurationCodeException.Kind.Corrupt,
                    $"The code contains a part this version doesn't know (part code {b.PartCode}). " +
                    "It may have been made with a newer catalogue.");

            model.Beams.Add(b);
        }

        for (uint i = 0; i < panelCount; i++)
        {
            var p = new PanelRecord
            {
                XMm = ReadVarInt(body, ref pos),
                YMm = ReadVarInt(body, ref pos),
                ZMm = ReadVarInt(body, ref pos)
            };

            byte axisSide = ReadByte(body, ref pos);
            int axis = axisSide & 0x07;
            if (axis > 5)
                throw new ConfigurationCodeException(
                    ConfigurationCodeException.Kind.Corrupt,
                    $"Panel {i + 1} has an invalid slot axis ({axis}).");
            p.Axis = (SlotAxis)axis;
            p.SideMinus = (axisSide & 0x08) != 0;

            model.Panels.Add(p);
        }

        // Extensions: skip anything we don't know (forward compatibility).
        uint extCount = ReadVarUInt(body, ref pos);
        for (uint i = 0; i < extCount; i++)
        {
            ReadVarUInt(body, ref pos);                    // type — ignored in v1
            uint length = ReadVarUInt(body, ref pos);
            if (pos + length > body.Length)
                throw new ConfigurationCodeException(
                    ConfigurationCodeException.Kind.Corrupt,
                    "An extension block runs past the end of the code.");
            pos += (int)length;
        }

        return model;
    }

    // ------------------------------------------------------------------
    // Primitives
    // ------------------------------------------------------------------

    static void WriteVarUInt(Stream s, uint value)
    {
        while (value >= 0x80)
        {
            s.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }
        s.WriteByte((byte)value);
    }

    static void WriteVarInt(Stream s, int value) =>
        WriteVarUInt(s, (uint)((value << 1) ^ (value >> 31)));   // zigzag

    static uint ReadVarUInt(byte[] data, ref int pos)
    {
        uint value = 0;
        int shift = 0;
        while (true)
        {
            byte b = ReadByte(data, ref pos);
            value |= (uint)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                return value;
            shift += 7;
            if (shift > 28)
                throw new ConfigurationCodeException(
                    ConfigurationCodeException.Kind.Corrupt,
                    "A number in the code is malformed.");
        }
    }

    static int ReadVarInt(byte[] data, ref int pos)
    {
        uint raw = ReadVarUInt(data, ref pos);
        return (int)(raw >> 1) ^ -(int)(raw & 1);                // un-zigzag
    }

    static byte ReadByte(byte[] data, ref int pos)
    {
        if (pos >= data.Length)
            throw new ConfigurationCodeException(
                ConfigurationCodeException.Kind.Corrupt,
                "The code ends unexpectedly · it looks truncated.");
        return data[pos++];
    }

    static byte[] Slice(byte[] data, int offset, int count)
    {
        var result = new byte[count];
        Buffer.BlockCopy(data, offset, result, 0, count);
        return result;
    }

    static byte[] Deflate(byte[] data)
    {
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
            deflate.Write(data, 0, data.Length);
        return output.ToArray();
    }

    static byte[] Inflate(byte[] data, int offset, int count)
    {
        using var input = new MemoryStream(data, offset, count);
        using var inflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        inflate.CopyTo(output);
        return output.ToArray();
    }

    // ------------------------------------------------------------------
    // Text form — Base64Url without padding (RFC 4648 §5)
    // ------------------------------------------------------------------

    static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Internal for the self-test, which crafts tampered payloads.</summary>
    internal static string BytesToText(byte[] bytes) => TextPrefix + ToBase64Url(bytes);

    internal static byte[] TextToBytes(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ConfigurationCodeException(
                ConfigurationCodeException.Kind.BadFormat,
                "The code is empty.");

        code = code.Trim();
        if (!code.StartsWith(TextPrefix, StringComparison.Ordinal))
            throw new ConfigurationCodeException(
                ConfigurationCodeException.Kind.BadFormat,
                $"Configuration codes start with \"{TextPrefix}\".");

        string b64 = code.Substring(TextPrefix.Length)
            .Replace('-', '+').Replace('_', '/');
        switch (b64.Length % 4)
        {
            case 2: b64 += "=="; break;
            case 3: b64 += "="; break;
            case 1:
                throw new ConfigurationCodeException(
                    ConfigurationCodeException.Kind.BadFormat,
                    "The code has an impossible length · part of it is missing.");
        }

        try
        {
            return Convert.FromBase64String(b64);
        }
        catch (FormatException e)
        {
            throw new ConfigurationCodeException(
                ConfigurationCodeException.Kind.BadFormat,
                "The code contains characters that don't belong in a configuration code.", e);
        }
    }

    /// <summary>Internal for the self-test (recomputing CRCs of tampered payloads).</summary>
    internal static uint Crc32(byte[] data, int offset, int count)
    {
        uint crc = 0xFFFFFFFFu;
        for (int i = offset; i < offset + count; i++)
        {
            crc ^= data[i];
            for (int bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ (0xEDB88320u & (uint)-(int)(crc & 1));
        }
        return ~crc;
    }
}

public sealed class ConfigurationCodeException : Exception
{
    public enum Kind
    {
        BadFormat,           // not a code at all (prefix, characters, length)
        UnsupportedVersion,  // made by a newer schema
        ChecksumMismatch,    // typo / truncation
        Corrupt              // structurally broken payload
    }

    public Kind Failure { get; }

    public ConfigurationCodeException(Kind kind, string message, Exception inner = null)
        : base(message, inner)
    {
        Failure = kind;
    }
}

public struct ConfigurationCodeValidation
{
    public bool IsValid;
    public string Error;                 // set when invalid
    public List<string> Warnings;        // retired parts etc.
    public ConfigurationModel Model;     // set when valid
}
