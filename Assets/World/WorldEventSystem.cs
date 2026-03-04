using UnityEngine;

namespace Zombera.World
{
    /// <summary>
    /// Generates dynamic world encounters such as hordes, survivors, and supply drops.
    /// </summary>
    public sealed class WorldEventSystem : MonoBehaviour
    {
        [SerializeField] private float eventTickInterval = 5f;
        [SerializeField] private float eventChancePerTick = 0.25f;

        private float eventTickTimer;

        public void TickDynamicEvents(Vector3 playerPosition)
        {
            eventTickTimer += Time.deltaTime;

            if (eventTickTimer < eventTickInterval)
            {
                return;
            }

            eventTickTimer = 0f;

            if (Random.value > eventChancePerTick)
            {
                return;
            }

            TriggerRandomEvent(playerPosition);
        }

        public void TriggerRandomEvent(Vector3 playerPosition)
        {
            WorldDynamicEventType eventType = (WorldDynamicEventType)Random.Range(0, 3);

            switch (eventType)
            {
                case WorldDynamicEventType.ZombieHorde:
                    SpawnZombieHorde(playerPosition);
                    break;
                case WorldDynamicEventType.SurvivorEncounter:
                    SpawnSurvivorEncounter(playerPosition);
                    break;
                case WorldDynamicEventType.SupplyDrop:
                    SpawnSupplyDrop(playerPosition);
                    break;
            }

            // TODO: Bias event selection by region difficulty and player state.
            // TODO: Integrate cooldowns and anti-repeat event protection.
        }

        public void SpawnZombieHorde(Vector3 nearPosition)
        {
            // TODO: Request HordeManager to spawn and route a horde.
            _ = nearPosition;
        }

        public void SpawnSurvivorEncounter(Vector3 nearPosition)
        {
            // TODO: Spawn survivor NPC group and initialize dialogue/recruit hooks.
            _ = nearPosition;
        }

        public void SpawnSupplyDrop(Vector3 nearPosition)
        {
            // TODO: Spawn supply crate event and mark map indicator.
            _ = nearPosition;
        }
    }

    public enum WorldDynamicEventType
    {
        ZombieHorde,
        SurvivorEncounter,
        SupplyDrop
    }
}