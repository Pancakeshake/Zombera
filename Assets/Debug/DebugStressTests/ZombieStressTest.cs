using UnityEngine;
using Zombera.Debugging.DebugLogging;
using Zombera.Debugging.DebugTools;

namespace Zombera.Debugging.DebugStressTests
{
    /// <summary>
    /// Stress test scaffold for zombie population load testing.
    /// </summary>
    public sealed class ZombieStressTest : MonoBehaviour, IDebugTool
    {
        [SerializeField] private SpawnDebugTools spawnDebugTools;

        public string ToolName => nameof(ZombieStressTest);
        public bool IsToolEnabled { get; private set; } = true;

        private void OnEnable()
        {
            DebugManager.Instance?.RegisterDebugTool(this);
        }

        private void OnDisable()
        {
            DebugManager.Instance?.UnregisterDebugTool(this);
        }

        public void SetToolEnabled(bool enabled)
        {
            IsToolEnabled = enabled;
        }

        public void Spawn50Zombies()
        {
            RequestZombieBatch(50);
        }

        public void Spawn100Zombies()
        {
            RequestZombieBatch(100);
        }

        public void Spawn200Zombies()
        {
            RequestZombieBatch(200);
        }

        private void RequestZombieBatch(int count)
        {
            if (!IsToolEnabled)
            {
                return;
            }

            DebugLogger.Log(LogCategory.Stress, $"Zombie stress batch requested: {count}", this);

            for (int i = 0; i < count; i++)
            {
                spawnDebugTools?.SpawnZombie();
            }

            // TODO: Replace looped spawn requests with coroutine/job scheduler.
            // TODO: Capture spawn completion telemetry and timings.
        }
    }
}