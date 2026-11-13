using BepInEx.Logging;
using REPOPerformance.Config;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace REPOPerformance.Features;

/// <summary>
/// Gives the main camera a cull distance for every layer above 7 (detail layers), derived from its
/// far clip plane. Reapplied on each scene load, because each scene brings its own camera.
/// </summary>
internal static class CullDistanceBoost
{
    private static PerformanceConfig _cfg;
    private static ManualLogSource _log;
    private static bool _hooked;

    public static void Apply(PerformanceConfig cfg, ManualLogSource log)
    {
        if (!cfg.EnableCullDistanceBoost.Value)
        {
            return;
        }

        _cfg = cfg;
        _log = log;
        TryApply(SceneManager.GetActiveScene(), LoadSceneMode.Single);

        if (!_hooked)
        {
            SceneManager.sceneLoaded += TryApply;
            _hooked = true;
        }
    }

    private static void TryApply(Scene scene, LoadSceneMode mode)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        float far = camera.farClipPlane;
        float detail = far * Mathf.Clamp(_cfg.CullDistanceMultiplier.Value, 0.25f, 1f);

        // Unity has 32 layers. 0 means "use the far clip plane", kept for the built-in layers 0-7.
        var distances = new float[32];
        for (int layer = 0; layer < 32; layer++)
        {
            distances[layer] = layer >= 8 ? detail : 0f;
        }

        camera.layerCullDistances = distances;
        camera.layerCullSpherical = true;
        _log.LogInfo("Cull distance boost applied to '" + camera.name + "' in scene '" + scene.name + "' " +
                     $"(far={far:F0}, detail={detail:F0}).");
    }
}
