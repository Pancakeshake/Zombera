using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.Core;

namespace Zombera.Inventory
{
    /// <summary>
    /// Scans for nearby LootContainers each frame and exposes a single Interact() call
    /// to open + transfer all loot into the unit's inventory.
    /// Attach this to the player unit alongside UnitStats and UnitInventory.
    /// </summary>
    public sealed class ContainerInteractor : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float interactRadius = 2.5f;
        [SerializeField] private LayerMask containerLayerMask = ~0;

        private UnitStats unitStats;
        private UnitInventory inventory;

        /// <summary>The nearest eligible LootContainer this frame, or null.</summary>
        public LootContainer NearestContainer { get; private set; }

        private readonly Collider[] overlapBuffer = new Collider[16];

        private void Awake()
        {
            unitStats = GetComponent<UnitStats>();
            inventory = GetComponent<UnitInventory>();
        }

        private void Update()
        {
            NearestContainer = FindNearestContainer();
        }

        /// <summary>
        /// Attempt to open and loot the nearest container. Returns true if loot was transferred.
        /// </summary>
        public bool Interact()
        {
            LootContainer container = NearestContainer;
            if (container == null) return false;

            // Generate loot if this is the first open, applying Scavenging roll bonus.
            float scavMultiplier = unitStats != null ? unitStats.GetScavengingLootMultiplier() : 1f;
            IReadOnlyList<ItemStack> loot = container.OpenContainer(rollMultiplier: scavMultiplier);
            if (loot.Count == 0 && container.HasGeneratedLoot) return false;

            unitStats?.RecordContainerSearched();

            if (inventory == null) return false;

            bool transferred = container.TransferAllTo(inventory);

            if (transferred)
            {
                EventSystem.PublishGlobal(new ContainerLootedEvent
                {
                    ContainerId = container.ContainerId,
                    Position = container.transform.position,
                    ItemCount = inventory.Items.Count,
                    LooterObject = gameObject
                });
            }

            return transferred;
        }

        private LootContainer FindNearestContainer()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position, interactRadius, overlapBuffer, containerLayerMask,
                QueryTriggerInteraction.Collide);

            LootContainer nearest = null;
            float nearestSqDist = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                if (!overlapBuffer[i].TryGetComponent(out LootContainer candidate)) continue;

                // Skip fully-emptied containers.
                if (candidate.HasGeneratedLoot && candidate.GeneratedLoot.Count == 0) continue;

                float sqDist = (candidate.transform.position - transform.position).sqrMagnitude;
                if (sqDist < nearestSqDist)
                {
                    nearestSqDist = sqDist;
                    nearest = candidate;
                }
            }

            return nearest;
        }
    }
}
