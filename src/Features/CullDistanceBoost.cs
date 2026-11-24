using System;
using System.Collections.Generic;
using BepInEx.Logging;
using REPOPerformance.Config;
using UnityEngine;

namespace REPOPerformance.Features;

/// <summary>
/// Optional and off by default. Gives the main camera a shorter cull distance (far clip plane x
/// Multiplier) on the named layers above 7 that are not gameplay layers. It follows the camera and its
/// far clip plane, which the game changes while playing, and puts the camera's own distances back when
/// the feature is turned off or Multiplier is 1.0.
/// </summary>
internal static class CullDistanceBoost
{
    private const int LayerCount = 32;
    private const int FirstUserLayer = 8;
    private const float CheckIntervalSeconds = 0.5f;
    private const float InertMultiplier = 0.999f;

    /// <summary>Layers whose name contains one of these hold players, enemies, valuables or carts.</summary>
    private static readonly string[] GameplayLayerWords = { "Player", "Enemy", "PhysGrab", "StaticGrab", "Cart" };

    private static PerformanceConfig _cfg;
    private static ManualLogSource _log;
    private static Runner _runner;

    public static void Apply(PerformanceConfig cfg, ManualLogSource log)
    {
        _cfg = cfg;
        _log = log;

        _runner = RuntimeHost.Add<Runner>(cfg.EnableCullDistanceBoost.Value);
        if (cfg.EnableCullDistanceBoost.Value)
        {
            log.LogInfo(cfg.CullDistanceMultiplier.Value < InertMultiplier
                ? $"Cull distance override is on (multiplier {cfg.CullDistanceMultiplier.Value:F2})."
                : "Cull distance override is on, but Multiplier is 1.0, so it changes nothing.");
        }

        cfg.EnableCullDistanceBoost.SettingChanged += (_, _) =>
        {
            if (_runner != null)
            {
                _runner.enabled = _cfg.EnableCullDistanceBoost.Value;
                _log.LogInfo(_runner.enabled ? "Cull distance override is on." : "Cull distance override is off.");
            }
        };
    }

    private static bool IsGameplayLayer(string layerName)
    {
        foreach (string word in GameplayLayerWords)
        {
            if (layerName.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    private sealed class Runner : MonoBehaviour
    {
        private readonly float[] _distances = new float[LayerCount];
        private bool[] _affected;
        private string _affectedNames;
        private float _timer;

        private Camera _camera;
        private float[] _originalDistances;
        private bool _originalSpherical;
        private float _appliedFar;
        private float _appliedMultiplier;

        private void OnEnable()
        {
            _timer = CheckIntervalSeconds;
        }

        private void OnDisable()
        {
            Restore();
        }

        private void LateUpdate()
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer < CheckIntervalSeconds)
            {
                return;
            }
            _timer = 0f;

            float multiplier = Mathf.Clamp(_cfg.CullDistanceMultiplier.Value, 0.25f, 1f);
            if (multiplier >= InertMultiplier)
            {
                Restore();
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            FindAffectedLayers();
            if (_affectedNames.Length == 0)
            {
                return;
            }

            bool newCamera = camera != _camera;
            if (newCamera)
            {
                // Each level brings its own camera. Give the old one back its distances if it still exists.
                Restore();
                _camera = camera;
                _originalDistances = camera.layerCullDistances;
                _originalSpherical = camera.layerCullSpherical;
            }

            float far = camera.farClipPlane;
            bool newMultiplier = Mathf.Abs(multiplier - _appliedMultiplier) > 0.001f;
            if (!newCamera && !newMultiplier && Mathf.Abs(far - _appliedFar) < 0.25f)
            {
                return;
            }

            float distance = far * multiplier;
            for (int layer = 0; layer < LayerCount; layer++)
            {
                _distances[layer] = _affected[layer] ? distance : _originalDistances[layer];
            }

            camera.layerCullDistances = _distances;
            camera.layerCullSpherical = true;
            _appliedFar = far;
            _appliedMultiplier = multiplier;

            // The far clip plane moves all the time (fog, death camera), so only log the first apply.
            if (newCamera || newMultiplier)
            {
                _log.LogInfo($"Cull distance applied to '{camera.name}' (far={far:F0}, distance={distance:F0}, " +
                             $"layers: {_affectedNames}).");
            }
        }

        private void FindAffectedLayers()
        {
            if (_affected != null)
            {
                return;
            }

            _affected = new bool[LayerCount];
            var names = new List<string>();
            for (int layer = FirstUserLayer; layer < LayerCount; layer++)
            {
                string layerName = LayerMask.LayerToName(layer);
                if (!string.IsNullOrEmpty(layerName) && !IsGameplayLayer(layerName))
                {
                    _affected[layer] = true;
                    names.Add(layerName);
                }
            }

            _affectedNames = string.Join(", ", names.ToArray());
            if (names.Count == 0)
            {
                _log.LogInfo("Cull distance override: this game build has no non-gameplay layers above 7, so nothing is changed.");
            }
        }

        private void Restore()
        {
            if (_originalDistances == null)
            {
                return;
            }

            // A camera from a level that was unloaded compares equal to null; there is nothing to put back.
            if (_camera != null)
            {
                _camera.layerCullDistances = _originalDistances;
                _camera.layerCullSpherical = _originalSpherical;
                _log.LogInfo($"Cull distance restored on '{_camera.name}'.");
            }

            _camera = null;
            _originalDistances = null;
            _appliedFar = 0f;
            _appliedMultiplier = 0f;
        }
    }
}
