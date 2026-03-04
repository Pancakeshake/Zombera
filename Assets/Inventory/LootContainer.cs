using System.Collections.Generic;
using UnityEngine;
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

        public IReadOnlyList<ItemStack> OpenContainer(int deterministicSeed = 0)
        {
            if (!HasGeneratedLoot)
            {
                GenerateLoot(deterministicSeed);
            }

            return generatedLoot;
        }

        public bool TransferAllTo(InventoryManager targetInventory)
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

                if (!targetInventory.TryAddItem(stack.item, stack.quantity))
                {
                    continue;
                }

                generatedLoot.RemoveAt(i);
                movedAny = true;
            }

            return movedAny;
        }

        private void GenerateLoot(int deterministicSeed)
        {
            generatedLoot.Clear();

            if (lootTableSystem != null)
            {
                generatedLoot.AddRange(lootTableSystem.RollLoot(locationType, rollCount, deterministicSeed));
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

            // TODO: Mark container dirty for save system persistence.
        }
    }
}