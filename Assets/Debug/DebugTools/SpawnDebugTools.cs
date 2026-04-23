using UnityEngine;
using Zombera.AI;
using Zombera.Data;
using Zombera.Debugging.DebugLogging;
using Zombera.Inventory;
using Zombera.Systems;
using Zombera.World;

namespace Zombera.Debugging.DebugTools
{
    /// <summary>
    /// Debug spawn helper module.
    /// Responsibilities:
    /// - Expose spawn test entry points
    /// - Hold prefab references for debug spawning
    /// - Route spawn requests from keybinds and debug menu
    /// </summary>
    public sealed class SpawnDebugTools : MonoBehaviour, IDebugTool
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject zombiePrefab;
        [SerializeField] private GameObject survivorPrefab;
        [SerializeField] private GameObject lootContainerPrefab;

        [Header("Systems")]
        [SerializeField] private ZombieManager zombieManager;
        [SerializeField] private ZombieSpawner zombieSpawner;
        [SerializeField] private LootManager lootManager;
        [SerializeField] private LootSpawner worldLootSpawner;
        [SerializeField] private ZombieType debugZombieType;

        [Header("Spawn Context")]
        [SerializeField] private Transform spawnAnchor;
        [SerializeField] private Transform spawnParent;
        [SerializeField] private bool parentDebugSpawns = true;
        [SerializeField] private float hordeSpawnRadius = 8f;
        [SerializeField] private int defaultHordeSize = 10;

        public string ToolName => nameof(SpawnDebugTools);
        public bool IsToolEnabled { get; private set; } = true;

        private void OnEnable()
        {
            AutoWireRuntimeReferences();
            DebugManager.Instance?.RegisterDebugTool(this);
        }

        private void OnDisable()
        {
            DebugManager.Instance?.UnregisterDebugTool(this);
        }

        public void SetToolEnabled(bool enabled)
        {
            IsToolEnabled = enabled;
        }

        public void SpawnZombie()
        {
            if (!IsToolEnabled)
            {
                return;
            }

            Vector3 spawnPosition = ResolveOffsetPosition(1.5f);
            SpawnZombieAt(spawnPosition, true);
        }

        public void SpawnSurvivor()
        {
            if (!IsToolEnabled)
            {
                return;
            }

            if (survivorPrefab == null)
            {
                DebugLogger.LogWarning(LogCategory.Squad, "SpawnSurvivor() skipped (missing prefab)", this);
                return;
            }

            Vector3 spawnPosition = ResolveOffsetPosition(2f);
            Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            Transform parent = ResolveSpawnParent();

            GameObject survivorObject = parent != null
                ? Instantiate(survivorPrefab, spawnPosition, rotation, parent)
                : Instantiate(survivorPrefab, spawnPosition, rotation);

            if (survivorObject != null)
            {
                DebugLogger.Log(LogCategory.Squad, "SpawnSurvivor() completed", this);
            }
        }

        public void SpawnLootContainer()
        {
            if (!IsToolEnabled)
            {
                return;
            }

            Vector3 spawnPosition = ResolveOffsetPosition(2.5f);

            if (worldLootSpawner != null)
            {
                worldLootSpawner.SpawnLootAt(spawnPosition);
                DebugLogger.Log(LogCategory.Inventory, "SpawnLootContainer() routed through LootSpawner", this);
                return;
            }

            if (lootContainerPrefab == null)
            {
                DebugLogger.LogWarning(LogCategory.Inventory, "SpawnLootContainer() skipped (missing prefab)", this);
                return;
            }

            Transform parent = ResolveSpawnParent();
            Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            GameObject lootObject = parent != null
                ? Instantiate(lootContainerPrefab, spawnPosition, rotation, parent)
                : Instantiate(lootContainerPrefab, spawnPosition, rotation);

            if (lootManager != null && lootObject != null && lootObject.TryGetComponent(out LootContainer container))
            {
                lootManager.RegisterContainer(container);
            }

            DebugLogger.Log(LogCategory.Inventory, "SpawnLootContainer() completed", this);
        }

        public void SpawnZombieHorde()
        {
            SpawnZombieHorde(defaultHordeSize);
        }

        public void SpawnZombieHorde(int count)
        {
            if (!IsToolEnabled)
            {
                return;
            }

            int hordeCount = Mathf.Max(1, count);
            Vector3 center = ResolveSpawnOrigin();
            int successCount = 0;

            for (int i = 0; i < hordeCount; i++)
            {
                Vector2 offset = Random.insideUnitCircle * Mathf.Max(0.1f, hordeSpawnRadius);
                Vector3 spawnPosition = center + new Vector3(offset.x, 0f, offset.y);

                if (SpawnZombieAt(spawnPosition, false))
                {
                    successCount++;
                }
            }

            DebugLogger.Log(LogCategory.World, $"SpawnZombieHorde({hordeCount}) completed: {successCount} spawned", this);
        }

        private bool SpawnZombieAt(Vector3 spawnPosition, bool logOutcome)
        {
            AutoWireRuntimeReferences();

            ZombieAI zombie = null;

            if (zombieManager != null)
            {
                zombie = zombieManager.SpawnZombie(debugZombieType, spawnPosition);
            }

            if (zombie == null && zombieSpawner != null)
            {
                zombie = zombieSpawner.SpawnZombie(debugZombieType, spawnPosition);
            }

            if (zombie == null && zombiePrefab != null)
            {
                Transform parent = ResolveSpawnParent();
                Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                GameObject zombieObject = parent != null
                    ? Instantiate(zombiePrefab, spawnPosition, rotation, parent)
                    : Instantiate(zombiePrefab, spawnPosition, rotation);

                zombie = zombieObject != null ? zombieObject.GetComponent<ZombieAI>() : null;
                zombie?.Initialize();
            }

            if (!logOutcome)
            {
                return zombie != null;
            }

            if (zombie != null)
            {
                DebugLogger.Log(LogCategory.World, "SpawnZombie() completed", this);
            }
            else
            {
                DebugLogger.LogWarning(LogCategory.World, "SpawnZombie() failed (no manager/spawner/prefab path succeeded)", this);
            }

            return zombie != null;
        }

        private void AutoWireRuntimeReferences()
        {
            if (zombieManager == null)
            {
                zombieManager = FindFirstObjectByType<ZombieManager>();
            }

            if (zombieSpawner == null)
            {
                zombieSpawner = FindFirstObjectByType<ZombieSpawner>();
            }

            if (lootManager == null)
            {
                lootManager = FindFirstObjectByType<LootManager>();
            }

            if (worldLootSpawner == null)
            {
                worldLootSpawner = FindFirstObjectByType<LootSpawner>();
            }
        }

        private Vector3 ResolveSpawnOrigin()
        {
            if (spawnAnchor != null)
            {
                return spawnAnchor.position;
            }

            return transform.position;
        }

        private Vector3 ResolveOffsetPosition(float radius)
        {
            Vector3 origin = ResolveSpawnOrigin();
            Vector2 offset = Random.insideUnitCircle * Mathf.Max(0f, radius);
            return origin + new Vector3(offset.x, 0f, offset.y);
        }

        private Transform ResolveSpawnParent()
        {
            if (!parentDebugSpawns)
            {
                return null;
            }

            if (spawnParent != null)
            {
                return spawnParent;
            }

            return transform;
        }
    }
}