using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene → <see cref="ConfigurationModel"/>. Collection rules are identical
/// to <c>BuildHistory.ReadScene</c> (the undo system): placed beams are the
/// roots that carry <see cref="BeamConnections"/> with a valid part-id name,
/// panels are <see cref="PanelInstance"/> objects, ghosts are excluded by
/// layer. Positions become integer millimetres via <see cref="NeospaceUnits"/>;
/// rotations quantize through <see cref="PoseQuantizer"/>.
/// </summary>
public static class ConfigurationCapture
{
    static readonly HashSet<Transform> Roots = new HashSet<Transform>();

    public static ConfigurationModel Capture(BuildController build) => Capture(build, null);

    /// <summary>
    /// Capture the scene, or only one module of it.
    ///
    /// Passing a module is how "Add a Block" takes ONE structure out of a
    /// scene holding several, instead of every scattered part in it. The
    /// module decides membership on both sides — its frames and the panels
    /// seated in them — so a panel can never be captured away from the frames
    /// that hold it, or left behind by them.
    ///
    /// Null captures everything, which is what saving a project does.
    /// </summary>
    public static ConfigurationModel Capture(BuildController build, ModuleSolver.Module only)
    {
        var model = new ConfigurationModel();
        int ghostMask = build != null ? build.ghostLayerMask.value : 0;

        HashSet<Transform> allowedFrames = null;
        HashSet<PanelInstance> allowedPanels = null;
        if (only != null)
        {
            allowedFrames = new HashSet<Transform>(only.Frames);
            allowedPanels = new HashSet<PanelInstance>(only.Panels);
        }

        Roots.Clear();
        foreach (BeamConnections conn in Object.FindObjectsByType<BeamConnections>(FindObjectsSortMode.None))
        {
            if (conn == null)
                continue;
            if (allowedFrames != null && !allowedFrames.Contains(conn.transform.root))
                continue;
            Transform root = conn.transform.root;
            if (root == null || (ghostMask & (1 << root.gameObject.layer)) != 0)
                continue;
            if (!Roots.Add(root))
                continue;

            string partId = StructureClipboard.CleanPartId(root.name);
            if (partId == null)
                continue;

            if (!PartRegistry.TryGetCode(partId, out int code))
            {
                Debug.LogWarning($"[ConfigCode] Part '{partId}' has no registry id · skipped from capture.");
                continue;
            }

            var record = new BeamRecord
            {
                PartCode = code,
                XMm = ToMm(root.position.x),
                YMm = ToMm(root.position.y),
                ZMm = ToMm(root.position.z)
            };

            if (PoseQuantizer.TryGetOrientationIndex(root.rotation, out int index))
            {
                record.OrientIndex = index;
            }
            else
            {
                record.OrientIndex = -1;
                PoseQuantizer.ToEulerDeci(root.rotation,
                    out record.EulerXDeci, out record.EulerYDeci, out record.EulerZDeci);
            }

            model.Beams.Add(record);
        }

        foreach (PanelInstance pi in Object.FindObjectsByType<PanelInstance>(FindObjectsSortMode.None))
        {
            if (pi == null || (ghostMask & (1 << pi.gameObject.layer)) != 0)
                continue;
            if (allowedPanels != null && !allowedPanels.Contains(pi))
                continue;

            model.Panels.Add(new PanelRecord
            {
                XMm = ToMm(pi.transform.position.x),
                YMm = ToMm(pi.transform.position.y),
                ZMm = ToMm(pi.transform.position.z),
                Axis = PoseQuantizer.ToSlotAxis(pi.transform.forward),
                SideMinus = pi.side < 0
            });
        }

        model.FinishApplied = AnyFinishVisible();

        if (only != null)
            MoveToOwnOrigin(model);

        return model;
    }

    /// <summary>
    /// Rewrite a captured module's positions so they are measured from the
    /// module's own corner instead of the centre of the world.
    ///
    /// A block is a THING, not a place. Without this, building the same wall
    /// twice in different spots saves two blocks that are identical in every
    /// way but share no description: one reads "posts at 968 and 1848", the
    /// other "posts at 2552 and 3608". They would sit in the library as
    /// look-alike duplicates, cache two masters for one shape, embed twice
    /// into a space code, and give two people the same shelf under different
    /// codes. Measured from its own corner, both read "posts at 0 and 880".
    ///
    /// Only applied to a MODULE capture. A whole-scene capture is a project,
    /// and a project is a scene: where things stand is the point of it.
    ///
    /// Two rules make this safe:
    ///
    ///   X and Z only. Height is not position — it is part of what the block
    ///   is. Dropping a wall-hung module to the floor because nothing below
    ///   it was captured would change the object, and the ground-contact test
    ///   that decides which posts get feet reads the same Y.
    ///
    ///   The shift is a whole number of 88 mm modules. Everything sits on that
    ///   lattice — every part in a real scene measures an exact multiple of it
    ///   — and shifting by anything else would leave every part BETWEEN grid
    ///   points, connected to nothing. Flooring to the module below also keeps
    ///   two identical modules identical even if they were somehow off-lattice,
    ///   since both carry the same sub-module remainder.
    /// </summary>
    static void MoveToOwnOrigin(ConfigurationModel model)
    {
        if (model.Beams.Count == 0 && model.Panels.Count == 0)
            return;

        int minX = int.MaxValue, minZ = int.MaxValue;
        foreach (BeamRecord b in model.Beams)
        {
            minX = Mathf.Min(minX, b.XMm);
            minZ = Mathf.Min(minZ, b.ZMm);
        }
        foreach (PanelRecord p in model.Panels)
        {
            minX = Mathf.Min(minX, p.XMm);
            minZ = Mathf.Min(minZ, p.ZMm);
        }

        int shiftX = ModuleFloor(minX);
        int shiftZ = ModuleFloor(minZ);
        if (shiftX == 0 && shiftZ == 0)
            return;

        for (int i = 0; i < model.Beams.Count; i++)
        {
            BeamRecord b = model.Beams[i];
            b.XMm -= shiftX;
            b.ZMm -= shiftZ;
            model.Beams[i] = b;
        }

        for (int i = 0; i < model.Panels.Count; i++)
        {
            PanelRecord p = model.Panels[i];
            p.XMm -= shiftX;
            p.ZMm -= shiftZ;
            model.Panels[i] = p;
        }
    }

    /// <summary>
    /// The largest whole number of modules at or below <paramref name="mm"/>.
    /// Floor rather than truncate: truncation rounds toward zero, so a part
    /// at -176 mm would shift by -88 and land at -88 instead of 0, and the
    /// same module built left of the origin would not match itself built
    /// right of it.
    /// </summary>
    public static int ModuleFloor(int mm)
    {
        int module = Mathf.RoundToInt(CatalogueData.ModuleMm);
        return Mathf.FloorToInt(mm / (float)module) * module;
    }

    /// <summary>Finish (veneer) state today is a single toggle: are any strips shown?</summary>
    static bool AnyFinishVisible()
    {
        foreach (SelectableBeam sb in Object.FindObjectsByType<SelectableBeam>(FindObjectsSortMode.None))
        {
            if (sb == null)
                continue;
            foreach (GameObject strip in sb.veneerStrips)
                if (strip != null && strip.activeSelf)
                    return true;
        }
        return false;
    }

    static int ToMm(float worldUnits) =>
        Mathf.RoundToInt(NeospaceUnits.ToMm(worldUnits));
}
