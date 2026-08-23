#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ConfigurationCodeSelfTestMenu
{
    [MenuItem("Tools/Configurator/Run ConfigCode Selftest")]
    public static void Run()
    {
        int total = ConfigurationCodeSelfTest.RunAll(out List<string> failures);
        if (failures.Count == 0)
        {
            Debug.Log($"ConfigCode selftest: {total} checks, 0 failures");
            EditorUtility.DisplayDialog("ConfigCode Selftest", $"{total} checks passed.", "OK");
        }
        else
        {
            foreach (string f in failures)
                Debug.LogError("FAIL: " + f);
            EditorUtility.DisplayDialog(
                "ConfigCode Selftest",
                $"{failures.Count} / {total} checks failed. See Console.",
                "OK");
        }
    }
}
#endif
