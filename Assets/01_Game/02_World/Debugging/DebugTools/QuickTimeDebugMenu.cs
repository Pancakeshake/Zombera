#region

using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using Zombera.Characters;
using Zombera.Environment;
using Zombera.Systems;
using Zombera.World;

#endregion

namespace Zombera.Debugging.DebugTools
{
    /// <summary>
    ///     Runtime debug actions for time progression and diagnostics.
    ///     Intended to be invoked by DebugMenuController buttons.
    /// </summary>
    public sealed class QuickTimeDebugMenu : MonoBehaviour, IDebugTool
    {
        [Header("Time Control")] [SerializeField] private float hourStep = 3f;

        private string _lastDiagnosticsSummary = "No diagnostics run yet.";

        public string ToolName => nameof(QuickTimeDebugMenu);
        public bool IsToolEnabled { get; private set; } = true;
        public string LastDiagnosticsSummary => _lastDiagnosticsSummary;

        private void OnEnable()
        {
            DebugManager.Instance?.RegisterDebugTool(this);
        }

        private void OnDisable()
        {
            DebugManager.Instance?.UnregisterDebugTool(this);
        }

        public void SetToolEnabled(bool isEnabled)
        {
            IsToolEnabled = isEnabled;
        }

        public bool AddThreeHours()
        {
            return TryAddHours(hourStep);
        }

        public bool SubtractThreeHours()
        {
            return TryAddHours(-hourStep);
        }

        public bool TryAddHours(float deltaHours)
        {
            if (!IsToolEnabled) return false;

            var dayNight = DayNightController.Instance;
            if (dayNight == null)
            {
                Debug.LogWarning("[QuickTimeDebugMenu] DayNightController not found. Cannot adjust time.", this);
                return false;
            }

            dayNight.SetHour(dayNight.CurrentHour + deltaHours);
            Debug.Log($"[QuickTimeDebugMenu] Time adjusted by {deltaHours:0.##}h. New hour: {dayNight.CurrentHour:00.00}", this);
            return true;
        }

        public string RunFullDiagnostics()
        {
            if (!IsToolEnabled) return _lastDiagnosticsSummary;

            var unitManager = FindFirstObjectByType<UnitManager>();
            var zombieManager = FindFirstObjectByType<ZombieManager>();
            var chunkLoader = FindFirstObjectByType<ChunkLoader>();
            var dayNight = DayNightController.Instance;

            var activeZombies = zombieManager != null
                ? zombieManager.ActiveZombieCount
                : unitManager != null ? unitManager.CountZombies() : 0;
            var activeSquad = unitManager != null ? unitManager.CountByRole(UnitRole.SquadMember) : 0;
            var activeChunks = chunkLoader != null ? chunkLoader.LoadedChunks.Count : 0;

            var sb = new StringBuilder(1024);
            sb.AppendLine($"Timestamp: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            sb.AppendLine($"Time Scale: {Time.timeScale:0.###}");
            sb.AppendLine($"Target Frame Rate: {Application.targetFrameRate}");
            sb.AppendLine($"VSync: {QualitySettings.vSyncCount}");
            sb.AppendLine($"Quality Level: {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
            sb.AppendLine($"GPU: {SystemInfo.graphicsDeviceName}");
            sb.AppendLine($"CPU: {SystemInfo.processorType} ({SystemInfo.processorCount} cores)");
            sb.AppendLine($"System RAM: {SystemInfo.systemMemorySize} MB");
            sb.AppendLine($"Graphics RAM: {SystemInfo.graphicsMemorySize} MB");

            if (dayNight != null)
                sb.AppendLine($"World Time: Day {dayNight.DayNumber} @ {dayNight.CurrentHour:00.00} ({dayNight.CurrentPhase})");
            else
                sb.AppendLine("World Time: DayNightController missing");

            sb.AppendLine($"Active Zombies: {activeZombies}");
            sb.AppendLine($"Active Squad Units: {activeSquad}");
            sb.AppendLine($"Loaded Chunks: {activeChunks}");
            sb.AppendLine($"RAM Allocated: {ToMb(Profiler.GetTotalAllocatedMemoryLong()):0.0} MB");
            sb.AppendLine($"RAM Reserved: {ToMb(Profiler.GetTotalReservedMemoryLong()):0.0} MB");
            sb.AppendLine($"Mono Used: {ToMb(Profiler.GetMonoUsedSizeLong()):0.0} MB");
            sb.AppendLine($"Particle Systems: {FindObjectCount<ParticleSystem>()}");
            sb.AppendLine($"Skinned Mesh Renderers: {FindObjectCount<SkinnedMeshRenderer>()}");
            sb.AppendLine($"Terrain Count: {Terrain.activeTerrains.Length}");

            _lastDiagnosticsSummary = sb.ToString();
            Debug.Log("[QuickTimeDebugMenu] Full diagnostics snapshot\n" + _lastDiagnosticsSummary, this);
            return _lastDiagnosticsSummary;
        }

        private static int FindObjectCount<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None).Length;
#else
            return Object.FindObjectsOfType<T>().Length;
#endif
        }

        private static float ToMb(long bytes)
        {
            return bytes / (1024f * 1024f);
        }
    }
}