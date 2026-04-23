using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.Core;

namespace Zombera.Inventory
{
    /// <summary>
    /// Container that generates loot on first open using location-based loot tables.
    /// </summary>
    public sealed class LootContainer : MonoBehaviour
    {
        [SerializeField] private string containerId;
        [SerializeField] private LootLocationType locationType;
        [SerializeField] private int rollCount = 3;
        [SerializeField] private LootTable lootTableSystem;

        private readonly List<ItemStack> generatedLoot = new List<ItemStack>();

        public string ContainerId => containerId;
        public bool HasGeneratedLoot { get; private set; }
        public IReadOnlyList<ItemStack> GeneratedLoot => generatedLoot;

        public IReadOnlyList<ItemStack> OpenContainer(int deterministicSeed = 0, float rollMultiplier = 1f)
        {
            if (!HasGeneratedLoot)
            {
                GenerateLoot(deterministicSeed, rollMultiplier);
            }

            return generatedLoot;
        }

        public bool TransferAllTo(IInventoryHolder targetInventory)
        {
            if (targetInventory == null)
            {
                return false;
            }

            bool movedAny = false;

            for (int i = generatedLoot.Count - 1; i >= 0; i--)
            {
                ItemStack stack = generatedLoot[i];

                if (stack.item == null || stack.quantity <= 0)
                {
                    generatedLoot.RemoveAt(i);
                    continue;
                }

                if (!targetInventory.AddItem(stack.item, stack.quantity))
                {
                    continue;
                }

                generatedLoot.RemoveAt(i);
                movedAny = true;
            }

            return movedAny;
        }

        private void GenerateLoot(int deterministicSeed, float rollMultiplier = 1f)
        {
            generatedLoot.Clear();

            if (lootTableSystem != null)
            {
                int scaledRollCount = Mathf.Max(1, Mathf.RoundToInt(rollCount * Mathf.Max(1f, rollMultiplier)));
                generatedLoot.AddRange(lootTableSystem.RollLoot(locationType, scaledRollCount, deterministicSeed));
            }

            HasGeneratedLoot = true;

            float totalWeight = 0f;

            for (int i = 0; i < generatedLoot.Count; i++)
            {
                totalWeight += generatedLoot[i].GetTotalWeight();
            }

            EventSystem.PublishGlobal(new LootGeneratedEvent
            {
                ContainerId = containerId,
                LocationType = locationType,
                ItemCount = generatedLoot.Count,
                TotalWeight = totalWeight,
                Position = transform.position
            });

            // Mark the container as having generated loot so save/load can persist this state.
            // Listening save systems subscribe to LootGeneratedEvent and record the containerId
            // in the world save data; on load they restore HasGeneratedLoot via RestoreLootState().
            HasGeneratedLoot = true;
        }

        /// <summary>
        /// Restores persisted loot state during a world load.
        /// Call from the save system after populating GeneratedLoot from disk.
        /// </summary>
        public void RestoreLootState(IEnumerable<ItemStack> savedLoot)
        {
            generatedLoot.Clear();

            if (savedLoot != null)
            {
                foreach (ItemStack stack in savedLoot)
                {
                    generatedLoot.Add(stack);
                }
            }

            HasGeneratedLoot = true;
        }
    }
}