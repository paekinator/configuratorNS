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

    /// <summary>Render the camera's current view (no UI) into a small texture.</summary>
    public static Texture2D CaptureThumbnail(Camera cam, int width = 288, int height = 192)
    {
        if (cam == null)
            return null;

        var rt = RenderTexture.GetTemporary(width, height, 24);
        RenderTexture prevTarget = cam.targetTexture;
        RenderTexture prevActive = RenderTexture.active;

        try
        {
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            tex.Apply();
            return tex;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Pieces] Thumbnail capture failed: " + e.Message);
            return null;
        }
        finally
        {
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    /// <summary>
    /// Render the given world bounds from a fixed three-quarter angle using a
    /// throwaway camera cloned from <paramref name="reference"/>. Unlike
    /// <see cref="CaptureThumbnail"/> this does not depend on where the user
    /// happens to be looking, so thumbnails are consistent and always framed.
    /// Falls back to the reference camera's own view for empty bounds.
    /// </summary>
    public static Texture2D CaptureThumbnailFramed(
        Camera reference, Bounds bounds, int width = 288, int height = 192)
    {
        if (reference == null)
            return null;
        if (bounds.size.sqrMagnitude < 1e-8f)
            return CaptureThumbnail(reference, width, height);

        var go = new GameObject("PieceThumbnailCamera");
        go.hideFlags = HideFlags.HideAndDontSave;
        var cam = go.AddComponent<Camera>();
        try
        {
            cam.CopyFrom(reference);
            cam.targetTexture = null;
            cam.enabled = false;   // render on demand only

            float radius = Mathf.Max(0.05f, bounds.extents.magnitude);
            cam.fieldOfView = Mathf.Clamp(cam.fieldOfView, 25f, 40f);
            float distance = radius * 1.35f / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

            Vector3 direction = new Vector3(1f, 0.75f, -1f).normalized;
            cam.transform.position = bounds.center + direction * distance;
            cam.transform.LookAt(bounds.center);
            cam.nearClipPlane = Mathf.Max(0.01f, distance - radius * 2f);
            cam.farClipPlane = distance + radius * 2f;

            return CaptureThumbnail(cam, width, height);
        }
        finally
        {
            UnityEngine.Object.Destroy(go);
        }
    }

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
