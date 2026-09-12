#region

using UnityEngine;

#endregion

namespace Zombera.AI
{
    /// <summary>
    ///     Attack slot manager — unlimited mode.
    ///     Every zombie in range is allowed to attack simultaneously.
    /// </summary>
    public sealed class ZombieAttackSlotManager : MonoBehaviour
    {
        public static ZombieAttackSlotManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <returns>Always true — all zombies may attack simultaneously.</returns>
        public static bool RequestSlot(ZombieStateMachine requester)
        {
            return true;
        }

        /// <summary>No-op in unlimited mode — no slot tracking needed.</summary>
        public static void ReleaseSlot(ZombieStateMachine requester)
        {
            // Intentionally empty: unlimited mode does not track per-zombie attack slots.
        }

        // ReSharper disable once UnusedMember.Global
        public static bool HasSlot(ZombieStateMachine requester)
        {
            return true;
        }
    }
}