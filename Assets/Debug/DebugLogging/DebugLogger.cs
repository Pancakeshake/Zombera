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
            return $"[{category}][F{UnityEngine.Time.frameCount}] {message}";
        }

        /// <summary>Writes a log line to a rolling file sink if a sink path is configured.</summary>
        private static string logFilePath;

        public static void SetLogFilePath(string path)
        {
            logFilePath = path;
        }

        private static void FlushToFile(string line)
        {
            if (string.IsNullOrEmpty(logFilePath))
            {
                return;
            }

#if !UNITY_WEBGL
            try
            {
                System.IO.File.AppendAllText(logFilePath, line + System.Environment.NewLine);
            }
            catch (System.Exception)
            {
                // Silently skip file write errors so debug logging never crashes the game.
            }
#endif
        }
    }
}