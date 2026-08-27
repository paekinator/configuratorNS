using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure planning for NST1-style V posts: 1 / 2 / 4 posts from ground rectangle + height.
/// Positions are Unity metres on XZ; Y is left at 0 for the spawner to seat on the floor.
/// </summary>
public static class T1PostsPlanner
{
    public struct Plan
    {
        public bool IsValid;
        public string Message;
        public int VSize;
        public int PostCount;
        public Vector3[] GroundPoints; // XZ metres, Y ignored
        public List<TemplatePartPose> Parts;
    }

    /// <summary>
    /// Resolve a T1 plan from picks.
    /// base1/base2/depth are floor hits; topY is world Y of the height pick (or same as floor for V1).
    /// floorY is the floor surface height used for height delta.
    /// </summary>
    public static Plan PlanFromPicks(
        Vector3 base1,
        Vector3? base2,
        Vector3? depthPick,
        float topY,
        float floorY,
        Quaternion vRotation)
    {
        var plan = new Plan
        {
            Parts = new List<TemplatePartPose>(),
            GroundPoints = System.Array.Empty<Vector3>()
        };

        Vector3 g1 = SnapGround(base1);

        // Single post: base2 omitted or same as base1
        if (!base2.HasValue || HorizontalDistance(g1, SnapGround(base2.Value)) < NeospaceUnits.ModuleMeters * 0.25f)
        {
            Catalogue.SnapHeight((topY - floorY) * NeospaceUnits.MetersToMm, out _, out int vSize);
            if (!Catalogue.IsValidV(vSize))
            {
                plan.Message = "Invalid V size";
                return plan;
            }

            plan.IsValid = true;
            plan.VSize = vSize;
            plan.PostCount = 1;
            plan.GroundPoints = new[] { g1 };
            plan.Parts.Add(new TemplatePartPose("V" + vSize, g1, vRotation, "T1 post"));
            plan.Message = $"1 × V{vSize}";
            return plan;
        }

        Vector3 g2 = SnapGround(base2.Value);
        ResolveGap(g1, g2, out Vector3 dir, out float spanM, out int? spanSize);
        if (!spanSize.HasValue || spanM < 1e-6f)
        {
            // collapsed to single after snap
            Catalogue.SnapHeight((topY - floorY) * NeospaceUnits.MetersToMm, out _, out int vSizeSingle);
            plan.IsValid = Catalogue.IsValidV(vSizeSingle);
            plan.VSize = vSizeSingle;
            plan.PostCount = 1;
            plan.GroundPoints = new[] { g1 };
            if (plan.IsValid)
                plan.Parts.Add(new TemplatePartPose("V" + vSizeSingle, g1, vRotation, "T1 post"));
            plan.Message = plan.IsValid ? $"1 × V{vSizeSingle}" : "Invalid V size";
            return plan;
        }

        Vector3 p2 = g1 + dir * spanM;
        Vector3 perp = new Vector3(-dir.z, 0f, dir.x);

        bool fourPosts = depthPick.HasValue &&
                         HorizontalDistance(p2, SnapGround(depthPick.Value)) >= NeospaceUnits.ModuleMeters * 0.25f;

        Vector3 p3 = p2;
        Vector3 p4 = g1;
        int? depthSize = null;
        float depthM = 0f;

        if (fourPosts)
        {
            ResolveDepth(p2, perp, SnapGround(depthPick.Value), out Vector3 depthDir, out depthM, out depthSize);
            if (!depthSize.HasValue || depthM < 1e-6f)
                fourPosts = false;
            else
            {
                p3 = p2 + depthDir * depthM;
                p4 = g1 + depthDir * depthM;
            }
        }

        Catalogue.SnapHeight((topY - floorY) * NeospaceUnits.MetersToMm, out _, out int vSizeFinal);
        if (!Catalogue.IsValidV(vSizeFinal))
        {
            plan.Message = "Invalid V size";
            return plan;
        }

        var grounds = fourPosts
            ? new[] { g1, p2, p3, p4 }
            : new[] { g1, p2 };

        plan.IsValid = true;
        plan.VSize = vSizeFinal;
        plan.PostCount = grounds.Length;
        plan.GroundPoints = grounds;
        string partId = "V" + vSizeFinal;
        for (int i = 0; i < grounds.Length; i++)
            plan.Parts.Add(new TemplatePartPose(partId, grounds[i], vRotation, "T1 post " + (i + 1)));

        plan.Message = fourPosts
            ? $"4 × {partId} (span H{spanSize}, depth H{depthSize})"
            : $"2 × {partId} (span H{spanSize})";
        return plan;
    }

    public static Vector3 SnapGround(Vector3 world)
    {
        float module = NeospaceUnits.ModuleMeters;
        float x = Mathf.Round(world.x / module) * module;
        float z = Mathf.Round(world.z / module) * module;
        return new Vector3(x, world.y, z);
    }

    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    static void ResolveGap(Vector3 from, Vector3 to, out Vector3 direction, out float spanM, out int? hSize)
    {
        Vector3 v = to - from;
        v.y = 0f;
        if (Mathf.Abs(v.x) >= Mathf.Abs(v.z))
        {
            direction = v.x >= 0f ? Vector3.right : Vector3.left;
            Catalogue.SnapSpan(Mathf.Abs(v.x) * NeospaceUnits.MetersToMm, out float mm, out hSize);
            spanM = NeospaceUnits.Mm(mm);
        }
        else
        {
            direction = v.z >= 0f ? Vector3.forward : Vector3.back;
            Catalogue.SnapSpan(Mathf.Abs(v.z) * NeospaceUnits.MetersToMm, out float mm, out hSize);
            spanM = NeospaceUnits.Mm(mm);
        }
    }

    static void ResolveDepth(
        Vector3 from,
        Vector3 perp,
        Vector3 to,
        out Vector3 direction,
        out float depthM,
        out int? hSize)
    {
        Vector3 v = to - from;
        v.y = 0f;
        float proj = Vector3.Dot(v, perp);
        direction = proj >= 0f ? perp : -perp;
        Catalogue.SnapSpan(Mathf.Abs(proj) * NeospaceUnits.MetersToMm, out float mm, out hSize);
        depthM = NeospaceUnits.Mm(mm);
    }
}
