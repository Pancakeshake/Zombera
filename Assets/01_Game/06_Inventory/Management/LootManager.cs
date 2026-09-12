#region

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zombera.Core;
using Zombera.Inventory;

#endregion

namespace Zombera.Systems
{
    /// <summary>
    ///     Coordinates container registration, loot generation tracking, and loot analytics.
    /// </summary>
    public sealed class LootManager : MonoBehaviour, IGameSystem
    {
        private readonly HashSet<LootContainer> _trackedContainers = new();
        public int RegisteredContainerCount => _trackedContainers.Count;
        public int GeneratedLootEventCount { get; private set; }

        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            if (IsInitialized) return;

            IsInitialized = true;
            GeneratedLootEventCount = 0;
            CoreEventBus.Instance?.Subscribe<LootGeneratedEvent>(OnLootGenerated);
        }

        public void Shutdown()
        {
            if (!IsInitialized) return;

            IsInitialized = false;
            CoreEventBus.Instance?.Unsubscribe<LootGeneratedEvent>(OnLootGenerated);
            _trackedContainers.Clear();
        }

        public void RegisterContainer(LootContainer container)
        {
            if (container == null) return;

            _trackedContainers.Add(container);
        }

        public void UnregisterContainer(LootContainer container)
        {
            if (container == null) return;

            _trackedContainers.Remove(container);
        }

        public IReadOnlyList<ItemStack> OpenContainer(LootContainer container, int deterministicSeed = 0)
        {
            if (container == null) return Array.Empty<ItemStack>();

            RegisterContainer(container);
            return container.OpenContainer(deterministicSeed);
        }

        public List<LootContainer> GetTrackedContainers(List<LootContainer> result = null)
        {
            var containers = result ?? new List<LootContainer>(_trackedContainers.Count);
            containers.Clear();

            containers.AddRange(_trackedContainers.Where(container => container != null));

            return containers;
        }

        private void OnLootGenerated(LootGeneratedEvent gameEvent)
        {
            GeneratedLootEventCount++;
            TrackLootMetrics(gameEvent);
        }

        private void TrackLootMetrics(LootGeneratedEvent gameEvent)
        {
            if (!IsInitialized) return;

            // Emit a lightweight debug log entry for balancing review.
            // A full telemetry sink (CSV, remote analytics) can subscribe to
            // LootGeneratedEvent directly without touching this manager.
            Debug.Log(
                $"[LootManager] Loot generated — container:{gameEvent.ContainerId} items:{gameEvent.ItemCount} weight:{gameEvent.TotalWeight:F1} type:{gameEvent.LocationType} total:{GeneratedLootEventCount}");
        }
    }
}