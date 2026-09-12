#region

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

#endregion

// ReSharper disable Unity.PerformanceCriticalCodeInvocation
// ReSharper disable Unity.PerformanceCriticalCodeNullComparison
// ReSharper disable MemberCanBePrivate.Global

namespace Zombera.BuildingSystem
{
    /// <summary>
    ///     Destructible health pool for a door.
    ///     Attach to the same GameObject as the NavMeshObstacle (DoorBlockerNode).
    ///     When health reaches zero:
    ///     - Removes the attached door leaf
    ///     - Permanently disables the NavMeshObstacle so agents can path through
    ///     This is separate from the wall's StructureHealth — the wall can survive
    ///     while the door is destroyed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DoorHealth : MonoBehaviour
    {
        /// <summary>All live DoorHealth instances in the scene.</summary>
        public static readonly List<DoorHealth> All = new();

        [SerializeField] [Min(1f)] private float maxHealth = 80f;

        [Tooltip("Third-party door component (DoorScript.Door) on the door leaf child. Optional.")]
        [SerializeField]
        private Component thirdPartyDoorLeaf;

        [Tooltip("Custom door leaf controller on the door leaf child. Optional.")]
        [SerializeField]
        private DoorController customDoorLeaf;

        [Tooltip("NavMeshObstacle on this or a sibling node. Assign in Inspector.")] [SerializeField]
        private NavMeshObstacle obstacle;

        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }
        public bool IsDestroyed { get; private set; }

        /// <summary>False if no supported door leaf was assigned/resolved — prevents stray DoorHealth components from being targeted.</summary>
        public bool IsValid => customDoorLeaf != null || ThirdPartyDoorBridge.IsThirdPartyDoor(thirdPartyDoorLeaf);

        private void Awake()
        {
            CurrentHealth = maxHealth;

            if (customDoorLeaf == null)
                customDoorLeaf = GetComponentInChildren<DoorController>(true);

            if (thirdPartyDoorLeaf == null)
                ThirdPartyDoorBridge.TryGetInChildren(transform, out thirdPartyDoorLeaf);
        }

        private void OnEnable()
        {
            All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        /// <summary>
        ///     Fills <paramref name="buffer" /> with doors within <paramref name="radius" /> of <paramref name="origin" />
        ///     (horizontal distance uses full 3D sqr magnitude).
        /// </summary>
        public static void FindNearbyDoors(Vector3 origin, float radius, List<DoorHealth> buffer)
        {
            if (buffer == null) return;

            buffer.Clear();
            var radiusSqr = radius * radius;

            // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
            foreach (var door in All)
            {
                if (!door) continue;
                if (!door.isActiveAndEnabled) continue;
                if (door.IsDestroyed) continue;
                if (!door.IsValid) continue;
                if ((door.transform.position - origin).sqrMagnitude > radiusSqr) continue;
                buffer.Add(door);
            }
        }

        public void TakeDamage(float amount, GameObject source = null)
        {
            if (IsDestroyed || amount <= 0f)
                return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);

            if (CurrentHealth <= 0f)
                BreakDoor(source != null ? source.transform.position : null);
        }

        private void BreakDoor(Vector3? attackerPosition)
        {
            _ = attackerPosition;
            IsDestroyed = true;

            // Delete the door leaf GameObject entirely — it's broken.
            if (customDoorLeaf)
                Destroy(customDoorLeaf.gameObject);
            else if (thirdPartyDoorLeaf)
                Destroy(thirdPartyDoorLeaf.gameObject);

            // Permanently disable the carving obstacle — agents can now path through freely.
            if (obstacle)
                obstacle.enabled = false;

            Debug.Log($"[DoorHealth] Door on '{gameObject.name}' destroyed.", this);
        }
    }
}