using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zombera.Core;
using Zombera.Inventory;

namespace Zombera.Systems
{
    public sealed class PickupSaveProvider : MonoBehaviour, ISaveProvider
    {
        [SerializeField] private ItemSaveRegistry itemSaveRegistry;

        public int Priority => 5; // Load after containers

        public void OnSave(GameSaveData saveData)
        {
            EnsureReferences();
            var pickups = ItemPickup.ActivePickups;

            saveData.loosePickups.Clear();
            foreach (var pickup in pickups)
            {
                if (pickup == null || pickup.ItemDefinition == null) continue;

                saveData.loosePickups.Add(new LoosePickupSaveData
                {
                    itemId = pickup.ItemDefinition.itemId,
                    quantity = pickup.Quantity,
                    position = pickup.transform.position,
                    rotation = pickup.transform.rotation
                });
            }
        }

        public void OnLoad(GameSaveData saveData)
        {
            EnsureReferences();
            if (saveData.loosePickups == null || saveData.loosePickups.Count == 0) return;

            ClearExistingPickups();

            foreach (var data in saveData.loosePickups)
            {
                var item = GetItemDefinitionById(data.itemId);
                if (item == null) continue;

                SpawnPickup(item, data);
            }
        }

        private static void ClearExistingPickups()
        {
            // Copy first: destroying unregisters pickups from the live registry.
            var pickups = new List<ItemPickup>(ItemPickup.ActivePickups);
            foreach (var pickup in pickups)
            {
                if (pickup == null) continue;

                if (Application.isPlaying) Destroy(pickup.gameObject);
                else DestroyImmediate(pickup.gameObject);
            }
        }

        private void SpawnPickup(ItemDefinition item, LoosePickupSaveData data)
        {
            var go = new GameObject($"Pickup_{item.displayName}");
            go.transform.SetPositionAndRotation(data.position, data.rotation);
            
            // Physics Setup
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.45f;

            var pickup = go.AddComponent<ItemPickup>();
            pickup.Initialize(item, data.quantity);
        }

        private void EnsureReferences()
        {
            if (itemSaveRegistry != null) return;

            var registries = Resources.FindObjectsOfTypeAll<ItemSaveRegistry>();
            if (registries.Length > 0) itemSaveRegistry = registries[0];
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