using UnityEngine;

namespace Zombera.Debugging.DebugLogging
{
    /// <summary>
    /// Centralized structured logging helper.
    /// Responsibilities:
    /// - Prefix logs by category
    /// - Provide unified log entry format
    /// - Support warning/error channels
    /// </summary>
    public static class DebugLogger
    {
        public static bool EnableLogs = true;

        public static void Log(LogCategory category, string message, Object context = null)
        {
            if (!EnableLogs)
            {
                return;
            }

            string formatted = FormatMessage(category, message);

            if (context != null)
            {
                Debug.Log(formatted, context);
            }
            else
            {
                Debug.Log(formatted);
            }
        }

        public static void LogWarning(LogCategory category, string message, Object context = null)
        {
            if (!EnableLogs)
            {
                return;
            }

            string formatted = FormatMessage(category, message);

            if (context != null)
            {
                Debug.LogWarning(formatted, context);
            }
            else
            {
                Debug.LogWarning(formatted);
            }
        }

        public static void LogError(LogCategory category, string message, Object context = null)
        {
            if (!EnableLogs)
            {
                return;
            }

            string formatted = FormatMessage(category, message);

            if (context != null)
            {
                Debug.LogError(formatted, context);
            }
            else
            {
                Debug.LogError(formatted);
            }
        }

        private static string FormatMessage(LogCategory category, string message)
        {
            return $"[{category}] {message}";
        }

        // TODO: Add optional timestamp/frame index formatting.
        // TODO: Add log sinks for file output and external telemetry.
    }
}