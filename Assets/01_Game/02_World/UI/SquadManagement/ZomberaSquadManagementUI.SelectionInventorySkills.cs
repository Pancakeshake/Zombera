#region

using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.Inventory;
using Zombera.Systems;

#endregion

namespace Zombera.UI.SquadManagement
{
    public sealed partial class ZomberaSquadManagementUI
    {
        private void ApplySelectionDataFromContext(LiveSurvivorContext context)
        {
            RefreshSelectionCardFromContext(context);
            BuildInventoryForContext(context);
            BuildSkillsForContext(context);
            inventoryTab?.SetSlots(_inventorySlots);
            skillsTab?.SetSkills(_skillEntries);
            
            if (context != null)
            {
                craftingTab?.SetContext(context.Unit, context.Unit?.Inventory);
            }
        }

        private void RefreshSelectionCardFromEntry(SquadListPanelController.SurvivorEntryData data)
        {
            if (selectedConditionText != null) selectedConditionText.text = "Condition: " + data.condition;

            if (selectedHealthText != null)
            {
                var healthPercent = Mathf.RoundToInt(Mathf.Clamp01(data.health01) * 100f);
                selectedHealthText.text = "Health: " + healthPercent + "%";
            }
        }

        private void RefreshSelectionCardFromContext(LiveSurvivorContext context)
        {
            var unit = context != null ? context.Unit : null;
            var health = unit != null ? unit.Health : null;

            if (health == null || health.MaxHealth <= 0f)
            {
                if (_selectedSurvivorIndex >= 0 && _selectedSurvivorIndex < _survivors.Count)
                    RefreshSelectionCardFromEntry(_survivors[_selectedSurvivorIndex]);

                return;
            }

            var health01 = Mathf.Clamp01(health.CurrentHealth / health.MaxHealth);

            if (selectedConditionText != null) selectedConditionText.text = "Condition: " + ResolveCondition(health01);

            if (selectedHealthText != null)
                selectedHealthText.text = "Health: "
                                          + Mathf.CeilToInt(health.CurrentHealth)
                                          + " / "
                                          + Mathf.CeilToInt(health.MaxHealth);
        }

        private void BuildInventoryForContext(LiveSurvivorContext context)
        {
            _inventorySlots.Clear();

            var unit = context != null ? context.Unit : null;
            var unitInventory = unit != null ? unit.Inventory : null;
            if (unitInventory == null) return;

            var stacks = unitInventory.Items;
            for (var i = 0; i < stacks.Count; i++)
            {
                var stack = stacks[i];
                if (stack.item == null || stack.quantity <= 0) continue;

                var itemName = ResolveItemName(stack.item);
                _inventorySlots.Add(new InventoryTabController.InventorySlotData(
                    itemName,
                    stack.item.inventoryIcon,
                    stack.quantity,
                    InventoryTabController.InventorySlotState.Occupied));
            }
        }

        private static string ResolveItemName(ItemDefinition item)
        {
            if (item == null) return "Unknown Item";

            if (!string.IsNullOrWhiteSpace(item.displayName)) return item.displayName.Trim();

            if (!string.IsNullOrWhiteSpace(item.itemId)) return item.itemId.Trim();

            return item.name;
        }

        private void BuildSkillsForContext(LiveSurvivorContext context)
        {
            _skillEntries.Clear();

            var unit = context != null ? context.Unit : null;
            var stats = unit != null ? unit.Stats : null;
            if (stats != null)
            {
                AddStatSkill("Shooting", "Combat", stats.Shooting, "Ranged proficiency and recoil control.");
                AddStatSkill("Melee", "Combat", stats.Melee, "Close-quarters efficiency and timing.");
                AddStatSkill("Strength", "Combat", stats.Strength, "Carry power and physical output.");
                AddStatSkill("Medical", "Support", stats.Medical, "Healing and stabilisation capability.");
                AddStatSkill("Engineering", "Support", stats.Engineering, "Repair and technical handling.");
            }

            var survivorController = context != null ? context.SurvivorController : null;
            if (survivorController != null)
            {
                var traits = survivorController.Traits;
                for (var i = 0; i < traits.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(traits[i])) continue;

                    var trait = traits[i].Trim();
                    _skillEntries.Add(new SkillsTabController.SkillEntryData(
                        trait,
                        "Traits",
                        SkillsTabController.SkillState.Passive,
                        true,
                        1,
                        "Innate survivor trait."));
                }
            }

            if (_skillEntries.Count == 0)
                _skillEntries.Add(new SkillsTabController.SkillEntryData(
                    "Untrained",
                    "General",
                    SkillsTabController.SkillState.Unlocked,
                    false,
                    1,
                    "No advanced skill profile is available for this unit."));
        }

        private void AddStatSkill(string statName, string category, int value, string description)
        {
            var clamped = Mathf.Clamp(value, 0, 100);
            var rank = Mathf.Clamp(Mathf.RoundToInt(clamped / 20f), 0, 5);

            _skillEntries.Add(new SkillsTabController.SkillEntryData(
                statName,
                category,
                SkillStateFromValue(clamped),
                !string.Equals(category, "Combat", StringComparison.Ordinal),
                rank,
                description + " Value: " + clamped + "."));
        }

        private static SkillsTabController.SkillState SkillStateFromValue(int value)
        {
            if (value >= 75) return SkillsTabController.SkillState.Active;

            if (value >= 35) return SkillsTabController.SkillState.Unlocked;

            return SkillsTabController.SkillState.Locked;
        }

        private void SeedDemoData()
        {
            _selectedSurvivorIndex = -1;
            _survivors.Clear();
            _survivors.Add(new SquadListPanelController.SurvivorEntryData("u01", "Mara Voss", null, 0.84f, 0.72f,
                SquadListPanelController.SurvivorCondition.Stable));
            _survivors.Add(new SquadListPanelController.SurvivorEntryData("u02", "Eli Griggs", null, 0.58f, 0.46f,
                SquadListPanelController.SurvivorCondition.Wounded));
            _survivors.Add(new SquadListPanelController.SurvivorEntryData("u03", "Noa Pike", null, 0.93f, 0.88f,
                SquadListPanelController.SurvivorCondition.Stable));
            _survivors.Add(new SquadListPanelController.SurvivorEntryData("u04", "Rook Hale", null, 0.44f, 0.29f,
                SquadListPanelController.SurvivorCondition.Exhausted));
            _survivors.Add(new SquadListPanelController.SurvivorEntryData("u05", "Iris Dune", null, 0.27f, 0.34f,
                SquadListPanelController.SurvivorCondition.Critical));

            squadListPanel?.SetEntries(_survivors);

            _inventorySlots.Clear();
            _inventorySlots.Add(new InventoryTabController.InventorySlotData("9mm Ammo", null, 48,
                InventoryTabController.InventorySlotState.Occupied));
            _inventorySlots.Add(new InventoryTabController.InventorySlotData("Bandage Roll", null, 5,
                InventoryTabController.InventorySlotState.Equipped));
            _inventorySlots.Add(new InventoryTabController.InventorySlotData("Rust Knife", null, 1,
                InventoryTabController.InventorySlotState.Equipped));
            _inventorySlots.Add(new InventoryTabController.InventorySlotData("Water Flask", null, 2,
                InventoryTabController.InventorySlotState.Occupied));
            _inventorySlots.Add(new InventoryTabController.InventorySlotData("Scrap Metal", null, 17,
                InventoryTabController.InventorySlotState.Occupied));
            _inventorySlots.Add(new InventoryTabController.InventorySlotData("Molotov", null, 1,
                InventoryTabController.InventorySlotState.Occupied));
            _inventorySlots.Add(new InventoryTabController.InventorySlotData("Broken Radio", null, 1,
                InventoryTabController.InventorySlotState.Damaged));
            _inventorySlots.Add(new InventoryTabController.InventorySlotData("Painkillers", null, 4,
                InventoryTabController.InventorySlotState.Occupied));
            _inventorySlots.Add(new InventoryTabController.InventorySlotData("Ration Pack", null, 6,
                InventoryTabController.InventorySlotState.Occupied));

            inventoryTab?.SetSlots(_inventorySlots);

            _skillEntries.Clear();
            _skillEntries.Add(new SkillsTabController.SkillEntryData("Steady Aim", "Combat",
                SkillsTabController.SkillState.Active, false, 2, "Improves ranged accuracy while crouched."));
            _skillEntries.Add(new SkillsTabController.SkillEntryData("Bleed Control", "Survival",
                SkillsTabController.SkillState.Passive, true, 1, "Bandages stop bleed effects faster."));
            _skillEntries.Add(new SkillsTabController.SkillEntryData("Silent Steps", "Survival",
                SkillsTabController.SkillState.Unlocked, false, 1, "Lowers movement noise by 20 percent."));
            _skillEntries.Add(new SkillsTabController.SkillEntryData("Command Presence", "Leadership",
                SkillsTabController.SkillState.Passive, true, 3,
                "Nearby squad members perform better under pressure."));
            _skillEntries.Add(new SkillsTabController.SkillEntryData("Frontline Push", "Leadership",
                SkillsTabController.SkillState.Locked, false, 0, "Temporarily boosts squad aggression."));
            _skillEntries.Add(new SkillsTabController.SkillEntryData("Field Repair", "Support",
                SkillsTabController.SkillState.Unlocked, false, 1, "Repair damaged gear from scrap."));
            skillsTab?.SetSkills(_skillEntries);

            var names = new List<string>(_survivors.Count);
            for (var i = 0; i < _survivors.Count; i++) names.Add(_survivors[i].displayName);

            squadCustomiserTab?.SetSquadName("Squad 1");
            squadCustomiserTab?.SetMembers(names);

            RefreshTopStatus();
            if (_survivors.Count > 0)
            {
                _selectedSurvivorIndex = 0;
                HandleSurvivorSelection(0, _survivors[0]);
                squadListPanel?.SelectIndex(0);
            }
        }

        private void HandleSurvivorSelection(int index, SquadListPanelController.SurvivorEntryData data)
        {
            _selectedSurvivorIndex = index;

            if (selectedNameText != null) selectedNameText.text = data.displayName;

            RefreshSelectionCardFromEntry(data);

            if (selectedPortraitInitialText != null)
                selectedPortraitInitialText.text = GetInitial(data.displayName);

            inventoryTab?.SetContextSurvivor(data.displayName);
            skillsTab?.SetContextSurvivor(data.displayName);
            craftingTab?.SetContextSurvivor(data.displayName);
            squadCustomiserTab?.SetSelectedIndex(index);

            UnsubscribeFromEquipment();

            if (useLiveGameData && index >= 0 && index < _liveSurvivorContexts.Count)
            {
                var context = _liveSurvivorContexts[index];
                ApplySelectionDataFromContext(context);
                
                if (context.Unit != null)
                {
                    if (portraitStudio != null &&
                        portraitStudio.TryGetCachedPortrait(ResolvePortraitUnitKey(context.Unit), out _))
                    {
                        ApplySelectedPortraitDisplay(true);
                    }
                    else
                    {
                        RequestPortraitForUnit(context.Unit);
                    }

                    var avatar = context.Unit.GetComponentInChildren<UMA.CharacterSystem.DynamicCharacterAvatar>();
                    if (avatar != null &&
                        context.Unit.TryGetComponent<EquipmentSystem>(out var equipment))
                    {
                        _subscribedEquipment = equipment;
                        _subscribedAvatar = avatar;
                        _subscribedEquipment.OnEquipmentChanged += HandlePortraitRefreshRequest;
                    }
                }
            }
        }

        private void UnsubscribeFromEquipment()
        {
            if (_subscribedEquipment != null)
                _subscribedEquipment.OnEquipmentChanged -= HandlePortraitRefreshRequest;

            _subscribedEquipment = null;
            _subscribedAvatar = null;
        }

        private void HandlePortraitRefreshRequest()
        {
            if (_selectedSurvivorIndex < 0 || _selectedSurvivorIndex >= _liveSurvivorContexts.Count) return;

            var unit = _liveSurvivorContexts[_selectedSurvivorIndex].Unit;
            if (unit != null)
                RequestPortraitForUnit(unit);
        }

        private void RefreshTopStatus()
        {
            if (_survivors.Count == 0) return;

            var health = 0f;
            var criticalCount = 0;
            for (var i = 0; i < _survivors.Count; i++)
            {
                health += _survivors[i].health01;
                if (_survivors[i].condition == SquadListPanelController.SurvivorCondition.Critical) criticalCount++;
            }

            health /= _survivors.Count;

            if (conditionValueText != null)
            {
                if (health > 0.72f)
                    conditionValueText.text = "Holding";
                else if (health > 0.42f)
                    conditionValueText.text = "Strained";
                else
                    conditionValueText.text = "Fragile";
            }

            if (suppliesValueText != null)
            {
                var usableSlotCount = 0;

                if (useLiveGameData && _liveSurvivorContexts.Count > 0)
                    for (var i = 0; i < _liveSurvivorContexts.Count; i++)
                    {
                        var unit = _liveSurvivorContexts[i].Unit;
                        var inventory = unit != null ? unit.Inventory : null;
                        if (inventory == null) continue;

                        var stacks = inventory.Items;
                        for (var j = 0; j < stacks.Count; j++)
                            if (stacks[j].item != null && stacks[j].quantity > 0)
                                usableSlotCount++;
                    }
                else
                    for (var i = 0; i < _inventorySlots.Count; i++)
                        if (_inventorySlots[i].state != InventoryTabController.InventorySlotState.Empty)
                            usableSlotCount++;

                string suppliesText;
                if (usableSlotCount >= 8)
                    suppliesText = "Stable";
                else if (usableSlotCount >= 4)
                    suppliesText = "Scarce";
                else
                    suppliesText = "Critical";

                suppliesValueText.text = suppliesText;
            }

            if (threatValueText != null)
            {
                string threatText;
                if (criticalCount >= 2)
                    threatText = "Severe";
                else if (criticalCount == 1)
                    threatText = "Elevated";
                else
                    threatText = "Watchful";

                threatValueText.text = threatText;
            }
        }
    }
}
