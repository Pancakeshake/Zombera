using System.Collections.Generic;
using UnityEngine;
using Zombera.Core;
using Zombera.Inventory;

namespace Zombera.Systems
{
    /// <summary>
    /// Coordinates container registration, loot generation tracking, and loot analytics.
    /// </summary>
    public sealed class LootManager : MonoBehaviour, IGameSystem
    {
        private readonly HashSet<LootContainer> trackedContainers = new HashSet<LootContainer>();

        public bool IsInitialized { get; private set; }
        public int RegisteredContainerCount => trackedContainers.Count;
        public int GeneratedLootEventCount { get; private set; }

        public void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

            IsInitialized = true;
            GeneratedLootEventCount = 0;
            EventSystem.Instance?.Subscribe<LootGeneratedEvent>(OnLootGenerated);
        }

        public void Shutdown()
        {
            if (!IsInitialized)
            {
                return;
            }

            IsInitialized = false;
            EventSystem.Instance?.Unsubscribe<LootGeneratedEvent>(OnLootGenerated);
            trackedContainers.Clear();
        }

        public void RegisterContainer(LootContainer container)
        {
            if (container == null)
            {
                return;
            }

            trackedContainers.Add(container);
        }

        public void UnregisterContainer(LootContainer container)
        {
            if (container == null)
            {
                return;
            }

            trackedContainers.Remove(container);
        }

        public IReadOnlyList<ItemStack> OpenContainer(LootContainer container, int deterministicSeed = 0)
        {
            if (container == null)
            {
                return System.Array.Empty<ItemStack>();
            }

            RegisterContainer(container);
            return container.OpenContainer(deterministicSeed);
        }

        private void OnLootGenerated(LootGeneratedEvent gameEvent)
        {
            GeneratedLootEventCount++;

            // TODO: Feed loot metrics into balancing telemetry.
            _ = gameEvent;
        }
    }
}