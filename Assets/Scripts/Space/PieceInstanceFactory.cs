using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turns a saved piece (its configuration code) into placeable Space Mode
/// content.
///
/// For each distinct piece code a frozen MASTER group is built once: the code
/// is decoded and replayed through the normal restore pipeline — but shifted
/// far off-camera so nothing flashes on screen — then every spawned part is
/// reparented under one group, every script and collider is stripped
/// (renderers only), the pivot is normalised to the footprint centre on the
/// floor, and a single box collider is added for picking. Ghosts and placed
/// instances are then plain clones of that master — instant and cheap.
///
/// Masters are cached per (piece id + code), so overwriting a piece later
/// yields a fresh master while existing instances keep their old geometry.
/// </summary>
public class PieceInstanceFactory : MonoBehaviour
{
    public BuildController buildController;

    /// <summary>
    /// Built far away so the staging never shows on screen. Must stay a
    /// multiple of 88 mm: the master pivot is quantized to the module
    /// lattice, which only preserves grid alignment if the staging shift
    /// kept the parts ON the lattice.
    /// </summary>
    const int StagingOffsetMm = 2272 * 88;   // 199 936 mm ≈ 200 m

    static readonly Color GhostTint = new Color(0.30f, 0.55f, 1.00f, 0.45f);

    readonly Dictionary<string, GameObject> _masters = new Dictionary<string, GameObject>();
    readonly Queue<(string pieceId, string code, Action<GameObject> onReady)> _queue =
        new Queue<(string, string, Action<GameObject>)>();
    Material _ghostMaterial;
    bool _building;

    public bool IsBuilding => _building;

    /// <summary>
    /// Fetch (or build) the frozen master for a piece and hand it to
    /// <paramref name="onReady"/>; null when the code cannot be restored.
    /// Requests arriving while a build runs are queued, not dropped — a
    /// pasted space may need several masters in a row.
    /// </summary>
    public void GetMaster(string pieceId, string code, Action<GameObject> onReady)
    {
        string key = pieceId + "|" + code;
        if (_masters.TryGetValue(key, out GameObject cached) && cached != null)
        {
            onReady?.Invoke(cached);
            // Keep draining: when a build finishes, the queue head often
            // resolves from this fresh cache — without this, the remaining
            // queued requests stall until the NEXT GetMaster call (symptom:
            // loading a space placed only some of its pieces on first try).
            ProcessQueue();
            return;
        }

        if (_building)
        {
            _queue.Enqueue((pieceId, code, onReady));
            return;
        }

        ConfigurationCodeValidation check = ConfigurationCode.Validate(code);
        if (!check.IsValid)
        {
            SelectionStatus.Set("This piece can't be loaded: " + check.Error, 6f);
            onReady?.Invoke(null);
            ProcessQueue();
            return;
        }

        // Shift the whole model off-camera for staging.
        for (int i = 0; i < check.Model.Beams.Count; i++)
        {
            BeamRecord b = check.Model.Beams[i];
            b.XMm += StagingOffsetMm;
            check.Model.Beams[i] = b;
        }
        for (int i = 0; i < check.Model.Panels.Count; i++)
        {
            PanelRecord p = check.Model.Panels[i];
            p.XMm += StagingOffsetMm;
            check.Model.Panels[i] = p;
        }

        _building = true;
        var restorer = ConfigurationCode.GetOrCreateRestorer(buildController);
        restorer.Restore(check.Model, report =>
        {
            _building = false;
            GameObject master = HarvestMaster(key);
            if (master == null)
                SelectionStatus.Set("This piece could not be prepared for placing.", 5f);
            onReady?.Invoke(master);
            ProcessQueue();
        });
    }

    void ProcessQueue()
    {
        if (_building || _queue.Count == 0)
            return;
        (string pieceId, string code, Action<GameObject> onReady) = _queue.Dequeue();
        GetMaster(pieceId, code, onReady);
    }

    /// <summary>
    /// Collect everything the restore just spawned into one frozen group.
    /// </summary>
    GameObject HarvestMaster(string key)
    {
        var parts = new HashSet<Transform>();
        int ghostMask = buildController != null ? buildController.ghostLayerMask.value : 0;

        foreach (BeamConnections conn in FindObjectsByType<BeamConnections>(FindObjectsSortMode.None))
        {
            Transform root = conn.transform.root;
            if ((ghostMask & (1 << root.gameObject.layer)) == 0 &&
                StructureClipboard.CleanPartId(root.name) != null)
                parts.Add(root);
        }

        // Panels live under the shared "PanelsRoot" container — take the panel
        // OBJECT, never its transform.root, or the whole container (with every
        // other panel in the scene) would be swallowed into this master.
        foreach (PanelInstance pi in FindObjectsByType<PanelInstance>(FindObjectsSortMode.None))
        {
            if ((ghostMask & (1 << pi.gameObject.layer)) == 0)
                parts.Add(pi.transform);
        }

        if (parts.Count == 0)
            return null;

        var master = new GameObject("PieceMaster_" + key.GetHashCode().ToString("X8"));
        master.transform.SetParent(transform, false);

        foreach (Transform part in parts)
        {
            if (part.TryGetComponent(out BeamConnections conn))
                conn.ReleaseAll();
            part.SetParent(master.transform, true);
        }

        // The slot manager still holds references to the panels we just took
        // (slot.panelPlus/Minus). Break those links now, otherwise its next
        // rescan would "preserve" the panels by ripping them back out of the
        // frozen master.
        foreach (PanelSlotHandle slot in FindObjectsByType<PanelSlotHandle>(FindObjectsSortMode.None))
        {
            if (slot.panelPlus != null && slot.panelPlus.transform.IsChildOf(master.transform))
                slot.panelPlus = null;
            if (slot.panelMinus != null && slot.panelMinus.transform.IsChildOf(master.transform))
                slot.panelMinus = null;
            if (slot.blocker != null)
                slot.blocker.gameObject.SetActive(slot.HasAnyPanel());
        }

        Freeze(master);

        // Pivot: footprint centre at floor level, snapped onto the piece's
        // own POST-CENTRE lattice. Instances snap their pivot to world grid
        // multiples, so with the pivot on the post lattice every post centre
        // lands exactly on a grid intersection — the same alignment build
        // mode uses (posts snap to grid points there). Rounding the bounds
        // centre alone left posts ~half a profile off the grid lines.
        Bounds bounds = RendererBounds(master);
        float module = NeospaceUnits.ModuleMeters;
        Vector3 shift;
        if (TryPostAnchor(master, out Vector3 anchor))
        {
            shift = new Vector3(
                anchor.x + Mathf.Round((bounds.center.x - anchor.x) / module) * module,
                bounds.min.y,
                anchor.z + Mathf.Round((bounds.center.z - anchor.z) / module) * module);
        }
        else
        {
            shift = new Vector3(
                Mathf.Round(bounds.center.x / module) * module,
                bounds.min.y,
                Mathf.Round(bounds.center.z / module) * module);
        }
        foreach (Transform child in master.transform)
            child.position -= shift;

        var box = master.AddComponent<BoxCollider>();
        box.center = new Vector3(
            bounds.center.x - shift.x,
            bounds.size.y * 0.5f,
            bounds.center.z - shift.z);
        box.size = bounds.size;

        master.SetActive(false);
        _masters[key] = master;
        return master;
    }

    /// <summary>
    /// Strip every script and collider — pure geometry remains. Must be
    /// IMMEDIATE (not deferred Destroy), because the master may be cloned in
    /// this very frame and deferred-destroyed components would survive in
    /// the clones. [RequireComponent] chains can block a single pass, so
    /// sweep a few times until everything is gone.
    /// </summary>
    static void Freeze(GameObject group)
    {
        for (int pass = 0; pass < 4; pass++)
        {
            var scripts = group.GetComponentsInChildren<MonoBehaviour>(true);
            if (scripts.Length == 0)
                break;
            foreach (MonoBehaviour mb in scripts)
                if (mb != null)
                    DestroyImmediate(mb);
        }
        foreach (Rigidbody rb in group.GetComponentsInChildren<Rigidbody>(true))
            if (rb != null)
                DestroyImmediate(rb);
        foreach (Collider col in group.GetComponentsInChildren<Collider>(true))
            if (col != null)
                DestroyImmediate(col);
    }

    /// <summary>
    /// World centre of one vertical post in the harvested master — the
    /// reference for the piece's frame lattice.
    /// </summary>
    static bool TryPostAnchor(GameObject master, out Vector3 anchor)
    {
        foreach (Transform child in master.transform)
        {
            string id = StructureClipboard.CleanPartId(child.name);
            if (id == null || !BeamPartUtility.IsVertical(id))
                continue;

            var renderers = child.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0)
                continue;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
            anchor = b.center;
            return true;
        }
        anchor = Vector3.zero;
        return false;
    }

    static Bounds RendererBounds(GameObject group)
    {
        // Active renderers only: disabled leftovers (e.g. slot blockers)
        // would inflate the footprint.
        var renderers = group.GetComponentsInChildren<Renderer>(false);
        if (renderers.Length == 0)
            return new Bounds(group.transform.position, Vector3.zero);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    // ------------------------------------------------------------------
    // Clones
    // ------------------------------------------------------------------

    public SpaceInstance CreateInstance(GameObject master,
        string pieceId, string pieceName, string code, float price,
        Vector3 position, float yawDegrees)
    {
        GameObject go = Instantiate(master);
        go.name = "Piece_" + pieceName;
        go.transform.SetParent(null, true);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yawDegrees, 0f));
        go.SetActive(true);

        var instance = go.AddComponent<SpaceInstance>();
        instance.pieceId = pieceId;
        instance.pieceName = pieceName;
        instance.code = code;
        instance.price = price;
        var masterBox = master.GetComponent<BoxCollider>();
        instance.size = masterBox.size;
        instance.footprintCenter = masterBox.center;
        return instance;
    }

    /// <summary>Translucent blue preview clone (collider disabled).</summary>
    public GameObject CreateGhost(GameObject master)
    {
        GameObject go = Instantiate(master);
        go.name = "PieceGhost";
        go.transform.SetParent(null, true);

        foreach (Collider col in go.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        Material ghostMat = GhostMaterial();
        foreach (Renderer rend in go.GetComponentsInChildren<Renderer>(true))
        {
            var mats = new Material[rend.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++)
                mats[i] = ghostMat;
            rend.sharedMaterials = mats;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        go.SetActive(true);
        return go;
    }

    Material GhostMaterial()
    {
        if (_ghostMaterial != null)
            return _ghostMaterial;

        var ghostController = FindFirstObjectByType<GhostController>();
        if (ghostController != null && ghostController.validMaterial != null)
        {
            _ghostMaterial = ghostController.validMaterial;
        }
        else
        {
            _ghostMaterial = new Material(Shader.Find("Sprites/Default"));
            _ghostMaterial.color = GhostTint;
        }
        return _ghostMaterial;
    }

    Material _ghostBlockedMaterial;

    Material GhostBlockedMaterial()
    {
        if (_ghostBlockedMaterial != null)
            return _ghostBlockedMaterial;

        var ghostController = FindFirstObjectByType<GhostController>();
        if (ghostController != null && ghostController.invalidMaterial != null)
        {
            _ghostBlockedMaterial = ghostController.invalidMaterial;
        }
        else
        {
            _ghostBlockedMaterial = new Material(Shader.Find("Sprites/Default"));
            _ghostBlockedMaterial.color = new Color(0.95f, 0.30f, 0.25f, 0.45f);
        }
        return _ghostBlockedMaterial;
    }

    /// <summary>Repaint a placement ghost blue (fits) or red (would overlap).</summary>
    public void TintGhost(GameObject ghost, bool valid)
    {
        Material mat = valid ? GhostMaterial() : GhostBlockedMaterial();
        foreach (Renderer rend in ghost.GetComponentsInChildren<Renderer>(true))
        {
            var mats = new Material[rend.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++)
                mats[i] = mat;
            rend.sharedMaterials = mats;
        }
    }
}
