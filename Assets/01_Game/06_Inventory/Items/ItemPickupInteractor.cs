#region

using UnityEngine;
using Zombera.Characters;

#endregion

namespace Zombera.Inventory
{
    /// <summary>
    ///     Finds nearby ItemPickup objects and transfers them into the unit inventory on Interact().
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ItemPickupInteractor : MonoBehaviour
    {
        [SerializeField] [Min(0.1f)] private float interactRadius = 2f;
        [SerializeField] private LayerMask pickupLayerMask = ~0;

        [Tooltip("How often the proximity scan runs. Interact() always rescans, so the prompt may lag at most one interval.")]
        [SerializeField] [Min(0.02f)] private float scanIntervalSeconds = 0.1f;
        private readonly Collider[] _overlapBuffer = new Collider[24];

        private UnitInventory _inventory;
        private float _nextScanAt;

        public ItemPickup NearestPickup { get; private set; }

        private void Awake()
        {
            _inventory = GetComponent<UnitInventory>();
        }

        private void Update()
        {
            if (Time.time < _nextScanAt) return;

            _nextScanAt = Time.time + Mathf.Max(0.02f, scanIntervalSeconds);
            NearestPickup = FindNearestPickup();
        }

        public bool Interact()
        {
            _inventory ??= GetComponent<UnitInventory>();
            NearestPickup = FindNearestPickup();
            var pickup = NearestPickup;
            return _inventory != null && pickup != null && pickup.TryPickup(_inventory);
        }

        private ItemPickup FindNearestPickup()
        {
            var hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                interactRadius,
                _overlapBuffer,
                pickupLayerMask,
                QueryTriggerInteraction.Collide);

            ItemPickup nearest = null;
            var nearestSqDist = float.MaxValue;

            for (var i = 0; i < hitCount; i++)
            {
                var overlapCollider = _overlapBuffer[i];
                if (overlapCollider == null) continue;

                var pickup = overlapCollider.GetComponent<ItemPickup>();
                if (pickup == null) pickup = overlapCollider.GetComponentInParent<ItemPickup>();

                if (pickup == null || !pickup.isActiveAndEnabled) continue;

                var sqDist = (pickup.transform.position - transform.position).sqrMagnitude;
                if (sqDist >= nearestSqDist) continue;

                nearestSqDist = sqDist;
                nearest = pickup;
            }

            return nearest;
        }
    }
}