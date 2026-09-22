using UnityEngine;

/// <summary>
/// Runtime wiring for configuration codes in the live app: puts the restorer
/// and the Load-code dialog UI on the "ConfigurationTools" host. Same
/// injection pattern as the other bootstraps, so existing scenes get the
/// features without a rebuild.
/// </summary>
public static class ConfigurationCodeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBootstrap()
    {
        var build = Object.FindFirstObjectByType<BuildController>();
        if (build == null)
            return;

        ConfigurationRestorer restorer = ConfigurationCode.GetOrCreateRestorer(build);

        var ui = restorer.GetComponent<ConfigurationCodeUI>();
        if (ui == null)
            ui = restorer.gameObject.AddComponent<ConfigurationCodeUI>();
        ui.buildController = build;

        // The block library used to be a second component here, driving a
        // panel that flew out of the rail. Blocks live in the dock's Blocks
        // tab now, and two doors to one library is one too many: the panel
        // and the gallery each had their own idea of renaming, deleting and
        // which collection a block was in.
    }
}
