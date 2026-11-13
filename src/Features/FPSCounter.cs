using BepInEx.Logging;
using REPOPerformance.Config;
using UnityEngine;

namespace REPOPerformance.Features;

/// <summary>
/// Optional overlay in the top-left corner: smoothed FPS, frame time and the current physics tick rate.
/// </summary>
internal static class FPSCounter
{
    private static GameObject _host;

    public static void Apply(PerformanceConfig cfg, ManualLogSource log)
    {
        if (!cfg.ShowFpsOverlay.Value)
        {
            return;
        }

        _host = new GameObject("REPOPerformance_FPSCounter");
        Object.DontDestroyOnLoad(_host);
        _host.hideFlags = HideFlags.HideAndDontSave;
        _host.AddComponent<FPSCounterBehaviour>();
        log.LogInfo("FPS overlay enabled.");
    }

    private sealed class FPSCounterBehaviour : MonoBehaviour
    {
        private float _smoothed = 60f;
        private GUIStyle _style;

        private void Update()
        {
            float fps = 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            _smoothed = Mathf.Lerp(_smoothed, fps, 0.1f);
        }

        private void OnGUI()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    alignment = TextAnchor.UpperLeft,
                    normal = { textColor = Color.white }
                };
            }

            float frameMs = Time.unscaledDeltaTime * 1000f;
            string text = $"FPS {_smoothed:F0}   {frameMs:F1}ms   physTick {1f / Time.fixedDeltaTime:F0}Hz";

            // Black copy one pixel down-right as a shadow, then the white text on top.
            GUIStyle shadow = _style;
            Color white = shadow.normal.textColor;
            shadow.normal.textColor = Color.black;
            GUI.Label(new Rect(11f, 11f, 400f, 20f), text, shadow);
            shadow.normal.textColor = white;
            GUI.Label(new Rect(10f, 10f, 400f, 20f), text, _style);
        }
    }
}
