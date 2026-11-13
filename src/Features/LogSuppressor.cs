using System;
using System.Collections.Generic;
using BepInEx.Logging;
using REPOPerformance.Config;
using UnityEngine;

namespace REPOPerformance.Features;

/// <summary>
/// Cuts log cost two ways: turns off Unity's stack-trace capture for plain logs and warnings,
/// and puts a filtering listener in front of every BepInEx log listener.
/// </summary>
internal static class LogSuppressor
{
    private static FilteringListener _listener;

    public static void Apply(PerformanceConfig cfg, ManualLogSource log)
    {
        if (cfg.DisableUnityStackTraces.Value)
        {
            try
            {
                Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
                Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
                Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.ScriptOnly);
                Application.SetStackTraceLogType(LogType.Exception, StackTraceLogType.ScriptOnly);
                Application.SetStackTraceLogType(LogType.Assert, StackTraceLogType.ScriptOnly);
                log.LogInfo("Unity stack-trace capture disabled for Log/Warning.");
            }
            catch (Exception ex)
            {
                log.LogWarning("Failed to set Unity stack trace log type: " + ex.Message);
            }
        }

        if (cfg.SuppressPluginLogSpam.Value || cfg.DedupeRepeatedLogs.Value)
        {
            List<string> substrings = ParseSubstrings(cfg.SuppressedSubstrings.Value);
            _listener = new FilteringListener(substrings, cfg.DedupeRepeatedLogs.Value);
            BepInEx.Logging.Logger.Listeners.Add(_listener);
            log.LogInfo($"Installed filtering log listener ({substrings.Count} substring filters, " +
                        $"dedupe={cfg.DedupeRepeatedLogs.Value}).");
        }
    }

    private static List<string> ParseSubstrings(string raw)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return result;
        }

        foreach (string part in raw.Split(','))
        {
            string trimmed = part.Trim();
            if (trimmed.Length > 0)
            {
                result.Add(trimmed.ToLowerInvariant());
            }
        }
        return result;
    }

    /// <summary>
    /// Takes over the existing listeners (console, disk, Unity log) and forwards only the lines
    /// that pass the substring filter and the repeat check. Fatal, Error and Warning lines are
    /// never filtered by substring.
    /// </summary>
    private sealed class FilteringListener : ILogListener
    {
        private readonly List<string> _substrings;
        private readonly bool _dedupe;
        private readonly List<ILogListener> _downstream = new List<ILogListener>();
        private string _lastMessage;
        private int _lastRepeat;
        private readonly object _lock = new object();

        public FilteringListener(List<string> substrings, bool dedupe)
        {
            _substrings = substrings;
            _dedupe = dedupe;

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
        }

        public LogLevel LogLevelFilter => LogLevel.All;

        public void LogEvent(object sender, LogEventArgs e)
        {
            string message = e.Data == null ? string.Empty : e.Data.ToString();

            if ((e.Level & (LogLevel.Fatal | LogLevel.Error | LogLevel.Warning)) == 0)
            {
                string lower = message.ToLowerInvariant();
                foreach (string substring in _substrings)
                {
                    if (lower.Contains(substring))
                    {
                        return;
                    }
                }
            }

            lock (_lock)
            {
                if (_dedupe && message == _lastMessage)
                {
                    _lastRepeat++;
                    return;
                }

                if (_lastRepeat > 0)
                {
                    var summary = new LogEventArgs($"(previous message repeated {_lastRepeat} more times)", LogLevel.Info, e.Source);
                    ForwardAll(sender, summary);
                    _lastRepeat = 0;
                }

                _lastMessage = message;
                ForwardAll(sender, e);
            }
        }

        private void ForwardAll(object sender, LogEventArgs e)
        {
            foreach (ILogListener listener in _downstream)
            {
                try
                {
                    listener.LogEvent(sender, e);
                }
                catch
                {
                    // A broken downstream listener must not take the others down with it.
                }
            }
        }

        public void Dispose()
        {
            foreach (ILogListener listener in _downstream)
            {
                try
                {
                    listener.Dispose();
                }
                catch
                {
                }
            }
            _downstream.Clear();
        }
    }
}
