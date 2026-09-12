#region

using UnityEngine;
using Zombera.Characters;
using Zombera.Core;

#endregion

namespace Zombera.Inventory
{
    /// <summary>
    ///     Scans for nearby LootContainers each frame and exposes a single Interact() call
    ///     to open + transfer all loot into the unit's inventory.
    ///     Attach this to the player unit alongside UnitStats and UnitInventory.
    /// </summary>
    public sealed class ContainerInteractor : MonoBehaviour
    {
        [SerializeField] [Min(0.1f)] private float interactRadius = 2.5f;
        [SerializeField] private LayerMask containerLayerMask = ~0;

        [Tooltip("How often the proximity scan runs. Interact() always rescans, so the prompt may lag at most one interval.")]
        [SerializeField] [Min(0.02f)] private float scanIntervalSeconds = 0.1f;

        private readonly Collider[] _overlapBuffer = new Collider[16];
        private UnitInventory _inventory;
        private float _nextScanAt;

        private UnitStats _unitStats;

        /// <summary>The nearest eligible LootContainer from the latest scan, or null.</summary>
        public LootContainer NearestContainer { get; private set; }

        private void Awake()
        {
            _unitStats = GetComponent<UnitStats>();
            _inventory = GetComponent<UnitInventory>();
        }

        private void Update()
        {
            if (Time.time < _nextScanAt) return;

            _nextScanAt = Time.time + Mathf.Max(0.02f, scanIntervalSeconds);
            NearestContainer = FindNearestContainer();
        }

        /// <summary>
        ///     Attempt to open and loot the nearest container. Returns true if loot was transferred.
        /// </summary>
        public bool Interact()
        {
            NearestContainer = FindNearestContainer();
            var container = NearestContainer;
            if (container == null) return false;

            if (_inventory == null) return false;

            // Generate loot if this is the first open, applying Scavenging roll bonus.
            var scavMultiplier = _unitStats != null ? _unitStats.GetScavengingLootMultiplier() : 1f;
            var loot = container.OpenContainer(rollMultiplier: scavMultiplier);
            if (loot.Count == 0 && container.HasGeneratedLoot) return false;

            _unitStats?.RecordContainerSearched();

            var transferred = container.TransferAllTo(_inventory);
            if (!transferred) return false;

            CoreEventBus.PublishGlobal(new ContainerLootedEvent
            {
                ContainerId = container.ContainerId,
                Position = container.transform.position,
                ItemCount = _inventory.Items.Count,
                LooterObject = gameObject
            });

            return true;
        }

        private LootContainer FindNearestContainer()
        {
            var hitCount = Physics.OverlapSphereNonAlloc(
                transform.position, interactRadius, _overlapBuffer, containerLayerMask,
                QueryTriggerInteraction.Collide);

            LootContainer nearest = null;
            var nearestSqDist = float.MaxValue;

            for (var i = 0; i < hitCount; i++)
            {
                if (!_overlapBuffer[i].TryGetComponent(out LootContainer candidate)) continue;

                // Skip fully-emptied containers.
                if (candidate.HasGeneratedLoot && candidate.GeneratedLoot.Count == 0) continue;

                var sqDist = (candidate.transform.position - transform.position).sqrMagnitude;
                if (sqDist >= nearestSqDist) continue;

                nearestSqDist = sqDist;
                nearest = candidate;
            }

            return nearest;
        }
    }
}