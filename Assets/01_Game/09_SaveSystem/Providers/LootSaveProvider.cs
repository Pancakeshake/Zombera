using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zombera.Core;
using Zombera.Inventory;

namespace Zombera.Systems
{
    public sealed class LootSaveProvider : MonoBehaviour, ISaveProvider
    {
        [SerializeField] private LootManager lootManager;
        [SerializeField] private ItemSaveRegistry itemSaveRegistry;

        public int Priority => 10; // Load last

        private readonly List<LootContainer> _lootContainerBuffer = new();

        public void OnSave(GameSaveData saveData)
        {
            EnsureReferences();
            if (lootManager == null) return;

            var containers = lootManager.GetTrackedContainers(_lootContainerBuffer);
            foreach (var container in containers)
            {
                if (container == null) continue;

                var containerData = new LootContainerSaveData
                {
                    containerId = container.ContainerId,
                    position = container.transform.position,
                    lootGenerated = container.HasGeneratedLoot
                };

                var generatedLoot = container.GeneratedLoot;
                foreach (var stack in generatedLoot)
                {
                    if (stack.item == null) continue;
                    containerData.itemIds.Add(stack.item.itemId);
                    containerData.quantities.Add(stack.quantity);
                }

                saveData.lootContainers.Add(containerData);
            }
        }

        public void OnLoad(GameSaveData saveData)
        {
            EnsureReferences();
            if (saveData.lootContainers == null || saveData.lootContainers.Count == 0) return;

            var containers = Object.FindObjectsByType<LootContainer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var savedContainerMap = saveData.lootContainers.ToDictionary(c => c.containerId, c => c);

            foreach (var container in containers)
            {
                if (container == null) continue;

                if (savedContainerMap.TryGetValue(container.ContainerId, out var containerData))
                {
                    var restoredStacks = new List<ItemStack>();
                    for (int i = 0; i < containerData.itemIds.Count; i++)
                    {
                        var item = GetItemDefinitionById(containerData.itemIds[i]);
                        if (item != null)
                        {
                            restoredStacks.Add(new ItemStack(item, containerData.quantities[i]));
                        }
                    }
                    container.RestoreLootState(restoredStacks);
                }
            }
        }

        private void EnsureReferences()
        {
            if (lootManager == null) lootManager = Object.FindFirstObjectByType<LootManager>();
            if (itemSaveRegistry == null)
            {
                var registries = Resources.FindObjectsOfTypeAll<ItemSaveRegistry>();
                if (registries.Length > 0) itemSaveRegistry = registries[0];
            }
        }

        private ItemDefinition GetItemDefinitionById(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return null;
            if (itemSaveRegistry != null) return itemSaveRegistry.GetItem(itemId);

            var allItems = Resources.FindObjectsOfTypeAll<ItemDefinition>();
            return allItems.FirstOrDefault(i => i.itemId == itemId);
        }
    }
}