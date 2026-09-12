using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zombera.Core;
using Zombera.Inventory;
using Zombera.BuildingSystem;

namespace Zombera.Systems
{
    public sealed class BuildingSaveProvider : MonoBehaviour, ISaveProvider
    {
        [SerializeField] private BaseManager baseManager;
        [SerializeField] private ItemSaveRegistry itemSaveRegistry;
        [SerializeField] private BuildingSaveRegistry buildingSaveRegistry;
        [SerializeField] private Transform placedPiecesRoot;

        public int Priority => 30; // Load after player but before loot

        public void OnSave(GameSaveData saveData)
        {
            EnsureReferences();
            if (baseManager == null) return;

            // 1. Save Base Metadata & Storage
            var completedIds = baseManager.CompletedBuildingIds;
            var storage = baseManager.GetBaseStorage();

            foreach (var completedId in completedIds)
            {
                var entry = new BaseSaveData
                {
                    baseId = completedId,
                    position = Vector3.zero,
                    buildingState = "Completed"
                };

                if (storage != null)
                {
                    var stacks = storage.MaterialStacks;
                    foreach (var stack in stacks)
                    {
                        if (stack.item == null) continue;
                        entry.storageItemIds.Add(stack.item.itemId);
                        entry.storageQuantities.Add(stack.amount);
                    }
                }

                // 2. Save modular pieces associated with this base (or just all pieces for now)
                saveData.bases.Add(entry);
            }

            // Capture all BuildPiece instances in the world
            var allPieces = Object.FindObjectsByType<BuildPiece>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (saveData.bases.Count > 0)
            {
                var primaryBase = saveData.bases[0];
                foreach (var piece in allPieces)
                {
                    primaryBase.placedPieces.Add(new PlacedPieceSaveData
                    {
                        prefabName = piece.gameObject.name.Replace("(Clone)", "").Trim(),
                        position = piece.transform.position,
                        rotation = piece.transform.rotation,
                        health = piece.Health != null ? piece.Health.CurrentHealth : 100f
                    });
                }
            }
        }

        public void OnLoad(GameSaveData saveData)
        {
            EnsureReferences();
            if (saveData.bases == null || saveData.bases.Count == 0) return;

            // 1. Restore Blueprints
            var blueprints = Object.FindObjectsByType<Zombera.BaseBuilding.Blueprint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var completedIds = new HashSet<string>(saveData.bases.Select(b => b.baseId));

            foreach (var blueprint in blueprints)
            {
                if (blueprint == null || blueprint.BuildingData == null) continue;
                if (completedIds.Contains(blueprint.BuildingData.buildingId))
                {
                    blueprint.MarkCompleted();
                }
            }

            // 2. Restore Storage
            var storage = baseManager != null ? baseManager.GetBaseStorage() : null;
            if (storage != null && saveData.bases.Count > 0)
            {
                storage.ClearStorage();
                var firstBase = saveData.bases[0];
                for (int i = 0; i < firstBase.storageItemIds.Count; i++)
                {
                    var item = GetItemDefinitionById(firstBase.storageItemIds[i]);
                    if (item != null)
                    {
                        storage.AddMaterial(item, firstBase.storageQuantities[i]);
                    }
                }
            }

            // 3. Restore Modular Pieces
            ClearExistingPieces();
            foreach (var baseData in saveData.bases)
            {
                foreach (var pieceData in baseData.placedPieces)
                {
                    SpawnPiece(pieceData);
                }
            }
        }

        private void ClearExistingPieces()
        {
            var allPieces = Object.FindObjectsByType<BuildPiece>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var piece in allPieces)
            {
                if (Application.isPlaying) Destroy(piece.gameObject);
                else DestroyImmediate(piece.gameObject);
            }
        }

        private void SpawnPiece(PlacedPieceSaveData data)
        {
            if (buildingSaveRegistry == null)
            {
                Debug.LogError("[BuildingSaveProvider] BuildingSaveRegistry is not assigned!");
                return;
            }

            var prefab = buildingSaveRegistry.GetPiecePrefab(data.prefabName);
            if (prefab == null)
            {
                Debug.LogWarning($"[BuildingSaveProvider] Could not find prefab for piece: {data.prefabName}");
                return;
            }

            var pieceObj = Instantiate(prefab, data.position, data.rotation, placedPiecesRoot);
            
            // Restore Health
            var health = pieceObj.GetComponent<StructureHealth>();
            if (health != null)
            {
                // We use TakeDamage to effectively set the health if we want to be safe, 
                // but since it's a new object, we can just use a private field setter or a public method if available.
                // StructureHealth has ResetHealthToMax and TakeDamage.
                // We'll use a hacky but effective way if there's no SetHealth method.
                // Looking at StructureHealth, it doesn't have a public SetCurrentHealth.
                // It has Heal and TakeDamage.
                
                health.ResetHealthToMax();
                float damageToApply = health.MaxHealth - data.health;
                if (damageToApply > 0)
                {
                    health.TakeDamage(damageToApply);
                }
            }
        }

        private void EnsureReferences()
        {
            if (baseManager == null) baseManager = Object.FindFirstObjectByType<BaseManager>();
            if (itemSaveRegistry == null) itemSaveRegistry = FindItemSaveRegistry();
            if (buildingSaveRegistry == null)
            {
                var registries = Resources.FindObjectsOfTypeAll<BuildingSaveRegistry>();
                if (registries.Length > 0) buildingSaveRegistry = registries[0];
            }
        }

        private static ItemSaveRegistry FindItemSaveRegistry()
        {
            var registries = Resources.FindObjectsOfTypeAll<ItemSaveRegistry>();
            return registries.Length > 0 ? registries[0] : null;
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