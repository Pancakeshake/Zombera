using System.Collections.Generic;
using UMA;
using UnityEngine;

namespace Zombera.Data
{
    /// <summary>
    /// Limited UMA appearance pool for Tier 1 zombie variants.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombera/Data/Zombie UMA Visual Profile", fileName = "ZombieUmaVisualProfile")]
    public sealed class ZombieUmaVisualProfile : ScriptableObject
    {
        [Tooltip("Allowed race names for this zombie profile.")]
        public List<string> raceNames = new List<string>
        {
            "HumanMale",
            "HumanFemale"
        };

        [Tooltip("Optional UMATextRecipe assets (recommended for authored variants).")]
        public List<UMATextRecipe> umaTextRecipes = new List<UMATextRecipe>();

        [Tooltip("Optional pre-baked UMA recipe text assets.")]
        public List<TextAsset> recipeAssets = new List<TextAsset>();

        [Tooltip("Optional inline UMA recipe strings for quick iteration.")]
        public List<string> recipeStrings = new List<string>();

        [Tooltip("Uniform scale variation range for this profile.")]
        public Vector2 uniformScaleRange = new Vector2(0.97f, 1.03f);
    }
}
