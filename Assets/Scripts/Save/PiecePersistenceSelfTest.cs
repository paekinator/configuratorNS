#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Real filesystem fault-state tests; never reads/writes the user's saved library.</summary>
public static class PiecePersistenceSelfTest
{
    public static int RunAll(out List<string> failures)
    {
        var issues = new List<string>();
        failures = issues;
        int total = 0;
        void Check(bool passed, string label) { total++; if (!passed) issues.Add(label); }
        string folder = Path.Combine(Path.GetTempPath(), "NeospaceSaveTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "piece.json");
        Func<string, bool> valid = s => s == "old" || s == "new";
        string Read(out bool recovered) => RecoverableFile.Read(path, valid, out recovered);
        void Clear()
        {
            foreach (string file in Directory.GetFiles(folder)) File.Delete(file);
        }

        try
        {
            RecoverableFile.Write(path, "old", valid);
            Check(Read(out bool recovered) == "old" && !recovered, "first save reopens from committed file");
            RecoverableFile.Write(path, "new", valid);
            Check(Read(out recovered) == "new" && !recovered, "update reopens newest committed file");
            Check(File.ReadAllText(path + ".bak") == "old", "update retains previous valid revision");

            File.WriteAllText(path, "truncated");
            Check(Read(out recovered) == "old" && recovered, "corrupt primary recovers previous revision");
            RecoverableFile.Write(path, "new", valid);
            Check(Read(out recovered) == "new" && File.ReadAllText(path + ".bak") == "old",
                "saving after recovery preserves the valid backup");

            Clear();
            File.WriteAllText(path, "old");
            File.WriteAllText(path + ".tmp", "new");
            Check(Read(out recovered) == "old" && !recovered, "staged update never displaces committed data");
            File.Delete(path);
            Check(Read(out recovered) == "new" && recovered, "interrupted first commit recovers validated staging file");

            File.WriteAllText(path + ".bak", "old");
            Check(Read(out recovered) == "old" && recovered, "interrupted update prefers last committed backup");
            File.Move(path + ".bak", path + ".bak.tmp");
            Check(Read(out recovered) == "old" && recovered, "interrupted backup promotion retains previous content");

            Clear();
            File.WriteAllText(path + ".tmp", "truncated");
            bool rejected = false;
            try { Read(out _); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "incomplete staging file is never presented as a saved piece");

            Clear();
            RecoverableFile.Write(path, "old", valid);
            rejected = false;
            try { RecoverableFile.Write(path, "truncated", valid); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected && Read(out _) == "old", "validation failure leaves existing save untouched");

            string id = Guid.NewGuid().ToString("N");
            var record = new PieceLibrary.PieceRecord
            {
                id = id, name = "Étagère 東京", code = ConfigurationCodeSelfTest.GoldenCode,
                createdUtc = PieceLibrary.NowUtc(), modifiedUtc = PieceLibrary.NowUtc(),
                beamCount = 4, panelCount = 1
            };
            string json = JsonUtility.ToJson(record);
            Check(PieceLibrary.ValidRecord(json, id), "valid piece including unicode name is accepted");
            string legacyJson = json.Replace(",\"finishCount\":0", string.Empty);
            var legacyRecord = JsonUtility.FromJson<PieceLibrary.PieceRecord>(legacyJson);
            Check(!legacyJson.Contains("\"finishCount\"") && PieceLibrary.ValidRecord(legacyJson, id) &&
                legacyRecord.finishCount == 0 && legacyRecord.PartCount == 5,
                "legacy record without finish metadata remains readable with its original count");
            Check(!PieceLibrary.ValidRecord(json, Guid.NewGuid().ToString("N")), "record/file identity mismatch is rejected");
            Check(!PieceLibrary.IsValidId("../piece"), "piece identifiers cannot escape the library folder");
            record.code = "NS1-damaged";
            Check(!PieceLibrary.ValidRecord(JsonUtility.ToJson(record), id), "damaged configuration cannot be committed");

            var model = ConfigurationCode.Decode(ConfigurationCodeSelfTest.GoldenCode);
            model.FinishApplied = true;
            record.code = ConfigurationCode.Encode(model);
            record.finishCount = 12;
            record.price = 173f;
            string richJson = JsonUtility.ToJson(record);
            RecoverableFile.Write(path, richJson, text => PieceLibrary.ValidRecord(text, id));
            string reopened = RecoverableFile.Read(path, text => PieceLibrary.ValidRecord(text, id), out _);
            var restoredRecord = JsonUtility.FromJson<PieceLibrary.PieceRecord>(reopened);
            Check(restoredRecord.name == record.name && ConfigurationCode.Decode(restoredRecord.code).FinishApplied,
                "save/reopen preserves unicode name, geometry code, and finish toggle");
            Check(restoredRecord.beamCount == 4 && restoredRecord.panelCount == 1 &&
                restoredRecord.finishCount == 12 && restoredRecord.PartCount == 17 && restoredRecord.price == 173f,
                "save/reopen preserves finish-inclusive card count and estimate metadata");

            model.FinishApplied = false;
            record.code = ConfigurationCode.Encode(model);
            record.finishCount = 0;
            record.price = 125f;
            RecoverableFile.Write(path, JsonUtility.ToJson(record), text => PieceLibrary.ValidRecord(text, id));
            reopened = RecoverableFile.Read(path, text => PieceLibrary.ValidRecord(text, id), out _);
            restoredRecord = JsonUtility.FromJson<PieceLibrary.PieceRecord>(reopened);
            Check(restoredRecord.finishCount == 0 && restoredRecord.PartCount == 5 &&
                restoredRecord.price == 125f && !ConfigurationCode.Decode(restoredRecord.code).FinishApplied,
                "updating with finish removed replaces the saved count and estimate");
        }
        catch (Exception e) { issues.Add("Unexpected persistence test exception: " + e); }
        finally
        {
            // This directory is created above with a fresh GUID and contains
            // only this test's files. Never delete a user persistence folder.
            foreach (string file in Directory.GetFiles(folder)) File.Delete(file);
            Directory.Delete(folder);
        }
        return total;
    }
}
#endif
