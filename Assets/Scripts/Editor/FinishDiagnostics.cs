using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Batch entry for the finishing diagnostics: opens the real configurator
/// scene and lets <see cref="FinishDiagnosticsHost"/> build, dress, measure
/// and render representative structures. Results land in
/// Logs/finish-diagnostics (report.txt plus PNG evidence).
///
///   Unity.exe -batchmode -projectPath &lt;project&gt; -executeMethod FinishDiagnostics.Run
/// </summary>
public static class FinishDiagnostics
{
    [MenuItem("Tools/Configurator/Run Finish Diagnostics")]
    public static void Run()
    {
        SessionState.SetString("FinishDiagnostics.Code", "");
        SessionState.SetBool("FinishDiagnostics.Pending", true);
        EditorSceneManager.OpenScene("Assets/Scenes/ConfiguratorScene.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    /// <summary>
    /// Diagnose one saved design: restores the configuration code given
    /// after <c>-finishCode</c> on the command line (batch) and reports and
    /// renders its finish instead of the built-in scenarios.
    /// </summary>
    public static void RunCode()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        string code = "";
        for (int i = 0; i + 1 < args.Length; i++)
            if (args[i] == "-finishCode")
                code = args[i + 1];
        SessionState.SetString("FinishDiagnostics.Code", code);
        SessionState.SetBool("FinishDiagnostics.Pending", true);
        EditorSceneManager.OpenScene("Assets/Scenes/ConfiguratorScene.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
}
