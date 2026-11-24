using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace REPOPerformance.Config;

/// <summary>
/// Every setting the plugin reads, bound to BepInEx/config/com.mentalize.repoperformance.cfg.
/// </summary>
internal sealed class PerformanceConfig
{
    /// <summary>Settings that earlier versions wrote and this version no longer reads.</summary>
    private static readonly ConfigDefinition[] ObsoleteEntries =
    {
        new ConfigDefinition("FrameRate", "OverrideTargetFrameRate"),
        new ConfigDefinition("FrameRate", "TargetFrameRate"),
        new ConfigDefinition("FrameRate", "VSyncCount"),
        new ConfigDefinition("AdaptivePhysics", "Enable"),
        new ConfigDefinition("AdaptivePhysics", "BaselineFixedDeltaTime"),
        new ConfigDefinition("GC", "EnableIncrementalGC"),
    };

    // Logging
    public readonly ConfigEntry<bool> DisableUnityStackTraces;
    public readonly ConfigEntry<bool> SuppressPluginLogSpam;
    public readonly ConfigEntry<string> SuppressedSubstrings;
    public readonly ConfigEntry<bool> DedupeRepeatedLogs;

    // FrameRate
    public readonly ConfigEntry<float> MaximumDeltaTime;

    // AdaptivePhysics
    public readonly ConfigEntry<bool> EnableAdaptivePhysics;
    public readonly ConfigEntry<float> DegradedFixedDeltaTime;
    public readonly ConfigEntry<float> DegradeBelowFps;
    public readonly ConfigEntry<float> RecoverAboveFps;

    // Culling
    public readonly ConfigEntry<bool> EnableCullDistanceBoost;
    public readonly ConfigEntry<float> CullDistanceMultiplier;

    // GC
    public readonly ConfigEntry<bool> CollectGCOnSceneLoad;

    // TextureStreaming
    public readonly ConfigEntry<bool> EnableTextureStreaming;
    public readonly ConfigEntry<int> StreamingMipmapsMemoryBudget;

    // Diagnostics
    public readonly ConfigEntry<bool> ShowFpsOverlay;

    public PerformanceConfig(ConfigFile file, ManualLogSource log)
    {
        // BepInEx rewrites the whole file on every Bind() unless this is off. Bind everything first,
        // then write the file once.
        bool saveOnConfigSet = file.SaveOnConfigSet;
        file.SaveOnConfigSet = false;
        try
        {
            DisableUnityStackTraces = file.Bind("Logging", "DisableUnityStackTraces", true,
                "Turn off Unity's stack-trace capture for Debug.Log and Debug.LogWarning. Errors, exceptions and asserts keep a script stack trace.");
            SuppressPluginLogSpam = file.Bind("Logging", "SuppressPluginLogSpam", true,
                "Drop Info, Debug and Message log lines that contain one of SuppressedSubstrings. Warnings, errors and fatals are never dropped.");
            SuppressedSubstrings = file.Bind("Logging", "SuppressedSubstrings", "Patched method,Applying patch,Harmony.PatchAll",
                "Comma-separated, case-insensitive substrings used by SuppressPluginLogSpam. Leave empty to drop nothing.");
            DedupeRepeatedLogs = file.Bind("Logging", "DedupeRepeatedLogs", true,
                "Collapse identical consecutive Info, Debug and Message lines from the same source into one line plus '(previous message repeated N more times)'. Warnings, errors and fatals are never collapsed.");

            MaximumDeltaTime = file.Bind("FrameRate", "MaximumDeltaTime", 0.1f,
                new ConfigDescription(
                    "Time.maximumDeltaTime in seconds: the most game time one frame may advance, which caps physics catch-up after a hitch. 0 = leave the game's own value.",
                    new AcceptableValueRange<float>(0f, 0.5f)));

            EnableAdaptivePhysics = file.Bind("AdaptivePhysics", "EnableAdaptivePhysics", false,
                "While the smoothed frame rate is low, run physics at a lower tick rate, and put the game's own tick rate back when the frame rate recovers. Changes how physics behaves, and on a host it affects every player. Off by default.");
            DegradedFixedDeltaTime = file.Bind("AdaptivePhysics", "DegradedFixedDeltaTime", 0.0333f,
                new ConfigDescription(
                    "Time.fixedDeltaTime used while the frame rate is low (0.0333 = 30 Hz). Only used when it is larger than the game's own value.",
                    new AcceptableValueRange<float>(0.02f, 0.05f)));
            DegradeBelowFps = file.Bind("AdaptivePhysics", "DegradeBelowFps", 40f,
                new ConfigDescription(
                    "Switch to DegradedFixedDeltaTime when the smoothed FPS falls below this.",
                    new AcceptableValueRange<float>(10f, 120f)));
            RecoverAboveFps = file.Bind("AdaptivePhysics", "RecoverAboveFps", 55f,
                new ConfigDescription(
                    "Put the game's own fixedDeltaTime back when the smoothed FPS rises above this. Treated as at least DegradeBelowFps + 5.",
                    new AcceptableValueRange<float>(15f, 240f)));

            EnableCullDistanceBoost = file.Bind("Culling", "Enable", false,
                "Give the main camera a shorter cull distance on non-gameplay layers above 7 (far clip plane x Multiplier). Does nothing while Multiplier is 1.0. Player, enemy, grabbable-object and cart layers are never touched.");
            CullDistanceMultiplier = file.Bind("Culling", "Multiplier", 1f,
                new ConfigDescription(
                    "Cull distance as a fraction of the main camera's far clip plane. 1.0 = unchanged, lower = objects on the affected layers stop rendering sooner.",
                    new AcceptableValueRange<float>(0.25f, 1f)));

            CollectGCOnSceneLoad = file.Bind("GC", "CollectGCOnSceneLoad", true,
                "Run a full GC.Collect after each scene change (menu, lobby or level), while the loading screen is still up.");

            EnableTextureStreaming = file.Bind("TextureStreaming", "Enable", true,
                "Turn on Unity mipmap streaming. Only textures the game imported with streaming mipmaps take part; the log reports how many there are after each scene change.");
            StreamingMipmapsMemoryBudget = file.Bind("TextureStreaming", "MemoryBudgetMB", 1024,
                new ConfigDescription(
                    "Memory budget for streamed textures in MB (Unity's default is 512).",
                    new AcceptableValueRange<int>(128, 8192)));

            ShowFpsOverlay = file.Bind("Diagnostics", "ShowFpsOverlay", false,
                "Draw a small FPS / frame time / physics tick counter in the top-left corner.");

            RemoveObsoleteEntries(file, log);
        }
        finally
        {
            file.SaveOnConfigSet = saveOnConfigSet;
        }

        try
        {
            file.Save();
        }
        catch (Exception ex)
        {
            // Another program holding the file open must not stop the plugin; the values are already in memory.
            log.LogWarning("Could not write the config file (" + ex.Message + "). The settings still apply for this session.");
        }
    }

    /// <summary>
    /// BepInEx keeps values it found in the file but nobody bound ("orphaned" entries) and writes them
    /// back on every save. That list is private, so it is reached by reflection; if BepInEx changes it,
    /// the old lines simply stay in the file.
    /// </summary>
    private static void RemoveObsoleteEntries(ConfigFile file, ManualLogSource log)
    {
        try
        {
            PropertyInfo property = typeof(ConfigFile).GetProperty("OrphanedEntries", BindingFlags.Instance | BindingFlags.NonPublic);
            if (property == null || !(property.GetValue(file, null) is Dictionary<ConfigDefinition, string> orphaned))
            {
                return;
            }

            int removed = 0;
            foreach (ConfigDefinition entry in ObsoleteEntries)
            {
                if (orphaned.Remove(entry))
                {
                    removed++;
                }
            }

            if (removed > 0)
            {
                log.LogInfo($"Removed {removed} setting(s) from the config file that this version no longer uses.");
            }
        }
        catch (Exception ex)
        {
            log.LogDebug("Could not remove obsolete config entries: " + ex.Message);
        }
    }
}
