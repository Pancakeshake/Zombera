#region

using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Data;
using Zombera.Systems;
using Random = UnityEngine.Random;

#endregion

namespace Zombera.World
{
    /// <summary>
    ///     Generates dynamic world encounters such as hordes, survivors, and supply drops.
    /// </summary>
    public sealed class WorldEventSystem : MonoBehaviour
    {
        [Serializable]
        private sealed class WeightedZombieTypeEntry
        {
            public ZombieType zombieType = null;

            [Min(0f)] public float weight = 1f;
        }

        [SerializeField] private float eventTickInterval = 5f;
        [SerializeField] private float eventChancePerTick = 0.25f;

        [Header("Zombie Horde Events")] [SerializeField]
        private ZombieManager zombieManager;

        [SerializeField] private ZombieType eventZombieType;
        [SerializeField] private List<WeightedZombieTypeEntry> weightedEventZombieTypes = new();
        [SerializeField] private bool mirrorAmbientZombieTypeSelectionWhenEventWeightsUnset = true;
        [SerializeField] private int minHordeSize = 6;
        [SerializeField] private int maxHordeSize = 14;
        [SerializeField] private float hordeSpawnRadius = 10f;

        [Header("Survivor Encounter Events")] [SerializeField]
        private GameObject survivorEncounterPrefab;

        [SerializeField] private int minSurvivorCount = 1;
        [SerializeField] private int maxSurvivorCount = 2;
        [SerializeField] private float survivorSpawnRadius = 4f;

        [Header("Supply Drop Events")] [SerializeField]
        private LootSpawner lootSpawner;

        [Header("Spawn Context")] [SerializeField]
        private float eventSpawnDistanceFromPlayer = 24f;

        [SerializeField] private Transform eventRoot;

        [Header("Debug")] [SerializeField] private bool logEvents;

        [Header("Event Cooldowns")] [SerializeField] [Min(0f)]
        private float samEventCooldownSeconds = 60f;

        private readonly Dictionary<WorldDynamicEventType, float> _eventLastFiredAt = new();

        private float _eventTickTimer;

        private void Awake()
        {
            if (zombieManager == null) zombieManager = FindFirstObjectByType<ZombieManager>();

            if (lootSpawner == null) lootSpawner = FindFirstObjectByType<LootSpawner>();
        }

        public void TickDynamicEvents(Vector3 playerPosition)
        {
            _eventTickTimer += Time.deltaTime;

            if (_eventTickTimer < eventTickInterval) return;

            _eventTickTimer = 0f;

            if (Random.value > eventChancePerTick) return;

            TriggerRandomEvent(playerPosition);
        }

        public void TriggerRandomEvent(Vector3 playerPosition)
        {
            // Build a list of event types that are off cooldown.
            var available = new List<WorldDynamicEventType>(3);
            var now = Time.time;
            for (var i = 0; i < 3; i++)
            {
                var evt = (WorldDynamicEventType)i;
                _eventLastFiredAt.TryGetValue(evt, out var lastFired);
                if (now - lastFired >= samEventCooldownSeconds)
                    available.Add(evt);
            }

            if (available.Count == 0) return;

            var eventType = available[Random.Range(0, available.Count)];
            _eventLastFiredAt[eventType] = now;

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
                default:
                    LogEvent($"Unknown world event type '{eventType}', skipping.");
                    break;
            }
        }

        public void SpawnZombieHorde(Vector3 nearPosition)
        {
            if (zombieManager == null) zombieManager = FindFirstObjectByType<ZombieManager>();

            if (zombieManager == null)
            {
                LogEvent("Zombie horde event skipped (missing ZombieManager)");
                return;
            }

            var minimum = Mathf.Max(1, minHordeSize);
            var maximum = Mathf.Max(minimum, maxHordeSize);
            var hordeSize = Random.Range(minimum, maximum + 1);
            var spawnCenter = ResolveSpawnCenter(nearPosition);
            var selectedZombieType = ResolveEventZombieType();
            zombieManager.SpawnZombieWave(selectedZombieType, spawnCenter, hordeSize, Mathf.Max(0.5f, hordeSpawnRadius));

            LogEvent($"Zombie horde spawned ({hordeSize}) at {spawnCenter}");
        }

        public void SpawnSurvivorEncounter(Vector3 nearPosition)
        {
            if (survivorEncounterPrefab == null)
            {
                LogEvent("Survivor encounter skipped (missing prefab)");
                return;
            }

            var minimum = Mathf.Max(1, minSurvivorCount);
            var maximum = Mathf.Max(minimum, maxSurvivorCount);
            var survivorCount = Random.Range(minimum, maximum + 1);
            var center = ResolveSpawnCenter(nearPosition);
            var parent = eventRoot != null ? eventRoot : transform;

            for (var i = 0; i < survivorCount; i++)
            {
                var offset = Random.insideUnitCircle * Mathf.Max(0f, survivorSpawnRadius);
                var spawnPosition = center + new Vector3(offset.x, 0f, offset.y);
                var rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                Instantiate(survivorEncounterPrefab, spawnPosition, rotation, parent);
            }

            LogEvent($"Survivor encounter spawned ({survivorCount}) near {center}");
        }

        public void SpawnSupplyDrop(Vector3 nearPosition)
        {
            if (lootSpawner == null) lootSpawner = FindFirstObjectByType<LootSpawner>();

            if (lootSpawner == null)
            {
                LogEvent("Supply drop skipped (missing LootSpawner)");
                return;
            }

            var spawnPosition = ResolveSpawnCenter(nearPosition);
            lootSpawner.SpawnLootAt(spawnPosition);
            LogEvent($"Supply drop spawned at {spawnPosition}");
        }

        private Vector3 ResolveSpawnCenter(Vector3 nearPosition)
        {
            var offset = Random.insideUnitCircle;

            if (offset.sqrMagnitude <= 0.0001f) offset = Vector2.right;

            offset.Normalize();
            return nearPosition + new Vector3(offset.x, 0f, offset.y) * Mathf.Max(1f, eventSpawnDistanceFromPlayer);
        }

        private ZombieType ResolveEventZombieType()
        {
            if (TrySelectWeightedEventZombieType(out var weightedType)) return weightedType;

            if (zombieManager == null) zombieManager = FindFirstObjectByType<ZombieManager>();

            if (mirrorAmbientZombieTypeSelectionWhenEventWeightsUnset && zombieManager != null)
            {
                var ambientType = zombieManager.ResolveAmbientZombieTypeForSpawn();
                if (ambientType != null) return ambientType;
            }

            return eventZombieType;
        }

        private bool TrySelectWeightedEventZombieType(out ZombieType selectedZombieType)
        {
            selectedZombieType = null;

            if (weightedEventZombieTypes == null || weightedEventZombieTypes.Count == 0) return false;

            var totalWeight = 0f;
            foreach (var entry in weightedEventZombieTypes)
            {
                if (!TryGetWeightedZombieEntry(entry, out _, out var weight)) continue;
                totalWeight += weight;
            }

            if (totalWeight <= 0f) return false;

            var roll = Random.value * totalWeight;
            foreach (var entry in weightedEventZombieTypes)
            {
                if (!TryGetWeightedZombieEntry(entry, out var zombieType, out var weight)) continue;

                roll -= weight;

                if (roll > 0f) continue;

                selectedZombieType = zombieType;
                return true;
            }

            return TrySelectFirstWeightedZombieType(out selectedZombieType);
        }

        private bool TrySelectFirstWeightedZombieType(out ZombieType selectedZombieType)
        {
            selectedZombieType = null;

            foreach (var entry in weightedEventZombieTypes)
            {
                if (!TryGetWeightedZombieEntry(entry, out var zombieType, out _)) continue;

                selectedZombieType = zombieType;
                return true;
            }

            return false;
        }

        private static bool TryGetWeightedZombieEntry(
            WeightedZombieTypeEntry entry,
            out ZombieType zombieType,
            out float weight)
        {
            zombieType = null;
            weight = 0f;

            if (entry?.zombieType == null) return false;

            var normalizedWeight = Mathf.Max(0f, entry.weight);
            if (normalizedWeight <= 0f) return false;

            zombieType = entry.zombieType;
            weight = normalizedWeight;
            return true;
        }

        private void LogEvent(string message)
        {
            if (!logEvents) return;

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