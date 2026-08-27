using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Self-tests for the space codec and the edit-from-space semantics —
/// pure logic, no scene needed. Run from
/// Tools → Configurator → Run SpaceCode Selftest.
/// </summary>
public static class SpaceCodeSelfTest
{
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

        bool Throws(Action action, ConfigurationCodeException.Kind? kind = null)
        {
            try { action(); return false; }
            catch (ConfigurationCodeException e)
            {
                return kind == null || e.Failure == kind.Value;
            }
        }

        // Two valid piece codes to embed.
        string chairCode = ConfigurationCodeSelfTest.GoldenCode;

        var tableModel = new ConfigurationModel();
        tableModel.Beams.Add(new BeamRecord { PartCode = 2, XMm = 0, YMm = 305, ZMm = 0, OrientIndex = 0 });
        tableModel.Beams.Add(new BeamRecord { PartCode = 13, XMm = 704, YMm = 305, ZMm = 0, OrientIndex = 3 });
        string tableCode = ConfigurationCodec.Encode(tableModel);

        Check(chairCode != tableCode, "the two sample piece codes differ");

        SpaceHistory.InstanceState At(string name, string code, float price,
            int xModules, int zModules, float yaw) => new SpaceHistory.InstanceState
        {
            PieceId = "test",
            PieceName = name,
            Code = code,
            Price = price,
            Position = new Vector3(
                NeospaceUnits.Mm(xModules * 88), 0f, NeospaceUnits.Mm(zModules * 88)),
            YawDegrees = yaw
        };

        // One table + four chairs, like the acceptance scenario.
        List<SpaceHistory.InstanceState> Space() => new List<SpaceHistory.InstanceState>
        {
            At("Table", tableCode, 260f, 8, 8, 0f),
            At("Chair", chairCode, 45f, 0, 8, 90f),
            At("Chair", chairCode, 45f, 16, 8, 270f),
            At("Chair", chairCode, 45f, 8, 0, 0f),
            At("Chair", chairCode, 45f, 8, 16, 180f)
        };

        // ------------------------------------------------------------
        // Round trip
        // ------------------------------------------------------------
        List<SpaceHistory.InstanceState> space = Space();
        string spaceCode = SpaceCodec.Encode(space);

        Check(spaceCode.StartsWith(SpaceCodec.Prefix, StringComparison.Ordinal),
            "space code carries the NSS1- prefix");
        Check(SpaceCodec.LooksLikeSpaceCode(spaceCode), "space code is recognised as one");
        Check(!SpaceCodec.LooksLikeSpaceCode(chairCode), "piece codes are not mistaken for space codes");

        List<SpaceHistory.InstanceState> back = SpaceCodec.Decode(spaceCode);
        Check(back.Count == 5, "round trip keeps the instance count");
        Check(SpaceCodec.Encode(back) == spaceCode, "encode(decode(code)) == code");

        int chairs = 0, tables = 0;
        foreach (SpaceHistory.InstanceState s in back)
        {
            if (s.Code == chairCode) chairs++;
            if (s.Code == tableCode) tables++;
        }
        Check(chairs == 4 && tables == 1, "embedded piece codes survive (4 chairs, 1 table)");

        bool namesOk = true, pricesOk = true;
        foreach (SpaceHistory.InstanceState s in back)
        {
            if (s.Code == chairCode && (s.PieceName != "Chair" || Mathf.Abs(s.Price - 45f) > 0.001f))
                namesOk = pricesOk = false;
            if (s.Code == tableCode && (s.PieceName != "Table" || Mathf.Abs(s.Price - 260f) > 0.001f))
                namesOk = pricesOk = false;
        }
        Check(namesOk && pricesOk, "names and prices survive the round trip");

        // Relative layout survives (encoding normalises to min = 0, which the
        // sample already is).
        bool poseFound = false;
        foreach (SpaceHistory.InstanceState s in back)
        {
            if (s.Code == tableCode &&
                Mathf.RoundToInt(NeospaceUnits.ToMm(s.Position.x)) == 8 * 88 &&
                Mathf.RoundToInt(NeospaceUnits.ToMm(s.Position.z)) == 8 * 88 &&
                Mathf.RoundToInt(s.YawDegrees) == 0)
                poseFound = true;
        }
        Check(poseFound, "instance grid positions and yaw survive");

        // ------------------------------------------------------------
        // Determinism & canonicalization
        // ------------------------------------------------------------
        Check(SpaceCodec.Encode(Space()) == spaceCode, "encoding twice gives the identical code");

        List<SpaceHistory.InstanceState> shuffled = Space();
        shuffled.Reverse();
        Check(SpaceCodec.Encode(shuffled) == spaceCode, "instance order does not change the code");

        List<SpaceHistory.InstanceState> shifted = Space();
        for (int i = 0; i < shifted.Count; i++)
        {
            SpaceHistory.InstanceState s = shifted[i];
            s.Position += new Vector3(NeospaceUnits.Mm(10 * 88), 0f, NeospaceUnits.Mm(7 * 88));
            shifted[i] = s;
        }
        Check(SpaceCodec.Encode(shifted) == spaceCode,
            "where the space stands on the grid does not change the code");

        List<SpaceHistory.InstanceState> spun = Space();
        SpaceHistory.InstanceState spunState = spun[1];
        spunState.YawDegrees = 450f;   // == 90°
        spun[1] = spunState;
        Check(SpaceCodec.Encode(spun) == spaceCode, "yaw is normalised to quarter turns");

        // ------------------------------------------------------------
        // Invalid codes fail clearly, never partially
        // ------------------------------------------------------------
        Check(Throws(() => SpaceCodec.Encode(new List<SpaceHistory.InstanceState>())),
            "encoding an empty space fails with a clear message");
        Check(Throws(() => SpaceCodec.Decode(null)), "null input fails");
        Check(Throws(() => SpaceCodec.Decode("")), "empty input fails");
        Check(Throws(() => SpaceCodec.Decode(chairCode)), "a piece code is rejected by the space decoder");
        Check(Throws(() => SpaceCodec.Decode("NSS1-@@@@")), "garbage characters fail");
        Check(Throws(() => SpaceCodec.Decode(spaceCode.Substring(0, spaceCode.Length - 6))),
            "a truncated code fails");

        char[] corrupted = spaceCode.ToCharArray();
        int mid = corrupted.Length / 2;
        corrupted[mid] = corrupted[mid] == 'A' ? 'B' : 'A';
        Check(Throws(() => SpaceCodec.Decode(new string(corrupted))),
            "a single flipped character fails (checksum)");

        // ------------------------------------------------------------
        // Version compatibility
        // ------------------------------------------------------------
        byte[] bytes = FromBase64Url(spaceCode.Substring(SpaceCodec.Prefix.Length));
        bytes[3] = 99;   // pretend a future schema wrote this
        uint crc = ConfigurationCodec.Crc32(bytes, 0, bytes.Length - 4);
        bytes[bytes.Length - 4] = (byte)(crc & 0xFF);
        bytes[bytes.Length - 3] = (byte)((crc >> 8) & 0xFF);
        bytes[bytes.Length - 2] = (byte)((crc >> 16) & 0xFF);
        bytes[bytes.Length - 1] = (byte)((crc >> 24) & 0xFF);
        string futureCode = SpaceCodec.Prefix + ToBase64Url(bytes);

        Check(Throws(() => SpaceCodec.Decode(futureCode),
                ConfigurationCodeException.Kind.UnsupportedVersion),
            "a newer-version code fails with the version message");

        // ------------------------------------------------------------
        // Edit semantics: update-all vs make-unique
        // ------------------------------------------------------------
        string editedChair = ConfigurationCodec.Encode(BuildEditedChair());
        Check(editedChair != chairCode, "the edited chair has a new code");

        // Update all: every chair switches, the table is untouched.
        List<SpaceHistory.InstanceState> updated = SpaceEditSession.ApplyEdit(
            Space(), new HashSet<int> { 1 }, chairCode, editedChair, 55f, "Chair (unique)",
            updateAll: true);

        int updatedChairs = 0;
        bool tableUntouched = false, namesKept = true;
        foreach (SpaceHistory.InstanceState s in updated)
        {
            if (s.Code == editedChair)
            {
                updatedChairs++;
                if (s.PieceName != "Chair") namesKept = false;
                if (Mathf.Abs(s.Price - 55f) > 0.001f) namesKept = false;
            }
            if (s.Code == tableCode)
                tableUntouched = true;
        }
        Check(updatedChairs == 4, "update-all switches all four chairs to the new definition");
        Check(tableUntouched, "update-all leaves the table alone");
        Check(namesKept, "update-all keeps names and applies the new price");

        // Make unique: only the targeted chair switches.
        List<SpaceHistory.InstanceState> unique = SpaceEditSession.ApplyEdit(
            Space(), new HashSet<int> { 1 }, chairCode, editedChair, 55f, "Chair (unique)",
            updateAll: false);

        int oldChairs = 0, newChairs = 0;
        string uniqueName = null;
        foreach (SpaceHistory.InstanceState s in unique)
        {
            if (s.Code == chairCode) oldChairs++;
            if (s.Code == editedChair) { newChairs++; uniqueName = s.PieceName; }
        }
        Check(newChairs == 1 && oldChairs == 3,
            "make-unique switches only the targeted chair (3 keep the old design)");
        Check(uniqueName == "Chair (unique)", "the unique copy takes the unique name");

        // The edited space still produces a valid, loadable code.
        string updatedCode = SpaceCodec.Encode(updated);
        Check(SpaceCodec.Encode(SpaceCodec.Decode(updatedCode)) == updatedCode,
            "the updated space round-trips");
        Check(updatedCode != spaceCode, "the updated space has a different code");

        return total;
    }

    static ConfigurationModel BuildEditedChair()
    {
        var model = new ConfigurationModel();
        model.Beams.Add(new BeamRecord { PartCode = 1, XMm = 0, YMm = 305, ZMm = 0, OrientIndex = 0 });
        model.Beams.Add(new BeamRecord { PartCode = 3, XMm = 704, YMm = 305, ZMm = 0, OrientIndex = 0 });
        return model;
    }

    // Local Base64Url helpers (the codec's are private by design).
    static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    static byte[] FromBase64Url(string text)
    {
        string s = text.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}
