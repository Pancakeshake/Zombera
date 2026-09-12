#region

using UnityEngine;

#endregion

namespace Zombera.Debugging.DebugLogging
{
    /// <summary>
    ///     Centralized structured logging helper.
    ///     Responsibilities:
    ///     - Prefix logs by category
    ///     - Provide unified log entry format
    ///     - Support warning/error channels
    /// </summary>
    public static class DebugLogger
    {
        public static bool EnableLogs = true;
        public static bool EnableTraceLogs = false;

        public static void Configure(bool enableLogs, bool enableTraceLogs)
        {
            EnableLogs = enableLogs;
            EnableTraceLogs = enableTraceLogs;
        }

        public static void Log(LogCategory category, string message, Object context = null)
        {
            if (!EnableLogs) return;

            var formatted = FormatMessage(category, message);

            if (context != null)
                Debug.Log(formatted, context);
            else
                Debug.Log(formatted);
        }

        public static void LogWarning(LogCategory category, string message, Object context = null)
        {
            if (!EnableLogs) return;

            var formatted = FormatMessage(category, message);

            if (context != null)
                Debug.LogWarning(formatted, context);
            else
                Debug.LogWarning(formatted);
        }

        public static void LogError(LogCategory category, string message, Object context = null)
        {
            if (!EnableLogs) return;

            var formatted = FormatMessage(category, message);

            if (context != null)
                Debug.LogError(formatted, context);
            else
                Debug.LogError(formatted);
        }

        public static void LogTrace(LogCategory category, string message, Object context = null)
        {
            if (!EnableTraceLogs) return;

            var formatted = FormatMessage(category, message);

            if (context != null)
                Debug.Log(formatted, context);
            else
                Debug.Log(formatted);
        }

        private static string FormatMessage(LogCategory category, string message)
        {
            return $"[{category}][F{Time.frameCount}] {message}";
        }
    }
}