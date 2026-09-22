using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Finds the MODULES in the scene: each one a set of frames connected as a
/// whole, plus the panels seated in them.
///
/// This is the Unity answer to the completeness rule NSFINISH enforces in
/// Rhino (neospace_rhino/structure.py) — "the selection must be a whole
/// connected component; you cannot finish half a structure". The reasoning is
/// the owner's and it is the same here: every finishing part, from panels to
/// veneers and caps and feet, hangs off the frame structure, so identifying a
/// module IS identifying a connected set of frames.
///
/// What is NOT ported is how Rhino finds the connections. There it has to
/// re-derive them geometrically on every run — joint endpoints matched to
/// frames within a tolerance, panel edges matched to collinear frame
/// skeletons — because a Rhino document is just blocks in space and nothing
/// records what plugged into what.
///
/// Unity already knows. Every placement writes the link down:
///
///     scenePoint.pairedWith = ownPoint;      // BuildController.PlacementCommit
///     ownPoint.pairedWith   = scenePoint;
///
/// and PanelSlotManager pairs a panel's pegs to the frame holes it sits in
/// exactly the same way. So the adjacency graph is maintained as the user
/// builds, and this only has to walk it. No tolerances, no geometry, nothing
/// to disagree with the placement rules about — which matters, because a
/// solver that disagreed would split one module in two and quietly capture
/// half a structure as a block.
///
/// The walk itself is pure and lives in <see cref="Components"/>, so it is
/// covered by selftests without needing a scene.
/// </summary>
public static class ModuleSolver
{
    /// <summary>One connected structure: what it holds and how big it is.</summary>
    public class Module
    {
        /// <summary>Placed beam roots — the frames that define the module.</summary>
        public readonly List<Transform> Frames = new List<Transform>();

        /// <summary>Panels seated in those frames.</summary>
        public readonly List<PanelInstance> Panels = new List<PanelInstance>();

        /// <summary>Every root in the module, frames and panels together.</summary>
        public readonly List<Transform> Roots = new List<Transform>();

        public Bounds WorldBounds;
        public bool HasBounds;

        public int PartCount => Frames.Count + Panels.Count;

        public int WidthMm => Mm(WorldBounds.size.x);
        public int DepthMm => Mm(WorldBounds.size.z);
        public int HeightMm => Mm(WorldBounds.size.y);

        static int Mm(float worldUnits) => Mathf.RoundToInt(NeospaceUnits.ToMm(worldUnits));
    }

    // ------------------------------------------------------------------
    // Scene walk
    // ------------------------------------------------------------------

    /// <summary>
    /// Every module currently placed, largest first so the list reads the way
    /// a person would rank them. Ghost geometry is excluded by layer, exactly
    /// as <see cref="ConfigurationCapture"/> excludes it — a ghost following
    /// the cursor is not part of anything yet.
    /// </summary>
    public static List<Module> FindAll(BuildController build)
    {
        int ghostMask = build != null ? build.ghostLayerMask.value : 0;

        // Nodes: placed beam roots and panel roots. Index them first so the
        // pure component walk can work on integers.
        // Nodes are FRAMES ONLY. Panels are not in this graph and cannot be:
        // they carry no attachment points (the peg/hole pairing is beam to
        // beam), and every panel in the scene is parented to one shared
        // panelsRoot, so they do not even have distinct roots to key on.
        // Treating them as nodes made every panel in the scene collapse into
        // a single node joined to nothing, and the module count came out as
        // "frame components + 1" — one phantom module holding all the panels.
        //
        // Frames define the module; the panels are assigned to one afterwards.
        // That is the owner's rule as stated, and Rhino's: everything that is
        // not a frame hangs off the frames.
        var index = new Dictionary<Transform, int>();
        var roots = new List<Transform>();

        foreach (BeamConnections conn in Object.FindObjectsByType<BeamConnections>(FindObjectsSortMode.None))
        {
            if (conn == null)
                continue;
            Transform root = conn.transform.root;
            if (!Placed(root, ghostMask) || index.ContainsKey(root))
                continue;
            // Same gate ConfigurationCapture uses: a root whose name carries no
            // catalogue part id is scenery, not a frame.
            if (StructureClipboard.CleanPartId(root.name) == null)
                continue;

            index[root] = roots.Count;
            roots.Add(root);
        }

        // Edges: one per live pairing. Both halves of a pair record each
        // other, so every edge is seen twice — harmless, union is idempotent.
        var edges = new List<(int, int)>();
        List<AttachmentPoint> live = AttachmentPoint.Live;
        for (int i = 0; i < live.Count; i++)
        {
            AttachmentPoint ap = live[i];
            // A pairing whose other half was destroyed compares == null in
            // Unity even though the reference survives. OccupancySanitizer
            // clears stale `occupant` but not stale `pairedWith`, so this has
            // to check rather than trust.
            if (ap == null || ap.pairedWith == null)
                continue;

            if (index.TryGetValue(ap.transform.root, out int a) &&
                index.TryGetValue(ap.pairedWith.transform.root, out int b) &&
                a != b)
            {
                edges.Add((a, b));
            }
        }

        var modules = new List<Module>();
        var moduleOfFrame = new Dictionary<Transform, Module>();

        foreach (List<int> group in Components(roots.Count, edges))
        {
            var module = new Module();
            foreach (int node in group)
            {
                Transform root = roots[node];
                module.Frames.Add(root);
                module.Roots.Add(root);
                moduleOfFrame[root] = module;
                Grow(module, root);
            }
            modules.Add(module);
        }

        AssignPanels(modules, moduleOfFrame, ghostMask);

        modules.Sort((a, b) => b.PartCount.CompareTo(a.PartCount));
        return modules;
    }

    /// <summary>Widen a module's bounds to take in everything a root draws.</summary>
    static void Grow(Module module, Transform root)
    {
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (r == null || !r.enabled)
                continue;
            if (!module.HasBounds)
            {
                module.WorldBounds = r.bounds;
                module.HasBounds = true;
            }
            else
            {
                module.WorldBounds.Encapsulate(r.bounds);
            }
        }
    }

    /// <summary>
    /// Give every panel to the module whose frames bound it.
    ///
    /// A panel is placed into a SLOT, and a slot is an opening in the frames —
    /// its four corners lie where the bounding frames meet, physically inside
    /// those frames. So the frame that owns a corner owns the panel. That is
    /// the recorded truth about where the panel sits, rather than a guess from
    /// proximity, and it is the same relationship Rhino derives the hard way
    /// ("a frame counts when it runs along any PART of an edge").
    ///
    /// A panel whose slot has gone falls back to the frame nearest its centre,
    /// so it is never orphaned into a module of its own — which is the bug
    /// this whole method exists to stop happening again by another route.
    /// </summary>
    static void AssignPanels(List<Module> modules, Dictionary<Transform, Module> moduleOfFrame,
                             int ghostMask)
    {
        if (modules.Count == 0)
            return;

        var slots = Object.FindFirstObjectByType<PanelSlotManager>();

        foreach (PanelInstance pi in Object.FindObjectsByType<PanelInstance>(FindObjectsSortMode.None))
        {
            if (pi == null || (ghostMask & (1 << pi.gameObject.layer)) != 0)
                continue;

            Module owner = null;

            if (slots != null && !string.IsNullOrEmpty(pi.slotId) &&
                slots.TryGetSlot(pi.slotId, out PanelSlotHandle slot) && slot != null)
            {
                owner = OwnerOfCorners(slot, moduleOfFrame);
            }

            owner ??= NearestModule(pi.transform.position, modules);
            if (owner == null)
                continue;

            owner.Panels.Add(pi);
            owner.Roots.Add(pi.transform);
            Grow(owner, pi.transform);
        }
    }

    /// <summary>
    /// The module holding most of the frames at this slot's corners. "Most"
    /// rather than "the first": a slot between two structures standing flush
    /// would touch both, and the one owning three corners owns the panel.
    /// </summary>
    static Module OwnerOfCorners(PanelSlotHandle slot, Dictionary<Transform, Module> moduleOfFrame)
    {
        var votes = new Dictionary<Module, int>();
        var corners = new[] { slot.corner0, slot.corner1, slot.corner2, slot.corner3 };

        foreach (KeyValuePair<Transform, Module> entry in moduleOfFrame)
        {
            Bounds b = FrameBounds(entry.Key);
            foreach (Vector3 corner in corners)
            {
                if (!b.Contains(corner))
                    continue;
                votes.TryGetValue(entry.Value, out int n);
                votes[entry.Value] = n + 1;
            }
        }

        Module best = null;
        int bestVotes = 0;
        foreach (KeyValuePair<Module, int> vote in votes)
        {
            if (vote.Value > bestVotes)
            {
                bestVotes = vote.Value;
                best = vote.Key;
            }
        }
        return best;
    }

    static Bounds FrameBounds(Transform root)
    {
        var bounds = new Bounds(root.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (r == null || !r.enabled)
                continue;
            if (!any)
            {
                bounds = r.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }
        // A slot corner sits on the frame's surface, and Bounds.Contains is
        // exclusive at the face, so grow by a hair to take the corner in.
        bounds.Expand(NeospaceUnits.Mm(4f));
        return bounds;
    }

    static Module NearestModule(Vector3 point, List<Module> modules)
    {
        Module best = null;
        float bestDistance = float.MaxValue;

        foreach (Module m in modules)
        {
            if (!m.HasBounds)
                continue;
            float d = m.WorldBounds.SqrDistance(point);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = m;
            }
        }
        return best;
    }

    static bool Placed(Transform root, int ghostMask) =>
        root != null && (ghostMask & (1 << root.gameObject.layer)) == 0;

    /// <summary>
    /// What the scene holds versus what the solver claimed, and the reason
    /// for any difference.
    ///
    /// A frame the solver cannot see is silently missing from every block
    /// captured from its module, which is the worst kind of bug this can
    /// have: the block looks plausible and is wrong. So the count is checked
    /// against the same beams the price readout totals, and anything rejected
    /// is named with the reason rather than merely subtracted.
    ///
    /// Lives here rather than in the editor menu because the gates it reports
    /// on — BeamPartUtility, StructureClipboard — are internal to this
    /// assembly, and a diagnostic is not a reason to widen them.
    /// </summary>
    public class Reconciliation
    {
        public int SceneFrames;
        public int ScenePanels;
        public int SolvedFrames;
        public int SolvedPanels;
        public readonly List<string> Rejected = new List<string>();

        public bool FramesAgree => SolvedFrames == SceneFrames;
        public bool PanelsAgree => SolvedPanels == ScenePanels;
    }

    public static Reconciliation Reconcile(BuildController build, List<Module> modules)
    {
        var report = new Reconciliation();
        int ghostMask = build != null ? build.ghostLayerMask.value : 0;

        var solved = new HashSet<Transform>();
        foreach (Module m in modules)
        {
            report.SolvedFrames += m.Frames.Count;
            report.SolvedPanels += m.Panels.Count;
            foreach (Transform t in m.Frames)
                solved.Add(t);
        }

        foreach (PanelInstance pi in Object.FindObjectsByType<PanelInstance>(FindObjectsSortMode.None))
            if (pi != null)
                report.ScenePanels++;

        // The same set UIBuildStats prices: a SelectableBeam root that is not
        // a ghost and whose name is a catalogue beam.
        var beams = new HashSet<Transform>();
        foreach (SelectableBeam sb in Object.FindObjectsByType<SelectableBeam>(FindObjectsSortMode.None))
        {
            if (sb == null)
                continue;
            Transform root = sb.transform.root;
            if (root == null || root.name.Contains("_GhostInstance"))
                continue;
            if (!BeamPartUtility.IsBeam(root.name))
                continue;
            beams.Add(root);
        }
        report.SceneFrames = beams.Count;

        foreach (Transform root in beams)
        {
            if (solved.Contains(root))
                continue;

            string why =
                root.GetComponentInChildren<BeamConnections>(true) == null ? "no BeamConnections"
                : StructureClipboard.CleanPartId(root.name) == null ? "name carries no catalogue part id"
                : !Placed(root, ghostMask) ? "on the ghost layer"
                : "unknown";

            report.Rejected.Add($"{root.name} at {root.position:F2} — {why}");
        }

        return report;
    }

    /// <summary>The module a scene object belongs to, or null.</summary>
    public static Module ModuleOf(Transform anyPart, List<Module> modules)
    {
        if (anyPart == null || modules == null)
            return null;

        Transform root = anyPart.root;
        foreach (Module m in modules)
            if (m.Roots.Contains(root))
                return m;
        return null;
    }

    // ------------------------------------------------------------------
    // The pure part
    // ------------------------------------------------------------------

    /// <summary>
    /// Connected components of an undirected graph, by union-find with path
    /// compression. Nodes are 0..count-1; every node belongs to exactly one
    /// component, so an unconnected node comes back as a group of one — which
    /// is the right answer for a single frame standing on its own.
    ///
    /// Groups are returned in ascending order of their lowest node, and each
    /// group's members ascend, so the result is stable: the same graph always
    /// produces the same list, whatever order the edges arrived in.
    /// </summary>
    public static List<List<int>> Components(int count, IEnumerable<(int, int)> edges)
    {
        var parent = new int[count];
        for (int i = 0; i < count; i++)
            parent[i] = i;

        int Find(int x)
        {
            while (parent[x] != x)
                x = parent[x] = parent[parent[x]];   // halve the path as we go
            return x;
        }

        if (edges != null)
        {
            foreach ((int a, int b) in edges)
            {
                if (a < 0 || b < 0 || a >= count || b >= count)
                    continue;
                int ra = Find(a), rb = Find(b);
                if (ra != rb)
                    parent[ra] = rb;
            }
        }

        var byRoot = new Dictionary<int, List<int>>();
        var order = new List<int>();
        for (int i = 0; i < count; i++)
        {
            int root = Find(i);
            if (!byRoot.TryGetValue(root, out List<int> group))
            {
                group = new List<int>();
                byRoot[root] = group;
                order.Add(root);
            }
            group.Add(i);
        }

        var result = new List<List<int>>(order.Count);
        foreach (int root in order)
            result.Add(byRoot[root]);
        return result;
    }
}
