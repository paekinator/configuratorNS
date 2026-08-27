using UnityEngine;

/// <summary>
/// Converts Rhino catalogue millimetres into Unity world units.
/// The NEOSPACE prefabs are modelled at 1 unit = 100 mm (a V3's hole rows sit
/// 0.88 units apart = one 88 mm module), so the default scale is 0.01 units/mm.
/// <see cref="Calibrate"/> can refine this at runtime by measuring a real prefab.
/// </summary>
public static class NeospaceUnits
{
    /// <summary>World units per millimetre. Default measured from the beam prefabs.</summary>
    public static float UnitsPerMm { get; private set; } = 0.01f;

    /// <summary>Multiply world units by this to get millimetres.</summary>
    public static float MetersToMm => 1f / UnitsPerMm;

    /// <summary>Millimetres → world units.</summary>
    public static float Mm(float millimetres) => millimetres * UnitsPerMm;

    /// <summary>World units → millimetres.</summary>
    public static float ToMm(float worldUnits) => worldUnits / UnitsPerMm;

    public static float ModuleMeters => Mm(CatalogueData.ModuleMm);
    public static float GroundOffsetMeters => Mm(CatalogueData.GroundOffsetMm);
    public static float PanelBasepointOffsetMeters => Mm(CatalogueData.PanelBasepointOffsetMm);

    /// <summary>
    /// Override the world scale (units per mm), e.g. after measuring the actual
    /// module spacing on a prefab. Ignores nonsensical values.
    /// </summary>
    public static void Calibrate(float unitsPerMm)
    {
        if (unitsPerMm > 1e-5f && unitsPerMm < 1f &&
            !float.IsNaN(unitsPerMm) && !float.IsInfinity(unitsPerMm))
        {
            UnitsPerMm = unitsPerMm;
        }
    }

    /// <summary>
    /// Measure the world scale from a beam prefab: attachment points named
    /// "AP_&lt;group&gt;_&lt;index&gt;" sit exactly one 88 mm module apart within a group.
    /// Returns false when no usable prefab/points are found.
    /// </summary>
    public static bool CalibrateFromPrefab(GameObject beamPrefab)
    {
        if (beamPrefab == null)
            return false;

        var points = beamPrefab.GetComponentsInChildren<AttachmentPoint>(true);
        if (points.Length < 2)
            return false;

        // Group by everything before the trailing index so consecutive rows of
        // the same side can be compared.
        var groups = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Vector3>>();
        for (int i = 0; i < points.Length; i++)
        {
            string name = points[i].name;
            int cut = name.LastIndexOf('_');
            string key = cut > 0 ? name.Substring(0, cut) : name;
            if (!groups.TryGetValue(key, out var list))
                groups[key] = list = new System.Collections.Generic.List<Vector3>();
            list.Add(points[i].transform.position);
        }

        float best = 0f;
        foreach (var pair in groups)
        {
            var list = pair.Value;
            if (list.Count < 2)
                continue;

            // Smallest distance between two points of the same group = one module.
            for (int a = 0; a < list.Count; a++)
            for (int b = a + 1; b < list.Count; b++)
            {
                float d = Vector3.Distance(list[a], list[b]);
                if (d > 1e-4f && (best <= 0f || d < best))
                    best = d;
            }
        }

        if (best <= 0f)
            return false;

        Calibrate(best / CatalogueData.ModuleMm);
        return true;
    }
}
