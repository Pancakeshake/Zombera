using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Zombera.BuildingSystem
{
    /// <summary>
    /// Destructible health pool for a door.
    /// Attach to the same GameObject as the NavMeshObstacle (DoorBlockerNode).
    ///
    /// When health reaches zero:
    ///   - Forces the door open via DoorScript.Door
    ///   - Permanently disables the NavMeshObstacle so agents can path through
    ///
    /// This is separate from the wall's StructureHealth — the wall can survive
    /// while the door is destroyed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DoorHealth : MonoBehaviour
    {
        /// <summary>All live DoorHealth instances in the scene.</summary>
        public static readonly List<DoorHealth> All = new List<DoorHealth>();

        [SerializeField, Min(1f)] private float maxHealth = 80f;

        [Tooltip("DoorScript.Door on the door leaf child. Assign in Inspector.")]
        [SerializeField] private DoorScript.Door doorLeaf;

        [Tooltip("NavMeshObstacle on this or a sibling node. Assign in Inspector.")]
        [SerializeField] private NavMeshObstacle obstacle;

        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }
        public bool IsDestroyed { get; private set; }
        /// <summary>False if doorLeaf was never assigned — prevents stray DoorHealth components from being targeted.</summary>
        public bool IsValid => doorLeaf != null;

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        private void OnEnable()  { All.Add(this); }
        private void OnDisable() { All.Remove(this); }
        public void TakeDamage(float amount, GameObject source = null)
        {
            if (IsDestroyed || amount <= 0f)
                return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);

            if (CurrentHealth <= 0f)
                BreakDoor(source != null ? source.transform.position : (Vector3?)null);
        }

        private void BreakDoor(Vector3? attackerPosition)
        {
            IsDestroyed = true;

            // Delete the door leaf GameObject entirely — it's broken.
            if (doorLeaf != null)
                Destroy(doorLeaf.gameObject);

            // Permanently disable the carving obstacle — agents can now path through freely.
            if (obstacle != null)
                obstacle.enabled = false;

            Debug.Log($"[DoorHealth] Door on '{gameObject.name}' destroyed.", this);
        }
    }
}
