#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class SpaceCodeSelfTestMenu
{
    [MenuItem("Tools/Configurator/Run SpaceCode Selftest")]
    public static void Run()
    {
        int total = SpaceCodeSelfTest.RunAll(out List<string> failures);
        if (failures.Count == 0)
        {
            Debug.Log($"SpaceCode selftest: {total} checks, 0 failures");
            EditorUtility.DisplayDialog("SpaceCode Selftest", $"{total} checks passed.", "OK");
        }
        else
        {
            foreach (string f in failures)
                Debug.LogError("FAIL: " + f);
            EditorUtility.DisplayDialog(
                "SpaceCode Selftest",
                $"{failures.Count} / {total} checks failed. See Console.",
                "OK");
        }
    }
}
#endif
