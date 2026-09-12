#region

using UnityEngine;

#endregion

namespace Zombera.Data
{
    /// <summary>
    ///     Region/biome data used for difficulty, spawn scaling, and loot multipliers.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombera/Data/Region Data", fileName = "RegionData")]
    public sealed class RegionData : ScriptableObject
    {
        public string regionId;
        public string biomeName;
        public float difficulty = 1f;
        public float zombieDensity = 1f;
        public float lootMultiplier = 1f;

        [Header("Weather")] [Tooltip("Asset ID of the weather profile applied when the player is in this region.")]
        public string weatherProfileId;

        [Header("Ambient Events")]
        [Tooltip(
            "Relative spawn weights for ambient event categories: [0]=Horde, [1]=Scavenge, [2]=Storm, [3]=Trader.")]
        public float[] ambientEventWeights = { 1f, 1f, 0f, 0f };
    }
}