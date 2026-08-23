using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Pins the import scale of the CAD-exported FBX models: the beam frames
/// (V*/H*/HT* in Assets/Models) and the finish parts (veneers, caps, Foot
/// in Assets/Resources/Finish). The CAD exports come in at 1/10 scale, and
/// re-exporting a model resets its importer to globalScale 1, which makes
/// the parts import 10x too small — exactly what happened when the missing
/// veneer sizes were added. Enforcing the scale here means every import,
/// including future re-exports, is correct without touching .meta files by
/// hand.
/// </summary>
public class BeamModelImportEnforcer : AssetPostprocessor
{
    const float RequiredScale = 10f;
    const string ModelsFolder = "Assets/Models/";
    const string FinishFolder = "Assets/Resources/Finish/";
    static readonly Regex BeamName = new Regex(@"^(HT|V|H)\d+$", RegexOptions.IgnoreCase);
    static readonly Regex FinishName = new Regex(@"^(Veneer .+|Cap Side|Cap End|Foot)$",
        RegexOptions.IgnoreCase);

    static bool IsPinnedModel(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        if (path.StartsWith(ModelsFolder))
            return BeamName.IsMatch(name);
        if (path.StartsWith(FinishFolder))
            return FinishName.IsMatch(name);
        return false;
    }

    void OnPreprocessModel()
    {
        if (!IsPinnedModel(assetPath))
            return;

        var importer = (ModelImporter)assetImporter;
        if (Mathf.Approximately(importer.globalScale, RequiredScale))
            return;

        importer.globalScale = RequiredScale;
        Debug.Log($"[BeamModelImportEnforcer] {Path.GetFileName(assetPath)}: " +
                  $"import scale corrected to {RequiredScale}.");
    }

    [MenuItem("Tools/Configurator/Reimport Beam + Finish Models At Correct Scale")]
    static void ReimportAllPinnedModels()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model",
            new[] { "Assets/Models", "Assets/Resources/Finish" });
        int count = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!IsPinnedModel(path))
                continue;

            if (AssetImporter.GetAtPath(path) is ModelImporter importer &&
                !Mathf.Approximately(importer.globalScale, RequiredScale))
            {
                importer.globalScale = RequiredScale;
                importer.SaveAndReimport();
                count++;
            }
        }

        Debug.Log($"[BeamModelImportEnforcer] Reimported {count} models at scale {RequiredScale}.");
    }
}
