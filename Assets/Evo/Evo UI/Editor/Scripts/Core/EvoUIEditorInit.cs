using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace Evo.UI
{
    [InitializeOnLoad]
    public static class EditorInit
    {
        static EditorInit()
        {
            AddDefineSymbol();
        }

        static void AddDefineSymbol()
        {
            // Get all build target groups from BuildTarget enum
            var allBuildTargets = System.Enum.GetValues(typeof(BuildTarget))
                .Cast<BuildTarget>()
                .Where(bt => bt > 0) // Filter out invalid/unknown targets
                .ToList();

            foreach (var buildTarget in allBuildTargets)
            {
                try
                {
                    // Convert BuildTarget to NamedBuildTarget
                    var namedTarget = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(buildTarget));

                    if (namedTarget == NamedBuildTarget.Unknown)
                        continue;

                    string defines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
                    if (!defines.Contains(Constants.DefineSymbol))
                    {
                        if (string.IsNullOrEmpty(defines)) { defines = Constants.DefineSymbol; }
                        else { defines += ";" + Constants.DefineSymbol; }
                        PlayerSettings.SetScriptingDefineSymbols(namedTarget, defines);
                    }
                }
                catch
                {
                    // Skip platforms that aren't installed or supported
                    continue;
                }
            }
        }
    }

    public class EvoUIAssetPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string deletedAsset in deletedAssets)
            {
                // Check if this specific script file was deleted
                if (deletedAsset.EndsWith("EvoUIEditorInit.cs"))
                {
                    RemoveDefineSymbol();
                    break;
                }
            }
        }

        static void RemoveDefineSymbol()
        {
            // Get all build target groups from BuildTarget enum
            var allBuildTargets = System.Enum.GetValues(typeof(BuildTarget))
                .Cast<BuildTarget>()
                .Where(bt => bt > 0) // Filter out invalid/unknown targets
                .ToList();

            foreach (var buildTarget in allBuildTargets)
            {
                try
                {
                    // Convert BuildTarget to NamedBuildTarget
                    var namedTarget = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(buildTarget));

                    if (namedTarget == NamedBuildTarget.Unknown)
                        continue;

                    string defines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);

                    if (defines.Contains(Constants.DefineSymbol))
                    {
                        // Split by semicolon, remove the specific symbol, rejoin
                        var symbolList = defines.Split(';')
                            .Select(s => s.Trim())
                            .Where(s => !string.IsNullOrEmpty(s) && s != Constants.DefineSymbol)
                            .ToList();

                        string newDefines = string.Join(";", symbolList);
                        PlayerSettings.SetScriptingDefineSymbols(namedTarget, newDefines);
                    }
                }
                catch
                {
                    // Skip platforms that aren't installed or supported
                    continue;
                }
            }
        }
    }
}