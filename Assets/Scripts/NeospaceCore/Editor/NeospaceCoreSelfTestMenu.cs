#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class NeospaceCoreSelfTestMenu
{
    [MenuItem("Tools/Configurator/Run NeospaceCore Selftest")]
    public static void Run()
    {
        int total = NeospaceSelfTest.RunAll(out List<string> failures);
        if (failures.Count == 0)
        {
            Debug.Log($"NEOSPACE selftest: {total} checks, 0 failures");
            EditorUtility.DisplayDialog("NeospaceCore Selftest", $"{total} checks passed.", "OK");
        }
        else
        {
            foreach (string f in failures)
                Debug.LogError("FAIL: " + f);
            EditorUtility.DisplayDialog(
                "NeospaceCore Selftest",
                $"{failures.Count} / {total} checks failed. See Console.",
                "OK");
        }
    }
}
#endif
