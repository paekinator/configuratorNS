using UnityEngine;

/// <summary>
/// Runtime wiring for the adaptive build environment: the grid patch that
/// grows with the structure and the CAD-style dimension annotations.
/// Injected on scene load so existing scenes pick it up without a rebuild.
/// </summary>
public static class EnvironmentBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBootstrap()
    {
        var build = Object.FindFirstObjectByType<BuildController>();
        if (build == null)
            return;

        GameObject host = GameObject.Find("BuildEnvironment");
        if (host == null)
            host = new GameObject("BuildEnvironment");

        // ONE grid component now. AdaptiveGridController drew the 88 mm lines
        // on a second quad that followed the build while this one followed the
        // camera; both drew the same lattice from different sources, and the
        // H7 lines were every eighth line of each. GroundGridController draws
        // every layer in one pass, so they cannot land a fraction of a pixel
        // apart.
        var grid = host.GetComponent<GroundGridController>()
                   ?? host.AddComponent<GroundGridController>();
        grid.buildController = build;

        var dims = host.GetComponent<StructureDimensionsController>() ?? host.AddComponent<StructureDimensionsController>();
        dims.buildController = build;

        // The default camera pose is baked into the scene by the editor
        // builder (facing the grid start) — no runtime repositioning here:
        // the camera controllers cache their look state on load, and moving
        // the camera under them breaks mouse look.
    }
}
