#region

using System.Collections;
using UnityEngine;
using Zombera.Debugging.DebugLogging;
using Zombera.Debugging.DebugTools;

#endregion

namespace Zombera.Debugging.DebugStressTests
{
    /// <summary>
    ///     Stress test scaffold for zombie population load testing.
    /// </summary>
    public sealed class ZombieStressTest : MonoBehaviour, IDebugTool
    {
        [SerializeField] private SpawnDebugTools spawnDebugTools;

        private void OnEnable()
        {
            DebugManager.Instance?.RegisterDebugTool(this);
        }

        private void OnDisable()
        {
            DebugManager.Instance?.UnregisterDebugTool(this);
        }

        public string ToolName => nameof(ZombieStressTest);
        public bool IsToolEnabled { get; private set; } = true;

        public void SetToolEnabled(bool isEnabled)
        {
            IsToolEnabled = isEnabled;
        }

        [ContextMenu("Stress/Spawn 50 Zombies")]
        // ReSharper disable once UnusedMember.Global
        public void Spawn50Zombies()
        {
            RequestZombieBatch(50);
        }

        [ContextMenu("Stress/Spawn 100 Zombies")]
        // ReSharper disable once UnusedMember.Global
        public void Spawn100Zombies()
        {
            RequestZombieBatch(100);
        }

        [ContextMenu("Stress/Spawn 200 Zombies")]
        // ReSharper disable once UnusedMember.Global
        public void Spawn200Zombies()
        {
            RequestZombieBatch(200);
        }

        private void RequestZombieBatch(int count)
        {
            if (!IsToolEnabled) return;

            DebugLogger.Log(LogCategory.Stress, $"Zombie stress batch requested: {count}", this);

            StartCoroutine(StagedZombieSpawn(count));
        }

        private IEnumerator StagedZombieSpawn(int count)
        {
            const int batchSize = 10;
            var spawned = 0;

            while (spawned < count)
            {
                var thisWave = Mathf.Min(batchSize, count - spawned);
                var t0 = Time.realtimeSinceStartup;

                for (var i = 0; i < thisWave; i++) spawnDebugTools?.SpawnZombie();

                spawned += thisWave;
                var elapsed = (Time.realtimeSinceStartup - t0) * 1000f;
                DebugLogger.Log(LogCategory.Stress,
                    $"Zombie wave spawned: {spawned}/{count} — batch wall-time: {elapsed:F1}ms", this);
                yield return new WaitForSeconds(0.05f);
            }
        }
    }
}