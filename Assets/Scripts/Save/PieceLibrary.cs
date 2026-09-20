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
        public int widthMm;
        public int depthMm;
        public int heightMm;
        public float price;          // AUD, from the live stats readout
        public bool hasThumbnail;
    }

    public static string Folder =>
        Path.Combine(Application.persistentDataPath, "Pieces");

    // ------------------------------------------------------------------
    // Records
    // ------------------------------------------------------------------

    /// <summary>All pieces, newest modification first.</summary>
    public static List<PieceRecord> LoadAll()
    {
        var list = new List<PieceRecord>();
        if (!Directory.Exists(Folder))
            return list;

        foreach (string file in Directory.GetFiles(Folder, "*.json"))
        {
            try
            {
                var record = JsonUtility.FromJson<PieceRecord>(File.ReadAllText(file));
                if (record != null && !string.IsNullOrEmpty(record.id))
                    list.Add(record);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Pieces] Skipping unreadable piece file {file}: {e.Message}");
            }
        }

        list.Sort((a, b) => string.CompareOrdinal(b.modifiedUtc, a.modifiedUtc));
        return list;
    }

    public static void Save(PieceRecord record)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(JsonPath(record.id), JsonUtility.ToJson(record, prettyPrint: true));
    }

    public static void Delete(string id)
    {
        if (File.Exists(JsonPath(id)))
            File.Delete(JsonPath(id));
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
        Directory.CreateDirectory(Folder);
        File.WriteAllBytes(ThumbnailPath(id), texture.EncodeToPNG());
    }

    /// <summary>Caller owns (and should Destroy) the returned texture.</summary>
    public static Texture2D LoadThumbnail(string id)
    {
        string path = ThumbnailPath(id);
        if (!File.Exists(path))
            return null;

        var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
        if (!tex.LoadImage(File.ReadAllBytes(path)))
        {
            UnityEngine.Object.Destroy(tex);
            return null;
        }
        return tex;
    }

    // Taking the picture is not piece-specific and now lives in
    // ThumbnailCapture, which the project library uses too. Only the
    // id-to-file mapping above is ours.

    static string JsonPath(string id) => Path.Combine(Folder, id + ".json");
    static string ThumbnailPath(string id) => Path.Combine(Folder, id + ".png");
}
