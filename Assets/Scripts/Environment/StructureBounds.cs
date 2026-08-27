using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The configurator's internal coordinate system and structure metrics.
///
/// Coordinates: the world is an 88 mm module grid centred on the world
/// origin (the same grid every placement snaps to). <see cref="WorldToModule"/>
/// and <see cref="ModuleToWorld"/> convert between world metres and integer
/// module cells.
///
/// Metrics: <see cref="TryCompute"/> measures everything currently built —
/// world bounds, centre, and width / depth / height in millimetres. This is
/// the data a saved configuration will carry, and what the dimension
/// annotations and the adaptive grid feed on.
/// </summary>
public static class StructureBounds
{
    public struct Info
    {
        public Bounds WorldBounds;
        public int PartCount;

        /// <summary>Geometric centre of everything built (world metres).</summary>
        public Vector3 Center;
        /// <summary>Centre of the footprint at ground level (world metres).</summary>
        public Vector3 GroundCenter;

        public float WidthMm;    // world X extent
        public float DepthMm;    // world Z extent
        public float HeightMm;   // world Y extent

        /// <summary>Inclusive module-cell range the structure occupies.</summary>
        public Vector3Int MinModule;
        public Vector3Int MaxModule;
    }

    public static Vector3Int WorldToModule(Vector3 world)
    {
        float m = NeospaceUnits.ModuleMeters;
        return new Vector3Int(
            Mathf.RoundToInt(world.x / m),
            Mathf.RoundToInt(world.y / m),
            Mathf.RoundToInt(world.z / m));
    }

    public static Vector3 ModuleToWorld(Vector3Int module)
    {
        float m = NeospaceUnits.ModuleMeters;
        return new Vector3(module.x * m, module.y * m, module.z * m);
    }

    static readonly HashSet<Transform> Roots = new HashSet<Transform>();

    /// <summary>
    /// Measure everything currently built (placed beams + panels, ghosts
    /// excluded). False when nothing is placed.
    /// </summary>
    public static bool TryCompute(BuildController build, out Info info)
    {
        info = default;
        int ghostMask = build != null ? build.ghostLayerMask.value : 0;

        Roots.Clear();
        foreach (BeamConnections conn in Object.FindObjectsByType<BeamConnections>(FindObjectsSortMode.None))
        {
            if (conn == null)
                continue;
            Transform root = conn.transform.root;
            if (root == null || (ghostMask & (1 << root.gameObject.layer)) != 0)
                continue;
            Roots.Add(root);
        }
        foreach (PanelInstance pi in Object.FindObjectsByType<PanelInstance>(FindObjectsSortMode.None))
        {
            if (pi == null || (ghostMask & (1 << pi.gameObject.layer)) != 0)
                continue;
            Roots.Add(pi.transform);
        }

        // Space Mode: placed piece instances count as structure too, so the
        // adaptive grid keeps following what's on the floor. They are frozen
        // clones without BeamConnections/PanelInstance, hence the own pass;
        // in Piece Mode they are inactive and skipped automatically.
        foreach (SpaceInstance inst in Object.FindObjectsByType<SpaceInstance>(FindObjectsSortMode.None))
        {
            if (inst != null)
                Roots.Add(inst.transform);
        }

        bool any = false;
        Bounds bounds = default;
        foreach (Transform root in Roots)
        {
            // Piece masters are staged ~200 m off-camera while they build;
            // never let that staging area count as structure.
            Vector3 p = root.position;
            if (Mathf.Abs(p.x) > 500f || Mathf.Abs(p.z) > 500f)
                continue;

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
                    bounds.Encapsulate(r.bounds);
            }
        }

        if (!any)
            return false;

        float m = NeospaceUnits.ModuleMeters;
        info.WorldBounds = bounds;
        info.PartCount = Roots.Count;
        info.Center = bounds.center;
        info.GroundCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        info.WidthMm = NeospaceUnits.ToMm(bounds.size.x);
        info.DepthMm = NeospaceUnits.ToMm(bounds.size.z);
        info.HeightMm = NeospaceUnits.ToMm(bounds.size.y);
        info.MinModule = new Vector3Int(
            Mathf.FloorToInt(bounds.min.x / m + 1e-4f),
            Mathf.FloorToInt(bounds.min.y / m + 1e-4f),
            Mathf.FloorToInt(bounds.min.z / m + 1e-4f));
        info.MaxModule = new Vector3Int(
            Mathf.CeilToInt(bounds.max.x / m - 1e-4f),
            Mathf.CeilToInt(bounds.max.y / m - 1e-4f),
            Mathf.CeilToInt(bounds.max.z / m - 1e-4f));
        return true;
    }
}
