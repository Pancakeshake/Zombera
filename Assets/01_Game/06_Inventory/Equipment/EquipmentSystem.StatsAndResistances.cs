#region

using UnityEngine;

#endregion

namespace Zombera.Inventory
{
    public sealed partial class EquipmentSystem
    {
        private void ApplyStatBonuses(ItemDefinition item)
        {
            if (item == null) return;

            if (unitStats != null)
            {
                if (item.statBonuses != null)
                    for (var i = 0; i < item.statBonuses.Length; i++)
                        unitStats.AddEquipmentBonus(item.statBonuses[i].skill, item.statBonuses[i].flatBonus);

                var armorStatBonuses = item.equippedArmorData != null ? item.equippedArmorData.statBonuses : null;
                if (armorStatBonuses != null)
                    for (var i = 0; i < armorStatBonuses.Length; i++)
                        unitStats.AddEquipmentBonus(armorStatBonuses[i].skill, armorStatBonuses[i].flatBonus);
            }

            ApplyArmorResistances(item, true);
        }

        private void RemoveStatBonuses(ItemDefinition item)
        {
            if (item == null) return;

            if (unitStats != null)
            {
                if (item.statBonuses != null)
                    foreach (var statBonus in item.statBonuses)
                        unitStats.RemoveEquipmentBonus(statBonus.skill, statBonus.flatBonus);

                var armorStatBonuses = item.equippedArmorData != null ? item.equippedArmorData.statBonuses : null;
                if (armorStatBonuses != null)
                    for (var i = 0; i < armorStatBonuses.Length; i++)
                        unitStats.RemoveEquipmentBonus(armorStatBonuses[i].skill, armorStatBonuses[i].flatBonus);
            }

            ApplyArmorResistances(item, false);
        }

        private void ApplyArmorResistances(ItemDefinition item, bool add)
        {
            if (unitHealth == null || item?.equippedArmorData == null || item.equippedArmorData.damageResistances == null)
                return;

            var damageResistances = item.equippedArmorData.damageResistances;
            for (var i = 0; i < damageResistances.Length; i++)
            {
                var entry = damageResistances[i];
                if (Mathf.Abs(entry.resistance) < 0.0001f) continue;

                if (add)
                    unitHealth.AddDamageResistance(entry.damageType, entry.resistance);
                else
                    unitHealth.RemoveDamageResistance(entry.damageType, entry.resistance);
            }
        }
    }
}
