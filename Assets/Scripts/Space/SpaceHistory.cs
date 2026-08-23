using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Undo / redo for Space Mode. Snapshots are tiny — one entry per placed
/// piece (identity + pose) — and restoring is synchronous: instances are
/// destroyed and re-cloned from the factory's cached masters, so stepping
/// through history is instant. Keyboard shortcuts are handled by
/// <see cref="SpaceModeController"/> (only while Space Mode is active);
/// the builder's own history is suspended for the duration.
/// </summary>
public class SpaceHistory : MonoBehaviour
{
    public int maxSteps = 100;

    public struct InstanceState
    {
        public string PieceId;
        public string PieceName;
        public string Code;
        public float Price;
        public Vector3 Position;
        public float YawDegrees;
    }

    class Snapshot
    {
        public readonly List<InstanceState> Instances = new List<InstanceState>();
        public string Signature;
    }

    readonly List<Snapshot> _timeline = new List<Snapshot>();
    int _index = -1;

    public bool CanUndo => _index > 0;
    public bool CanRedo => _index >= 0 && _index < _timeline.Count - 1;

    /// <summary>Wipes the timeline and stores the given state as the baseline.</summary>
    public void ResetBaseline(List<InstanceState> current)
    {
        _timeline.Clear();
        _index = -1;
        Record(current);
    }

    /// <summary>Call after every completed Space action (place, move, ...).</summary>
    public void Record(List<InstanceState> current)
    {
        Snapshot snap = Build(current);

        if (_index >= 0 && _timeline[_index].Signature == snap.Signature)
            return;

        if (_index < _timeline.Count - 1)
            _timeline.RemoveRange(_index + 1, _timeline.Count - _index - 1);

        _timeline.Add(snap);
        _index = _timeline.Count - 1;

        while (_timeline.Count > Mathf.Max(2, maxSteps))
        {
            _timeline.RemoveAt(0);
            _index--;
        }
    }

    /// <summary>Returns the state to rebuild, or null when at the beginning.</summary>
    public List<InstanceState> Undo()
    {
        if (!CanUndo)
        {
            SelectionStatus.Set("Nothing to undo in this space.", 2f);
            return null;
        }
        _index--;
        return new List<InstanceState>(_timeline[_index].Instances);
    }

    public List<InstanceState> Redo()
    {
        if (!CanRedo)
        {
            SelectionStatus.Set("Nothing to redo in this space.", 2f);
            return null;
        }
        _index++;
        return new List<InstanceState>(_timeline[_index].Instances);
    }

    static Snapshot Build(List<InstanceState> current)
    {
        var snap = new Snapshot();
        snap.Instances.AddRange(current);

        var lines = new List<string>(current.Count);
        foreach (InstanceState s in current)
        {
            lines.Add($"{s.PieceId}|{Mathf.RoundToInt(s.Position.x * 500f)}," +
                      $"{Mathf.RoundToInt(s.Position.y * 500f)},{Mathf.RoundToInt(s.Position.z * 500f)}" +
                      $"|{Mathf.RoundToInt(s.YawDegrees)}");
        }
        lines.Sort(System.StringComparer.Ordinal);

        var sb = new StringBuilder(lines.Count * 40);
        foreach (string line in lines)
            sb.AppendLine(line);
        snap.Signature = sb.ToString();
        return snap;
    }
}
