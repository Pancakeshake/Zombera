#region

using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Inventory;

#endregion

namespace Zombera.Characters
{
    /// <summary>
    ///     Unit-level weight-based inventory with encumbrance support.
    /// </summary>
    public sealed class UnitInventory : MonoBehaviour, IInventoryHolder
    {
        [SerializeField] private float weightLimit = 35f;

        private readonly List<ItemStack> _items = new();

        public IReadOnlyList<ItemStack> Items => _items;

        // ReSharper disable once UnusedMember.Global
        public bool IsEncumbered => CurrentWeight > WeightLimit;
        public float CarryRatio => WeightLimit > 0f ? CurrentWeight / WeightLimit : 0f;
        public float WeightLimit => weightLimit;
        public float CurrentWeight { get; private set; }

        public bool AddItem(ItemDefinition itemDefinition, int quantity)
        {
            return TryAddItem(itemDefinition, quantity);
        }

        public bool RemoveItem(ItemDefinition itemDefinition, int quantity)
        {
            if (itemDefinition == null || quantity <= 0) return false;

            for (var i = 0; i < _items.Count; i++)
            {
                if (_items[i].item != itemDefinition) continue;

                var current = _items[i];

                if (current.quantity < quantity) return false;

                current.quantity -= quantity;

                if (current.quantity <= 0)
                    _items.RemoveAt(i);
                else
                    _items[i] = current;

                RecalculateWeight();
                return true;
            }

            return false;
        }

        public void ClearInventory()
        {
            _items.Clear();
            RecalculateWeight();
        }

        public float GetWeight()
        {
            return CurrentWeight;
        }

        // ReSharper disable once MemberCanBePrivate.Global
        public bool TryAddItem(ItemDefinition itemDefinition, int quantity)
        {
            if (itemDefinition == null || quantity <= 0) return false;

            var addedWeight = itemDefinition.weight * quantity;

            if (CurrentWeight + addedWeight > WeightLimit) return false;

            if (itemDefinition.stackable)
                for (var i = 0; i < _items.Count; i++)
                {
                    if (_items[i].item != itemDefinition) continue;

                    var stacked = _items[i];
                    stacked.quantity += quantity;
                    _items[i] = stacked;
                    RecalculateWeight();
                    return true;
                }

            _items.Add(new ItemStack(itemDefinition, quantity));
            RecalculateWeight();
            return true;
        }

        // ReSharper disable once UnusedMember.Global
        public bool HasItem(ItemDefinition itemDefinition)
        {
            // ReSharper disable once LoopCanBeConvertedToQuery
            for (var i = 0; i < _items.Count; i++)
                if (_items[i].item == itemDefinition)
                    return true;

            return false;
        }

        public int GetQuantity(ItemDefinition itemDefinition)
        {
            if (itemDefinition == null) return 0;

            // ReSharper disable once LoopCanBeConvertedToQuery
            for (var i = 0; i < _items.Count; i++)
                if (_items[i].item == itemDefinition)
                    return _items[i].quantity;

            return 0;
        }

        public void SetWeightLimit(float value)
        {
            weightLimit = Mathf.Max(1f, value);
            RecalculateWeight();
        }

        // ReSharper disable once UnusedMember.Global
        public bool IsHeavyCarry(float threshold01)
        {
            return Mathf.Clamp01(CarryRatio) >= Mathf.Clamp01(threshold01);
        }

        /// <summary>
        ///     Consumes one of <paramref name="itemDefinition" /> from this inventory,
        ///     applying its heal amount and awarding Constitution XP via <paramref name="stats" />.
        ///     Returns false if the item is not in inventory.
        /// </summary>
        // ReSharper disable once UnusedMember.Global
        public bool ConsumeItem(ItemDefinition itemDefinition, UnitHealth health, UnitStats stats)
        {
            if (itemDefinition == null) return false;

            if (!RemoveItem(itemDefinition, 1)) return false;

            if (health != null && itemDefinition.healAmount > 0f) health.Heal(itemDefinition.healAmount, stats);

            if (stats == null) return true;

            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (itemDefinition.itemType)
            {
                case ItemType.Food:
                    stats.RecordMealConsumed(itemDefinition.mealQuality);
                    break;
                case ItemType.Vitamin:
                    stats.RecordVitaminConsumed();
                    break;
            }

            return true;
        }

        /// <summary>
        ///     Raised whenever <see cref="CurrentWeight" /> changes (items added or removed).
        ///     Subscribe in <see cref="UnitController" /> to reactively refresh movement speed.
        /// </summary>
        public event Action OnInventoryChanged;

        private void RecalculateWeight()
        {
            var total = 0f;
            // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
            foreach (var stack in _items)
            {
                if (stack.item == null) continue;
                total += stack.item.weight * stack.quantity;
            }

            CurrentWeight = total;
            OnInventoryChanged?.Invoke();
        }
    }
}