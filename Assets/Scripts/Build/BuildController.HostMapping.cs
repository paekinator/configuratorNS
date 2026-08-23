using UnityEngine;

public partial class BuildController
{
    private enum HostKind
    {
        Unknown = 0,
        V = 1,
        H = 2,
        T = 3
    }

    private HostKind InferHostKindFromRoot(Transform root)
    {
        if (root == null)
            return HostKind.Unknown;

        AttachmentPoint[] attachmentPoints =
            root.GetComponentsInChildren<AttachmentPoint>(true);
        for (int i = 0; i < attachmentPoints.Length; i++)
        {
            AttachmentPoint attachmentPoint = attachmentPoints[i];
            if (attachmentPoint == null || attachmentPoint.ownerBeamKind == BeamKind.Unknown)
                continue;

            switch (attachmentPoint.ownerBeamKind)
            {
                case BeamKind.V:
                    return HostKind.V;
                case BeamKind.H:
                    return HostKind.H;
                case BeamKind.TwistH:
                    return HostKind.T;
            }
        }

        switch (BeamPartUtility.GetKind(root.name))
        {
            case BeamKind.V:
                return HostKind.V;
            case BeamKind.H:
                return HostKind.H;
            case BeamKind.TwistH:
                return HostKind.T;
            default:
                return HostKind.Unknown;
        }
    }

    private Vector3 GetSideOutLocal(HostKind hostKind, int faceIndex)
    {
        switch (hostKind)
        {
            case HostKind.V:
                return SelectFaceVector(
                    faceIndex,
                    vHostSide1OutLocal,
                    vHostSide2OutLocal,
                    vHostSide3OutLocal,
                    vHostSide4OutLocal,
                    vHostSide2OutLocal);

            case HostKind.T:
                return SelectFaceVector(
                    faceIndex,
                    tHostSide1OutLocal,
                    tHostSide2OutLocal,
                    tHostSide3OutLocal,
                    tHostSide4OutLocal,
                    tHostSide2OutLocal);

            case HostKind.H:
            case HostKind.Unknown:
            default:
                return SelectFaceVector(
                    faceIndex,
                    hHostSide1OutLocal,
                    hHostSide2OutLocal,
                    hHostSide3OutLocal,
                    hHostSide4OutLocal,
                    hHostSide2OutLocal);
        }
    }

    private Vector3 GetFaceEulerOffset(HostKind hostKind, int faceIndex)
    {
        switch (hostKind)
        {
            case HostKind.V:
                return SelectFaceVector(
                    faceIndex,
                    vHostFace1EulerOffset,
                    vHostFace2EulerOffset,
                    vHostFace3EulerOffset,
                    vHostFace4EulerOffset,
                    Vector3.zero);

            case HostKind.H:
                return SelectFaceVector(
                    faceIndex,
                    hHostFace1EulerOffset,
                    hHostFace2EulerOffset,
                    hHostFace3EulerOffset,
                    hHostFace4EulerOffset,
                    Vector3.zero);

            case HostKind.T:
                return SelectFaceVector(
                    faceIndex,
                    tHostFace1EulerOffset,
                    tHostFace2EulerOffset,
                    tHostFace3EulerOffset,
                    tHostFace4EulerOffset,
                    Vector3.zero);

            default:
                return Vector3.zero;
        }
    }

    private static Vector3 SelectFaceVector(
        int faceIndex,
        Vector3 face1,
        Vector3 face2,
        Vector3 face3,
        Vector3 face4,
        Vector3 fallback)
    {
        switch (faceIndex)
        {
            case 1: return face1;
            case 2: return face2;
            case 3: return face3;
            case 4: return face4;
            default: return fallback;
        }
    }
}
