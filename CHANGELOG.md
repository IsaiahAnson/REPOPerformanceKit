# Changelog

## 0.1.4
- Added `REPOModding-REPOConfig` as a required dependency, so the in-game Mods settings page is installed together with the plugin.
- Every setting now applies as soon as it is saved on the Mods page, with no restart. Turning a feature off puts the game's own values back. Number settings have ranges, so the page shows sensible sliders.
- Adaptive physics is now off by default and its switch is renamed to `EnableAdaptivePhysics`, so after updating it is off until you turn it on. It restores the game's own `Time.fixedDeltaTime` when FPS recovers, and a single long loading frame no longer triggers it; `BaselineFixedDeltaTime` is removed.
- Cull distance override is off by default for new installs, does nothing at `Multiplier = 1.0`, never touches player, enemy, grabbable-object or cart layers, and follows the camera across level loads and far clip plane changes.
- Removed `OverrideTargetFrameRate`, `TargetFrameRate` and `VSyncCount`: the game overwrites both values from its own graphics settings.
- Removed `EnableIncrementalGC`: incremental GC is set when the game is built and a plugin cannot switch it. Its state is now only logged.
- Log filter: `SuppressPluginLogSpam = false` now turns the substring filter off, the "repeated N more times" line is logged under the source and level it belongs to, and warnings and errors are no longer collapsed.
- The scene-load GC pass now also runs between levels. The current game build changes level without a full scene load, which 0.1.3 waited for.
- Texture streaming logs how many textures in each scene actually stream.
- Each feature starts on its own, so one that fails no longer stops the rest, and the config file is written once at startup. Another program having the file open no longer stops the plugin.
- The DLL now reports the same version as the package (it said 0.1.0 before). Settings this version no longer reads are deleted from the config file.
- Added `CHANGELOG.md` to the package so the Thunderstore Changelog tab is populated.

## 0.1.3
- Version bump (0.1.2 previously published).

## 0.1.2
- Rewrote description and README to strictly match the code's actual behaviour.

## 0.1.1
- Added `TextureStreaming` feature.

## 0.1.0
- Initial implementation (LogSuppressor, FrameRateTuner, GCTuner, AdaptivePhysics, CullDistanceBoost, FPSCounter).
