using UnityEngine;

/// <summary>
/// Runtime wiring for configuration codes in the live app: puts the
/// restorer, the Load-code dialog UI and the Pieces library UI on the
/// "ConfigurationTools" host. Same injection pattern as the other
/// bootstraps, so existing scenes get the features without a rebuild.
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

        var pieces = restorer.GetComponent<PieceUI>();
        if (pieces == null)
            pieces = restorer.gameObject.AddComponent<PieceUI>();
        pieces.buildController = build;
    }
}
