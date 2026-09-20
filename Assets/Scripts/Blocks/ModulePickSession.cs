using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// "Add a Block" armed: point at the scene, the whole module under the
/// cursor lights up, click to take it.
///
/// Whole module, not the part you touched. That is the point of the solver —
/// a block is a structure, and letting someone capture one post of a shelf
/// because that is what the ray hit would produce a block that looks like a
/// mistake. Highlighting the entire module before the click is also what
/// makes the rule VISIBLE: you see what "connected as a whole" means without
/// anyone explaining it.
///
/// While armed, the build tools are asleep. Pointing at the scene otherwise
/// means placing parts, and a picker that placed a beam every time you looked
/// for a module would be unusable. Same list of controllers Space Mode sleeps
/// on the way in, restored exactly as it was on the way out.
/// </summary>
public class ModulePickSession : MonoBehaviour
{
    /// <summary>Same set SpaceModeController sleeps; see the note there.</summary>
    static readonly Type[] SleepTypes =
    {
        typeof(BuildController), typeof(FreePartSession),
        typeof(MarqueeSelectionController), typeof(PanelLayerMover),
        typeof(MoveGizmoController), typeof(AttachmentMarkerController),
        typeof(TemplateSession),
        typeof(TemplateGhostPreview), typeof(TemplatePreviewGuide),
        typeof(StructureClipboard), typeof(GhostController)

        // NOT GuidedModeController. What this needs is that nothing BUILDS
        // while the picker owns the cursor, and that controller handles no
        // scene clicks — it answers the Tools/Parts switch. Sleeping it was
        // copied from Space Mode's list, where hiding the panels on the way
        // through was the point. Here it only meant waking it again ran its
        // OnEnable in the middle of a different tab.
    };

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    public static bool Armed { get; private set; }

    BuildController _build;
    Camera _camera;
    Action<ModuleSolver.Module> _onPicked;
    Action _onCancelled;

    readonly List<Behaviour> _slept = new List<Behaviour>();
    readonly List<Renderer> _tinted = new List<Renderer>();
    List<ModuleSolver.Module> _modules;
    ModuleSolver.Module _hovered;

    public static ModulePickSession GetOrCreate(BuildController build)
    {
        var session = FindFirstObjectByType<ModulePickSession>();
        if (session == null)
        {
            var go = new GameObject("ModulePickSession");
            session = go.AddComponent<ModulePickSession>();
        }
        session._build = build;
        return session;
    }

    /// <summary>
    /// Arm the picker. The module list is solved ONCE, here, rather than per
    /// frame: it is a scene-wide walk, and nothing can change the structure
    /// while the build tools are asleep.
    /// </summary>
    public void Arm(Action<ModuleSolver.Module> onPicked, Action onCancelled = null)
    {
        if (Armed)
            Cancel();

        _build = _build != null ? _build : FindFirstObjectByType<BuildController>();
        _camera = _build != null && _build.cam != null ? _build.cam : Camera.main;

        _modules = ModuleSolver.FindAll(_build);
        if (_modules.Count == 0)
        {
            SelectionStatus.Set("Nothing to make a block from · build something first.", 5f);
            onCancelled?.Invoke();
            return;
        }

        _onPicked = onPicked;
        _onCancelled = onCancelled;
        Armed = true;
        Sleep(true);

        SelectionStatus.Set(Describe(null), 0f);
    }

    /// <summary>
    /// What the pill says while picking. With a module under the cursor it
    /// names what would be taken — the counts and the size are the whole
    /// answer to "is this the thing I meant?", and they are cheaper to read
    /// than the highlight is to interpret on a crowded scene.
    /// </summary>
    static string Describe(ModuleSolver.Module module)
    {
        if (module == null)
            return "Point at a module to add it as a block · Esc to stop.";

        string frames = Count(module.Frames.Count, "frame");
        string panels = module.Panels.Count > 0 ? ", " + Count(module.Panels.Count, "panel") : string.Empty;
        return $"Add this module · {frames}{panels} · "
               + $"{module.WidthMm}×{module.DepthMm}×{module.HeightMm} mm · Esc to stop.";

        string Count(int n, string noun) => n == 1 ? $"1 {noun}" : $"{n} {noun}s";
    }

    public void Cancel()
    {
        if (!Armed)
            return;

        Action cancelled = _onCancelled;
        Finish();
        cancelled?.Invoke();
    }

    void Finish()
    {
        ClearTint();
        _hovered = null;
        _modules = null;
        _onPicked = null;
        _onCancelled = null;
        Armed = false;
        Sleep(false);
        SelectionStatus.Clear();
    }

    void Update()
    {
        if (!Armed)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cancel();
            return;
        }

        // Over the dock or a panel: no module, and no pick. Otherwise moving
        // the cursor down to the Blocks tab would highlight whatever module
        // happened to be behind it.
        bool overUI = EventSystem.current != null &&
                      EventSystem.current.IsPointerOverGameObject();

        ModuleSolver.Module under = overUI ? null : ModuleUnderCursor();
        if (under != _hovered)
        {
            ClearTint();
            _hovered = under;
            if (_hovered != null)
                Tint(_hovered);
            SelectionStatus.Set(Describe(_hovered), 0f);
        }

        if (_hovered != null && Input.GetMouseButtonDown(0))
        {
            ModuleSolver.Module picked = _hovered;
            Action<ModuleSolver.Module> handler = _onPicked;
            Finish();
            handler?.Invoke(picked);
        }
    }

    ModuleSolver.Module ModuleUnderCursor()
    {
        if (_camera == null || _modules == null)
            return null;

        int ghostMask = _build != null ? _build.ghostLayerMask.value : 0;
        Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 500f, ~ghostMask, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (l, r) => l.distance.CompareTo(r.distance));

        foreach (RaycastHit hit in hits)
        {
            Transform t = hit.collider.transform;

            // A panel resolves through its own object or its slot blocker,
            // exactly as MarqueeSelectionController resolves one — the
            // blocker sits just in front of the panel and would otherwise
            // stop the ray at nothing.
            var pi = t.GetComponentInParent<PanelInstance>();
            if (pi != null)
                return ModuleOfPanel(pi);

            var slot = t.GetComponentInParent<PanelSlotHandle>();
            if (slot != null)
            {
                GameObject panel = slot.panelPlus != null ? slot.panelPlus : slot.panelMinus;
                if (panel != null && panel.TryGetComponent(out PanelInstance seated))
                    return ModuleOfPanel(seated);
                continue;   // an empty slot's helper — look through it
            }

            ModuleSolver.Module module = ModuleSolver.ModuleOf(t, _modules);
            if (module != null)
                return module;

            return null;   // the floor or some other solid occludes the ray
        }

        return null;
    }

    ModuleSolver.Module ModuleOfPanel(PanelInstance panel)
    {
        foreach (ModuleSolver.Module module in _modules)
            if (module.Panels.Contains(panel))
                return module;
        return null;
    }

    // ------------------------------------------------------------------
    // Highlight
    // ------------------------------------------------------------------

    /// <summary>
    /// Tint through a MaterialPropertyBlock rather than swapping materials
    /// the way SelectableBeam does. Swapping is how SELECTION is drawn, and
    /// borrowing it here would leave the scene looking selected — and would
    /// fight the selection system over who restores what. A property block
    /// touches no material and is undone by clearing it.
    /// </summary>
    void Tint(ModuleSolver.Module module)
    {
        Color tint = UIThemeController.HighlightColor;
        var block = new MaterialPropertyBlock();

        foreach (Transform root in module.Roots)
        {
            if (root == null)
                continue;

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                if (r == null || !r.enabled)
                    continue;

                block.Clear();
                block.SetColor(BaseColorId, tint);   // URP/Lit
                block.SetColor(ColorId, tint);       // built-in, and older shaders
                r.SetPropertyBlock(block);
                _tinted.Add(r);
            }
        }
    }

    void ClearTint()
    {
        foreach (Renderer r in _tinted)
            if (r != null)
                r.SetPropertyBlock(null);
        _tinted.Clear();
    }

    // ------------------------------------------------------------------
    // Sleeping the build tools
    // ------------------------------------------------------------------

    void Sleep(bool sleep)
    {
        if (sleep)
        {
            // Put down whatever was in hand first, or its ghost stays on
            // screen through the whole pick.
            var free = FindFirstObjectByType<FreePartSession>();
            if (free != null)
                free.SetKind(FreePartKind.None);
            if (_build != null)
                _build.currentPartId = null;

            _slept.Clear();
            foreach (Type type in SleepTypes)
            {
                foreach (UnityEngine.Object found in FindObjectsByType(type, FindObjectsSortMode.None))
                {
                    if (found is Behaviour behaviour && behaviour.enabled)
                    {
                        behaviour.enabled = false;
                        _slept.Add(behaviour);
                    }
                }
            }
            return;
        }

        // Only what WE switched off goes back on, so a controller that was
        // already disabled for its own reasons stays that way.
        foreach (Behaviour behaviour in _slept)
            if (behaviour != null)
                behaviour.enabled = true;
        _slept.Clear();
    }

    void OnDisable()
    {
        if (Armed)
            Finish();
    }
}
