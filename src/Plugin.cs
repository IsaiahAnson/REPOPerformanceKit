using BepInEx;
using BepInEx.Logging;
using REPOPerformance.Config;
using REPOPerformance.Features;

namespace REPOPerformance;

/// <summary>
/// Entry point. Reads the config once and applies each Unity setting it turns on.
/// Nothing here patches R.E.P.O.'s own code.
/// </summary>
[BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;
    internal static PerformanceConfig PerfConfig;

    private void Awake()
    {
        Log = Logger;
        PerfConfig = new PerformanceConfig(Config);

        Log.LogInfo("REPOPerformance 0.1.0 loading…");

        LogSuppressor.Apply(PerfConfig, Log);
        FrameRateTuner.Apply(PerfConfig, Log);
        GCTuner.Apply(PerfConfig, Log);
        TextureStreamingTuner.Apply(PerfConfig, Log);
        AdaptivePhysics.Apply(PerfConfig, Log);
        CullDistanceBoost.Apply(PerfConfig, Log);
        FPSCounter.Apply(PerfConfig, Log);

        Log.LogInfo("REPOPerformance ready.");
    }
}
