using System.Collections.Generic;

/// <summary>
/// The logical content of a configuration code — plain integers only, no
/// Unity scene types, so it can be built, compared and round-tripped
/// headless. Millimetres everywhere; conversion to world units happens in
/// <see cref="ConfigurationCapture"/> / <see cref="ConfigurationRestorer"/>.
///
/// Addressing (per the v1 schema, see CONFIG_CODE_SCHEMA.md):
///  - PANELS are attachment-slot references: quantized slot center + the
///    slot's normal axis + side, re-resolved to a live slot on restore.
///  - BEAMS are quantized poses (integer mm + one of 24 axis-aligned
///    orientations, with a 0.1° euler escape hatch). Slot-addressing beams
///    is a v2 idea once hole indices are stable.
/// </summary>
public sealed class ConfigurationModel
{
    public bool FinishApplied;
    public readonly List<BeamRecord> Beams = new List<BeamRecord>();
    public readonly List<PanelRecord> Panels = new List<PanelRecord>();
}

public struct BeamRecord
{
    public int PartCode;             // PartRegistry id
    public int XMm, YMm, ZMm;        // root position, integer millimetres

    /// <summary>0–23 = axis-aligned orientation index; -1 = use euler fields.</summary>
    public int OrientIndex;
    /// <summary>Euler angles in 0.1° steps (0–3599), only when OrientIndex is -1.</summary>
    public int EulerXDeci, EulerYDeci, EulerZDeci;
}

/// <summary>Slot plane normal, snapped to a world axis.</summary>
public enum SlotAxis : byte
{
    PlusX = 0, MinusX = 1,
    PlusY = 2, MinusY = 3,   // lying panels (shelf / floor)
    PlusZ = 4, MinusZ = 5
}

public struct PanelRecord
{
    public int XMm, YMm, ZMm;        // panel board center, integer millimetres
    public SlotAxis Axis;            // slot normal axis
    public bool SideMinus;           // true = panel sits on the -normal side
}
