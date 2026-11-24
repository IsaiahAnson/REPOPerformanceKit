using UnityEngine;

namespace REPOPerformance.Features;

/// <summary>
/// One hidden GameObject that survives scene loads and carries every per-frame component of the plugin.
/// </summary>
internal static class RuntimeHost
{
    private static GameObject _host;

    /// <summary>
    /// Adds a component to the host. Unity runs its OnEnable once during the add, before
    /// <paramref name="enabled"/> is applied, so components must tolerate an OnEnable/OnDisable pair
    /// at startup.
    /// </summary>
    public static T Add<T>(bool enabled) where T : MonoBehaviour
    {
        if (_host == null)
        {
            _host = new GameObject("REPOPerformance");
            Object.DontDestroyOnLoad(_host);
            _host.hideFlags = HideFlags.HideAndDontSave;
        }

        T component = _host.AddComponent<T>();
        component.enabled = enabled;
        return component;
    }
}
