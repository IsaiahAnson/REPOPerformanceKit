using System;
using BepInEx.Logging;
using REPOPerformance.Config;
using UnityEngine;

namespace REPOPerformance.Features;

/// <summary>
/// Turns on mipmap streaming with a VRAM budget, so only the texture detail the camera can see is loaded.
/// </summary>
internal static class TextureStreamingTuner
{
    public static void Apply(PerformanceConfig cfg, ManualLogSource log)
    {
        if (!cfg.EnableTextureStreaming.Value)
        {
            return;
        }

        try
        {
            QualitySettings.streamingMipmapsActive = true;
            int budgetMb = Mathf.Clamp(cfg.StreamingMipmapsMemoryBudget.Value, 128, 8192);
            QualitySettings.streamingMipmapsMemoryBudget = budgetMb;
            QualitySettings.streamingMipmapsMaxLevelReduction = 2;
            QualitySettings.streamingMipmapsMaxFileIORequests = 256;
            QualitySettings.streamingMipmapsAddAllCameras = true;
            log.LogInfo($"Texture streaming enabled (budget={budgetMb}MB).");
        }
        catch (Exception ex)
        {
            log.LogWarning("Could not enable texture streaming: " + ex.Message);
        }
    }
}
