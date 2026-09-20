using UnityEngine;

/// <summary>
/// Where a frozen master's pivot sits in the block's OWN coordinates — the
/// millimetres its configuration code stores, before any staging shift.
///
/// A block has to be placed from somewhere, and that somewhere has to be the
/// same point for the preview and for the result. The master's pivot is not
/// a plain bounds centre: it is snapped onto the block's post-centre lattice
/// so every post lands on a grid intersection (see PieceInstanceFactory).
/// Nothing reading only the configuration code can arrive at that point —
/// the rule needs renderer bounds and a post's position, neither of which a
/// code carries.
///
/// So the factory writes it down here rather than leaving every caller to
/// approximate it. Pro's block stamp reads it and re-bases its part offsets
/// onto the same origin; without it the stamped block sat a constant
/// distance from the ghost, because the two were measuring from different
/// definitions of "the middle".
/// </summary>
public class PieceMasterInfo : MonoBehaviour
{
    public Vector3 ModelPivot;
}
