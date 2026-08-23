using UnityEngine;

/// <summary>
/// Keeps every panel in the scene dressed in the current palette's panel
/// swatch (<see cref="FinishStyle"/>): live build panels, the frozen panel
/// clones inside placed Space pieces, and the merge-derived re-cut pieces.
/// Sweeps are change-driven (panel/piece version counters, merge rebuilds,
/// palette changes) — idle frames cost two integer compares, no scene scans.
///
/// Ghosts are untouched (ghost-layer filter — placement previews keep their
/// blue/red tint), and a selection-highlighted panel is skipped; while one
/// was skipped, a short retry poll stays alive so deselection restores the
/// palette look exactly as before.
/// </summary>
public class FinishStyleController : MonoBehaviour
{
    const float PollInterval = 0.35f;

    public static FinishStyleController Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() => Ensure();

    public static FinishStyleController Ensure()
    {
        if (Instance == null)
        {
            var go = new GameObject("FinishStyleController");
            Instance = go.AddComponent<FinishStyleController>();
        }
        return Instance;
    }

    BuildController _buildController;
    float _nextSweep;
    int _seenPanelVersion;
    int _seenSpaceVersion;
    bool _retrySkipped;
    static bool _sweepRequested;

    /// <summary>External systems (e.g. the Space merge) ask for a restyle pass.</summary>
    public static void RequestSweep() => _sweepRequested = true;

    void Awake()
    {
        Instance = this;
        _buildController = FindFirstObjectByType<BuildController>();
    }

    void OnEnable() => FinishStyle.Changed += OnStyleChanged;

    void OnDisable() => FinishStyle.Changed -= OnStyleChanged;

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void OnStyleChanged()
    {
        _retrySkipped = SweepPanels();
        if (FinishController.Instance != null)
            FinishController.Instance.RestyleDressing();
        _nextSweep = Time.unscaledTime + PollInterval;
    }

    void Update()
    {
        bool changed = _sweepRequested ||
                       _seenPanelVersion != PanelInstance.Version ||
                       _seenSpaceVersion != SpaceInstance.Version;

        // While a selected panel was skipped, keep the old poll cadence so a
        // deselection is caught just as quickly as before.
        if (!changed && !(_retrySkipped && Time.unscaledTime >= _nextSweep))
            return;

        _sweepRequested = false;
        _seenPanelVersion = PanelInstance.Version;
        _seenSpaceVersion = SpaceInstance.Version;
        _nextSweep = Time.unscaledTime + PollInterval;
        _retrySkipped = SweepPanels();
    }

    int GhostMask() => _buildController != null ? _buildController.ghostLayerMask.value : 0;

    /// <summary>Restyles everything; true when a selected panel had to be skipped.</summary>
    bool SweepPanels()
    {
        Material mat = FinishStyle.PanelMaterial;
        int ghostMask = GhostMask();
        bool skipped = false;

        foreach (PanelInstance pi in FindObjectsByType<PanelInstance>(FindObjectsSortMode.None))
        {
            if (pi == null || !pi.gameObject.activeInHierarchy)
                continue;
            if ((ghostMask & (1 << pi.gameObject.layer)) != 0)
                continue;
            skipped |= Apply(pi.gameObject, mat);
        }

        foreach (SpaceInstance inst in FindObjectsByType<SpaceInstance>(FindObjectsSortMode.None))
        {
            if (inst == null || !inst.gameObject.activeInHierarchy)
                continue;
            foreach (Transform child in inst.transform)
            {
                if (!child.gameObject.activeSelf || !child.name.Contains("Panel"))
                    continue;
                skipped |= Apply(child.gameObject, mat);
            }
        }

        Transform derived = SpaceMerge.DerivedRoot;
        if (derived != null && derived.gameObject.activeInHierarchy)
        {
            foreach (Transform child in derived)
            {
                if (!child.gameObject.activeSelf || !child.name.Contains("Panel"))
                    continue;
                skipped |= Apply(child.gameObject, mat);
            }
        }

        return skipped;
    }

    /// <summary>Returns true when the panel was skipped because it is selected.</summary>
    static bool Apply(GameObject go, Material mat)
    {
        var selectable = go.GetComponentInParent<SelectableBeam>();
        if (selectable != null && selectable.IsSelected())
            return true; // don't stomp the highlight; a retry sweep catches it

        bool changed = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(false))
        {
            Material[] mats = r.sharedMaterials;
            bool dirty = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == mat)
                    continue;
                mats[i] = mat;
                dirty = true;
            }
            if (dirty)
            {
                r.sharedMaterials = mats;
                changed = true;
            }
        }

        // Selection restores the materials it saw on selection — keep its
        // memory in step with the restyle, or deselecting would flash the
        // panel back to its pre-palette look.
        if (changed && selectable != null)
            selectable.RefreshOriginalMaterials();
        return false;
    }
}
