#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using Zombera.Characters;
using Zombera.Systems;
using Zombera.World;

#endregion

namespace Zombera.Debugging.DebugStressTests
{
    /// <summary>
    ///     Runtime performance readout panel.
    ///     Displays:
    ///     - FPS
    ///     - Active zombies
    ///     - Active squad units
    ///     - Active chunks
    /// </summary>
    public sealed class PerformanceMonitor : MonoBehaviour, IDebugTool
    {
        [Header("UI")] [SerializeField] private TextMeshProUGUI fpsText;

        [SerializeField] private TextMeshProUGUI zombiesText;
        [SerializeField] private TextMeshProUGUI squadText;
        [SerializeField] private TextMeshProUGUI chunksText;

        [Header("References")] [SerializeField]
        private UnitManager unitManager;

        [SerializeField] private ZombieManager zombieManager;
        [SerializeField] private ChunkLoader chunkLoader;

        [Header("Update")] [SerializeField] private float refreshInterval = 0.25f;

        // Frametime history for a simple runtime graph.
        private const int FrametimeHistoryCapacity = 120;
        private readonly Queue<float> _frametimeHistory = new(FrametimeHistoryCapacity);

        private float _refreshTimer;

        // ReSharper disable once UnusedMember.Global
        public IReadOnlyCollection<float> FrametimeHistory => _frametimeHistory;

        private void Update()
        {
            if (!IsToolEnabled) return;

            _refreshTimer += Time.unscaledDeltaTime;

            if (_refreshTimer < refreshInterval) return;

            _refreshTimer = 0f;
            RefreshReadout();

            var ft = Time.unscaledDeltaTime * 1000f; // ms
            if (_frametimeHistory.Count >= FrametimeHistoryCapacity) _frametimeHistory.Dequeue();
            _frametimeHistory.Enqueue(ft);
        }

        private void OnEnable()
        {
            DebugManager.Instance?.RegisterDebugTool(this);
        }

        private void OnDisable()
        {
            DebugManager.Instance?.UnregisterDebugTool(this);
        }

        public string ToolName => nameof(PerformanceMonitor);
        public bool IsToolEnabled { get; private set; } = true;

        public void SetToolEnabled(bool isEnabled)
        {
            IsToolEnabled = isEnabled;
            gameObject.SetActive(isEnabled);
        }

        private void RefreshReadout()
        {
            var fps = Time.unscaledDeltaTime > 0f ? 1f / Time.unscaledDeltaTime : 0f;
            var activeZombies = ResolveActiveZombieCount();
            var activeSquad = unitManager != null ? unitManager.CountByRole(UnitRole.SquadMember) : 0;
            var activeChunks = chunkLoader != null ? chunkLoader.LoadedChunks.Count : 0;

            SetText(fpsText, $"FPS: {Mathf.RoundToInt(fps)}");
            SetText(zombiesText, $"Active Zombies: {activeZombies}");
            SetText(squadText, $"Active Squad Units: {activeSquad}");
            SetText(chunksText, $"Active Chunks: {activeChunks}");
        }

        private int ResolveActiveZombieCount()
        {
            if (zombieManager != null) return zombieManager.ActiveZombieCount;
            return unitManager != null ? unitManager.CountZombies() : 0;
        }

        private static void SetText(TMP_Text textComponent, string value)
        {
            if (textComponent != null) textComponent.text = value;
        }

        /// <summary>Exports a CSV row of the last 120 frametime samples to a file.</summary>
        // ReSharper disable once UnusedMember.Global
        public void ExportCsvSnapshot(string filePath)
        {
#if !UNITY_WEBGL
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("frame_ms");

                foreach (var ms in _frametimeHistory) sb.AppendLine(ms.ToString("F3"));

                File.AppendAllText(filePath, sb.ToString());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PerformanceMonitor] CSV export failed: {e.Message}");
            }
#endif
        }

        [ContextMenu("Perf/Export CSV Snapshot (persistentDataPath)")]
        // ReSharper disable once UnusedMember.Global
        private void ExportCsvSnapshot_DefaultPath()
        {
#if !UNITY_WEBGL
            var resolved = Path.Combine(Application.persistentDataPath, "frametime_snapshot.csv");
            ExportCsvSnapshot(resolved);
#endif
        }
    }
}