using TMPro;
using UnityEngine;
using Zombera.Characters;
using Zombera.Systems;
using Zombera.World;

namespace Zombera.Debugging.DebugStressTests
{
    /// <summary>
    /// Runtime performance readout panel.
    /// Displays:
    /// - FPS
    /// - Active zombies
    /// - Active squad units
    /// - Active chunks
    /// </summary>
    public sealed class PerformanceMonitor : MonoBehaviour, IDebugTool
    {
        [Header("UI")]
        [SerializeField] private TextMeshProUGUI fpsText;
        [SerializeField] private TextMeshProUGUI zombiesText;
        [SerializeField] private TextMeshProUGUI squadText;
        [SerializeField] private TextMeshProUGUI chunksText;

        [Header("References")]
        [SerializeField] private UnitManager unitManager;
        [SerializeField] private ZombieManager zombieManager;
        [SerializeField] private ChunkLoader chunkLoader;

        [Header("Update")]
        [SerializeField] private float refreshInterval = 0.25f;

        public string ToolName => nameof(PerformanceMonitor);
        public bool IsToolEnabled { get; private set; } = true;

        private float refreshTimer;

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
            gameObject.SetActive(enabled);
        }

        private void Update()
        {
            if (!IsToolEnabled)
            {
                return;
            }

            refreshTimer += Time.unscaledDeltaTime;

            if (refreshTimer < refreshInterval)
            {
                return;
            }

            refreshTimer = 0f;
            RefreshReadout();
        }

        private void RefreshReadout()
        {
            float fps = Time.unscaledDeltaTime > 0f ? 1f / Time.unscaledDeltaTime : 0f;
            int activeZombies = zombieManager != null ? zombieManager.ActiveZombieCount : (unitManager != null ? unitManager.CountZombies() : 0);
            int activeSquad = unitManager != null ? unitManager.CountByRole(UnitRole.SquadMember) : 0;
            int activeChunks = chunkLoader != null ? chunkLoader.LoadedChunks.Count : 0;

            SetText(fpsText, $"FPS: {Mathf.RoundToInt(fps)}");
            SetText(zombiesText, $"Active Zombies: {activeZombies}");
            SetText(squadText, $"Active Squad Units: {activeSquad}");
            SetText(chunksText, $"Active Chunks: {activeChunks}");
        }

        private static void SetText(TMP_Text textComponent, string value)
        {
            if (textComponent != null)
            {
                textComponent.text = value;
            }
        }

        // TODO: Add frametime graphs and memory/GC metrics.
        // TODO: Add CSV export for stress test sessions.
    }
}