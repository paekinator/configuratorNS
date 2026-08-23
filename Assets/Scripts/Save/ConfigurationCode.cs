using UnityEngine;

/// <summary>
/// The small public API for configuration codes:
///
///   string code = ConfigurationCode.Encode(buildController);   // scene → code
///   var model   = ConfigurationCode.Decode(code);              // throws ConfigurationCodeException
///   var check   = ConfigurationCode.Validate(code);            // never throws
///   ConfigurationCode.Load(code, buildController);             // code → scene (play mode)
///
/// See CONFIG_CODE_SCHEMA.md for the format.
/// </summary>
public static class ConfigurationCode
{
    /// <summary>Capture the current build and encode it. Same build → same code, always.</summary>
    public static string Encode(BuildController build) =>
        ConfigurationCodec.Encode(ConfigurationCapture.Capture(build));

    public static string Encode(ConfigurationModel model) =>
        ConfigurationCodec.Encode(model);

    /// <summary>Parse a code. Throws <see cref="ConfigurationCodeException"/> with a clear message.</summary>
    public static ConfigurationModel Decode(string code) =>
        ConfigurationCodec.Decode(code);

    /// <summary>Parse without throwing; carries the error text and retirement warnings.</summary>
    public static ConfigurationCodeValidation Validate(string code) =>
        ConfigurationCodec.Validate(code);

    /// <summary>
    /// Decode and rebuild the scene from a code (play mode). Throws
    /// <see cref="ConfigurationCodeException"/> for bad codes before touching
    /// the scene.
    /// </summary>
    public static void Load(string code, BuildController build,
        System.Action<ConfigurationRestorer.Report> onDone = null)
    {
        ConfigurationModel model = Decode(code);   // fail fast, scene untouched
        GetOrCreateRestorer(build).Restore(model, onDone);
    }

    public static ConfigurationRestorer GetOrCreateRestorer(BuildController build)
    {
        GameObject host = GameObject.Find("ConfigurationTools");
        if (host == null)
            host = new GameObject("ConfigurationTools");

        var restorer = host.GetComponent<ConfigurationRestorer>();
        if (restorer == null)
            restorer = host.AddComponent<ConfigurationRestorer>();

        restorer.buildController = build;
        restorer.panelSlotManager = build != null ? build.panelSlotManager : null;
        return restorer;
    }
}
