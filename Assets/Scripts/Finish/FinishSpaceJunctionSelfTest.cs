#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Joint regression using the supplied prefabs' physical AP endpoints: H7/T7
/// peg markers are +/-332 mm about their centre, so a connector between post
/// axes 704 mm apart ends on the socket faces at 20 and 684 mm. The post's
/// channel must recognize that occupied socket, not only a tip on its axis.
/// No panel policy is changed or used by these independent frame-only cases.
/// </summary>
public static class FinishSpaceJunctionSelfTest
{
    public static int RunAll(out List<string> failures)
    {
        failures = new List<string>();
        var failed = failures;
        int checks = 0;
        var fixtures = new List<GameObject>();
        void Check(bool passed, string label)
        {
            checks++;
            if (!passed) failed.Add("Space finish junction: " + label);
        }

        try
        {
            foreach (int size in new[] { 9, 25 })
            foreach (float yaw in new[] { 0f, 37f })
            {
                Quaternion rotation = Quaternion.AngleAxis(yaw, Vector3.up);
                Vector3 face = rotation * Vector3.right;
                Vector3 across = rotation * Vector3.forward;
                var post = Post(size, rotation, fixtures);
                string prefix = post.PartId + " at " + yaw + " degrees: ";
                var bare = Plan(post);
                var nominal = Plan(post, Connector("H7", post.Center, face, 0f, false, fixtures));
                var physical = Plan(post, Connector("H7", post.Center, face, 20f, false, fixtures));

                Check(Signature(nominal, post, face) != Signature(bare, post, face),
                    prefix + "the nominal axis-tip reference actually occupies the middle socket");
                Check(!HasVeneerAt(physical, post, face, 0f),
                    prefix + "a real H peg on the 20 mm socket face must interrupt the inward veneer at its hole");
                Check(!HasCapAt(physical, post, face, 0f),
                    prefix + "an installed H joint must not receive a Cap Side over its occupied socket");
                Check(AllFacesSignature(physical, post) == AllFacesSignature(nominal, post),
                    prefix + "physical face-tip and nominal axis-tip H joints produce identical post finishing");
                // Rhino rule: a connection level divides every channel of the
                // post, so the three free faces take a Cap Side ring at the
                // joint hole and their veneers meet it from above and below.
                foreach (Vector3 other in new[] { -face, across, -across })
                    Check(HasCapAt(physical, post, other, 0f) && !HasVeneerAt(physical, post, other, 0f) &&
                          HasVeneerAt(physical, post, other, -176f) && HasVeneerAt(physical, post, other, 176f),
                        prefix + "the three unconnected post faces divide at the connection level with a Cap Side and veneers either side");
                Check(HasVeneerAt(physical, post, face, -176f) && HasVeneerAt(physical, post, face, 176f),
                    prefix + "the connected face keeps exposed veneer above and below the socket");

                var rounded = Connector("H7", post.Center, face, 19.99998f, false, fixtures);
                rounded.EndA += across * NeospaceUnits.Mm(0.0002f) + Vector3.up * NeospaceUnits.Mm(0.0001f);
                Check(AllFacesSignature(Plan(post, rounded), post) == AllFacesSignature(nominal, post),
                    prefix + "normal imported AP rounding still recognizes the socket connection");

                var lateralMiss = Connector("H7", post.Center + across * NeospaceUnits.Mm(20f), face, 20f, false, fixtures);
                Check(AllFacesSignature(Plan(post, lateralMiss), post) == AllFacesSignature(bare, post),
                    prefix + "a connector displaced 20 mm across the face must not hide an unoccupied socket");
                var shortOfFace = Connector("H7", post.Center, face, 44f, false, fixtures);
                Check(AllFacesSignature(Plan(post, shortOfFace), post) == AllFacesSignature(bare, post),
                    prefix + "a peg ending 24 mm short of the socket face must not hide the post");
                var offModule = Connector("H7", post.Center + Vector3.up * NeospaceUnits.Mm(17f), face, 20f, false, fixtures);
                Check(AllFacesSignature(Plan(post, offModule), post) == AllFacesSignature(bare, post),
                    prefix + "a connector 17 mm off the hole level must not become a joint through a broad distance tolerance");

                var twistA = Connector("T7", post.Center, face, 20f, false, fixtures);
                Check(AllFacesSignature(Plan(post, twistA), post) == AllFacesSignature(bare, post),
                    prefix + "twist Peg A belongs to an H host and must not mask a V channel");
                foreach (int sign in new[] { 1, -1 })
                {
                    Vector3 connectedFace = face * sign;
                    var twistB = Plan(post, Connector("T7", post.Center, connectedFace, 20f, true, fixtures));
                    var nominalB = Plan(post, Connector("T7", post.Center, connectedFace, 0f, true, fixtures));
                    Check(AllFacesSignature(twistB, post) == AllFacesSignature(nominalB, post),
                        prefix + "physical twist Peg B produces the nominal joint coverage on side " + sign);
                    Check(!HasVeneerAt(twistB, post, connectedFace, 0f) && !HasCapAt(twistB, post, connectedFace, 0f),
                        prefix + "twist Peg B on side " + sign + " has no veneer or cap over its occupied socket");
                }
            }
        }
        finally
        {
            foreach (GameObject fixture in fixtures)
                if (fixture != null) Object.DestroyImmediate(fixture);
        }
        return checks;
    }

    static FrameOverlapResolver.FrameRecord Post(int size, Quaternion rotation, List<GameObject> fixtures)
    {
        Vector3 center = Vector3.up * NeospaceUnits.Mm(1600f);
        Transform root = Root("V" + size, center, fixtures);
        root.rotation = rotation;
        Vector3 half = Vector3.up * NeospaceUnits.Mm(Skeleton.VSkeletonLength(size) * 0.5f);
        return new FrameOverlapResolver.FrameRecord
        {
            Root = root, PartId = "V" + size, Size = size, Center = center,
            LengthAxis = Vector3.up, LocalY = Vector3.up, EndA = center - half, EndB = center + half
        };
    }

    static FrameOverlapResolver.FrameRecord Connector(string id, Vector3 socketAxis, Vector3 direction,
        float faceTipMm, bool pegBAtPost, List<GameObject> fixtures)
    {
        Vector3 center = socketAxis + direction * NeospaceUnits.Mm(352f);
        Vector3 near = socketAxis + direction * NeospaceUnits.Mm(faceTipMm);
        Vector3 far = socketAxis + direction * NeospaceUnits.Mm(704f - faceTipMm);
        return new FrameOverlapResolver.FrameRecord
        {
            Root = Root(id, center, fixtures), PartId = id, Size = 7, Center = center,
            LengthAxis = pegBAtPost ? -direction : direction, LocalY = Vector3.up,
            EndA = pegBAtPost ? far : near, EndB = pegBAtPost ? near : far
        };
    }

    static Transform Root(string name, Vector3 center, List<GameObject> fixtures)
    {
        var root = new GameObject(name + "_JunctionFixture") { hideFlags = HideFlags.HideInHierarchy };
        root.transform.position = center;
        fixtures.Add(root);
        return root.transform;
    }

    static FinishGenerator.Result Plan(params FrameOverlapResolver.FrameRecord[] frames)
        => FinishGenerator.Plan(new List<FrameOverlapResolver.FrameRecord>(frames), new List<FinishGenerator.PanelBox>());

    static bool OnFace(FinishGenerator.Placement part, FrameOverlapResolver.FrameRecord post, Vector3 face)
        => (part.Model == "Cap Side" || part.Model.StartsWith("Veneer H", StringComparison.Ordinal)) &&
           Vector3.Dot(part.Normal, face) > 0.999f && Mathf.Abs(Vector3.Dot(part.LengthDir, post.LengthAxis)) > 0.999f;

    static bool HasVeneerAt(FinishGenerator.Result result, FrameOverlapResolver.FrameRecord post, Vector3 face, float heightMm)
    {
        foreach (var part in result.Parts)
        {
            if (!OnFace(part, post, face) || !part.Model.StartsWith("Veneer H", StringComparison.Ordinal)) continue;
            float offset = NeospaceUnits.ToMm(Vector3.Dot(part.Center - post.Center, post.LengthAxis));
            int size = int.Parse(part.Model.Substring(8));
            float halfLength = (Skeleton.VeneerContactLength(size) - 2.08575f) * 0.5f;
            if (heightMm > offset - halfLength && heightMm < offset + halfLength) return true;
        }
        return false;
    }

    static bool HasCapAt(FinishGenerator.Result result, FrameOverlapResolver.FrameRecord post, Vector3 face, float heightMm)
    {
        foreach (var part in result.Parts)
            if (part.Model == "Cap Side" && OnFace(part, post, face) &&
                Mathf.Abs(NeospaceUnits.ToMm(Vector3.Dot(part.Center - post.Center, post.LengthAxis)) - heightMm) < 0.01f) return true;
        return false;
    }

    static string AllFacesSignature(FinishGenerator.Result result, FrameOverlapResolver.FrameRecord post)
    {
        Vector3 face = post.Root.right;
        Vector3 across = Vector3.Cross(face, Vector3.up);
        return Signature(result, post, face) + "|" + Signature(result, post, -face) + "|" +
               Signature(result, post, across) + "|" + Signature(result, post, -across);
    }

    static string Signature(FinishGenerator.Result result, FrameOverlapResolver.FrameRecord post, Vector3 face)
    {
        var rows = new List<string>();
        foreach (var part in result.Parts)
            if (OnFace(part, post, face))
                rows.Add(part.Model + "@" + Mathf.RoundToInt(100f * NeospaceUnits.ToMm(Vector3.Dot(part.Center - post.Center, post.LengthAxis))));
        rows.Sort(StringComparer.Ordinal);
        return string.Join(";", rows);
    }
}
#endif
