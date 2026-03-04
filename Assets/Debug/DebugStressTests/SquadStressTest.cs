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

            for (int i = 0; i < count; i++)
            {
                spawnDebugTools?.SpawnSurvivor();
            }

            // TODO: Replace looped requests with staged spawn waves.
            // TODO: Track AI update cost after each squad batch.
        }
    }
}