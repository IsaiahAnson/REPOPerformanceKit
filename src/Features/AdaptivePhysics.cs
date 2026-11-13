using BepInEx.Logging;
using REPOPerformance.Config;
using UnityEngine;

namespace REPOPerformance.Features;

/// <summary>
/// Lowers the physics tick rate while the smoothed frame rate is low and restores it once the
/// frame rate recovers. The gap between the two thresholds stops it from flapping.
/// </summary>
internal static class AdaptivePhysics
{
    private static GameObject _host;

    public static void Apply(PerformanceConfig cfg, ManualLogSource log)
    {
        if (!cfg.EnableAdaptivePhysics.Value)
        {
            return;
        }

        Time.fixedDeltaTime = cfg.BaselineFixedDeltaTime.Value;

        _host = new GameObject("REPOPerformance_AdaptivePhysics");
        Object.DontDestroyOnLoad(_host);
        _host.hideFlags = HideFlags.HideAndDontSave;
        _host.AddComponent<AdaptivePhysicsRunner>().Configure(cfg, log);
        log.LogInfo("Adaptive physics runner started.");
    }

    private sealed class AdaptivePhysicsRunner : MonoBehaviour
    {
        private PerformanceConfig _cfg;
        private ManualLogSource _log;
        private float _smoothedFps = 60f;
        private float _sampleTimer;
        private bool _degraded;

        public void Configure(PerformanceConfig cfg, ManualLogSource log)
        {
            _cfg = cfg;
            _log = log;
        }

        private void Update()
        {
            if (_cfg == null)
            {
                return;
            }

            float fps = 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            _smoothedFps = Mathf.Lerp(_smoothedFps, fps, 0.05f);

            // Decide once a second, on the smoothed value.
            _sampleTimer += Time.unscaledDeltaTime;
            if (_sampleTimer < 1f)
            {
                return;
            }
            _sampleTimer = 0f;

            if (!_degraded && _smoothedFps < _cfg.DegradeBelowFps.Value)
            {
                Time.fixedDeltaTime = _cfg.DegradedFixedDeltaTime.Value;
                _degraded = true;
                _log.LogInfo($"Adaptive physics: degraded -> fixedDeltaTime={Time.fixedDeltaTime:F4} " +
                             $"(fps {_smoothedFps:F1}).");
            }
            else if (_degraded && _smoothedFps > _cfg.RecoverAboveFps.Value)
            {
                Time.fixedDeltaTime = _cfg.BaselineFixedDeltaTime.Value;
                _degraded = false;
                _log.LogInfo($"Adaptive physics: recovered -> fixedDeltaTime={Time.fixedDeltaTime:F4} " +
                             $"(fps {_smoothedFps:F1}).");
            }
        }
    }
}
