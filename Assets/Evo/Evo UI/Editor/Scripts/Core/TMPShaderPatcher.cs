using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Evo.UI
{
    /// <summary>
    /// Automatically finds the TextMeshPro install location and updates the SoftMaskTMP shader.
    /// </summary>
    public class TMPShaderPatcher : AssetPostprocessor
    {
        // Cache the Regex so it doesn't recompile repeatedly
        static readonly Regex PropsRegex = new(@"#include\s+""[^""]*TMPro_Properties\.cginc""", RegexOptions.Compiled);
        static readonly Regex CoreRegex = new(@"#include\s+""[^""]*TMPro\.cginc""", RegexOptions.Compiled);

        /// <summary>
        /// Fires automatically whenever any asset or folder is moved, imported, or deleted.
        /// </summary>
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (movedAssets.Length == 0 && importedAssets.Length == 0)
                return;

            bool relevantChange = false;

            // Avoid AssetDatabase calls inside these loops to prevent stutter during massive imports.
            // When a folder is moved, Unity inherently includes the files inside it in these arrays.
            for (int i = 0; i < movedAssets.Length; i++)
            {
                if (movedAssets[i].EndsWith(".cginc", StringComparison.OrdinalIgnoreCase) ||
                    movedAssets[i].EndsWith(".shader", StringComparison.OrdinalIgnoreCase))
                {
                    relevantChange = true;
                    break;
                }
            }

            if (!relevantChange)
            {
                for (int i = 0; i < importedAssets.Length; i++)
                {
                    if (importedAssets[i].EndsWith(".cginc", StringComparison.OrdinalIgnoreCase) ||
                        importedAssets[i].EndsWith(".shader", StringComparison.OrdinalIgnoreCase))
                    {
                        relevantChange = true;
                        break;
                    }
                }
            }

            if (relevantChange)
            {
                // Delaying the call slightly ensures the AssetDatabase has completely finished 
                // processing the move before we attempt to search it.
                EditorApplication.delayCall -= PatchShaderPaths;
                EditorApplication.delayCall += PatchShaderPaths;
            }
        }

        public static void PatchShaderPaths()
        {
            // Locate the Soft Mask TMP Shader
            string[] shaderGuids = AssetDatabase.FindAssets("SoftMaskTMP t:Shader");
          
            if (shaderGuids.Length == 0)
                return; // Shader not found

            string shaderPath = AssetDatabase.GUIDToAssetPath(shaderGuids[0]);
            string shaderContent = File.ReadAllText(shaderPath);

            // Locate the TMP .cginc files anywhere in the project
            string propsPath = FindFile("TMPro_Properties.cginc", "Assets/TextMesh Pro/Shaders/TMPro_Properties.cginc");
            string corePath = FindFile("TMPro.cginc", "Assets/TextMesh Pro/Shaders/TMPro.cginc");

            if (string.IsNullOrEmpty(propsPath) || string.IsNullOrEmpty(corePath))
                return; // TMP not installed

            // Compute strict relative paths to prevent the pink shader bug
            string relPropsPath = GetRelativePath(shaderPath, propsPath);
            string relCorePath = GetRelativePath(shaderPath, corePath);

            // Regex Replace
            bool changed = false;
            string newPropsLine = $"#include \"{relPropsPath}\"";
            string newCoreLine = $"#include \"{relCorePath}\"";

            string patchedContent = PropsRegex.Replace(shaderContent, newPropsLine);
            if (patchedContent != shaderContent) changed = true;

            string patchedContent2 = CoreRegex.Replace(patchedContent, newCoreLine);
            if (patchedContent2 != patchedContent) changed = true;

            // Save and re-import only if the paths were updated
            if (changed)
            {
                File.WriteAllText(shaderPath, patchedContent2);
                AssetDatabase.ImportAsset(shaderPath);
                // Debug.Log($"[Evo UI] Automatically patched SoftMaskTMP.shader with relative paths after detecting a folder move:" +
                //    $"\n{relPropsPath}\n{relCorePath}");
            }
        }

        static string FindFile(string fileName, string defaultPath)
        {
            // Fast path: Standard Package Manager
            if (File.Exists(Path.GetFullPath(defaultPath)))
                return defaultPath;

            // Slow Path: user manually moved it
            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            string[] guids = AssetDatabase.FindAssets(nameWithoutExt);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (path.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                    return path;
            }

            return null;
        }

        static string GetRelativePath(string fromShaderPath, string toIncludePath)
        {
            string fromDir = Path.GetDirectoryName(Path.GetFullPath(fromShaderPath));
           
            if (!fromDir.EndsWith(Path.DirectorySeparatorChar.ToString()) && !fromDir.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
                fromDir += Path.DirectorySeparatorChar;

            Uri fromUri = new(fromDir);
            Uri toUri = new(Path.GetFullPath(toIncludePath));

            if (fromUri.Scheme != toUri.Scheme)
                return toIncludePath;

            Uri relativeUri = fromUri.MakeRelativeUri(toUri);
            return Uri.UnescapeDataString(relativeUri.ToString()).Replace('\\', '/');
        }
    }
}