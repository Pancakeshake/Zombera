#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor.StyleMatch
{
    /// <summary>
    /// Skips repeated MicroSplat remap/compile within one editor session (avoids texture reimport storms).
    /// </summary>
    internal static class StyleMatchMicroSplatCompileGuard
    {
        private const string SessionRelativePath = "Library/StyleMatch/microsplat-compile-session.txt";

        public enum Operation
        {
            Remap,
            CompileConfig,
            CompileTemplate,
        }

        public static bool TryRun(Operation operation, Func<bool> action, bool force = false)
        {
            if (!force && WasCompleted(operation))
            {
                Debug.LogWarning(
                    "[StyleMatch] Skipping MicroSplat " + operation +
                    " — already ran this editor session. Use Tools/World/Style Match/Reset MicroSplat Compile Session to force.");
                return true;
            }

            var ok = action();
            if (ok)
                MarkCompleted(operation);

            return ok;
        }

        [MenuItem("Tools/World/Style Match/Reset MicroSplat Compile Session")]
        public static void MenuResetSession()
        {
            ResetSession();
            Debug.Log("[StyleMatch] MicroSplat compile session reset — remap/compile allowed again.");
        }

        public static void ResetSession()
        {
            var path = ResolvePath();
            if (File.Exists(path))
                File.Delete(path);
        }

        private static bool WasCompleted(Operation operation)
        {
            var path = ResolvePath();
            if (!File.Exists(path))
                return false;

            var key = operation.ToString();
            foreach (var line in File.ReadAllLines(path))
            {
                if (string.Equals(line.Trim(), key, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static void MarkCompleted(Operation operation)
        {
            var path = ResolvePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            if (File.Exists(path))
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        keys.Add(line.Trim());
                }
            }

            keys.Add(operation.ToString());
            File.WriteAllLines(path, keys);
        }

        private static string ResolvePath() =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", SessionRelativePath));
    }
}
#endif
