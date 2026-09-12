using System.Collections.Generic;
using System.Linq;
using Zombera.Characters;

namespace Zombera.Inventory.Crafting
{
    /// <summary>
    /// Handles the atomic exchange of ingredients for outputs in an inventory.
    /// </summary>
    public static class CraftingInventoryTransaction
    {
        /// <summary>
        /// Validates if the inventory can afford the ingredients and has enough weight capacity for the outputs.
        /// </summary>
        public static bool CanFulfill(CraftingRecipe recipe, IInventoryHolder inventory, int batchSize = 1)
        {
            if (recipe == null || inventory == null || batchSize <= 0) return false;

            // 1. Check ingredients availability
            foreach (var ingredient in recipe.ingredients)
            {
                int required = ingredient.amount * batchSize;
                if (ingredient.useTag)
                {
                    if (string.IsNullOrEmpty(ingredient.requiredTag)) continue;
                    
                    int count = 0;
                    foreach (var stack in inventory.Items)
                    {
                        if (stack.item != null && stack.item.tags != null && stack.item.tags.Contains(ingredient.requiredTag))
                        {
                            count += stack.quantity;
                        }
                    }
                    if (count < required) return false;
                }
                else
                {
                    if (ingredient.item == null) continue;
                    if (inventory.GetQuantity(ingredient.item) < required) return false;
                }
            }

            // 2. Check weight capacity
            float totalIngredientWeight = 0;
            foreach (var ingredient in recipe.ingredients)
            {
                int remaining = ingredient.amount * batchSize;
                if (ingredient.useTag)
                {
                    foreach (var stack in inventory.Items)
                    {
                        if (stack.item != null && stack.item.tags != null && stack.item.tags.Contains(ingredient.requiredTag))
                        {
                            int take = System.Math.Min(stack.quantity, remaining);
                            totalIngredientWeight += stack.item.weight * take;
                            remaining -= take;
                            if (remaining <= 0) break;
                        }
                    }
                }
                else if (ingredient.item != null)
                {
                    totalIngredientWeight += ingredient.item.weight * remaining;
                }
            }

            float totalOutputWeight = 0;
            foreach (var output in recipe.outputs)
            {
                if (output.item != null)
                {
                    totalOutputWeight += output.item.weight * (output.amount * batchSize);
                }
            }

            float projectedWeight = inventory.CurrentWeight - totalIngredientWeight + totalOutputWeight;
            if (projectedWeight > inventory.WeightLimit)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Executes the transaction: removes ingredients and adds outputs.
        /// Returns true if successful.
        /// </summary>
        public static bool Execute(CraftingRecipe recipe, IInventoryHolder inventory, int batchSize = 1)
        {
            if (!CanFulfill(recipe, inventory, batchSize)) return false;

            // Record consumed items in case we need to roll back (though CanFulfill should prevent failure)
            List<(ItemDefinition item, int qty)> consumed = new List<(ItemDefinition item, int qty)>();

            // Consume ingredients
            foreach (var ingredient in recipe.ingredients)
            {
                int toRemove = ingredient.amount * batchSize;
                if (ingredient.useTag)
                {
                    var itemsToTake = new List<(ItemDefinition, int)>();
                    foreach (var stack in inventory.Items)
                    {
                        if (stack.item != null && stack.item.tags != null && stack.item.tags.Contains(ingredient.requiredTag))
                        {
                            int take = System.Math.Min(stack.quantity, toRemove);
                            itemsToTake.Add((stack.item, take));
                            toRemove -= take;
                            if (toRemove <= 0) break;
                        }
                    }
                    
                    foreach (var itemPair in itemsToTake)
                    {
                        if (inventory.RemoveItem(itemPair.Item1, itemPair.Item2))
                        {
                            consumed.Add(itemPair);
                        }
                    }
                }
                else if (ingredient.item != null)
                {
                    if (inventory.RemoveItem(ingredient.item, toRemove))
                    {
                        consumed.Add((ingredient.item, toRemove));
                    }
                }
            }

            // Grant outputs
            bool allOutputsAdded = true;
            List<(ItemDefinition item, int qty)> added = new List<(ItemDefinition item, int qty)>();
            
            foreach (var output in recipe.outputs)
            {
                if (output.item != null)
                {
                    int qty = output.amount * batchSize;
                    if (inventory.AddItem(output.item, qty))
                    {
                        added.Add((output.item, qty));
                    }
                    else
                    {
                        allOutputsAdded = false;
                        break;
                    }
                }
            }

            if (!allOutputsAdded)
            {
                // Rollback: remove added outputs and return consumed ingredients
                foreach (var itemPair in added)
                {
                    inventory.RemoveItem(itemPair.item, itemPair.qty);
                }
                foreach (var itemPair in consumed)
                {
                    inventory.AddItem(itemPair.item, itemPair.qty);
                }
                return false;
            }

            return true;
        }
    }
}
