using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Self-tests for the configuration codec, following the
/// <see cref="NeospaceSelfTest"/> pattern (pure logic, no scene needed).
/// Run from Tools → Configurator → Run ConfigCode Selftest.
/// </summary>
public static class ConfigurationCodeSelfTest
{
    /// <summary>
    /// Format freeze: this exact string is schema v1 for a single V1 frame at
    /// (0, 305, 0) mm with orientation 0. If encoding it ever produces
    /// anything else, the byte layout changed and SchemaVersion MUST be
    /// bumped (old codes in the wild depend on it).
    /// </summary>
    public const string GoldenCode = "NS1-TlNDAQAAAQABAOIEAAAAvCOpXA";

    public static int RunAll(out List<string> failures)
    {
        var failureList = new List<string>();
        failures = failureList;
        int total = 0;

        void Check(bool ok, string label)
        {
            total++;
            if (!ok)
                failureList.Add(label);
        }

        // ------------------------------------------------------------
        // Golden code — byte-layout freeze
        // ------------------------------------------------------------
        var golden = new ConfigurationModel();
        golden.Beams.Add(new BeamRecord { PartCode = 1, XMm = 0, YMm = 305, ZMm = 0, OrientIndex = 0 });

        Check(ConfigurationCodec.Encode(golden) == GoldenCode,
            "golden model encodes to the frozen v1 code");

        ConfigurationModel g = ConfigurationCodec.Decode(GoldenCode);
        Check(g.Beams.Count == 1 && g.Panels.Count == 0 && !g.FinishApplied,
            "golden code decodes to 1 beam, 0 panels, no finish");
        Check(g.Beams[0].PartCode == 1 && g.Beams[0].XMm == 0 &&
              g.Beams[0].YMm == 305 && g.Beams[0].ZMm == 0 && g.Beams[0].OrientIndex == 0,
            "golden beam fields survive decode");

        // ------------------------------------------------------------
        // Round trip — everything the format stores survives
        // ------------------------------------------------------------
        ConfigurationModel rich = BuildRichModel();
        string richCode = ConfigurationCodec.Encode(rich);
        ConfigurationModel back = ConfigurationCodec.Decode(richCode);

        Check(ConfigurationCodec.Encode(back) == richCode,
            "encode(decode(code)) == code (full fidelity)");
        Check(back.Beams.Count == rich.Beams.Count && back.Panels.Count == rich.Panels.Count,
            "round trip keeps part counts");
        Check(back.FinishApplied == rich.FinishApplied,
            "round trip keeps the finish flag");

        BeamRecord euler = back.Beams.Find(b => b.OrientIndex == -1);
        Check(euler.EulerXDeci == 123 && euler.EulerYDeci == 456 && euler.EulerZDeci == 3599,
            "euler-fallback rotation survives to the 0.1 degree");

        PanelRecord lying = back.Panels.Find(p => p.Axis == SlotAxis.PlusY);
        Check(lying.SideMinus, "panel axis + side survive");

        // ------------------------------------------------------------
        // Determinism & canonicalization
        // ------------------------------------------------------------
        Check(ConfigurationCodec.Encode(rich) == richCode,
            "encoding twice gives the identical code");

        ConfigurationModel shuffled = BuildRichModel();
        shuffled.Beams.Reverse();
        shuffled.Panels.Reverse();
        Check(ConfigurationCodec.Encode(shuffled) == richCode,
            "record order does not change the code (canonical sort)");

        BeamRecord offsetBeam = back.Beams.Find(b => b.PartCode == 35 && b.OrientIndex == -1);
        Check(offsetBeam.XMm == -88 && offsetBeam.ZMm == 176,
            "absolute grid X/Z survive encode (not shifted to origin)");

        ConfigurationModel moved = BuildRichModel();
        for (int i = 0; i < moved.Beams.Count; i++)
        {
            BeamRecord b = moved.Beams[i]; b.XMm += 880; b.ZMm += 1760; moved.Beams[i] = b;
        }
        for (int i = 0; i < moved.Panels.Count; i++)
        {
            PanelRecord p = moved.Panels[i]; p.XMm += 880; p.ZMm += 1760; moved.Panels[i] = p;
        }
        string movedCode = ConfigurationCodec.Encode(moved);
        Check(movedCode != richCode,
            "the same build on different grid cells produces a different code");
        ConfigurationModel movedBack = ConfigurationCodec.Decode(movedCode);
        Check(movedBack.Beams.Exists(b => b.PartCode == 35 && b.OrientIndex == -1 &&
                                         b.XMm == -88 + 880 && b.ZMm == 176 + 1760),
            "import restores the saved grid cell, not the origin");

        ConfigurationModel raised = BuildRichModel();
        for (int i = 0; i < raised.Beams.Count; i++)
        {
            BeamRecord b = raised.Beams[i]; b.YMm += 88; raised.Beams[i] = b;
        }
        Check(ConfigurationCodec.Encode(raised) != richCode,
            "height is absolute · raising the build changes the code");

        // ------------------------------------------------------------
        // Orientation table sanity
        // ------------------------------------------------------------
        bool orientationsOk = true;
        for (int i = 0; i < 24; i++)
        {
            if (!PoseQuantizer.TryGetOrientationIndex(PoseQuantizer.FromOrientationIndex(i), out int idx) || idx != i)
                orientationsOk = false;
        }
        Check(orientationsOk, "all 24 orientations round-trip through the index");
        Check(!PoseQuantizer.TryGetOrientationIndex(Quaternion.Euler(0f, 12.3f, 0f), out _),
            "an off-axis rotation is NOT matched to an orientation index");

        // ------------------------------------------------------------
        // Invalid codes fail clearly
        // ------------------------------------------------------------
        Check(FailsWith(null, ConfigurationCodeException.Kind.BadFormat), "null code → BadFormat");
        Check(FailsWith("", ConfigurationCodeException.Kind.BadFormat), "empty code → BadFormat");
        Check(FailsWith("hello world", ConfigurationCodeException.Kind.BadFormat), "no prefix → BadFormat");
        Check(FailsWith("NS1-!!!!", ConfigurationCodeException.Kind.BadFormat), "bad characters → BadFormat");
        Check(FailsWith("NS1-TlND", ConfigurationCodeException.Kind.BadFormat), "far too short → BadFormat");
        Check(FailsWith(GoldenCode.Substring(0, GoldenCode.Length - 5),
            ConfigurationCodeException.Kind.ChecksumMismatch, ConfigurationCodeException.Kind.BadFormat),
            "truncated code → checksum/format failure");
        Check(FailsWith(FlipMiddleChar(richCode), ConfigurationCodeException.Kind.ChecksumMismatch,
            ConfigurationCodeException.Kind.BadFormat),
            "one wrong character → checksum failure");

        var validation = ConfigurationCodec.Validate("NS1-garbage!");
        Check(!validation.IsValid && !string.IsNullOrEmpty(validation.Error),
            "Validate reports invalid with an error message");

        // ------------------------------------------------------------
        // Version compatibility
        // ------------------------------------------------------------
        byte[] raw = ConfigurationCodec.TextToBytes(GoldenCode);
        raw[3] = 2;   // pretend schema v2
        uint crc = ConfigurationCodec.Crc32(raw, 0, raw.Length - 4);
        raw[raw.Length - 4] = (byte)(crc & 0xFF);
        raw[raw.Length - 3] = (byte)((crc >> 8) & 0xFF);
        raw[raw.Length - 2] = (byte)((crc >> 16) & 0xFF);
        raw[raw.Length - 1] = (byte)((crc >> 24) & 0xFF);
        string v2Code = ConfigurationCodec.BytesToText(raw);

        Check(FailsWith(v2Code, ConfigurationCodeException.Kind.UnsupportedVersion),
            "a newer-schema code is refused as UnsupportedVersion");
        var v2Validation = ConfigurationCodec.Validate(v2Code);
        Check(!v2Validation.IsValid && v2Validation.Error.Contains("newer"),
            "the version error tells the user to update");

        // ------------------------------------------------------------
        // Part registry: permanence & retirement
        // ------------------------------------------------------------
        Check(PartRegistry.TryGetCode("V13", out int v13) && v13 == 7, "V13 has permanent id 7");
        Check(PartRegistry.TryGetCode("T5", out int t5) && t5 == 66, "T5 has permanent id 66");
        Check(PartRegistry.TryGetPart(14, out string v14, out bool retired) && v14 == "V14" && retired,
            "retired V14 still resolves (old codes stay readable)");
        Check(!PartRegistry.TryGetCode("V99", out _), "unknown part has no id");

        var withRetired = new ConfigurationModel();
        withRetired.Beams.Add(new BeamRecord { PartCode = 14, OrientIndex = 0 });
        var retiredValidation = ConfigurationCodec.Validate(ConfigurationCodec.Encode(withRetired));
        Check(retiredValidation.IsValid && retiredValidation.Warnings.Count == 1,
            "a code with a retired part validates with a warning");

        var bogus = new ConfigurationModel();
        bogus.Beams.Add(new BeamRecord { PartCode = 999, OrientIndex = 0 });
        bool encodeRejected = false;
        try { ConfigurationCodec.Encode(bogus); }
        catch (ArgumentException) { encodeRejected = true; }
        Check(encodeRejected, "encoding an unregistered part code is rejected");

        return total;
    }

    static ConfigurationModel BuildRichModel()
    {
        var model = new ConfigurationModel { FinishApplied = true };

        model.Beams.Add(new BeamRecord { PartCode = 7, XMm = 0, YMm = 30, ZMm = 0, OrientIndex = 0 });
        model.Beams.Add(new BeamRecord { PartCode = 7, XMm = 616, YMm = 30, ZMm = 0, OrientIndex = 5 });
        model.Beams.Add(new BeamRecord { PartCode = 35, XMm = 308, YMm = 448, ZMm = 0, OrientIndex = 23 });
        model.Beams.Add(new BeamRecord { PartCode = 66, XMm = 308, YMm = 888, ZMm = -264, OrientIndex = 11 });
        model.Beams.Add(new BeamRecord
        {
            PartCode = 35, XMm = -88, YMm = 448, ZMm = 176,
            OrientIndex = -1, EulerXDeci = 123, EulerYDeci = 456, EulerZDeci = 3599
        });

        model.Panels.Add(new PanelRecord { XMm = 308, YMm = 500, ZMm = 18, Axis = SlotAxis.PlusZ, SideMinus = false });
        model.Panels.Add(new PanelRecord { XMm = 308, YMm = 880, ZMm = -100, Axis = SlotAxis.PlusY, SideMinus = true });
        model.Panels.Add(new PanelRecord { XMm = 0, YMm = 500, ZMm = 18, Axis = SlotAxis.MinusX, SideMinus = false });

        return model;
    }

    static bool FailsWith(string code, params ConfigurationCodeException.Kind[] accepted)
    {
        try
        {
            ConfigurationCodec.Decode(code);
            return false;
        }
        catch (ConfigurationCodeException e)
        {
            return Array.IndexOf(accepted, e.Failure) >= 0;
        }
    }

    static string FlipMiddleChar(string code)
    {
        int i = code.Length / 2;
        char c = code[i] == 'A' ? 'B' : 'A';
        return code.Substring(0, i) + c + code.Substring(i + 1);
    }
}
