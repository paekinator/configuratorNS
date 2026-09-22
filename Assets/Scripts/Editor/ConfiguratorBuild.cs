using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Repeatable builds of the configurator entry scene, independent of the editor's scene list.</summary>
public static class ConfiguratorBuild
{
    const string EntryScene = "Assets/Scenes/ConfiguratorScene.unity";
    const string WebTemplate = "PROJECT:Configurator";

    [MenuItem("Tools/Configurator/Build/Windows x64")]
    public static void BuildWindows()
    {
        Build(BuildTarget.StandaloneWindows64, "Windows", "configurator.exe");
    }

    [MenuItem("Tools/Configurator/Build/Web")]
    public static void BuildWebGL()
    {
        Build(BuildTarget.WebGL, "WebGL", null);
    }

    static void Build(BuildTarget target, string directoryName, string executableName)
    {
        BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
        if (!BuildPipeline.IsBuildTargetSupported(group, target))
            throw new BuildFailedException($"{target} support is missing. Install its build module for Unity {Application.unityVersion} in Unity Hub.");
        if (!File.Exists(EntryScene))
            throw new BuildFailedException($"Configurator entry scene is missing: {EntryScene}");

        string projectDirectory = Directory.GetParent(Application.dataPath).FullName;
        string buildsDirectory = Path.GetFullPath(Path.Combine(projectDirectory, "Builds"));
        string requestedDirectory = Argument("-configuratorOutput") ?? Path.Combine(buildsDirectory, directoryName);
        string outputDirectory = Path.GetFullPath(Path.IsPathRooted(requestedDirectory)
            ? requestedDirectory : Path.Combine(projectDirectory, requestedDirectory));
        if (!outputDirectory.StartsWith(buildsDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new BuildFailedException("Build output must be a subdirectory of this project's Builds directory.");

        bool development = Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "-configuratorDevelopment");
        string location = executableName == null ? outputDirectory : Path.Combine(outputDirectory, executableName);
        string previousTemplate = PlayerSettings.WebGL.template;
        // Release builds carry their git tag as the product version
        // (Application.version), so a running player can always be traced
        // back to the exact commit it came from. Restored afterwards: the
        // setting belongs to the tag, not to the project file.
        string version = Argument("-configuratorVersion");
        string previousVersion = PlayerSettings.bundleVersion;
        BuildReport report;
        try
        {
            if (!string.IsNullOrEmpty(version))
                PlayerSettings.bundleVersion = version;
            if (target == BuildTarget.WebGL)
                PlayerSettings.WebGL.template = WebTemplate;
            report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { EntryScene },
                locationPathName = location,
                target = target,
                options = development ? BuildOptions.Development : BuildOptions.None
            });
        }
        finally
        {
            if (target == BuildTarget.WebGL)
                PlayerSettings.WebGL.template = previousTemplate;
            PlayerSettings.bundleVersion = previousVersion;
        }

        if (report == null || report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException($"{target} build failed. Check the Unity build log for the first error.");

        string entryPoint = executableName == null ? Path.Combine(outputDirectory, "index.html") : location;
        if (!File.Exists(entryPoint))
            throw new BuildFailedException($"Build reported success but its entry point is missing: {entryPoint}");

        File.WriteAllText(Path.Combine(outputDirectory, "build-report.json"), JsonUtility.ToJson(new BuildMetadata
        {
            unityVersion = Application.unityVersion,
            productVersion = string.IsNullOrEmpty(version) ? PlayerSettings.bundleVersion : version,
            target = target.ToString(),
            entryScene = EntryScene,
            development = development,
            builtAtUtc = DateTime.UtcNow.ToString("O"),
            totalBytes = report.summary.totalSize.ToString(),
            warnings = report.summary.totalWarnings,
            errors = report.summary.totalErrors
        }, true));
        Debug.Log($"Configurator build succeeded: {entryPoint}");
    }

    static string Argument(string key)
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, key);
        if (index < 0) return null;
        if (index + 1 >= args.Length || args[index + 1].StartsWith("-", StringComparison.Ordinal))
            throw new BuildFailedException($"{key} requires a path.");
        return args[index + 1];
    }

    [Serializable]
    class BuildMetadata
    {
        public string unityVersion;
        public string productVersion;
        public string target;
        public string entryScene;
        public bool development;
        public string builtAtUtc;
        public string totalBytes;
        public int warnings;
        public int errors;
    }
}
