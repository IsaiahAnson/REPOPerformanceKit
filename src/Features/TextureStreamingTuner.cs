using System;
using BepInEx.Logging;
using REPOPerformance.Config;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace REPOPerformance.Features;

/// <summary>
/// Turns on Unity mipmap streaming with a memory budget, and puts the game's own values back when the
/// setting is turned off. Only textures the game imported with streaming mipmaps take part, so the
/// number of such textures is logged after each scene change.
/// </summary>
internal static class TextureStreamingTuner
{
    private const int MaxLevelReduction = 2;
    private const int MaxFileIoRequests = 256;
    private const float ReportDelaySeconds = 10f;

    private static PerformanceConfig _cfg;
    private static ManualLogSource _log;
    private static Reporter _reporter;

    private static bool _applied;
    private static bool _originalActive;
    private static float _originalBudget;
    private static int _originalLevelReduction;
    private static int _originalFileIoRequests;
    private static bool _originalAddAllCameras;

    public static void Apply(PerformanceConfig cfg, ManualLogSource log)
    {
        _cfg = cfg;
        _log = log;
        SceneWatcher.Start();
        _reporter = RuntimeHost.Add<Reporter>(false);

        Refresh();
        cfg.EnableTextureStreaming.SettingChanged += (_, _) => Refresh();
        cfg.StreamingMipmapsMemoryBudget.SettingChanged += (_, _) => Refresh();
    }

    private static void Refresh()
    {
        try
        {
            if (_cfg.EnableTextureStreaming.Value)
            {
                if (!_applied)
                {
                    _originalActive = QualitySettings.streamingMipmapsActive;
                    _originalBudget = QualitySettings.streamingMipmapsMemoryBudget;
                    _originalLevelReduction = QualitySettings.streamingMipmapsMaxLevelReduction;
                    _originalFileIoRequests = QualitySettings.streamingMipmapsMaxFileIORequests;
                    _originalAddAllCameras = QualitySettings.streamingMipmapsAddAllCameras;
                    _applied = true;
                }

                int budgetMb = Mathf.Clamp(_cfg.StreamingMipmapsMemoryBudget.Value, 128, 8192);
                QualitySettings.streamingMipmapsActive = true;
                QualitySettings.streamingMipmapsMemoryBudget = budgetMb;
                QualitySettings.streamingMipmapsMaxLevelReduction = MaxLevelReduction;
                QualitySettings.streamingMipmapsMaxFileIORequests = MaxFileIoRequests;
                QualitySettings.streamingMipmapsAddAllCameras = true;
                _log.LogInfo($"Texture streaming enabled (budget={budgetMb}MB).");
            }
            else if (_applied)
            {
                QualitySettings.streamingMipmapsActive = _originalActive;
                QualitySettings.streamingMipmapsMemoryBudget = _originalBudget;
                QualitySettings.streamingMipmapsMaxLevelReduction = _originalLevelReduction;
                QualitySettings.streamingMipmapsMaxFileIORequests = _originalFileIoRequests;
                QualitySettings.streamingMipmapsAddAllCameras = _originalAddAllCameras;
                _applied = false;
                _log.LogInfo("Texture streaming turned off; the game's own streaming settings were restored.");
            }

            if (_reporter != null)
            {
                _reporter.enabled = _applied;
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning("Could not change texture streaming: " + ex.Message);
        }
    }

    /// <summary>
    /// Logs the streaming texture counts once, a few seconds after each scene change and after the
    /// setting is turned on.
    /// </summary>
    private sealed class Reporter : MonoBehaviour
    {
        private float _reportAt = -1f;

        private void OnEnable()
        {
            SceneWatcher.Settled += OnSceneSettled;
            _reportAt = Time.unscaledTime + ReportDelaySeconds;
        }

        private void OnDisable()
        {
            SceneWatcher.Settled -= OnSceneSettled;
            _reportAt = -1f;
        }

        private void OnSceneSettled(string sceneName)
        {
            _reportAt = Time.unscaledTime + ReportDelaySeconds;
        }

        private void Update()
        {
            if (_reportAt < 0f || Time.unscaledTime < _reportAt)
            {
                return;
            }
            _reportAt = -1f;

            string sceneName = SceneManager.GetActiveScene().name;
            ulong streaming = Texture.streamingTextureCount;
            ulong fixedSize = Texture.nonStreamingTextureCount;
            ulong loadedMb = Texture.currentTextureMemory / (1024UL * 1024UL);
            string note = streaming == 0
                ? " No texture here was imported with streaming mipmaps, so this setting changes nothing in this scene."
                : string.Empty;
            _log.LogInfo($"Texture streaming in scene '{sceneName}': {streaming} streaming and {fixedSize} non-streaming " +
                         $"textures, {loadedMb} MB of textures loaded.{note}");
        }
    }
}
