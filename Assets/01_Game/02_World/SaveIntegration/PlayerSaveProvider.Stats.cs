using Zombera.Characters;
using Zombera.Core;
using Zombera.Inventory;

namespace Zombera.Systems
{
    public sealed partial class PlayerSaveProvider
    {
        // ──────────────────────────────────────────────
        //  Stats + inventory serialization
        // ──────────────────────────────────────────────

        private static UnitStatsSaveData BuildUnitStatsSaveData(UnitStats stats)
        {
            var data = new UnitStatsSaveData();
            if (stats == null) return data;

            data.hasSkillProgressionData = true;
            data.stamina = stats.Stamina;
            data.strength = stats.Strength;
            data.strengthXp = stats.GetCurrentExperience(UnitSkillType.Strength);
            data.shooting = stats.Shooting;
            data.shootingXp = stats.GetCurrentExperience(UnitSkillType.Shooting);
            data.melee = stats.Melee;
            data.meleeXp = stats.GetCurrentExperience(UnitSkillType.Melee);
            data.medical = stats.Medical;
            data.medicalXp = stats.GetCurrentExperience(UnitSkillType.Medical);
            data.engineering = stats.Engineering;
            data.engineeringXp = stats.GetCurrentExperience(UnitSkillType.Engineering);
            data.toughness = stats.Toughness;
            data.toughnessXp = stats.GetCurrentExperience(UnitSkillType.Toughness);
            data.constitution = stats.Constitution;
            data.constitutionXp = stats.GetCurrentExperience(UnitSkillType.Constitution);
            data.agility = stats.Agility;
            data.agilityXp = stats.GetCurrentExperience(UnitSkillType.Agility);
            data.endurance = stats.Endurance;
            data.enduranceXp = stats.GetCurrentExperience(UnitSkillType.Endurance);
            data.scavenging = stats.Scavenging;
            data.scavengingXp = stats.GetCurrentExperience(UnitSkillType.Scavenging);
            data.stealth = stats.Stealth;
            data.stealthXp = stats.GetCurrentExperience(UnitSkillType.Stealth);

            return data;
        }

        private static void ApplyUnitStatsSaveData(UnitStats stats, UnitStatsSaveData data)
        {
            if (stats == null || data is not { hasSkillProgressionData: true }) return;

            stats.SetSkillProgress(UnitSkillType.Strength, data.strength, data.strengthXp);
            stats.SetSkillProgress(UnitSkillType.Shooting, data.shooting, data.shootingXp);
            stats.SetSkillProgress(UnitSkillType.Melee, data.melee, data.meleeXp);
            stats.SetSkillProgress(UnitSkillType.Medical, data.medical, data.medicalXp);
            stats.SetSkillProgress(UnitSkillType.Engineering, data.engineering, data.engineeringXp);
            stats.SetSkillProgress(UnitSkillType.Toughness, data.toughness, data.toughnessXp);
            stats.SetSkillProgress(UnitSkillType.Constitution, data.constitution, data.constitutionXp);
            stats.SetSkillProgress(UnitSkillType.Agility, data.agility, data.agilityXp);
            stats.SetSkillProgress(UnitSkillType.Endurance, data.endurance, data.enduranceXp);
            stats.SetSkillProgress(UnitSkillType.Scavenging, data.scavenging, data.scavengingXp);
            stats.SetSkillProgress(UnitSkillType.Stealth, data.stealth, data.stealthXp);

            if (data.stamina > 0f) stats.SetStamina(data.stamina);
        }

        private static InventorySaveData BuildInventorySaveData(UnitInventory inventory)
        {
            var inventoryData = new InventorySaveData();
            var stacks = inventory.Items;

            foreach (var stack in stacks)
            {
                if (stack.item == null) continue;

                inventoryData.itemIds.Add(stack.item.itemId);
                inventoryData.quantities.Add(stack.quantity);

                inventoryData.itemInstances.Add(new ItemInstanceSaveData
                {
                    instanceId = string.Empty,
                    itemId = stack.item.itemId,
                    quantity = stack.quantity,
                    durability = 1f
                });
            }

            inventoryData.currentWeight = inventory.CurrentWeight;
            return inventoryData;
        }
    }
}
