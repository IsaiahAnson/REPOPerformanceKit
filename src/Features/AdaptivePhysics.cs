using BepInEx.Logging;
using REPOPerformance.Config;
using UnityEngine;

namespace REPOPerformance.Features;

/// <summary>
/// Optional and off by default. While the smoothed frame rate is low it raises Time.fixedDeltaTime
/// (fewer physics steps per second), and it puts the game's own value back when the frame rate recovers
/// or the setting is turned off. It never writes Time.fixedDeltaTime at any other time.
/// </summary>
internal static class AdaptivePhysics
{
    private const float MinimumGapFps = 5f;
    private const float Tolerance = 0.00005f;
    private const float HitchSeconds = 0.5f;

    private static PerformanceConfig _cfg;
    private static ManualLogSource _log;
    private static Runner _runner;

    public static void Apply(PerformanceConfig cfg, ManualLogSource log)
    {
        _cfg = cfg;
        _log = log;
        log.LogInfo($"Time.fixedDeltaTime is {Time.fixedDeltaTime:F4}s ({1f / Time.fixedDeltaTime:F0} Hz) in this game build.");

        _runner = RuntimeHost.Add<Runner>(cfg.EnableAdaptivePhysics.Value);
        if (cfg.EnableAdaptivePhysics.Value)
        {
            log.LogInfo("Adaptive physics is on.");
        }

        cfg.EnableAdaptivePhysics.SettingChanged += (_, _) =>
        {
            if (_runner != null)
            {
                _runner.enabled = _cfg.EnableAdaptivePhysics.Value;
                _log.LogInfo(_runner.enabled ? "Adaptive physics is on." : "Adaptive physics is off.");
            }
        };
    }

    private sealed class Runner : MonoBehaviour
    {
        private float _smoothedFps;
        private float _sampleTimer;
        private bool _degraded;
        private bool _warnedNoRoom;
        private float _original;
        private float _applied;

        private void OnEnable()
        {
            _smoothedFps = 0f;
            _sampleTimer = 0f;
            _warnedNoRoom = false;
        }

        private void OnDisable()
        {
            Restore("turned off");
        }

        private void Update()
        {
            // A frame this long is a loading hitch, not a frame rate. Counting it would lower the tick
            // rate for a moment after every load, and at startup the very first frame is one.
            float deltaTime = Time.unscaledDeltaTime;
            if (deltaTime <= 0f || deltaTime > HitchSeconds)
            {
                return;
            }

            float fps = 1f / deltaTime;
            _smoothedFps = _smoothedFps <= 0f ? fps : Mathf.Lerp(_smoothedFps, fps, 0.05f);

            // Decide once a second, on the smoothed value.
            _sampleTimer += deltaTime;
            if (_sampleTimer < 1f)
            {
                return;
            }
            _sampleTimer = 0f;

            float degradeBelow = _cfg.DegradeBelowFps.Value;
            // The gap between the two thresholds is what stops it from flapping, so keep one.
            float recoverAbove = Mathf.Max(_cfg.RecoverAboveFps.Value, degradeBelow + MinimumGapFps);

            if (_degraded)
            {
                WhileDegraded(recoverAbove);
            }
            else if (_smoothedFps < degradeBelow)
            {
                Degrade();
            }
        }

        private void Degrade()
        {
            float current = Time.fixedDeltaTime;
            float wanted = _cfg.DegradedFixedDeltaTime.Value;
            if (wanted <= current + Tolerance)
            {
                if (!_warnedNoRoom)
                {
                    _warnedNoRoom = true;
                    _log.LogInfo($"Adaptive physics: DegradedFixedDeltaTime ({wanted:F4}) is not above the game's " +
                                 $"fixedDeltaTime ({current:F4}), so there is nothing to lower.");
                }
                return;
            }

            _original = current;
            Time.fixedDeltaTime = wanted;
            _applied = Time.fixedDeltaTime;
            _degraded = true;
            _log.LogInfo($"Adaptive physics: degraded -> fixedDeltaTime={_applied:F4} (fps {_smoothedFps:F1}).");
        }

        private void WhileDegraded(float recoverAbove)
        {
            if (Mathf.Abs(Time.fixedDeltaTime - _applied) > Tolerance)
            {
                // The game or another mod set its own value. That value wins; forget ours.
                _degraded = false;
                _log.LogInfo($"Adaptive physics: fixedDeltaTime was changed elsewhere to {Time.fixedDeltaTime:F4}, leaving it alone.");
                return;
            }

            if (_smoothedFps > recoverAbove)
            {
                Restore("recovered");
                return;
            }

            // The degraded value was edited in the config menu while it is in use.
            float wanted = _cfg.DegradedFixedDeltaTime.Value;
            if (Mathf.Abs(wanted - _applied) > Tolerance)
            {
                if (wanted > _original + Tolerance)
                {
                    Time.fixedDeltaTime = wanted;
                    _applied = Time.fixedDeltaTime;
                    _log.LogInfo($"Adaptive physics: degraded -> fixedDeltaTime={_applied:F4} (fps {_smoothedFps:F1}).");
                }
                else
                {
                    Restore("recovered");
                }
            }
        }

        private void Restore(string reason)
        {
            if (!_degraded)
            {
                return;
            }
            _degraded = false;

            if (Mathf.Abs(Time.fixedDeltaTime - _applied) > Tolerance)
            {
                _log.LogInfo($"Adaptive physics: fixedDeltaTime was changed elsewhere to {Time.fixedDeltaTime:F4}, leaving it alone.");
                return;
            }

            Time.fixedDeltaTime = _original;
            _log.LogInfo($"Adaptive physics: {reason} -> fixedDeltaTime={Time.fixedDeltaTime:F4} (fps {_smoothedFps:F1}).");
        }
    }
}
