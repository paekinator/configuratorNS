using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared rules for sliding horizontal-like beams up/down along the frames
/// (posts) they are plugged into. A vertical offset is valid when EVERY
/// peg-to-post-hole connection of the moving set finds a hole in the same
/// column at the shifted height that is free (or occupied by one of the
/// moving beams themselves). Used by the panel layer mover and the selection
/// move gizmo.
/// </summary>
public static class BeamSlideRules
{
    /// <summary>
    /// Vertical offsets (including 0) the beam set can slide to. Returns
    /// false when none of the beams is plugged into a vertical frame.
    /// </summary>
    public static bool CollectValidDeltas(List<Transform> beams, List<float> deltas)
    {
        var connections = new List<AttachmentPoint>(); // post holes our pegs sit in

        foreach (Transform beam in beams)
        {
            if (beam == null)
                continue;
            foreach (AttachmentPoint ap in beam.GetComponentsInChildren<AttachmentPoint>())
            {
                if (ap.role != AttachmentPoint.PointRole.Peg || ap.pairedWith == null)
                    continue;
                AttachmentPoint hole = ap.pairedWith;
                Transform holeRoot = hole.transform.root;
                if (holeRoot == null || !BeamPartUtility.IsVertical(holeRoot.name))
                    continue;
                connections.Add(hole);
            }
        }

        if (connections.Count == 0)
            return false;

        float xzEps = NeospaceUnits.ModuleMeters * 0.06f;
        float yEps = NeospaceUnits.ModuleMeters * 0.06f;

        // Hole column of one connection defines the candidate offsets…
        var candidates = new List<float>();
        foreach (AttachmentPoint hole in HoleColumn(connections[0], xzEps))
            candidates.Add(hole.transform.position.y - connections[0].transform.position.y);

        // …every other connection must have a matching free hole at each offset.
        foreach (float delta in candidates)
        {
            bool ok = true;
            foreach (AttachmentPoint conn in connections)
            {
                float targetY = conn.transform.position.y + delta;
                bool found = false;
                foreach (AttachmentPoint hole in HoleColumn(conn, xzEps))
                {
                    if (Mathf.Abs(hole.transform.position.y - targetY) > yEps)
                        continue;
                    if (HoleIsAvailable(hole, beams))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    ok = false;
                    break;
                }
            }
            if (ok)
                deltas.Add(delta);
        }

        deltas.Sort();
        return deltas.Count > 0;
    }

    static bool HoleIsAvailable(AttachmentPoint hole, List<Transform> movingBeams)
    {
        if (!hole.isOccupied || hole.pairedWith == null)
            return true;
        Transform occupantRoot = hole.pairedWith.transform.root;
        return occupantRoot != null && movingBeams.Contains(occupantRoot);
    }

    /// <summary>All holes on the same post sharing the connection's XZ column.</summary>
    static List<AttachmentPoint> HoleColumn(AttachmentPoint connection, float xzEps)
    {
        var column = new List<AttachmentPoint>();
        Transform post = connection.transform.root;
        Vector3 anchor = connection.transform.position;

        foreach (AttachmentPoint ap in post.GetComponentsInChildren<AttachmentPoint>())
        {
            if (ap.role != AttachmentPoint.PointRole.Hole)
                continue;
            Vector3 p = ap.transform.position;
            if (Mathf.Abs(p.x - anchor.x) > xzEps || Mathf.Abs(p.z - anchor.z) > xzEps)
                continue;
            column.Add(ap);
        }
        return column;
    }
}
