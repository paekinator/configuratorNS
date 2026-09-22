using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menu entry for <see cref="UIWiringCheck"/>. Run it after any UI rebuild —
/// and again in play mode, where the runtime bootstraps have had their turn.
/// </summary>
public static class UIWiringCheckMenu
{
    [MenuItem("Tools/Configurator/Check UI Wiring")]
    public static void Run()
    {
        int checks = UIWiringCheck.RunAll(out List<string> failures);

        var sb = new StringBuilder();
        sb.Append("[UI wiring] ").Append(checks).Append(" checks, ")
          .Append(failures.Count).Append(" failures")
          .Append(Application.isPlaying ? " (play mode)" : " (edit mode — runtime checks skipped)");

        foreach (string f in failures)
            sb.Append("\n    FAIL ").Append(f);

        if (failures.Count == 0)
            Debug.Log(sb.ToString());
        else
            Debug.LogError(sb.ToString());
    }
}
