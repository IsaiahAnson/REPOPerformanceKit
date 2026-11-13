using System;
using System.Reflection;
using BepInEx.Logging;
using REPOPerformance.Config;
using UnityEngine.SceneManagement;

namespace REPOPerformance.Features;

/// <summary>
/// Turns on Unity's incremental GC, and runs a full collection while a new scene loads,
/// where the loading screen hides the pause.
/// </summary>
internal static class GCTuner
{
    private static PerformanceConfig _cfg;
    private static ManualLogSource _log;
    private static bool _hooked;

    public static void Apply(PerformanceConfig cfg, ManualLogSource log)
    {
        _cfg = cfg;
        _log = log;

        if (cfg.EnableIncrementalGC.Value)
        {
            try
            {
                // Looked up by name so the plugin still loads on Unity builds without the API.
                Type collector = Type.GetType("UnityEngine.Scripting.GarbageCollector, UnityEngine.CoreModule");
                if (collector != null)
                {
                    PropertyInfo gcMode = collector.GetProperty("GCMode", BindingFlags.Static | BindingFlags.Public);
                    if (gcMode != null && gcMode.PropertyType.IsEnum)
                    {
                        object enabled = Enum.Parse(gcMode.PropertyType, "Enabled");
                        gcMode.SetValue(null, enabled, null);
                        log.LogInfo("Unity incremental GC enabled.");
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogWarning("Could not enable incremental GC: " + ex.Message);
            }
        }

        if (cfg.CollectGCOnSceneLoad.Value && !_hooked)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            _hooked = true;
            log.LogInfo("GC.Collect will run on scene load.");
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
        {
            return;
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        _log.LogInfo("GC pass after loading scene '" + scene.name + "'.");
    }
}
