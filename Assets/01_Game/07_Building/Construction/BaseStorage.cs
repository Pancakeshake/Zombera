#region

using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Inventory;

#endregion

// ReSharper disable Unity.PerformanceCriticalCodeNullComparison

namespace Zombera.BaseBuilding
{
    /// <summary>
    ///     Stores base materials/resources used by construction jobs.
    ///     Supports optional capacity limits and a reservation system for pending construction.
    /// </summary>
    public sealed class BaseStorage : MonoBehaviour
    {
        [SerializeField] private List<MaterialStack> materialStacks = new();

        [Tooltip("Maximum units of any single material type. 0 = unlimited.")] [SerializeField] [Min(0)]
        private int maxPerMaterialType;

        // Reservation table: how many units of each item type are reserved by pending jobs.
        private readonly Dictionary<ItemDefinition, int> _reservations = new();

        public IReadOnlyList<MaterialStack> MaterialStacks => materialStacks;

        // ReSharper disable once MemberCanBePrivate.Global
        public void AddMaterial(ItemDefinition itemDefinition, int amount)
        {
            if (itemDefinition == null || amount <= 0) return;

            for (var i = 0; i < materialStacks.Count; i++)
                if (materialStacks[i].item == itemDefinition)
                {
                    var stack = materialStacks[i];
                    var newAmount = stack.amount + amount;
                    if (maxPerMaterialType > 0)
                        newAmount = Mathf.Min(newAmount, maxPerMaterialType);
                    stack.amount = newAmount;
                    materialStacks[i] = stack;
                    return;
                }

            var capped = maxPerMaterialType > 0 ? Mathf.Min(amount, maxPerMaterialType) : amount;
            materialStacks.Add(new MaterialStack(itemDefinition, capped));
        }

        public bool RemoveMaterial(ItemDefinition itemDefinition, int amount)
        {
            if (itemDefinition == null || amount <= 0) return false;

            for (var i = 0; i < materialStacks.Count; i++)
            {
                if (materialStacks[i].item != itemDefinition) continue;

                var stack = materialStacks[i];

                if (stack.amount < amount) return false;

                stack.amount -= amount;

                if (stack.amount <= 0)
                    materialStacks.RemoveAt(i);
                else
                    materialStacks[i] = stack;

                return true;
            }

            return false;
        }

        public int GetAmount(ItemDefinition itemDefinition)
        {
            if (itemDefinition == null) return 0;

            for (var i = 0; i < materialStacks.Count; i++)
                if (materialStacks[i].item == itemDefinition)
                    return materialStacks[i].amount;

            return 0;
        }

        public void ClearStorage()
        {
            materialStacks.Clear();
            _reservations.Clear();
        }

        /// <summary>
        ///     Routes a transfer request to a linked storage node.
        ///     When base storage networking is active this enables shared resource pools
        ///     across multiple BaseStorage components assigned to the same network.
        /// </summary>
        // ReSharper disable once UnusedMember.Global
        public int TryTransferToNetwork(ItemDefinition item, int amount, BaseStorage destination)
        {
            if (item == null || amount <= 0 || destination == null || destination == this) return 0;

            var available = Mathf.Min(GetAmount(item), amount);

            if (available <= 0) return 0;

            RemoveMaterial(item, available);
            destination.AddMaterial(item, available);
            return available;
        }

        /// <summary>Reserves <paramref name="amount" /> units for a pending job. Does not remove from stock.</summary>
        // ReSharper disable once UnusedMember.Global
        public void Reserve(ItemDefinition item, int amount)
        {
            if (item == null || amount <= 0) return;
            _reservations.TryGetValue(item, out var current);
            _reservations[item] = current + amount;
        }

        /// <summary>Releases a previously made reservation.</summary>
        // ReSharper disable once UnusedMember.Global
        public void ReleaseReservation(ItemDefinition item, int amount)
        {
            if (item == null || amount <= 0 || !_reservations.TryGetValue(item, out var current)) return;
            var newVal = current - amount;
            if (newVal <= 0)
                _reservations.Remove(item);
            else
                _reservations[item] = newVal;
        }

        /// <summary>Returns how many units of <paramref name="item" /> are available (stock minus reservations).</summary>
        // ReSharper disable once UnusedMember.Global
        public int GetAvailable(ItemDefinition item)
        {
            if (item == null) return 0;
            var stock = GetAmount(item);
            _reservations.TryGetValue(item, out var reserved);
            return Mathf.Max(0, stock - reserved);
        }
    }

    [Serializable]
    public struct MaterialStack
    {
        public ItemDefinition item;
        public int amount;

        public MaterialStack(ItemDefinition itemDefinition, int stackAmount)
        {
            item = itemDefinition;
            amount = stackAmount;
        }
    }
}