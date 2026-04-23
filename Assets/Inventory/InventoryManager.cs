using System.Collections.Generic;
using UnityEngine;
using Zombera.Core;
using Zombera.Inventory;

namespace Zombera.Inventory
{
    /// <summary>
    /// List-based inventory manager with weight limits and encumbrance state.
    /// </summary>
    public sealed class InventoryManager : MonoBehaviour
    {
        [SerializeField] private float weightLimit = 45f;
        [SerializeField] private List<ItemStack> items = new List<ItemStack>();

        public IReadOnlyList<ItemStack> Items => items;
        public float WeightLimit => weightLimit;
        public float CurrentWeight { get; private set; }

        private EncumbranceState _prevEncumbrance = EncumbranceState.Light;

        public EncumbranceState Encumbrance
        {
            get
            {
                if (CurrentWeight >= weightLimit)
                {
                    return EncumbranceState.Overburdened;
                }

                if (CurrentWeight >= weightLimit * 0.85f)
                {
                    return EncumbranceState.Heavy;
                }

                if (CurrentWeight >= weightLimit * 0.6f)
                {
                    return EncumbranceState.Medium;
                }

                return EncumbranceState.Light;
            }
        }

        public bool TryAddItem(ItemDefinition itemDefinition, int quantity)
        {
            if (itemDefinition == null || quantity <= 0)
            {
                return false;
            }

            float incomingWeight = itemDefinition.weight * quantity;

            if (CurrentWeight + incomingWeight > weightLimit)
            {
                return false;
            }

            if (itemDefinition.stackable)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    ItemStack existingStack = items[i];

                    if (existingStack.item != itemDefinition)
                    {
                        continue;
                    }

                    existingStack.quantity += quantity;
                    items[i] = existingStack;
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
                ItemStack stack = items[i];

                if (stack.item != itemDefinition)
                {
                    continue;
                }

                if (stack.quantity < quantity)
                {
                    return false;
                }

                stack.quantity -= quantity;

                if (stack.quantity <= 0)
                {
                    items.RemoveAt(i);
                }
                else
                {
                    items[i] = stack;
                }

                RecalculateWeight();
                return true;
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

        private void RecalculateWeight()
        {
            float total = 0f;

            for (int i = 0; i < items.Count; i++)
            {
                total += items[i].GetTotalWeight();
            }

            CurrentWeight = total;

            EncumbranceState newEncumbrance = Encumbrance;
            if (newEncumbrance != _prevEncumbrance)
            {
                EncumbranceState previous = _prevEncumbrance;
                _prevEncumbrance = newEncumbrance;
                EventSystem.PublishGlobal(new EncumbranceChangedEvent
                {
                    InventoryObject = gameObject,
                    PreviousState = previous,
                    NewState = newEncumbrance,
                    CarryRatio = WeightLimit > 0f ? CurrentWeight / WeightLimit : 0f
                });
            }
        }
    }

    public enum EncumbranceState
    {
        Light,
        Medium,
        Heavy,
        Overburdened
    }
}