#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;

namespace Zombera.Editor.StyleMatch
{
    /// <summary>
    /// Prevents concurrent skill iterations (MCP retry duplicates).
    /// Poll <see cref="AgentLogRelativePath"/> instead of re-invoking script-execute.
    /// </summary>
    internal static class StyleMatchIterationLock
    {
        public const string AgentLogRelativePath = "Library/StyleMatch/agent-iter.log";
        private const string LockRelativePath = "Library/StyleMatch/iteration-lock.json";
        private const string LastCompletionRelativePath = "Library/StyleMatch/last-iteration-completion.json";
        private static readonly TimeSpan StaleTimeout = TimeSpan.FromMinutes(45);
        private static readonly TimeSpan CompletionCooldown = TimeSpan.FromMinutes(5);

        public static bool IsLocked => TryReadLock(out _);

        public static bool TryAcquire(int iterationIndex, string hypothesis, out string error, bool bypassCooldown = false)
        {
            error = null;
            EnsureOutputDir();

            if (TryReadLock(out var existing) && !IsStale(existing))
            {
                error = "Iteration " + existing.IterationIndex + " in progress since " + existing.StartedUtc +
                        ". Poll " + AgentLogRelativePath + " — do not re-invoke script-execute.";
                WriteAgentLog("blocked " + iterationIndex + ": iter " + existing.IterationIndex + " running");
                return false;
            }

            if (!bypassCooldown && TryReadLastCompletion(out var lastIndex, out var lastCompleted) &&
                DateTime.UtcNow - lastCompleted < CompletionCooldown)
            {
                error = "Iter " + lastIndex + " finished at " + lastCompleted.ToString("o") +
                        ". Cooldown active — poll " + AgentLogRelativePath + "; do not re-invoke script-execute.";
                WriteAgentLog("blocked " + iterationIndex + ": cooldown after iter " + lastIndex);
                return false;
            }

            if (existing != null)
                Debug.LogWarning("[StyleMatch] Clearing stale iteration lock for iter " + existing.IterationIndex);

            var lockData = new LockData
            {
                IterationIndex = iterationIndex,
                Hypothesis = hypothesis ?? string.Empty,
                StartedUtc = DateTime.UtcNow.ToString("o"),
            };
            File.WriteAllText(ResolvePath(LockRelativePath), JsonUtility.ToJson(lockData));
            WriteAgentLog("running " + iterationIndex + ": " + (hypothesis ?? "skill iteration"));
            return true;
        }

        public static void Release(int iterationIndex, bool success, string detail)
        {
            if (TryReadLock(out var existing) && existing.IterationIndex != iterationIndex)
            {
                Debug.LogWarning(
                    "[StyleMatch] Lock held by iter " + existing.IterationIndex + "; releasing anyway for " +
                    iterationIndex);
            }

            var prefix = success ? "ok " : "fail ";
            WriteAgentLog(prefix + iterationIndex + (string.IsNullOrEmpty(detail) ? "" : " " + detail));
            if (success)
            {
                var note = TryReadLock(out var lockData) ? lockData.Hypothesis : null;
                RecordCompletion(iterationIndex, note);
            }
            ClearLock();
        }

        public static void RecordCompletion(int iterationIndex, string hypothesis)
        {
            var data = new CompletionData
            {
                IterationIndex = iterationIndex,
                Hypothesis = hypothesis ?? string.Empty,
                CompletedUtc = DateTime.UtcNow.ToString("o"),
            };
            File.WriteAllText(ResolvePath(LastCompletionRelativePath), JsonUtility.ToJson(data));
        }

        public static bool TryGetAgentLog(out string text)
        {
            text = null;
            var path = ResolvePath(AgentLogRelativePath);
            if (!File.Exists(path))
                return false;

            text = File.ReadAllText(path).Trim();
            return true;
        }

        public static void ForceClear()
        {
            ClearLock();
            WriteAgentLog("lock-cleared");
        }

        private static void EnsureOutputDir() =>
            Directory.CreateDirectory(Path.GetDirectoryName(ResolvePath(LockRelativePath))!);

        private static void WriteAgentLog(string line) =>
            File.WriteAllText(ResolvePath(AgentLogRelativePath), line);

        private static void ClearLock()
        {
            var path = ResolvePath(LockRelativePath);
            if (File.Exists(path))
                File.Delete(path);
        }

        private static bool TryReadLock(out LockData data)
        {
            data = null;
            var path = ResolvePath(LockRelativePath);
            if (!File.Exists(path))
                return false;

            try
            {
                data = JsonUtility.FromJson<LockData>(File.ReadAllText(path));
                return data != null && data.IterationIndex > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsStale(LockData data)
        {
            if (data == null || string.IsNullOrEmpty(data.StartedUtc))
                return true;

            if (!DateTime.TryParse(data.StartedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var started))
                return true;

            return DateTime.UtcNow - started.ToUniversalTime() > StaleTimeout;
        }

        private static string ResolvePath(string relativePath) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));

        private static bool TryReadLastCompletion(out int iterationIndex, out DateTime completedUtc)
        {
            iterationIndex = 0;
            completedUtc = default;
            var path = ResolvePath(LastCompletionRelativePath);
            if (!File.Exists(path))
                return false;

            try
            {
                var data = JsonUtility.FromJson<CompletionData>(File.ReadAllText(path));
                if (data == null || data.IterationIndex <= 0)
                    return false;

                if (!DateTime.TryParse(
                        data.CompletedUtc,
                        null,
                        System.Globalization.DateTimeStyles.RoundtripKind,
                        out var completed))
                    return false;

                iterationIndex = data.IterationIndex;
                completedUtc = completed.ToUniversalTime();
                return true;
            }
            catch
            {
                return false;
            }
        }

        [Serializable]
        private sealed class CompletionData
        {
            public int IterationIndex;
            public string Hypothesis;
            public string CompletedUtc;
        }

        [Serializable]
        private sealed class LockData
        {
            public int IterationIndex;
            public string Hypothesis;
            public string StartedUtc;
        }
    }
}
#endif
