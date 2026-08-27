using System.Collections.Generic;
using UnityEngine;

namespace Evo.UI
{
    [CreateAssetMenu(fileName = "Icon Library", menuName = "Evo/UI/Icon Library")]
    public class IconLibrary : ScriptableObject
    {
        public List<string> categories = new();
        public List<Item> icons = new();
        Dictionary<string, Item> iconCache;

        void EnsureCache()
        {
            if (iconCache != null && iconCache.Count == icons.Count)
                return;

            iconCache = new Dictionary<string, Item>();

            for (int i = 0; i < icons.Count; i++)
            {
                var item = icons[i];
                if (item != null && !string.IsNullOrEmpty(item.ID) && !iconCache.ContainsKey(item.ID))
                {
                    iconCache[item.ID] = item;
                }
            }
        }

        public void InvalidateCache()
        {
            iconCache = null;
        }

        public Item GetItem(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            EnsureCache();

            if (iconCache == null || !iconCache.TryGetValue(id, out Item item)) { return null; }
            return item;
        }

        public Sprite GetSprite(string id, int resolution = 0)
        {
            var item = GetItem(id);
            return item?.GetSprite(resolution);
        }

        public List<Item> Search(string query, string category = "")
        {
            var results = new List<Item>();

            for (int i = 0; i < icons.Count; i++)
            {
                var item = icons[i];

                if (item == null) { continue; }
                if (!string.IsNullOrEmpty(category) && item.category != category) { continue; } // Filter by category first
                if (string.IsNullOrEmpty(query))
                {
                    // If no query, add all icons (filtered by category if specified)
                    results.Add(item);
                    continue;
                }

                // Search by query
                string lowerQuery = query.ToLower();
                bool matched = false;

                if (item.ID.ToLower().Contains(lowerQuery)) { matched = true; }
                else if (item.searchKeywords != null)
                {
                    for (int j = 0; j < item.searchKeywords.Count; j++)
                    {
                        var keyword = item.searchKeywords[j];
                        if (!string.IsNullOrEmpty(keyword) && keyword.ToLower().Contains(lowerQuery))
                        {
                            matched = true;
                            break;
                        }
                    }
                }
                if (matched) { results.Add(item); }
            }

            return results;
        }

        public List<Item> GetItemsByCategory(string category)
        {
            var results = new List<Item>();

            for (int i = 0; i < icons.Count; i++)
            {
                var item = icons[i];
                if (item != null && item.category == category) { results.Add(item); }
            }
            return results;
        }

        /// <summary>
        /// Gets the default library from the Resources folder.
        /// </summary>
        public static IconLibrary GetDefault()
        {
            IconLibrary defaultPreset = Resources.Load<IconLibrary>(Constants.DefaultIconLibraryPath);
            if (defaultPreset != null) { return defaultPreset; }
            return null;
        }

        [System.Serializable]
        public class Item
        {
            public string ID;
            public string category;
            public List<string> searchKeywords = new();
            public List<Resolution> resolutions = new();

            public Sprite GetSprite(int resolution = 0)
            {
                if (resolutions == null || resolutions.Count == 0) { return null; }
                if (resolution <= 0) { return resolutions[0]?.sprite; }
                for (int i = 0; i < resolutions.Count; i++)
                {
                    var res = resolutions[i];
                    if (res != null && res.resolution == resolution) { return res.sprite; }
                }
                return resolutions[0]?.sprite;
            }

            public List<int> GetAvailableResolutions()
            {
                var resolutionList = new List<int>();

                if (resolutions == null) { return resolutionList; }
                for (int i = 0; i < resolutions.Count; i++)
                {
                    var res = resolutions[i];
                    if (res != null) { resolutionList.Add(res.resolution); }
                }
                return resolutionList;
            }

            [System.Serializable]
            public class Resolution
            {
                public int resolution = 64;
                public Sprite sprite;
            }
        }
    }
}