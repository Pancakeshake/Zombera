using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zombera.Characters;

namespace Zombera.Inventory.Crafting
{
    /// <summary>
    /// Central registry for all crafting recipes in the game.
    /// Provides methods for finding and filtering recipes based on various criteria.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombera/Inventory/Crafting Recipe Registry", fileName = "CraftingRecipeRegistry")]
    public class CraftingRecipeRegistry : ScriptableObject
    {
        private static CraftingRecipeRegistry _instance;

        /// <summary>
        /// Singleton instance loaded from Resources.
        /// </summary>
        public static CraftingRecipeRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<CraftingRecipeRegistry>("CraftingRecipeRegistry");
                    if (_instance == null)
                    {
                        // Fallback: search the entire project if not in a folder named 'Resources'
                        // but production systems should use Resources.Load for performance/reliability.
                        var registries = Resources.FindObjectsOfTypeAll<CraftingRecipeRegistry>();
                        if (registries != null && registries.Length > 0)
                        {
                            _instance = registries[0];
                        }
                    }

                    if (_instance == null)
                    {
                        Debug.LogWarning("CraftingRecipeRegistry instance not found. Ensure a CraftingRecipeRegistry asset exists in a Resources folder.");
                    }
                }
                return _instance;
            }
        }

        [SerializeField] 
        [Tooltip("All recipes available in the game.")]
        private List<CraftingRecipe> allRecipes = new List<CraftingRecipe>();

        /// <summary>
        /// Finds a recipe by its unique ID.
        /// </summary>
        public CraftingRecipe GetRecipeById(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId)) return null;
            return allRecipes.FirstOrDefault(r => r.recipeId == recipeId);
        }

        /// <summary>
        /// Returns all registered recipes.
        /// </summary>
        public IReadOnlyList<CraftingRecipe> GetAllRecipes()
        {
            return allRecipes;
        }

        /// <summary>
        /// Returns recipes filtered by station type.
        /// </summary>
        public IEnumerable<CraftingRecipe> GetRecipesByStation(CraftingStationType stationType)
        {
            return allRecipes.Where(r => r.requiredStationType == stationType);
        }

        /// <summary>
        /// Returns recipes filtered by category.
        /// </summary>
        public IEnumerable<CraftingRecipe> GetRecipesByCategory(CraftingCategory category)
        {
            return allRecipes.Where(r => r.category == category);
        }

        /// <summary>
        /// Returns recipes that the given unit has the skills to craft.
        /// </summary>
        public IEnumerable<CraftingRecipe> GetRecipesBySkill(UnitStats stats)
        {
            if (stats == null) return Enumerable.Empty<CraftingRecipe>();

            return allRecipes.Where(r => 
            {
                if (string.IsNullOrEmpty(r.requiredSkillType)) return true;
                
                if (Enum.TryParse<UnitSkillType>(r.requiredSkillType, true, out var skillType))
                {
                    return stats.GetSkillLevel(skillType) >= r.requiredSkillLevel;
                }
                
                // If it's not a valid UnitSkillType, we might want to log a warning or just skip.
                return false;
            });
        }

        /// <summary>
        /// Returns recipes that can be crafted with the items currently in the inventory.
        /// </summary>
        public IEnumerable<CraftingRecipe> GetAvailableRecipes(UnitInventory inventory)
        {
            if (inventory == null) return Enumerable.Empty<CraftingRecipe>();

            return allRecipes.Where(r => CanCraft(r, inventory));
        }

        /// <summary>
        /// Checks if the inventory contains all required ingredients for a recipe.
        /// </summary>
        public bool CanCraft(CraftingRecipe recipe, UnitInventory inventory)
        {
            if (recipe == null || inventory == null) return false;
            if (recipe.ingredients == null || recipe.ingredients.Count == 0) return true;

            foreach (var ingredient in recipe.ingredients)
            {
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
                    if (count < ingredient.amount) return false;
                }
                else
                {
                    if (ingredient.item == null) continue;
                    if (inventory.GetQuantity(ingredient.item) < ingredient.amount)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Helper to populate the registry from the inspector or editor scripts.
        /// </summary>
        public void SetRecipes(IEnumerable<CraftingRecipe> recipes)
        {
            allRecipes = recipes.ToList();
        }
    }
}
