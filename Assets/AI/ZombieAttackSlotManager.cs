using UnityEngine;

namespace Zombera.AI
{
    /// <summary>
    /// Attack slot manager — unlimited mode.
    /// Every zombie in range is allowed to attack simultaneously.
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
        public bool RequestSlot(ZombieStateMachine requester) => true;

        public void ReleaseSlot(ZombieStateMachine requester) { }

        public bool HasSlot(ZombieStateMachine requester) => true;
    }
}
