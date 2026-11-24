using BepInEx.Logging;
using REPOPerformance.Config;
using UnityEngine;

namespace REPOPerformance.Features;

/// <summary>
/// Optional overlay in the top-left corner: smoothed FPS, frame time and the current physics tick rate.
/// </summary>
internal static class FPSCounter
{
    private static PerformanceConfig _cfg;
    private static ManualLogSource _log;
    private static Overlay _overlay;

    public static void Apply(PerformanceConfig cfg, ManualLogSource log)
    {
        _cfg = cfg;
        _log = log;

        _overlay = RuntimeHost.Add<Overlay>(cfg.ShowFpsOverlay.Value);
        if (cfg.ShowFpsOverlay.Value)
        {
            log.LogInfo("FPS overlay enabled.");
        }

        cfg.ShowFpsOverlay.SettingChanged += (_, _) =>
        {
            if (_overlay != null)
            {
                _overlay.enabled = _cfg.ShowFpsOverlay.Value;
                _log.LogInfo(_overlay.enabled ? "FPS overlay enabled." : "FPS overlay disabled.");
            }
        };
    }

    private sealed class Overlay : MonoBehaviour
    {
        private const float TextIntervalSeconds = 0.25f;

        private static readonly Rect ShadowRect = new Rect(11f, 11f, 400f, 20f);
        private static readonly Rect TextRect = new Rect(10f, 10f, 400f, 20f);

        private float _smoothedFps;
        private float _textTimer;
        private string _text = string.Empty;
        private GUIStyle _style;
        private GUIStyle _shadowStyle;

        private void Awake()
        {
            // No GUILayout calls here, so Unity can skip the extra layout pass.
            useGUILayout = false;
        }

        private void OnEnable()
        {
            _smoothedFps = 0f;
            _textTimer = TextIntervalSeconds;
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            float fps = 1f / deltaTime;
            _smoothedFps = _smoothedFps <= 0f ? fps : Mathf.Lerp(_smoothedFps, fps, 0.1f);

            // Build the string a few times a second, not on every GUI event.
            _textTimer += deltaTime;
            if (_textTimer < TextIntervalSeconds)
            {
                return;
            }
            _textTimer = 0f;
            _text = $"FPS {_smoothedFps:F0}   {1000f / _smoothedFps:F1}ms   physTick {1f / Time.fixedDeltaTime:F0}Hz";
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || _text.Length == 0)
            {
                return;
            }

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.UpperLeft };
                _style.normal.textColor = Color.white;
                _shadowStyle = new GUIStyle(_style);
                _shadowStyle.normal.textColor = Color.black;
            }

            // Black copy one pixel down-right as a shadow, then the white text on top.
            GUI.Label(ShadowRect, _text, _shadowStyle);
            GUI.Label(TextRect, _text, _style);
        }
    }
}
