using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// A PROJECT is one saved scene. Where a piece (a BLOCK, in the current UI)
/// is a single item you can place over and over, a project is the whole
/// arrangement you are working on — you open a project, you do not place one.
///
/// Like a piece, a project is stored as a configuration code plus display
/// metadata and a thumbnail, never as geometry. Unlike a piece, it records
/// WHICH MODE built it, because the two modes save genuinely different
/// documents:
///
///   Pro  — every frame and panel individually ("NS1-…", see
///          CONFIG_CODE_SCHEMA.md). Parts stay editable.
///   Lite — which block sits where, in grid millimetres and quarter turns
///          ("NSS1-…", see SpaceCodec). Blocks are sealed.
///
/// A Pro project therefore cannot be shown in Lite: once you add a frame that
/// belongs to no block there is no list of blocks to display. <see cref="Mode"/>
/// is stored explicitly rather than sniffed from the code prefix, so that when
/// Pro mode gains the ability to place blocks — and its code format grows to
/// say so — old files still declare what they are.
///
/// v1 store: one JSON file per project plus a PNG thumbnail, under
/// <c>persistentDataPath/Projects</c>, read fresh on every <see cref="LoadAll"/>.
/// Separate folder from Pieces: the two libraries are browsed separately and a
/// project is never offered where a block is expected.
/// </summary>
public static class ProjectLibrary
{
    public enum Mode
    {
        Pro,
        Lite
    }

    [Serializable]
    public class ProjectRecord
    {
        public string id;            // GUID, doubles as the file name
        public string name;
        public string mode;          // "Pro" / "Lite" — see ModeOf
        public string code;          // "NS1-…" (Pro) or "NSS1-…" (Lite)
        public string createdUtc;    // ISO 8601
        public string modifiedUtc;

        public int beamCount;        // Pro
        public int panelCount;       // Pro
        public int blockCount;       // Lite

        public int widthMm;
        public int depthMm;
        public int heightMm;
        public float price;          // AUD, from the live readout
        public bool hasThumbnail;
    }

    public static string Folder =>
        Path.Combine(Application.persistentDataPath, "Projects");

    // ------------------------------------------------------------------
    // Mode
    // ------------------------------------------------------------------

    /// <summary>
    /// The mode a record declares. Anything unrecognised — including the
    /// empty string, which is what a file written before this field existed
    /// would carry — reads as Pro, matching the code prefix it would have.
    /// </summary>
    public static Mode ModeOf(ProjectRecord record) =>
        record != null && string.Equals(record.mode, "Lite", StringComparison.OrdinalIgnoreCase)
            ? Mode.Lite
            : Mode.Pro;

    public static string NameOf(Mode mode) =>
        mode == Mode.Lite ? UIChrome.LiteLabel : UIChrome.ProLabel;

    // ------------------------------------------------------------------
    // Records
    // ------------------------------------------------------------------

    /// <summary>All projects, most recently modified first.</summary>
    public static List<ProjectRecord> LoadAll()
    {
        var list = new List<ProjectRecord>();
        if (!Directory.Exists(Folder))
            return list;

        foreach (string file in Directory.GetFiles(Folder, "*.json"))
        {
            try
            {
                var record = JsonUtility.FromJson<ProjectRecord>(File.ReadAllText(file));
                if (record != null && !string.IsNullOrEmpty(record.id))
                    list.Add(record);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Projects] Skipping unreadable project file {file}: {e.Message}");
            }
        }

        list.Sort((a, b) => string.CompareOrdinal(b.modifiedUtc, a.modifiedUtc));
        return list;
    }

    public static void Save(ProjectRecord record)
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
    // Thumbnails (taken by ThumbnailCapture; this only maps id to file)
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

    static string JsonPath(string id) => Path.Combine(Folder, id + ".json");
    static string ThumbnailPath(string id) => Path.Combine(Folder, id + ".png");
}
