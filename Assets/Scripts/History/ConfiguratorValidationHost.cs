#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class ConfiguratorValidationHost : MonoBehaviour
{
    readonly List<string> _failures = new List<string>();
    readonly List<string> _editorDiagnostics = new List<string>();
    float _started;
    bool _finished;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (!SessionState.GetBool("ConfiguratorValidation.Pending", false)) return;
        SessionState.SetBool("ConfiguratorValidation.Pending", false);
        var host = new GameObject("ConfiguratorValidationHost").AddComponent<ConfiguratorValidationHost>();
        DontDestroyOnLoad(host.gameObject);
        host._started = Time.realtimeSinceStartup;
        string previous = SessionState.GetString("ConfiguratorValidation.Failures", "");
        if (!string.IsNullOrEmpty(previous)) host._failures.Add(previous);
        Application.logMessageReceived += host.Log;
        host.StartCoroutine(host.Run());
    }

    void Log(string condition, string stack, LogType type)
    {
        // This editor search-index exception also occurs with an empty scene.
        // Keep it in the report, separate from app failures; never suppress
        // exceptions with a project-script frame or other engine errors.
        if (stack.Contains("UnityEditor.Search.SearchDatabase") && !stack.Contains("Assets/Scripts/"))
        {
            _editorDiagnostics.Add(condition + "\n" + stack);
            return;
        }
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            _failures.Add(condition + "\n" + stack);
    }

    IEnumerator Run()
    {
        yield return null; // Let scene auto-bootstraps finish before creating test fixtures.
        if (SessionState.GetBool("ConfiguratorValidation.SpaceOnly", false))
        {
            yield return FinishSpaceSplitRuntimeRegression.RunAll(_failures.Add);
            yield return null;
            yield return FinishSpaceLifecycleRegression.RunAll(_failures.Add);
            Finish();
            yield break;
        }
        yield return BuildHistoryRegression.RunAll(_failures.Add);
        yield return null;
        yield return ConfigurationRestoreRegression.RunAll(_failures.Add);
        yield return null;
        yield return WorkflowUIRegression.RunAll(_failures.Add);
        yield return null;
        yield return FinishRuntimeRegression.RunAll(_failures.Add);
        yield return null;
        yield return FinishChannelExposureRuntimeRegression.RunAll(_failures.Add);
        yield return null;
        yield return BuildPartSummaryRegression.RunAll(_failures.Add);
        yield return null;
        yield return SpacePartSummaryRegression.RunAll(_failures.Add);
        yield return null;
        yield return FinishSpaceSplitRuntimeRegression.RunAll(_failures.Add);
        yield return null;
        yield return FinishSpaceLifecycleRegression.RunAll(_failures.Add);
        Finish();
    }

    void Update()
    {
        if (!_finished && Time.realtimeSinceStartup - _started > 180)
        {
            _failures.Add("Runtime validation timed out after 180 seconds.");
            Finish();
        }
    }

    void Finish()
    {
        if (_finished) return;
        _finished = true;
        Application.logMessageReceived -= Log;
        int count = SessionState.GetInt("ConfiguratorValidation.PureCount", 0);
        string result = $"Pure checks: {count}\nRuntime suites: history/summary, restore/rollback, configurator-scene UI, physical finishing/refresh, exposed top/bottom channels, installed parts/pricing, merged Space parts/pricing, actual split-frame finishing, Space finish lifecycle\nFailures: {_failures.Count}\n" + string.Join("\n", _failures);
        if (SessionState.GetBool("ConfiguratorValidation.SpaceOnly", false))
            result = $"Pure joint checks: {count}\nRuntime suites: actual split-frame finishing, Space finish lifecycle\nFailures: {_failures.Count}\n" + string.Join("\n", _failures);
        result += $"\nEditor-only search diagnostics: {_editorDiagnostics.Count}\n" + string.Join("\n", _editorDiagnostics);
        File.WriteAllText("Logs/validation-results.txt", result);
        Debug.Log(result);
        EditorApplication.Exit(_failures.Count == 0 ? 0 : 1);
    }
}
#endif
