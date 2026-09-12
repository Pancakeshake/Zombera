#region

using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.Core;

#endregion

namespace Zombera.Inventory
{
    /// <summary>
    ///     Container that generates loot on first open using location-based loot tables.
    /// </summary>
    public sealed class LootContainer : MonoBehaviour
    {
        [SerializeField] private string containerId;
        [SerializeField] private LootLocationType locationType;
        [SerializeField] private int rollCount = 3;
        [SerializeField] private LootTable lootTableSystem;

        private readonly List<ItemStack> _generatedLoot = new();

        public string ContainerId => containerId;
        public bool HasGeneratedLoot { get; private set; }
        public IReadOnlyList<ItemStack> GeneratedLoot => _generatedLoot;

        public bool HasLootRemaining()
        {
            if (!HasGeneratedLoot) return true;

            for (var i = 0; i < _generatedLoot.Count; i++)
            {
                var stack = _generatedLoot[i];
                if (stack.item != null && stack.quantity > 0) return true;
            }

            return false;
        }

        public IReadOnlyList<ItemStack> OpenContainer(int deterministicSeed = 0, float rollMultiplier = 1f)
        {
            if (!HasGeneratedLoot) GenerateLoot(deterministicSeed, rollMultiplier);

            return _generatedLoot;
        }

        public bool TransferAllTo(IInventoryHolder targetInventory)
        {
            if (targetInventory == null) return false;

            var movedAny = false;

            for (var i = _generatedLoot.Count - 1; i >= 0; i--)
            {
                var stack = _generatedLoot[i];

                if (stack.item == null || stack.quantity <= 0)
                {
                    _generatedLoot.RemoveAt(i);
                    continue;
                }

                if (!targetInventory.AddItem(stack.item, stack.quantity)) continue;

                _generatedLoot.RemoveAt(i);
                movedAny = true;
            }

            return movedAny;
        }

        private void GenerateLoot(int deterministicSeed, float rollMultiplier = 1f)
        {
            _generatedLoot.Clear();

            if (lootTableSystem != null)
            {
                var scaledRollCount = Mathf.Max(1, Mathf.RoundToInt(rollCount * Mathf.Max(1f, rollMultiplier)));
                _generatedLoot.AddRange(lootTableSystem.RollLoot(locationType, scaledRollCount, deterministicSeed));
            }

            HasGeneratedLoot = true;

            var totalWeight = 0f;

            for (var i = 0; i < _generatedLoot.Count; i++) totalWeight += _generatedLoot[i].GetTotalWeight();

            CoreEventBus.PublishGlobal(new LootGeneratedEvent
            {
                ContainerId = containerId,
                LocationType = locationType,
                ItemCount = _generatedLoot.Count,
                TotalWeight = totalWeight,
                Position = transform.position
            });

            // Mark the container as having generated loot so save/load can persist this state.
            // Listening save systems subscribe to LootGeneratedEvent and record the containerId
            // in the world save data; on load they restore HasGeneratedLoot via RestoreLootState().
            HasGeneratedLoot = true;
        }

        /// <summary>
        ///     Clears all loot state so a pooled container instance can be reused as a fresh,
        ///     never-opened container.
        /// </summary>
        public void ResetForReuse()
        {
            _generatedLoot.Clear();
            HasGeneratedLoot = false;
        }

        /// <summary>
        ///     Restores persisted loot state during a world load.
        ///     Call from the save system after populating GeneratedLoot from disk.
        /// </summary>
        public void RestoreLootState(IEnumerable<ItemStack> savedLoot)
        {
            _generatedLoot.Clear();

            if (savedLoot != null)
                foreach (var stack in savedLoot)
                    _generatedLoot.Add(stack);

            HasGeneratedLoot = true;
        }
    }
}