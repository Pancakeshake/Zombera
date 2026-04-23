using UnityEngine;

namespace Zombera.Data
{
    /// <summary>
    /// Zombie archetype data for spawning and behavior tuning.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombera/Data/Zombie Type", fileName = "ZombieType")]
    public sealed class ZombieType : ScriptableObject
    {
        public string zombieTypeId;
        public string displayName;
        public float baseHealth = 50f;
        public float moveSpeed = 1.6f;
        public float attackDamage = 8f;
        public float defaultAiTickInterval = 0.2f;

        [Header("Perception")]
        [Tooltip("Overrides the zombie's detection range. 0 = use prefab default.")]
        public float perceptionRadius = 0f;

        [Header("Aggression")]
        [Tooltip("Multiplier applied to the AI tick interval. >1 = faster reactions.")]
        [Min(0.1f)]
        public float aggressionMultiplier = 1f;

        [Header("Horde")]
        [Tooltip("0 = lone wolf, 1 = always joins a horde when spawned together.")]
        [Range(0f, 1f)]
        public float hordeAffinity = 0.5f;
    }
}