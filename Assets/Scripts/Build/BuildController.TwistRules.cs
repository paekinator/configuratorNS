using UnityEngine;

public partial class BuildController
{
    /// <summary>
    /// Twist (T*/HT) beams only accept OPPOSITE kinds on their two end pegs:
    /// if one peg already holds a V post, the other peg only accepts horizontal
    /// beams, and vice versa. Returns true when the incoming attachment must be
    /// rejected, with a human-readable reason for the status UI.
    /// </summary>
    private static bool TwistPegRejectsAttachment(
        AttachmentPoint hostPeg,
        bool incomingIsVertical,
        out string reason)
    {
        reason = null;
        if (hostPeg == null)
            return false;

        Transform hostRoot = hostPeg.transform.root;
        bool isTwist = hostPeg.ownerBeamKind == BeamKind.TwistH ||
                       (hostPeg.ownerBeamKind == BeamKind.Unknown &&
                        BeamPartUtility.IsTwist(hostRoot.name));
        if (!isTwist)
            return false;

        // Peg A always attaches to a horizontal frame; Peg B to a vertical post.
        if (BeamPartUtility.IsTwistPegA(hostPeg) && incomingIsVertical)
        {
            reason = $"Twist beam {hostRoot.name}: Peg A only attaches to a beam";
            return true;
        }
        if (BeamPartUtility.IsTwistPegB(hostPeg) && !incomingIsVertical)
        {
            reason = $"Twist beam {hostRoot.name}: Peg B only attaches to a frame";
            return true;
        }

        AttachmentPoint[] attachmentPoints =
            hostRoot.GetComponentsInChildren<AttachmentPoint>(true);

        for (int i = 0; i < attachmentPoints.Length; i++)
        {
            AttachmentPoint otherPeg = attachmentPoints[i];
            if (otherPeg == null ||
                otherPeg == hostPeg ||
                otherPeg.role != AttachmentPoint.PointRole.Peg ||
                !otherPeg.isOccupied ||
                otherPeg.pairedWith == null)
                continue;

            BeamKind otherKind = GetAttachmentOwnerKind(
                otherPeg.pairedWith, otherPeg.pairedWith.transform.root);
            bool otherIsVertical = otherKind == BeamKind.V;

            if (otherIsVertical == incomingIsVertical)
            {
                string kindLabel = incomingIsVertical ? "frame" : "beam";
                reason = $"Twist beam {hostRoot.name}: other peg already holds a " +
                         $"{(otherIsVertical ? "frame" : "beam")}, this peg only accepts the " +
                         $"opposite kind (rejected {kindLabel})";
                return true;
            }
        }

        return false;
    }
}
