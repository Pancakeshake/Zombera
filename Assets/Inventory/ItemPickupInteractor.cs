using UnityEngine;
using Zombera.Characters;

namespace Zombera.Inventory
{
    /// <summary>
    /// Finds nearby ItemPickup objects and transfers them into the unit inventory on Interact().
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ItemPickupInteractor : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float interactRadius = 2f;
        [SerializeField] private LayerMask pickupLayerMask = ~0;

        private UnitInventory inventory;
        private readonly Collider[] overlapBuffer = new Collider[24];

        public ItemPickup NearestPickup { get; private set; }

        private void Awake()
        {
            inventory = GetComponent<UnitInventory>();
        }

        private void Update()
        {
            NearestPickup = FindNearestPickup();
        }

        public bool Interact()
        {
            if (inventory == null)
            {
                inventory = GetComponent<UnitInventory>();
            }

            ItemPickup pickup = NearestPickup;
            if (pickup == null || inventory == null)
            {
                return false;
            }

            return pickup.TryPickup(inventory);
        }

        private ItemPickup FindNearestPickup()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                interactRadius,
                overlapBuffer,
                pickupLayerMask,
                QueryTriggerInteraction.Collide);

            ItemPickup nearest = null;
            float nearestSqDist = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                Collider collider = overlapBuffer[i];
                if (collider == null)
                {
                    continue;
                }

                ItemPickup pickup = collider.GetComponent<ItemPickup>();
                if (pickup == null)
                {
                    pickup = collider.GetComponentInParent<ItemPickup>();
                }

                if (pickup == null || !pickup.isActiveAndEnabled)
                {
                    continue;
                }

                float sqDist = (pickup.transform.position - transform.position).sqrMagnitude;
                if (sqDist < nearestSqDist)
                {
                    nearestSqDist = sqDist;
                    nearest = pickup;
                }
            }

            return nearest;
        }
    }
}
