using System.Collections.Generic;
using UnityEngine;
using Zombera.Inventory;

namespace Zombera.Characters
{
    /// <summary>
    /// Unit-level weight-based inventory with encumbrance support.
    /// </summary>
    public sealed class UnitInventory : MonoBehaviour, IInventoryHolder
    {
        [SerializeField] private float weightLimit = 35f;

        private readonly List<ItemStack> items = new List<ItemStack>();

        public IReadOnlyList<ItemStack> Items => items;
        public float WeightLimit => weightLimit;
        public float CurrentWeight { get; private set; }
        public bool IsEncumbered => CurrentWeight > WeightLimit;
        public float CarryRatio => WeightLimit > 0f ? CurrentWeight / WeightLimit : 0f;

        public bool AddItem(ItemDefinition itemDefinition, int quantity)
        {
            return TryAddItem(itemDefinition, quantity);
        }

        public bool TryAddItem(ItemDefinition itemDefinition, int quantity)
        {
            if (itemDefinition == null || quantity <= 0)
            {
                return false;
            }

            float addedWeight = itemDefinition.weight * quantity;

            if (CurrentWeight + addedWeight > WeightLimit)
            {
                return false;
            }

            if (itemDefinition.stackable)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].item != itemDefinition)
                    {
                        continue;
                    }

                    ItemStack stacked = items[i];
                    stacked.quantity += quantity;
                    items[i] = stacked;
                    RecalculateWeight();
                    return true;
                }
            }

            items.Add(new ItemStack(itemDefinition, quantity));
            RecalculateWeight();
            return true;
        }

        public bool RemoveItem(ItemDefinition itemDefinition, int quantity)
        {
            if (itemDefinition == null || quantity <= 0)
            {
                return false;
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].item != itemDefinition)
                {
                    continue;
                }

                ItemStack current = items[i];

                if (current.quantity < quantity)
                {
                    return false;
                }

                current.quantity -= quantity;

                if (current.quantity <= 0)
                {
                    items.RemoveAt(i);
                }
                else
                {
                    items[i] = current;
                }

                RecalculateWeight();
                return true;
            }

            return false;
        }

        public float GetWeight()
        {
            return CurrentWeight;
        }

        public bool HasItem(ItemDefinition itemDefinition)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].item == itemDefinition)
                {
                    return true;
                }
            }

            return false;
        }

        public int GetQuantity(ItemDefinition itemDefinition)
        {
            if (itemDefinition == null)
            {
                return 0;
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].item == itemDefinition)
                {
                    return items[i].quantity;
                }
            }

            return 0;
        }

        public void SetWeightLimit(float value)
        {
            weightLimit = Mathf.Max(1f, value);
            RecalculateWeight();
        }

        public bool IsHeavyCarry(float threshold01)
        {
            return Mathf.Clamp01(CarryRatio) >= Mathf.Clamp01(threshold01);
        }

        /// <summary>
        /// Consumes one of <paramref name="itemDefinition"/> from this inventory,
        /// applying its heal amount and awarding Constitution XP to <paramref name="owner"/>.
        /// Returns false if the item is not in inventory.
        /// </summary>
        public bool ConsumeItem(ItemDefinition itemDefinition, UnitHealth health, UnitStats stats)
        {
            if (itemDefinition == null)
            {
                return false;
            }

            if (!RemoveItem(itemDefinition, 1))
            {
                return false;
            }

            if (health != null && itemDefinition.healAmount > 0f)
            {
                health.Heal(itemDefinition.healAmount, stats);
            }

            if (stats != null)
            {
                switch (itemDefinition.itemType)
                {
                    case ItemType.Food:
                        stats.RecordMealConsumed(itemDefinition.mealQuality);
                        break;
                    case ItemType.Vitamin:
                        stats.RecordVitaminConsumed();
                        break;
                }
            }

            return true;
        }

        /// <summary>
        /// Raised whenever <see cref="CurrentWeight"/> changes (items added or removed).
        /// Subscribe in <see cref="UnitController"/> to reactively refresh movement speed.
        /// </summary>
        public event System.Action OnInventoryChanged;

        private void RecalculateWeight()
        {
            float total = 0f;

            for (int i = 0; i < items.Count; i++)
            {
                ItemStack stack = items[i];

                if (stack.item == null)
                {
                    continue;
                }

                total += stack.item.weight * stack.quantity;
            }

            CurrentWeight = total;
            OnInventoryChanged?.Invoke();
        }
    }
}