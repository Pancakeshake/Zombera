using UnityEngine;

namespace Zombera.World.Spawning
{
    /// <summary>
    ///     Marks a world-spawned building instance as poolable and remembers its source prefab
    ///     so WorldBuildingSpawner can return it to the right pool when its tile unloads.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PooledWorldBuilding : MonoBehaviour
    {
        public GameObject SourcePrefab { get; set; }
    }
}
