using System.Collections.Generic;
using UnityEngine;

namespace Zombera.Inventory.Crafting
{
    [CreateAssetMenu(menuName = "Zombera/Inventory/Crafting Recipe", fileName = "NewRecipe")]
    public class CraftingRecipe : ScriptableObject
    {
        public string recipeId;
        public string displayName;
        public Sprite icon;
        public CraftingCategory category;
        public List<CraftingIngredient> ingredients;
        public List<CraftingOutput> outputs;
        public float baseCraftSeconds;
        public string requiredSkillType = "Engineering";
        public int requiredSkillLevel;
        public CraftingStationType requiredStationType;
        public string[] requiredTags;
        public bool canBatch;
        public int maxBatchSize;
        [TextArea(3, 10)]
        public string description;
    }
}
