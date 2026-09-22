using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// A PIECE is a named, saved configuration of one furniture item — a chair,
/// a table, a shelf. It is NOT a mesh dump: the geometry lives entirely in
/// the piece's configuration code (see CONFIG_CODE_SCHEMA.md), plus display
/// metadata (bounds, part count, price) and an optional thumbnail.
///
/// v1 store: one JSON file per piece plus a PNG thumbnail, under
/// <c>persistentDataPath/Pieces</c>. Records are read fresh on every
/// <see cref="LoadAll"/>, so there is no cache to invalidate.
/// </summary>
public static class PieceLibrary
{
    [Serializable]
    public class PieceRecord
    {
        public string id;            // GUID, doubles as the file name
        public string name;
        public string code;          // "NS1-…" configuration code

        /// <summary>
        /// Which sub-collection this block is filed under in the Blocks tab.
        /// Empty on blocks saved before collections existed; those are shown
        /// in the seeded default rather than being stranded — see
        /// <see cref="BlockCollections.BlocksIn"/>.
        /// </summary>
        public string collectionId;
        public string createdUtc;    // ISO 8601
        public string modifiedUtc;
        public int beamCount;
        public int panelCount;
        public int finishCount;      // generated veneers, caps and feet; absent in legacy JSON = 0
        public int widthMm;
        public int depthMm;
        public int heightMm;
        public float price;          // AUD, from the live stats readout
        public bool hasThumbnail;
        [NonSerialized] public bool recovered;

        public int PartCount => beamCount + panelCount + finishCount;
    }

    public static string Folder =>
        Path.Combine(Application.persistentDataPath, "Pieces");

    public static string LastLoadWarning { get; private set; }

    // ------------------------------------------------------------------
    // Records
    // ------------------------------------------------------------------

    /// <summary>All pieces, newest modification first.</summary>
    public static List<PieceRecord> LoadAll()
    {
        LastLoadWarning = null;
        var list = new List<PieceRecord>();
        if (!Directory.Exists(Folder))
            return list;

        var ids = new HashSet<string>();
        foreach (string file in Directory.GetFiles(Folder, "*.json*"))
        {
            string filename = Path.GetFileName(file);
            int suffix = filename.IndexOf(".json", StringComparison.Ordinal);
            if (suffix > 0 && IsValidId(filename.Substring(0, suffix)))
                ids.Add(filename.Substring(0, suffix));
        }
        foreach (string id in ids)
        {
            try
            {
                string json = RecoverableFile.Read(JsonPath(id), text => ValidRecord(text, id), out bool recovered);
                var record = JsonUtility.FromJson<PieceRecord>(json);
                record.recovered = recovered;
                list.Add(record);
                if (recovered)
                    LastLoadWarning = "Recovered a saved piece from its previous valid copy. Check it before updating.";
            }
            catch (Exception e)
            {
                LastLoadWarning = "A saved piece could not be read. Its files were kept for recovery.";
                Debug.LogWarning($"[Pieces] Unable to read {id}: {e.Message}");
            }
        }

        list.Sort((a, b) => string.CompareOrdinal(b.modifiedUtc, a.modifiedUtc));
        return list;
    }

    public static void Save(PieceRecord record)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        RequireValidId(record.id);
        RecoverableFile.Write(JsonPath(record.id), JsonUtility.ToJson(record, prettyPrint: true),
            text => ValidRecord(text, record.id));
    }

    /// <summary>Success means local disk, or browser IndexedDB, acknowledged the write.</summary>
    public static void SaveConfirmed(PieceRecord record, Action<string> completed)
    {
        try { Save(record); }
        catch (Exception e) { completed?.Invoke(e.Message); return; }
        LocalSavePersistence.Flush(completed);
    }

    public static void Delete(string id)
    {
        RequireValidId(id);
        // Remove staging/recovery copies too, otherwise LoadAll would bring
        // the explicitly deleted piece back on the next open.
        foreach (string suffix in new[] { ".tmp", ".bak.tmp", ".bak", "" })
            if (File.Exists(JsonPath(id) + suffix)) File.Delete(JsonPath(id) + suffix);
        if (File.Exists(ThumbnailPath(id)))
            File.Delete(ThumbnailPath(id));
    }

    public static string NowUtc() =>
        DateTime.UtcNow.ToString("o");

    // ------------------------------------------------------------------
    // Thumbnails
    // ------------------------------------------------------------------

    public static void SaveThumbnail(string id, Texture2D texture)
    {
        RequireValidId(id);
        Directory.CreateDirectory(Folder);
        File.WriteAllBytes(ThumbnailPath(id), texture.EncodeToPNG());
    }

    /// <summary>Caller owns (and should Destroy) the returned texture.</summary>
    public static Texture2D LoadThumbnail(string id)
    {
        RequireValidId(id);
        string path = ThumbnailPath(id);
        if (!File.Exists(path))
            return null;

        var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
        try
        {
            if (tex.LoadImage(File.ReadAllBytes(path))) return tex;
        }
        catch (Exception e) { Debug.LogWarning("[Pieces] Thumbnail unavailable: " + e.Message); }
        UnityEngine.Object.Destroy(tex);
        return null;
    }

    // Taking the picture is not piece-specific and now lives in
    // ThumbnailCapture, which the project library uses too. Only the
    // id-to-file mapping above is ours.

    internal static bool ValidRecord(string json, string expectedId)
    {
        try
        {
            var record = JsonUtility.FromJson<PieceRecord>(json);
            return record != null && record.id == expectedId && IsValidId(record.id) &&
                !string.IsNullOrWhiteSpace(record.name) && ConfigurationCode.Validate(record.code).IsValid;
        }
        catch (Exception) { return false; }
    }

    internal static bool IsValidId(string id) =>
        id != null && id.Length == 32 && Guid.TryParseExact(id, "N", out _);

    static void RequireValidId(string id)
    {
        if (!IsValidId(id)) throw new ArgumentException("Invalid saved piece identifier.", nameof(id));
    }

    static string JsonPath(string id) => Path.Combine(Folder, id + ".json");
    static string ThumbnailPath(string id) => Path.Combine(Folder, id + ".png");
}
