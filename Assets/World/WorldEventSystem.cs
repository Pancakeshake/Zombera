using UnityEngine;
using Zombera.Data;
using Zombera.Systems;

namespace Zombera.World
{
    /// <summary>
    /// Generates dynamic world encounters such as hordes, survivors, and supply drops.
    /// </summary>
    public sealed class WorldEventSystem : MonoBehaviour
    {
        [SerializeField] private float eventTickInterval = 5f;
        [SerializeField] private float eventChancePerTick = 0.25f;

        [Header("Zombie Horde Events")]
        [SerializeField] private ZombieManager zombieManager;
        [SerializeField] private ZombieType eventZombieType;
        [SerializeField] private int minHordeSize = 6;
        [SerializeField] private int maxHordeSize = 14;
        [SerializeField] private float hordeSpawnRadius = 10f;

        [Header("Survivor Encounter Events")]
        [SerializeField] private GameObject survivorEncounterPrefab;
        [SerializeField] private int minSurvivorCount = 1;
        [SerializeField] private int maxSurvivorCount = 2;
        [SerializeField] private float survivorSpawnRadius = 4f;

        [Header("Supply Drop Events")]
        [SerializeField] private LootSpawner lootSpawner;

        [Header("Spawn Context")]
        [SerializeField] private float eventSpawnDistanceFromPlayer = 24f;
        [SerializeField] private Transform eventRoot;

        [Header("Debug")]
        [SerializeField] private bool logEvents = true;

        private float eventTickTimer;

        private void Awake()
        {
            if (zombieManager == null)
            {
                zombieManager = FindObjectOfType<ZombieManager>();
            }

            if (lootSpawner == null)
            {
                lootSpawner = FindObjectOfType<LootSpawner>();
            }
        }

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
            if (zombieManager == null)
            {
                zombieManager = FindObjectOfType<ZombieManager>();
            }

            if (zombieManager == null)
            {
                LogEvent("Zombie horde event skipped (missing ZombieManager)");
                return;
            }

            int minimum = Mathf.Max(1, minHordeSize);
            int maximum = Mathf.Max(minimum, maxHordeSize);
            int hordeSize = Random.Range(minimum, maximum + 1);
            Vector3 spawnCenter = ResolveSpawnCenter(nearPosition);
            zombieManager.SpawnZombieWave(eventZombieType, spawnCenter, hordeSize, Mathf.Max(0.5f, hordeSpawnRadius));

            LogEvent($"Zombie horde spawned ({hordeSize}) at {spawnCenter}");
        }

        public void SpawnSurvivorEncounter(Vector3 nearPosition)
        {
            if (survivorEncounterPrefab == null)
            {
                LogEvent("Survivor encounter skipped (missing prefab)");
                return;
            }

            int minimum = Mathf.Max(1, minSurvivorCount);
            int maximum = Mathf.Max(minimum, maxSurvivorCount);
            int survivorCount = Random.Range(minimum, maximum + 1);
            Vector3 center = ResolveSpawnCenter(nearPosition);
            Transform parent = eventRoot != null ? eventRoot : transform;

            for (int i = 0; i < survivorCount; i++)
            {
                Vector2 offset = Random.insideUnitCircle * Mathf.Max(0f, survivorSpawnRadius);
                Vector3 spawnPosition = center + new Vector3(offset.x, 0f, offset.y);
                Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                Instantiate(survivorEncounterPrefab, spawnPosition, rotation, parent);
            }

            LogEvent($"Survivor encounter spawned ({survivorCount}) near {center}");
        }

        public void SpawnSupplyDrop(Vector3 nearPosition)
        {
            if (lootSpawner == null)
            {
                lootSpawner = FindObjectOfType<LootSpawner>();
            }

            if (lootSpawner == null)
            {
                LogEvent("Supply drop skipped (missing LootSpawner)");
                return;
            }

            Vector3 spawnPosition = ResolveSpawnCenter(nearPosition);
            lootSpawner.SpawnLootAt(spawnPosition);
            LogEvent($"Supply drop spawned at {spawnPosition}");
        }

        private Vector3 ResolveSpawnCenter(Vector3 nearPosition)
        {
            Vector2 offset = Random.insideUnitCircle;

            if (offset.sqrMagnitude <= 0.0001f)
            {
                offset = Vector2.right;
            }

            offset.Normalize();
            return nearPosition + new Vector3(offset.x, 0f, offset.y) * Mathf.Max(1f, eventSpawnDistanceFromPlayer);
        }

        private void LogEvent(string message)
        {
            if (!logEvents)
            {
                return;
            }

            Debug.Log($"[WorldEventSystem] {message}", this);
        }
    }

    public enum WorldDynamicEventType
    {
        ZombieHorde,
        SurvivorEncounter,
        SupplyDrop
    }
}