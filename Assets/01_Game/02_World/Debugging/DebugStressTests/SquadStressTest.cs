#region

using System.Collections;
using UnityEngine;
using Zombera.Debugging.DebugLogging;
using Zombera.Debugging.DebugTools;

#endregion

namespace Zombera.Debugging.DebugStressTests
{
    /// <summary>
    ///     Stress test scaffold for squad unit load testing.
    /// </summary>
    public sealed class SquadStressTest : MonoBehaviour, IDebugTool
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

        public string ToolName => nameof(SquadStressTest);
        public bool IsToolEnabled { get; private set; } = true;

        public void SetToolEnabled(bool isEnabled)
        {
            IsToolEnabled = isEnabled;
        }

        [ContextMenu("Stress/Spawn 5 Units")]
        // ReSharper disable once UnusedMember.Global
        public void Spawn5Units()
        {
            RequestSquadBatch(5);
        }

        [ContextMenu("Stress/Spawn 10 Units")]
        // ReSharper disable once UnusedMember.Global
        public void Spawn10Units()
        {
            RequestSquadBatch(10);
        }

        [ContextMenu("Stress/Spawn 30 Units")]
        // ReSharper disable once UnusedMember.Global
        public void Spawn30Units()
        {
            RequestSquadBatch(30);
        }

        private void RequestSquadBatch(int count)
        {
            if (!IsToolEnabled) return;

            DebugLogger.Log(LogCategory.Stress, $"Squad stress batch requested: {count}", this);

            StartCoroutine(StagedSquadSpawn(count));
        }

        private IEnumerator StagedSquadSpawn(int count)
        {
            const int batchSize = 5;
            var spawned = 0;
            var aiCostBefore = Time.realtimeSinceStartup;

            while (spawned < count)
            {
                var thisWave = Mathf.Min(batchSize, count - spawned);

                for (var i = 0; i < thisWave; i++) spawnDebugTools?.SpawnSurvivor();

                spawned += thisWave;
                var aiCostAfter = Time.realtimeSinceStartup;
                DebugLogger.Log(LogCategory.Stress,
                    $"Squad wave spawned: {spawned}/{count} — batch real-time cost: {(aiCostAfter - aiCostBefore) * 1000f:F1}ms",
                    this);
                aiCostBefore = aiCostAfter;
                yield return new WaitForSeconds(0.1f);
            }
        }
    }
}