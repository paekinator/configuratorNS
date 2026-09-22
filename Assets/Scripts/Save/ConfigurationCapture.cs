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

    public static ConfigurationModel Capture(BuildController build)
    {
        var model = new ConfigurationModel();
        int ghostMask = build != null ? build.ghostLayerMask.value : 0;

        Roots.Clear();
        foreach (BeamConnections conn in Object.FindObjectsByType<BeamConnections>(FindObjectsSortMode.None))
        {
            if (conn == null)
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
                throw new System.InvalidOperationException(
                    $"Part '{partId}' is missing from the save catalogue. The piece was not saved; " +
                    "update the part registry before saving this build.");
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
        return model;
    }

    /// <summary>
    /// Finish state is the FinishController toggle (the generative veneer/cap
    /// dressing). The old pre-baked strip system it replaced never populated
    /// its strip lists, so reading those always said "off" and saved codes
    /// silently dropped the finish.
    /// </summary>
    static bool AnyFinishVisible()
    {
        return FinishController.Instance != null && FinishController.Instance.IsOn;
    }

    static int ToMm(float worldUnits) =>
        Mathf.RoundToInt(NeospaceUnits.ToMm(worldUnits));
}
