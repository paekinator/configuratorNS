using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// How blocks are filed in the Blocks tab: two fixed parent collections,
/// each holding sub-collections the user creates.
///
///   NEOSPACE's Collection — the range. Seeded with the collections from the
///                           mockup, empty until there is catalogue data to
///                           fill them; a shipped product list is not
///                           something this can invent.
///   My Collections        — what the user captures from their own scene.
///
/// The two parents are FIXED and are not records: they are the shape of the
/// panel, not data, and nothing should be able to delete or rename them into
/// a state the UI cannot draw. Only sub-collections are stored.
///
/// A block's home is <see cref="PieceLibrary.PieceRecord.collectionId"/>.
/// Blocks saved before collections existed carry none, so they fall into the
/// seeded "My Blocks" — no block is ever filed nowhere and invisible.
/// </summary>
public static class BlockCollections
{
    public enum Parent
    {
        Neospace,
        Mine
    }

    public const string NeospaceLabel = "NEOSPACE's Collection";
    public const string MineLabel = "My Collections";

    /// <summary>Where uncollected blocks live, and the id seeded for it.</summary>
    public const string DefaultMineId = "mine-default";

    [Serializable]
    public class Collection
    {
        public string id;
        public string parent;    // "Neospace" / "Mine" — see ParentOf
        public string name;
    }

    [Serializable]
    class Store
    {
        public List<Collection> collections = new List<Collection>();
    }

    static string Folder => Path.Combine(Application.persistentDataPath, "Blocks");
    static string FilePath => Path.Combine(Folder, "collections.json");

    public static Parent ParentOf(Collection c) =>
        c != null && string.Equals(c.parent, "Neospace", StringComparison.OrdinalIgnoreCase)
            ? Parent.Neospace
            : Parent.Mine;

    public static string LabelOf(Parent parent) =>
        parent == Parent.Neospace ? NeospaceLabel : MineLabel;

    // ------------------------------------------------------------------
    // Store
    // ------------------------------------------------------------------

    /// <summary>
    /// Every sub-collection, in the order they were created, seeded on first
    /// run. Order is the file's order and nothing sorts it: a list a person
    /// built that reshuffles itself is worse than one that grows at the end.
    /// </summary>
    public static List<Collection> LoadAll()
    {
        Store store = Read();
        if (store.collections.Count == 0)
        {
            store = Seed();
            Write(store);
        }
        return store.collections;
    }

    public static List<Collection> In(Parent parent)
    {
        var list = new List<Collection>();
        foreach (Collection c in LoadAll())
            if (ParentOf(c) == parent)
                list.Add(c);
        return list;
    }

    public static Collection ById(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        foreach (Collection c in LoadAll())
            if (c.id == id)
                return c;
        return null;
    }

    public static Collection Add(Parent parent, string name)
    {
        Store store = Read();
        if (store.collections.Count == 0)
            store = Seed();

        var created = new Collection
        {
            id = Guid.NewGuid().ToString("N"),
            parent = parent.ToString(),
            name = string.IsNullOrWhiteSpace(name) ? UniqueName(store, parent) : name.Trim()
        };

        store.collections.Add(created);
        Write(store);
        return created;
    }

    public static bool Rename(string id, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        Store store = Read();
        foreach (Collection c in store.collections)
        {
            if (c.id != id)
                continue;
            c.name = name.Trim();
            Write(store);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Remove an empty sub-collection. A collection holding blocks is refused
    /// rather than cascaded: deleting a folder should never be a way to lose
    /// work you did not know was in it, and the blocks have their own delete.
    /// The seeded home for uncollected blocks cannot be removed at all —
    /// without it, a block with no collection would have nowhere to appear.
    /// </summary>
    public static bool Delete(string id, out string refusal, bool force = false)
    {
        refusal = null;

        if (id == DefaultMineId)
        {
            refusal = "That collection is where uncollected blocks live · it cannot be removed.";
            return false;
        }

        // Refused by default, so no caller can delete a full collection by
        // accident. `force` is the panel saying the person was shown exactly
        // what would be lost and said yes anyway — it is not a way around the
        // rule, it is the answer to it.
        int held = BlocksIn(id).Count;
        if (held > 0 && !force)
        {
            refusal = $"That collection still holds {held} block{(held == 1 ? "" : "s")} · "
                      + "delete or move them first.";
            return false;
        }

        Store store = Read();
        for (int i = 0; i < store.collections.Count; i++)
        {
            if (store.collections[i].id != id)
                continue;
            store.collections.RemoveAt(i);
            Write(store);
            return true;
        }

        refusal = "That collection no longer exists.";
        return false;
    }

    // ------------------------------------------------------------------
    // Blocks in a collection
    // ------------------------------------------------------------------

    /// <summary>
    /// The blocks filed here, newest first. A block whose collectionId is
    /// empty — saved before collections existed — belongs to the seeded
    /// "My Blocks", so nothing is stranded where the UI cannot show it.
    /// </summary>
    public static List<PieceLibrary.PieceRecord> BlocksIn(string collectionId)
    {
        var list = new List<PieceLibrary.PieceRecord>();
        if (string.IsNullOrEmpty(collectionId))
            return list;

        foreach (PieceLibrary.PieceRecord record in PieceLibrary.LoadAll())
        {
            string home = string.IsNullOrEmpty(record.collectionId)
                ? DefaultMineId
                : record.collectionId;
            if (home == collectionId)
                list.Add(record);
        }
        return list;
    }

    /// <summary>
    /// The name a freshly captured block gets: "Untitled Block", then
    /// "Untitled Block 01", "Untitled Block 02"… once that name is taken.
    /// Scoped to the collection, because that is the list the user is looking
    /// at — the same name in two different collections reads fine.
    /// </summary>
    public const string UntitledBlock = "Untitled Block";

    public static string NextUntitledName(string collectionId)
    {
        var names = new List<string>();
        foreach (PieceLibrary.PieceRecord record in BlocksIn(collectionId))
            names.Add(record.name);
        return NextUntitledName(names);
    }

    /// <summary>
    /// The naming rule on its own, so it can be tested without a disk. The
    /// first block is plain "Untitled Block" — the suffix only appears once
    /// there is something to tell apart, which is what makes it useful.
    /// </summary>
    public static string NextUntitledName(IEnumerable<string> existingNames)
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (existingNames != null)
        {
            foreach (string name in existingNames)
                if (!string.IsNullOrWhiteSpace(name))
                    taken.Add(name.Trim());
        }

        if (!taken.Contains(UntitledBlock))
            return UntitledBlock;

        for (int n = 1; n < 1000; n++)
        {
            string candidate = $"{UntitledBlock} {n:00}";
            if (!taken.Contains(candidate))
                return candidate;
        }
        return UntitledBlock;
    }

    // ------------------------------------------------------------------
    // Disk
    // ------------------------------------------------------------------

    /// <summary>
    /// The demo collections that were seeded once. Dropping them from Seed()
    /// is not enough: anyone who opened the panel already has them on disk,
    /// and they would sit there forever. Removed on read, and only while
    /// still empty — if something was filed in one, the blocks matter more
    /// than the tidy-up.
    /// </summary>
    static readonly string[] RetiredSeedIds =
    {
        "neospace-essentials", "neospace-work", "neospace-living",
        "neospace-display", "neospace-room-dividers",
    };

    static Store Read()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var store = JsonUtility.FromJson<Store>(File.ReadAllText(FilePath));
                if (store?.collections != null)
                {
                    if (DropRetiredSeeds(store))
                        Write(store);
                    return store;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Blocks] Collections file unreadable, starting fresh: {e.Message}");
        }
        return new Store();
    }

    static bool DropRetiredSeeds(Store store)
    {
        bool changed = false;
        for (int i = store.collections.Count - 1; i >= 0; i--)
        {
            Collection c = store.collections[i];
            if (System.Array.IndexOf(RetiredSeedIds, c.id) < 0)
                continue;

            // Only if nothing was filed in it. Counting here rather than
            // calling BlocksIn avoids reading the collections file from
            // inside the code that is reading the collections file.
            bool empty = true;
            foreach (PieceLibrary.PieceRecord record in PieceLibrary.LoadAll())
            {
                if (record.collectionId == c.id)
                {
                    empty = false;
                    break;
                }
            }
            if (!empty)
                continue;

            store.collections.RemoveAt(i);
            changed = true;
        }
        return changed;
    }

    static void Write(Store store)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonUtility.ToJson(store, prettyPrint: true));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Blocks] Could not save collections: {e.Message}");
        }
    }

    /// <summary>
    /// One collection only: somewhere for blocks that name no collection to
    /// appear. NEOSPACE's Collection starts EMPTY.
    ///
    /// It was briefly seeded with the mockup's five names — Essentials, Work,
    /// Living, Display, Room dividers — which demonstrated the layout and
    /// nothing else. Five permanently empty folders that cannot be filled
    /// look like a fault in the app rather than a range awaiting its
    /// catalogue, and a person clicking through them learns only that nothing
    /// works. They come back when there is something to put in them.
    /// </summary>
    static Store Seed()
    {
        var store = new Store();

        store.collections.Add(new Collection
        {
            id = DefaultMineId,
            parent = Parent.Mine.ToString(),
            name = "My Blocks"
        });

        return store;
    }

    static string UniqueName(Store store, Parent parent)
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Collection c in store.collections)
            if (ParentOf(c) == parent && !string.IsNullOrEmpty(c.name))
                taken.Add(c.name);

        for (int n = 1; n < 1000; n++)
        {
            string candidate = n == 1 ? "New collection" : $"New collection {n:00}";
            if (!taken.Contains(candidate))
                return candidate;
        }
        return "New collection";
    }
}
