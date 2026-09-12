using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zombera.Characters;
using Zombera.Systems;
using Zombera.Building.Crafting;

namespace Zombera.Inventory.Crafting
{
    /// <summary>
    /// Global service for managing the crafting queue and executing crafting logic.
    /// </summary>
    public class CraftingService : MonoBehaviour
    {
        private static CraftingService _instance;
        public static CraftingService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<CraftingService>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("CraftingService");
                        _instance = go.AddComponent<CraftingService>();
                    }
                }
                return _instance;
            }
        }

        [SerializeField]
        private List<CraftingQueueEntry> queue = new List<CraftingQueueEntry>();

        public IReadOnlyList<CraftingQueueEntry> Queue => queue;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            TickQueue(Time.deltaTime);
        }

        /// <summary>
        /// Checks if a recipe can be crafted immediately by a unit with a given inventory.
        /// </summary>
        public bool CanCraftNow(CraftingRecipe recipe, IUnit unit, IInventoryHolder inventory)
        {
            if (recipe == null || inventory == null) return false;

            // Check station requirements
            if (!IsStationRequirementMet(recipe, unit)) return false;

            // Check skill level requirements
            if (!string.IsNullOrEmpty(recipe.requiredSkillType))
            {
                if (Enum.TryParse<UnitSkillType>(recipe.requiredSkillType, true, out var skillType))
                {
                    if (unit != null && unit.Stats != null)
                    {
                        if (unit.Stats.GetSkillLevel(skillType) < recipe.requiredSkillLevel)
                        {
                            return false;
                        }
                    }
                    else if (recipe.requiredSkillLevel > 0)
                    {
                        return false;
                    }
                }
            }

            // Check ingredients and weight capacity
            return CraftingInventoryTransaction.CanFulfill(recipe, inventory);
        }

        /// <summary>
        /// Instantly executes a crafting recipe, bypassing the queue.
        /// </summary>
        public bool CraftInstant(CraftingRecipe recipe, IInventoryHolder inventory, int batchSize = 1)
        {
            return CraftingInventoryTransaction.Execute(recipe, inventory, batchSize);
        }

        /// <summary>
        /// Adds a crafting task to the queue.
        /// </summary>
        public void QueueCraft(CraftingRecipe recipe, IUnit unit, IInventoryHolder inventory, int quantity)
        {
            if (recipe == null || inventory == null || quantity <= 0) return;

            // Check station requirements
            if (!IsStationRequirementMet(recipe, unit))
            {
                Debug.LogWarning($"[CraftingService] Cannot queue {recipe.displayName}: Required station {recipe.requiredStationType} not found or not powered near unit.");
                return;
            }

            float duration = recipe.baseCraftSeconds;
            
            // Apply skill speed bonuses: 2% faster per level
            if (unit != null && unit.Stats != null && !string.IsNullOrEmpty(recipe.requiredSkillType))
            {
                if (Enum.TryParse<UnitSkillType>(recipe.requiredSkillType, true, out var skillType))
                {
                    int skillLevel = unit.Stats.GetSkillLevel(skillType);
                    // Formula: craftSpeedMultiplier = 1 + (skillLevel * 0.02)
                    float speedMultiplier = 1f + (skillLevel * 0.02f);
                    
                    // Integrate Traits
                    if (unit is Component unitComp)
                    {
                        var survivor = unitComp.GetComponent<Zombera.Systems.SurvivorController>();
                        if (survivor != null)
                        {
                            if (survivor.Traits.Contains("Handy")) speedMultiplier += 0.25f; // 25% bonus
                            if (survivor.Traits.Contains("Expert")) speedMultiplier += 0.50f; // 50% bonus
                        }
                    }

                    duration /= speedMultiplier;
                }
            }

            var entry = new CraftingQueueEntry(recipe.recipeId, quantity, duration, unit?.UnitId);
            
            // If it's the only item in the queue for this unit/station, start it immediately
            if (!HasActiveEntryForCrafter(entry.crafterUnitId))
            {
                entry.status = CraftingStatus.Active;
                entry.startedAtGameTime = Time.timeAsDouble;
            }

            queue.Add(entry);
        }

        private bool IsStationRequirementMet(CraftingRecipe recipe, IUnit unit)
        {
            if (recipe.requiredStationType == CraftingStationType.None) return true;

            Vector3 unitPos = Vector3.zero;
            if (unit is Component comp)
            {
                unitPos = comp.transform.position;
            }
            else if (unit != null && UnitManager.HasInstance)
            {
                var activeUnit = UnitManager.Instance.GetAllActiveUnits().FirstOrDefault(u => u.UnitId == unit.UnitId);
                if (activeUnit != null) unitPos = activeUnit.transform.position;
            }

            return CraftingStation.FindAvailable(recipe.requiredStationType, unitPos) != null;
        }

        private bool HasActiveEntryForCrafter(string crafterUnitId)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                var e = queue[i];
                if (e.crafterUnitId == crafterUnitId && e.status == CraftingStatus.Active)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Updates the progress of active crafting entries and handles completion.
        /// </summary>
        public void TickQueue(float deltaTime)
        {
            if (queue.Count == 0) return;

            // Use a copy of the list to allow modification during iteration if needed
            // though we are mostly updating properties.
            for (int i = 0; i < queue.Count; i++)
            {
                var entry = queue[i];

                if (entry.status == CraftingStatus.Queued)
                {
                    // If nothing else is active for this crafter, start this one
                    if (!HasActiveEntryForCrafter(entry.crafterUnitId))
                    {
                        entry.status = CraftingStatus.Active;
                        entry.startedAtGameTime = Time.timeAsDouble;
                    }
                }

                if (entry.status == CraftingStatus.Active)
                {
                    entry.progressSeconds += deltaTime;
                    
                    if (entry.progressSeconds >= entry.perItemDurationSeconds)
                    {
                        if (TryCompleteItem(entry))
                        {
                            entry.progressSeconds = 0; // Reset for next item in batch
                            entry.quantityRemaining--;

                            if (entry.quantityRemaining <= 0)
                            {
                                entry.status = CraftingStatus.Complete;
                            }
                        }
                        else
                        {
                            // Failed to complete (e.g., inventory full or ingredients missing)
                            entry.status = CraftingStatus.Failed;
                        }
                    }
                }
            }
        }

        private bool TryCompleteItem(CraftingQueueEntry entry)
        {
            var recipe = CraftingRecipeRegistry.Instance.GetRecipeById(entry.recipeId);
            if (recipe == null)
            {
                Debug.LogError($"[CraftingService] Recipe not found: {entry.recipeId}");
                return false;
            }

            IInventoryHolder inventory = ResolveInventory(entry);
            if (inventory == null)
            {
                Debug.LogError($"[CraftingService] Could not resolve inventory for entry: {entry.queueEntryId}");
                return false;
            }

            if (CraftingInventoryTransaction.Execute(recipe, inventory, 1))
            {
                // Grant XP if a unit was involved
                if (!string.IsNullOrEmpty(entry.crafterUnitId) && UnitManager.HasInstance)
                {
                    var unit = UnitManager.Instance.GetAllActiveUnits().FirstOrDefault(u => u.UnitId == entry.crafterUnitId);
                    if (unit != null && unit.Stats != null && !string.IsNullOrEmpty(recipe.requiredSkillType))
                    {
                        if (Enum.TryParse<UnitSkillType>(recipe.requiredSkillType, true, out var skillType))
                        {
                            // Baseline XP: 10 + recipeLevel * 2
                            float xp = 10f + (recipe.requiredSkillLevel * 2f);
                            unit.Stats.RecordCraftingExperience(skillType, xp);
                        }
                    }
                }
                return true;
            }
            return false;
        }

        private IInventoryHolder ResolveInventory(CraftingQueueEntry entry)
        {
            // If unit ID is provided, try to find the unit's inventory
            if (!string.IsNullOrEmpty(entry.crafterUnitId))
            {
                if (UnitManager.HasInstance)
                {
                    var unit = UnitManager.Instance.GetAllActiveUnits().FirstOrDefault(u => u.UnitId == entry.crafterUnitId);
                    if (unit != null) return unit.Inventory;
                }
            }

            // Fallback to global InventoryManager
            return FindFirstObjectByType<InventoryManager>();
        }

        /// <summary>
        /// Cancels a specific entry in the queue.
        /// </summary>
        public void CancelEntry(string entryId)
        {
            var entry = queue.FirstOrDefault(e => e.queueEntryId == entryId);
            if (entry != null)
            {
                entry.status = CraftingStatus.Cancelled;
            }
        }

        /// <summary>
        /// Removes all completed, failed, or cancelled entries from the queue.
        /// </summary>
        public void ClearFinishedEntries()
        {
            queue.RemoveAll(e => e.status == CraftingStatus.Complete || 
                                 e.status == CraftingStatus.Failed || 
                                 e.status == CraftingStatus.Cancelled);
        }

        // Internal method to set the queue, used by persistence
        /// <summary>Replaces the crafting queue wholesale; used by save/load restore.</summary>
        public void SetQueue(List<CraftingQueueEntry> newQueue)
        {
            queue = newQueue ?? new List<CraftingQueueEntry>();
        }
    }
}

