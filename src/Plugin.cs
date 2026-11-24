using System;
using BepInEx;
using BepInEx.Logging;
using REPOPerformance.Config;
using REPOPerformance.Features;
using UnityEngine;

namespace REPOPerformance;

/// <summary>
/// Entry point. Binds the config, then starts each feature on its own, so one feature that fails
/// cannot stop the others. Nothing here patches R.E.P.O.'s own code.
/// </summary>
[BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;
    internal static PerformanceConfig PerfConfig;

    private void Awake()
    {
        Log = Logger;
        Log.LogInfo($"REPOPerformance {PluginInfo.Version} loading (Unity {Application.unityVersion}).");

        try
        {
            PerfConfig = new PerformanceConfig(Config, Log);
        }
        catch (Exception ex)
        {
            Log.LogError("Could not set up the config, so no feature was started: " + ex);
            return;
        }

        int failed = 0;
        failed += Start("log filtering", LogSuppressor.Apply);
        failed += Start("maximum delta time", FrameRateTuner.Apply);
        failed += Start("GC", GCTuner.Apply);
        failed += Start("texture streaming", TextureStreamingTuner.Apply);
        failed += Start("adaptive physics", AdaptivePhysics.Apply);
        failed += Start("cull distance", CullDistanceBoost.Apply);
        failed += Start("FPS overlay", FPSCounter.Apply);

        if (failed == 0)
        {
            Log.LogInfo("REPOPerformance ready.");
        }
        else
        {
            Log.LogWarning($"REPOPerformance ready, but {failed} of 7 features failed to start (see the errors above).");
        }
    }

    /// <summary>Returns 1 if the feature threw while starting, otherwise 0.</summary>
    private static int Start(string feature, Action<PerformanceConfig, ManualLogSource> apply)
    {
        try
        {
            apply(PerfConfig, Log);
            return 0;
        }
        catch (Exception ex)
        {
            Log.LogError($"The {feature} feature failed to start and was skipped: {ex}");
            return 1;
        }
    }
}
