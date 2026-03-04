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
        public float moveSpeed = 2.5f;
        public float attackDamage = 8f;
        public float defaultAiTickInterval = 0.2f;

        // TODO: Add perception radius, aggression profile, and horde affinity settings.
    }
}