using BepInEx.Logging;
using REPOPerformance.Config;
using UnityEngine;

namespace REPOPerformance.Features;

/// <summary>
/// Caps physics catch-up after a hitch, and optionally sets vSync and a target frame rate.
/// </summary>
internal static class FrameRateTuner
{
    public static void Apply(PerformanceConfig cfg, ManualLogSource log)
    {
        if (cfg.MaximumDeltaTime.Value > 0f)
        {
            Time.maximumDeltaTime = cfg.MaximumDeltaTime.Value;
            log.LogInfo($"Time.maximumDeltaTime set to {cfg.MaximumDeltaTime.Value:F3}s.");
        }

        if (cfg.VSyncCount.Value >= 0)
        {
            QualitySettings.vSyncCount = cfg.VSyncCount.Value;
            log.LogInfo($"QualitySettings.vSyncCount set to {cfg.VSyncCount.Value}.");
        }

        if (cfg.OverrideTargetFrameRate.Value)
        {
            Application.targetFrameRate = cfg.TargetFrameRate.Value;
            log.LogInfo($"Application.targetFrameRate set to {cfg.TargetFrameRate.Value}.");
        }
    }
}
