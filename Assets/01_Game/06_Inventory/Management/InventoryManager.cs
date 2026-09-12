#region

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zombera.Core;
using Zombera.Characters;

#endregion

namespace Zombera.Inventory
{
    /// <summary>
    ///     List-based inventory manager with weight limits and encumbrance state.
    /// </summary>
    public sealed class InventoryManager : MonoBehaviour, IInventoryHolder
    {
[SerializeField] private float weightLimit = 45f;
        [SerializeField] private List<ItemStack> items = new();

        private EncumbranceState _prevEncumbrance = EncumbranceState.Light;

        public IReadOnlyList<ItemStack> Items => items;
        public float WeightLimit => weightLimit;
        public float CurrentWeight { get; private set; }

        public EncumbranceState Encumbrance
        {
            get
            {
                return CurrentWeight switch
                {
                    var weight when weight >= weightLimit => EncumbranceState.Overburdened,
                    var weight when weight >= weightLimit * 0.85f => EncumbranceState.Heavy,
                    var weight when weight >= weightLimit * 0.6f => EncumbranceState.Medium,
                    _ => EncumbranceState.Light
                };
            }
        }

        public bool AddItem(ItemDefinition itemDefinition, int quantity)
        {
            return TryAddItem(itemDefinition, quantity);
        }

        public float GetWeight()
        {
            return CurrentWeight;
        }

        public bool TryAddItem(ItemDefinition itemDefinition, int quantity)
{
            if (itemDefinition == null || quantity <= 0) return false;

            var incomingWeight = itemDefinition.weight * quantity;

            if (CurrentWeight + incomingWeight > weightLimit) return false;

            if (itemDefinition.stackable)
                for (var i = 0; i < items.Count; i++)
                {
                    var existingStack = items[i];

                    if (existingStack.item != itemDefinition) continue;

                    existingStack.quantity += quantity;
                    items[i] = existingStack;
                    RecalculateWeight();
                    return true;
                }

            items.Add(new ItemStack(itemDefinition, quantity));
            RecalculateWeight();
            return true;
        }

        // ReSharper disable once UnusedMember.Global
        public bool RemoveItem(ItemDefinition itemDefinition, int quantity)
        {
            return itemDefinition != null && quantity > 0 && TryRemoveItem(itemDefinition, quantity);
        }

        public bool TryRemoveItem(ItemDefinition itemDefinition, int quantity)
        {
            var remainingToRemove = quantity;

            for (var i = items.Count - 1; i >= 0; i--)
            {
                var stack = items[i];

                if (stack.item != itemDefinition) continue;

                var toTake = Mathf.Min(stack.quantity, remainingToRemove);
                stack.quantity -= toTake;
                remainingToRemove -= toTake;

                if (stack.quantity <= 0)
                    items.RemoveAt(i);
                else
                    items[i] = stack;

                if (remainingToRemove <= 0) break;
            }

            RecalculateWeight();
            return remainingToRemove <= 0;
        }

        public event System.Action OnInventoryChanged;

        // ReSharper disable once UnusedMember.Global
        public int GetQuantity(ItemDefinition itemDefinition)
        {
            return itemDefinition == null
                ? 0
                : items.FirstOrDefault(stack => stack.item == itemDefinition).quantity;
        }

        private void RecalculateWeight()
        {
            CurrentWeight = items.Sum(item => item.GetTotalWeight());
            OnInventoryChanged?.Invoke();

            var newEncumbrance = Encumbrance;
            if (newEncumbrance == _prevEncumbrance) return;

            var previous = _prevEncumbrance;
            _prevEncumbrance = newEncumbrance;
            CoreEventBus.PublishGlobal(new EncumbranceChangedEvent
            {
                InventoryObject = gameObject,
                PreviousState = previous,
                NewState = newEncumbrance,
                CarryRatio = WeightLimit > 0f ? CurrentWeight / WeightLimit : 0f
            });
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