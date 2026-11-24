using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace REPOPerformance.Features;

/// <summary>
/// Tells the other features when the game has finished changing scene.
/// The game moves between levels by loading the next scene additively, making it the active scene and
/// then unloading the old one, so <c>SceneManager.sceneLoaded</c> never reports a full load there. This
/// watches the active scene and scene unloads instead, and reports once after they go quiet, so the
/// quick Main -> Reload -> Main hop of a level change counts as one scene change.
/// </summary>
internal static class SceneWatcher
{
    private const float QuietSeconds = 0.5f;

    /// <summary>Raised once per scene change with the name of the scene that is now active.</summary>
    public static event Action<string> Settled;

    private static Watcher _watcher;

    public static void Start()
    {
        if (_watcher == null)
        {
            _watcher = RuntimeHost.Add<Watcher>(true);
        }
    }

    private sealed class Watcher : MonoBehaviour
    {
        private float _settleAt = -1f;

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            _settleAt = -1f;
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            _settleAt = Time.unscaledTime + QuietSeconds;
        }

        // The old scene is unloaded after the new one is active, and only then can its memory be freed.
        private void OnSceneUnloaded(Scene scene)
        {
            _settleAt = Time.unscaledTime + QuietSeconds;
        }

        private void Update()
        {
            if (_settleAt < 0f || Time.unscaledTime < _settleAt)
            {
                return;
            }
            _settleAt = -1f;

            Action<string> handlers = Settled;
            if (handlers == null)
            {
                return;
            }

            string sceneName = SceneManager.GetActiveScene().name;
            foreach (Delegate handler in handlers.GetInvocationList())
            {
                // One feature failing here must not stop the other from hearing about the scene change.
                try
                {
                    ((Action<string>)handler)(sceneName);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[REPOPerformance] Scene change handler failed: " + ex.Message);
                }
            }
        }
    }
}
