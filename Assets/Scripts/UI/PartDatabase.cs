using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PartDatabase", menuName = "Configurator/Part Database", order = 1)]
public class PartDatabase : ScriptableObject
{
    [Serializable]
    public class PartEntry
    {
        [Header("Identity")]
        public string partId;              // e.g., "V3", "H11"

        [Header("Prefabs")]
        public GameObject realPrefab;      // actual prefab to instantiate
        public GameObject ghostPrefab;     // optional: ghost-specific prefab (can be null)

        [Header("V Placement Overrides (optional)")]
        [Tooltip("If enabled, this part will use these settings for first placement + snapping rotation/yaws.")]
        public bool overrideVPlacement = false;

        [Tooltip("World Y position to use for first V placement (if override enabled).")]
        public float vHeightOffset = 2.173f;

        [Tooltip("Base Euler rotation applied to V (if override enabled).")]
        public Vector3 vRotationEuler = new Vector3(90f, 0f, 0f);

        [Tooltip("Optional yaw angles to try for snapping (if empty -> BuildController default yaw angles).")]
        public float[] vYawAngles;
    }

    public List<PartEntry> parts = new List<PartEntry>();

    private Dictionary<string, PartEntry> _cache;

    static string Normalize(string id) => string.IsNullOrEmpty(id) ? "" : id.Trim();

    public void RebuildCache()
    {
        _cache = new Dictionary<string, PartEntry>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            if (p == null) continue;

            string key = Normalize(p.partId);
            if (string.IsNullOrEmpty(key)) continue;
            if (p.realPrefab == null && p.ghostPrefab == null) continue;

            // Warn if duplicates exist (last one wins)
            if (_cache.TryGetValue(key, out var existing))
            {
                string exReal = existing != null && existing.realPrefab != null ? existing.realPrefab.name : "NULL";
                string exGhost = existing != null && existing.ghostPrefab != null ? existing.ghostPrefab.name : "NULL";
                string newReal = p.realPrefab != null ? p.realPrefab.name : "NULL";
                string newGhost = p.ghostPrefab != null ? p.ghostPrefab.name : "NULL";

                Debug.LogWarning(
                    $"[PartDatabase] Duplicate partId \"{key}\" detected. " +
                    $"Earlier: real={exReal}, ghost={exGhost}. " +
                    $"Overwritten by index {i}: real={newReal}, ghost={newGhost}."
                );
            }

            _cache[key] = p;
        }
    }

    public bool TryGet(string partId, out PartEntry entry)
    {
        if (_cache == null) RebuildCache();

        string key = Normalize(partId);
        if (string.IsNullOrEmpty(key))
        {
            entry = null;
            return false;
        }

        return _cache.TryGetValue(key, out entry);
    }

    public GameObject GetRealPrefab(string partId)
    {
        if (TryGet(partId, out var entry))
            return entry.realPrefab;
        return null;
    }

    public GameObject GetGhostPrefabOrFallback(string partId)
    {
        if (TryGet(partId, out var entry))
        {
            if (entry.ghostPrefab != null) return entry.ghostPrefab;
            return entry.realPrefab;
        }
        return null;
    }
}