using System;
using System.Collections.Generic;
using BepInEx.Logging;
using REPOPerformance.Config;
using UnityEngine;

namespace REPOPerformance.Features;

/// <summary>
/// Cuts log cost two ways: turns off Unity's stack-trace capture for plain logs and warnings,
/// and puts a filtering listener in front of the BepInEx log listeners that exist when it starts.
/// </summary>
internal static class LogSuppressor
{
    private static readonly LogType[] LogTypes =
    {
        LogType.Log, LogType.Warning, LogType.Error, LogType.Exception, LogType.Assert
    };

    private static PerformanceConfig _cfg;
    private static ManualLogSource _log;
    private static StackTraceLogType[] _originalStackTraces;
    private static FilteringListener _listener;

    public static void Apply(PerformanceConfig cfg, ManualLogSource log)
    {
        _cfg = cfg;
        _log = log;

        RefreshStackTraces();
        RefreshListener();

        cfg.DisableUnityStackTraces.SettingChanged += (_, _) => RefreshStackTraces();
        cfg.SuppressPluginLogSpam.SettingChanged += (_, _) => RefreshListener();
        cfg.SuppressedSubstrings.SettingChanged += (_, _) => RefreshListener();
        cfg.DedupeRepeatedLogs.SettingChanged += (_, _) => RefreshListener();
    }

    private static void RefreshStackTraces()
    {
        try
        {
            if (_cfg.DisableUnityStackTraces.Value)
            {
                if (_originalStackTraces == null)
                {
                    _originalStackTraces = new StackTraceLogType[LogTypes.Length];
                    for (int i = 0; i < LogTypes.Length; i++)
                    {
                        _originalStackTraces[i] = Application.GetStackTraceLogType(LogTypes[i]);
                    }
                }

                Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
                Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
                Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.ScriptOnly);
                Application.SetStackTraceLogType(LogType.Exception, StackTraceLogType.ScriptOnly);
                Application.SetStackTraceLogType(LogType.Assert, StackTraceLogType.ScriptOnly);
                _log.LogInfo("Unity stack-trace capture disabled for Log/Warning.");
            }
            else if (_originalStackTraces != null)
            {
                for (int i = 0; i < LogTypes.Length; i++)
                {
                    Application.SetStackTraceLogType(LogTypes[i], _originalStackTraces[i]);
                }
                _originalStackTraces = null;
                _log.LogInfo("Unity stack-trace capture restored to the game's own settings.");
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning("Failed to set Unity stack trace log type: " + ex.Message);
        }
    }

    private static void RefreshListener()
    {
        bool filter = _cfg.SuppressPluginLogSpam.Value;
        bool dedupe = _cfg.DedupeRepeatedLogs.Value;
        string[] substrings = filter ? ParseSubstrings(_cfg.SuppressedSubstrings.Value) : new string[0];

        if (_listener == null)
        {
            if (!filter && !dedupe)
            {
                return;
            }

            _listener = new FilteringListener();
            _listener.Configure(substrings, dedupe);
            _listener.Install();
            _log.LogInfo($"Installed filtering log listener ({substrings.Length} substring filters, dedupe={dedupe}).");
            return;
        }

        // Once installed it stays in place. With both options off it forwards every line unchanged.
        _listener.Configure(substrings, dedupe);
        _log.LogInfo($"Filtering log listener updated ({substrings.Length} substring filters, dedupe={dedupe}).");
    }

    private static string[] ParseSubstrings(string raw)
    {
        var result = new List<string>();
        if (!string.IsNullOrWhiteSpace(raw))
        {
            foreach (string part in raw.Split(','))
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0)
                {
                    result.Add(trimmed);
                }
            }
        }
        return result.ToArray();
    }

    /// <summary>
    /// Takes over the listeners that exist when it is installed (console, disk, Unity log) and forwards
    /// only the lines that pass the substring filter and the repeat check. Fatal, Error and Warning
    /// lines are always forwarded as they are. Each downstream listener still applies its own log level.
    /// </summary>
    private sealed class FilteringListener : ILogListener
    {
        private const LogLevel AlwaysForwarded = LogLevel.Fatal | LogLevel.Error | LogLevel.Warning;

        private readonly List<ILogListener> _downstream = new List<ILogListener>();
        private readonly object _lock = new object();

        private volatile string[] _substrings = new string[0];
        private volatile bool _dedupe;

        // The last forwarded line that may be repeated. _lastMessage is null when there is none.
        private string _lastMessage;
        private LogLevel _lastLevel;
        private ILogSource _lastSource;
        private object _lastSender;
        private int _repeats;

        public void Configure(string[] substrings, bool dedupe)
        {
            _substrings = substrings ?? new string[0];
            lock (_lock)
            {
                _dedupe = dedupe;
                if (!dedupe)
                {
                    FlushRepeats();
                    Forget();
                }
            }
        }

        public void Install()
        {
            // Copy first: the loop removes from the live listener list.
            var existing = new List<ILogListener>(BepInEx.Logging.Logger.Listeners);
            foreach (ILogListener listener in existing)
            {
                if (listener != this)
                {
                    _downstream.Add(listener);
                    BepInEx.Logging.Logger.Listeners.Remove(listener);
                }
            }
            BepInEx.Logging.Logger.Listeners.Add(this);
        }

        public void LogEvent(object sender, LogEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            string[] substrings = _substrings;
            bool plain = (e.Level & AlwaysForwarded) == 0;
            string message = null;

            if (plain && (substrings.Length > 0 || _dedupe))
            {
                message = e.Data == null ? string.Empty : e.Data.ToString() ?? string.Empty;
                foreach (string substring in substrings)
                {
                    if (message.IndexOf(substring, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return;
                    }
                }
            }

            lock (_lock)
            {
                if (plain && _dedupe)
                {
                    // Dedupe may have been switched on after the check above skipped building the text.
                    message ??= e.Data == null ? string.Empty : e.Data.ToString() ?? string.Empty;

                    // A repeat is the same text at the same level from the same source.
                    if (_lastMessage != null && e.Level == _lastLevel && ReferenceEquals(e.Source, _lastSource) &&
                        message == _lastMessage)
                    {
                        _repeats++;
                        return;
                    }

                    FlushRepeats();
                    _lastMessage = message;
                    _lastLevel = e.Level;
                    _lastSource = e.Source;
                    _lastSender = sender;
                }
                else
                {
                    FlushRepeats();
                    Forget();
                }

                Forward(sender, e);
            }
        }

        /// <summary>Writes the pending repeat count under the name and level of the line it belongs to.</summary>
        private void FlushRepeats()
        {
            if (_repeats == 0)
            {
                return;
            }

            int repeats = _repeats;
            _repeats = 0;
            string text = $"(previous message repeated {repeats} more {(repeats == 1 ? "time" : "times")})";
            Forward(_lastSender, new LogEventArgs(text, _lastLevel, _lastSource));
        }

        private void Forget()
        {
            _lastMessage = null;
            _lastSource = null;
            _lastSender = null;
        }

        private void Forward(object sender, LogEventArgs e)
        {
            foreach (ILogListener listener in _downstream)
            {
                try
                {
                    listener.LogEvent(sender, e);
                }
                catch
                {
                    // A broken downstream listener must not take the others down with it,
                    // and logging the failure here would come straight back into this method.
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                FlushRepeats();
                Forget();
            }

            // These were taken out of BepInEx's list, so nothing else will dispose them.
            foreach (ILogListener listener in _downstream)
            {
                try
                {
                    listener.Dispose();
                }
                catch
                {
                    // Same reason as in Forward.
                }
            }
            _downstream.Clear();
        }
    }
}
