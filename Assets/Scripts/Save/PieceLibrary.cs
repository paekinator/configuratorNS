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

    static string JsonPath(string id) => Path.Combine(Folder, id + ".json");
    static string ThumbnailPath(string id) => Path.Combine(Folder, id + ".png");
}
