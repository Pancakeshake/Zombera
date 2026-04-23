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

            StartCoroutine(StagedZombieSpawn(count));
        }

        private System.Collections.IEnumerator StagedZombieSpawn(int count)
        {
            int batchSize = 10;
            int spawned = 0;

            while (spawned < count)
            {
                int thisWave = Mathf.Min(batchSize, count - spawned);
                float t0 = UnityEngine.Time.realtimeSinceStartup;

                for (int i = 0; i < thisWave; i++)
                {
                    spawnDebugTools?.SpawnZombie();
                }

                spawned += thisWave;
                float elapsed = (UnityEngine.Time.realtimeSinceStartup - t0) * 1000f;
                DebugLogger.Log(LogCategory.Stress, $"Zombie wave spawned: {spawned}/{count} — batch wall-time: {elapsed:F1}ms", this);
                yield return new UnityEngine.WaitForSeconds(0.05f);
            }
        }
    }
}