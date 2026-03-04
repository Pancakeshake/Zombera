using UnityEngine;

namespace Zombera.Data
{
    /// <summary>
    /// Region/biome data used for difficulty, spawn scaling, and loot multipliers.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombera/Data/Region Data", fileName = "RegionData")]
    public sealed class RegionData : ScriptableObject
    {
        public string regionId;
        public string biomeName;
        public float difficulty = 1f;
        public float zombieDensity = 1f;
        public float lootMultiplier = 1f;

        // TODO: Add weather profile and ambient event weighting.
    }
}