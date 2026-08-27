using System.Collections;
using UnityEngine;

public partial class BuildController
{
    private void QueuePromoteHostHToTByPegMix(Transform hostRoot)
    {
        // Kept as a no-op so existing commit call sites stay unchanged.
        // An H that holds a V on one peg and an H/T on the other is still an H.
        // Only a dedicated T/HT part is a twist — renaming the host used to
        // cancel its panel slots and rewrite its finish when a twist attached.
        _ = hostRoot;
    }

    private IEnumerator TryPromoteHostHToTByPegMixDelayed(Transform hostRoot)
    {
        yield break;
    }

    private void TryPromoteHostHToTByPegMix(Transform hostRoot)
    {
        _ = hostRoot;
    }

    private static BeamKind GetAttachmentOwnerKind(
        AttachmentPoint attachmentPoint,
        Transform root)
    {
        if (attachmentPoint != null && attachmentPoint.ownerBeamKind != BeamKind.Unknown)
            return attachmentPoint.ownerBeamKind;

        return root != null ? BeamPartUtility.GetKind(root.name) : BeamKind.Unknown;
    }
}
