using UnityEngine;
using Zombera.Debugging.DebugLogging;
using Zombera.Debugging.DebugTools;

namespace Zombera.Debugging.DebugStressTests
{
    /// <summary>
    /// Stress test scaffold for squad unit load testing.
    /// </summary>
    public sealed class SquadStressTest : MonoBehaviour, IDebugTool
    {
        [SerializeField] private SpawnDebugTools spawnDebugTools;

        public string ToolName => nameof(SquadStressTest);
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

        public void Spawn5Units()
        {
            RequestSquadBatch(5);
        }

        public void Spawn10Units()
        {
            RequestSquadBatch(10);
        }

        public void Spawn30Units()
        {
            RequestSquadBatch(30);
        }

        private void RequestSquadBatch(int count)
        {
            if (!IsToolEnabled)
            {
                return;
            }

            DebugLogger.Log(LogCategory.Stress, $"Squad stress batch requested: {count}", this);

            StartCoroutine(StagedSquadSpawn(count));
        }

        private System.Collections.IEnumerator StagedSquadSpawn(int count)
        {
            int batchSize = 5;
            int spawned = 0;
            float aiCostBefore = UnityEngine.Time.realtimeSinceStartup;

            while (spawned < count)
            {
                int thisWave = Mathf.Min(batchSize, count - spawned);

                for (int i = 0; i < thisWave; i++)
                {
                    spawnDebugTools?.SpawnSurvivor();
                }

                spawned += thisWave;
                float aiCostAfter = UnityEngine.Time.realtimeSinceStartup;
                DebugLogger.Log(LogCategory.Stress, $"Squad wave spawned: {spawned}/{count} — batch real-time cost: {(aiCostAfter - aiCostBefore) * 1000f:F1}ms", this);
                aiCostBefore = aiCostAfter;
                yield return new UnityEngine.WaitForSeconds(0.1f);
            }
        }
    }
}