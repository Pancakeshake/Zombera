using UnityEngine;

namespace Zombera.Data
{
    /// <summary>
    /// Region/biome data used for difficulty, spawn scaling, and loot multipliers.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombera/Data/Region Data", fileName = "RegionData")]
    public sealed class RegionData : ScriptableObject
    {
        [field: SerializeField] public string RegionId { get; private set; }
        [field: SerializeField] public string BiomeName { get; private set; }
        [field: SerializeField] public float Difficulty { get; private set; } = 1f;
        [field: SerializeField] public float ZombieDensity { get; private set; } = 1f;
        [field: SerializeField] public float LootMultiplier { get; private set; } = 1f;

        [Header("Weather")]
        [Tooltip("Asset ID of the weather profile applied when the player is in this region.")]
        [field: SerializeField] public string WeatherProfileId { get; private set; }

        [Header("Ambient Events")]
        [Tooltip("Relative spawn weights for ambient event categories: [0]=Horde, [1]=Scavenge, [2]=Storm, [3]=Trader.")]
        [field: SerializeField] public float[] AmbientEventWeights { get; private set; } = { 1f, 1f, 0f, 0f };
    }
}