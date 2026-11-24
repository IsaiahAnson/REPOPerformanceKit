using BepInEx.Logging;
using REPOPerformance.Config;
using UnityEngine;

namespace REPOPerformance.Features;

/// <summary>
/// Sets Time.maximumDeltaTime, which caps how much physics catch-up the game attempts after a hitch.
/// The game's own value is remembered and put back when the setting is changed to 0.
/// </summary>
internal static class FrameRateTuner
{
    private static PerformanceConfig _cfg;
    private static ManualLogSource _log;
    private static float _original;
    private static bool _changed;

    public static void Apply(PerformanceConfig cfg, ManualLogSource log)
    {
        _cfg = cfg;
        _log = log;
        _original = Time.maximumDeltaTime;
        log.LogInfo($"Time.maximumDeltaTime is {_original:F3}s in this game build.");

        Refresh();
        cfg.MaximumDeltaTime.SettingChanged += (_, _) => Refresh();
    }

    private static void Refresh()
    {
        float wanted = _cfg.MaximumDeltaTime.Value;
        if (wanted <= 0f)
        {
            if (_changed)
            {
                Time.maximumDeltaTime = _original;
                _changed = false;
                _log.LogInfo($"Time.maximumDeltaTime restored to {Time.maximumDeltaTime:F3}s.");
            }
            return;
        }

        Time.maximumDeltaTime = wanted;
        _changed = true;
        // Read it back: Unity does not let this go below Time.fixedDeltaTime.
        _log.LogInfo($"Time.maximumDeltaTime set to {Time.maximumDeltaTime:F3}s.");
    }
}
