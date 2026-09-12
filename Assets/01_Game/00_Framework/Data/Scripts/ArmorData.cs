#region

using System;
using UnityEngine;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Inventory;

#endregion

namespace Zombera.Data
{
    /// <summary>
    ///     Armor tuning values consumed by EquipmentSystem and UnitHealth.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombera/Data/Armor Data", fileName = "ArmorData")]
    public sealed class ArmorData : ScriptableObject
    {
        public string armorId;
        public string displayName;
        public EquipmentSlot equipSlot = EquipmentSlot.Chest;

        [Min(0f)] public float armorRating = 5f;

        [Tooltip("0-1 damage reduction per damage type while equipped.")]
        public ArmorDamageResistance[] damageResistances = Array.Empty<ArmorDamageResistance>();

        [Tooltip("Flat UnitStats bonuses applied while equipped.")]
        public ArmorStatBonus[] statBonuses = Array.Empty<ArmorStatBonus>();
    }

    [Serializable]
    public struct ArmorDamageResistance
    {
        public DamageType damageType;

        [Range(0f, 1f)]
        public float resistance;
    }

    [Serializable]
    public struct ArmorStatBonus
    {
        public UnitSkillType skill;
        public int flatBonus;
    }
}
