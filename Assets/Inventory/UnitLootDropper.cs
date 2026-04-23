using UnityEngine;

namespace Zombera.Inventory
{
    /// <summary>
    /// Spawns a loot container at this unit's position when it dies.
    /// Add this component to any unit prefab that should drop loot on death.
    /// Called from UnitHealth.Die() via GetComponent&lt;UnitLootDropper&gt;().
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitLootDropper : MonoBehaviour
    {
        [SerializeField] private GameObject lootContainerPrefab;
        [SerializeField] private bool dropOnlyOnce = true;

        private bool hasDropped;

        public void OnUnitDied()
        {
            if (dropOnlyOnce && hasDropped) return;
            if (lootContainerPrefab == null) return;

            hasDropped = true;

            Instantiate(lootContainerPrefab, transform.position, Quaternion.identity);
        }
    }
}
