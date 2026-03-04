using System.Collections.Generic;
using UnityEngine;
using Zombera.Inventory;

namespace Zombera.Characters
{
    /// <summary>
    /// Contract for objects that can receive health damage.
    /// </summary>
    public interface IDamageable
    {
        bool IsDead { get; }
        void TakeDamage(float amount, GameObject source = null);
        void Heal(float amount);
        void Die();
    }

    /// <summary>
    /// Contract for combat-capable units.
    /// </summary>
    public interface IAttackable
    {
        bool Attack(IReadOnlyList<IDamageable> visibleTargets);
        void Reload();
        IDamageable ChooseTarget(IReadOnlyList<IDamageable> visibleTargets);
    }

    /// <summary>
    /// Contract for components exposing inventory and carry weight behavior.
    /// </summary>
    public interface IInventoryHolder
    {
        float WeightLimit { get; }
        float CurrentWeight { get; }
        bool AddItem(ItemDefinition itemDefinition, int quantity);
        bool RemoveItem(ItemDefinition itemDefinition, int quantity);
        float GetWeight();
    }

    /// <summary>
    /// Contract for the root unit composition object.
    /// </summary>
    public interface IUnit
    {
        string UnitId { get; }
        UnitRole Role { get; }
        UnitController Controller { get; }
        UnitHealth Health { get; }
        UnitCombat Combat { get; }
        UnitInventory Inventory { get; }
        UnitStats Stats { get; }
        MonoBehaviour OptionalAI { get; }
    }
}