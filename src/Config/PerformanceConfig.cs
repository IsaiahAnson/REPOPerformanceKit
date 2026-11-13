using BepInEx.Configuration;

namespace REPOPerformance.Config;

/// <summary>
/// Every setting the plugin reads, bound to BepInEx/config/com.mentalize.repoperformance.cfg.
/// </summary>
internal sealed class PerformanceConfig
{
    // Logging
    public readonly ConfigEntry<bool> DisableUnityStackTraces;
    public readonly ConfigEntry<bool> SuppressPluginLogSpam;
    public readonly ConfigEntry<string> SuppressedSubstrings;
    public readonly ConfigEntry<bool> DedupeRepeatedLogs;

    // FrameRate
    public readonly ConfigEntry<bool> OverrideTargetFrameRate;
    public readonly ConfigEntry<int> TargetFrameRate;
    public readonly ConfigEntry<int> VSyncCount;
    public readonly ConfigEntry<float> MaximumDeltaTime;

    // AdaptivePhysics
    public readonly ConfigEntry<bool> EnableAdaptivePhysics;
    public readonly ConfigEntry<float> BaselineFixedDeltaTime;
    public readonly ConfigEntry<float> DegradedFixedDeltaTime;
    public readonly ConfigEntry<float> DegradeBelowFps;
    public readonly ConfigEntry<float> RecoverAboveFps;

    // Culling
    public readonly ConfigEntry<bool> EnableCullDistanceBoost;
    public readonly ConfigEntry<float> CullDistanceMultiplier;

    // GC
    public readonly ConfigEntry<bool> EnableIncrementalGC;
    public readonly ConfigEntry<bool> CollectGCOnSceneLoad;

    // TextureStreaming
    public readonly ConfigEntry<bool> EnableTextureStreaming;
    public readonly ConfigEntry<int> StreamingMipmapsMemoryBudget;

    // Diagnostics
    public readonly ConfigEntry<bool> ShowFpsOverlay;

    public PerformanceConfig(ConfigFile file)
    {
        DisableUnityStackTraces = file.Bind("Logging", "DisableUnityStackTraces", true,
            "Disables Unity's internal stack-trace capture for Debug.Log/Warning. Stack-trace capture is one of the biggest GC sources in Unity games. Errors still keep stack traces.");
        SuppressPluginLogSpam = file.Bind("Logging", "SuppressPluginLogSpam", true,
            "Filter the BepInEx log-listener chain so repetitive plugin log lines are dropped.");
        SuppressedSubstrings = file.Bind("Logging", "SuppressedSubstrings", "Patched method,Applying patch,Harmony.PatchAll",
            "Comma-separated list of case-insensitive substrings. Any log line containing one of these is dropped. Leave empty to disable substring filtering.");
        DedupeRepeatedLogs = file.Bind("Logging", "DedupeRepeatedLogs", true,
            "Collapse identical consecutive log lines (prints '(previous message x N)' instead).");

        OverrideTargetFrameRate = file.Bind("FrameRate", "OverrideTargetFrameRate", false,
            "Force Application.targetFrameRate. Leave OFF unless you know your display refresh rate.");
        TargetFrameRate = file.Bind("FrameRate", "TargetFrameRate", 144,
            "Target frame rate when OverrideTargetFrameRate is true. -1 = uncapped.");
        VSyncCount = file.Bind("FrameRate", "VSyncCount", -1,
            "QualitySettings.vSyncCount. 0 = off, 1 = every refresh, 2 = every second refresh. -1 = do not change.");
        MaximumDeltaTime = file.Bind("FrameRate", "MaximumDeltaTime", 0.1f,
            "Time.maximumDeltaTime. Caps how much physics catch-up the game tries after a hitch. Prevents 'physics death spiral' where a stutter triggers more stutters.");

        EnableAdaptivePhysics = file.Bind("AdaptivePhysics", "Enable", true,
            "Dynamically lower physics tick rate when FPS drops, restoring it when performance recovers.");
        BaselineFixedDeltaTime = file.Bind("AdaptivePhysics", "BaselineFixedDeltaTime", 0.02f,
            "Normal Time.fixedDeltaTime (0.02 = 50Hz, Unity default).");
        DegradedFixedDeltaTime = file.Bind("AdaptivePhysics", "DegradedFixedDeltaTime", 0.0333f,
            "Fallback fixedDeltaTime under load (0.0333 = 30Hz).");
        DegradeBelowFps = file.Bind("AdaptivePhysics", "DegradeBelowFps", 40f,
            "Drop to degraded fixedDeltaTime if smoothed FPS falls below this.");
        RecoverAboveFps = file.Bind("AdaptivePhysics", "RecoverAboveFps", 55f,
            "Return to baseline fixedDeltaTime if smoothed FPS rises above this. Must be above DegradeBelowFps to create a hysteresis band.");

        EnableCullDistanceBoost = file.Bind("Culling", "Enable", true,
            "Apply per-layer cull distances to the main camera so tiny/detail objects stop rendering at their multiplied distance. Invisible at conservative multipliers.");
        CullDistanceMultiplier = file.Bind("Culling", "Multiplier", 1f,
            "Multiplier applied to the main camera's far clip plane to derive a detail-cull distance. 1.0 = unchanged, 0.75 = more aggressive. Only affects layers > 7.");

        EnableIncrementalGC = file.Bind("GC", "EnableIncrementalGC", true,
            "Spread GC work across frames instead of one big stall (Unity incremental GC).");
        CollectGCOnSceneLoad = file.Bind("GC", "CollectGCOnSceneLoad", true,
            "Run a full GC.Collect during scene loads (hidden by the loading screen).");

        EnableTextureStreaming = file.Bind("TextureStreaming", "Enable", true,
            "Enable Unity mipmap streaming. Loads only the mip levels visible at the current camera distance, keeping VRAM bounded regardless of how many content mods are installed. Invisible at adequate memory budgets.");
        StreamingMipmapsMemoryBudget = file.Bind("TextureStreaming", "MemoryBudgetMB", 1024,
            "VRAM budget for streamed textures in MB. Unity default is 512. Raise to 1024-2048 on GPUs with >= 6GB VRAM for better LOD retention. Clamped to [128, 8192].");

        ShowFpsOverlay = file.Bind("Diagnostics", "ShowFpsOverlay", false,
            "Draw a small FPS / frametime counter in the top-left corner.");
    }
}
