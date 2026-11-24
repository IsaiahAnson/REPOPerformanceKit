using System;
using BepInEx.Logging;
using REPOPerformance.Config;
using UnityEngine.Scripting;

namespace REPOPerformance.Features;

/// <summary>
/// Runs a full collection after each scene change (menu, lobby or level), where the loading screen
/// hides the pause. Incremental GC is a build setting of the game that a plugin cannot switch, so it
/// is only reported.
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

        try
        {
            ReportIncrementalGc(log);
        }
        catch (Exception ex)
        {
            log.LogDebug("Could not read the incremental GC state: " + ex.Message);
        }

        if (!_hooked)
        {
            SceneWatcher.Settled += OnSceneSettled;
            SceneWatcher.Start();
            _hooked = true;
        }

        if (cfg.CollectGCOnSceneLoad.Value)
        {
            log.LogInfo("GC.Collect will run after each scene change.");
        }
    }

    // Kept in its own method so a Unity build without this API fails here, inside the caller's try.
    private static void ReportIncrementalGc(ManualLogSource log)
    {
        string state = GarbageCollector.isIncremental
            ? $"on (time slice {GarbageCollector.incrementalTimeSliceNanoseconds / 1000000.0:F1} ms)"
            : "off";
        log.LogInfo($"Incremental GC is {state} in this game build. This plugin does not change it.");
    }

    private static void OnSceneSettled(string sceneName)
    {
        // Read the setting here, so a change made in the config menu applies from the next scene change.
        if (!_cfg.CollectGCOnSceneLoad.Value)
        {
            return;
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        _log.LogInfo("GC pass after the scene change to '" + sceneName + "'.");
    }
}
