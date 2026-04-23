using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Zombera.AI.Brains;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Data;
using Zombera.Systems;
using Zombera.World;

namespace Zombera.AI
{
    /// <summary>
    /// Responsible for spawning zombies from type data and regional difficulty rules.
    /// </summary>
    public sealed class ZombieSpawner : MonoBehaviour
    {
        [SerializeField] private ZombieAI zombiePrefab;
        [SerializeField] private bool useObjectPooling = true;
        [SerializeField] private int initialPoolSize = 24;

        [Header("Placement")]
        [SerializeField] private bool alignSpawnToTerrain = true;
        [SerializeField] private Terrain spawnTerrain;
        [SerializeField] private bool snapSpawnToNavMesh = true;
        [SerializeField] private float navMeshSampleDistance = 20f;
        [SerializeField] private float spawnHeightOffset;

        [Header("Horde")]
        [SerializeField] private HordeManager hordeManager;

        private readonly Queue<ZombieAI> zombiePool = new Queue<ZombieAI>();
        private readonly HashSet<ZombieAI> activeZombies = new HashSet<ZombieAI>();
        private bool hasLoggedMissingPrefabWarning;
        private int _spawnerHordeId = -1;

        public void SetZombiePrefab(ZombieAI prefab)
        {
            if (prefab == null)
            {
                return;
            }

            zombiePrefab = prefab;
            hasLoggedMissingPrefabWarning = false;
        }

        private void Awake()
        {
            _ = EnsureZombiePrefab();

            if (useObjectPooling)
            {
                PrewarmPool(initialPoolSize);
            }
        }

        public void PrewarmPool(int count)
        {
            if (count <= 0 || !EnsureZombiePrefab())
            {
                return;
            }

            while (zombiePool.Count < count)
            {
                ZombieAI zombie = Instantiate(zombiePrefab, transform);
                zombie.gameObject.SetActive(false);
                zombiePool.Enqueue(zombie);
            }
        }

        public ZombieAI SpawnZombie(ZombieType zombieType, Vector3 position)
        {
            if (!EnsureZombiePrefab())
            {
                return null;
            }

            ZombieAI zombie = GetOrCreateZombie();
            Vector3 resolvedPosition = ResolveSpawnPosition(position);

            zombie.transform.SetPositionAndRotation(resolvedPosition, Quaternion.identity);
            zombie.gameObject.SetActive(true);
            ConfigureSpawnedZombie(zombie, zombieType);
            zombie.Initialize();

            // Keep the spawn position stable. UnitController will self-enable its agent
            // with retries, and forcing an immediate warp can snap to stale navmesh under terrain.
            zombie.transform.SetPositionAndRotation(resolvedPosition, Quaternion.identity);

            ApplySpawnedZombieVisuals(zombie);
            activeZombies.Add(zombie);

            EventSystem.PublishGlobal(new ZombieSpawnedEvent
            {
                ZombieTypeId = zombieType != null ? zombieType.zombieTypeId : string.Empty,
                Position = resolvedPosition,
                Zombie = zombie
            });

            // Register with HordeManager if assigned and the archetype wants horde membership.
            if (hordeManager != null && zombieType != null && zombieType.hordeAffinity > 0f)
            {
                if (_spawnerHordeId < 0)
                {
                    _spawnerHordeId = hordeManager.CreateHorde(new List<ZombieAI>());
                }
                hordeManager.AddZombieToHorde(_spawnerHordeId, zombie);
            }

            return zombie;
        }

        public void SpawnWave(ZombieType zombieType, Vector3 centerPosition, int count, float radius)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 spawnPosition = centerPosition + new Vector3(offset.x, 0f, offset.y);

                // Pull from pool for large waves to avoid instantiate spikes.
                if (useObjectPooling && zombiePool.Count > 0)
                {
                    ReusePooledZombie(zombieType, spawnPosition);
                }
                else
                {
                    SpawnZombie(zombieType, spawnPosition);
                }
            }
        }

        private void ReusePooledZombie(ZombieType zombieType, Vector3 position)
        {
            ZombieAI zombie = zombiePool.Dequeue();

            if (zombie == null)
            {
                SpawnZombie(zombieType, position);
                return;
            }

            zombie.transform.position = position;
            zombie.gameObject.SetActive(true);
            activeZombies.Add(zombie);
        }

        public void ReturnToPool(ZombieAI zombie)
        {
            if (zombie == null)
            {
                return;
            }

            activeZombies.Remove(zombie);

            if (!useObjectPooling)
            {
                Destroy(zombie.gameObject);
                return;
            }

            zombie.SetActive(false);
            zombie.transform.SetParent(transform);
            zombie.gameObject.SetActive(false);
            zombiePool.Enqueue(zombie);
        }

        private ZombieAI GetOrCreateZombie()
        {
            if (useObjectPooling && zombiePool.Count > 0)
            {
                return zombiePool.Dequeue();
            }

            return Instantiate(zombiePrefab);
        }

        private bool EnsureZombiePrefab()
        {
            if (zombiePrefab != null)
            {
                return true;
            }

            if (TryResolveSceneTemplateZombie(out ZombieAI sceneTemplate))
            {
                zombiePrefab = sceneTemplate;
                // Silence and park the scene-placed template so it isn't an active
                // physics object sitting at its editor position (which may be far from
                // the terrain, causing it to fall through the world indefinitely).
                sceneTemplate.gameObject.SetActive(false);
                sceneTemplate.transform.SetParent(transform, false);
                sceneTemplate.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                hasLoggedMissingPrefabWarning = false;
                return true;
            }

            zombiePrefab = BuildRuntimeZombiePrototype();

            if (zombiePrefab != null)
            {
                hasLoggedMissingPrefabWarning = false;
                return true;
            }

            if (!hasLoggedMissingPrefabWarning)
            {
                hasLoggedMissingPrefabWarning = true;
                Debug.LogWarning("ZombieSpawner has no zombiePrefab and runtime prototype creation failed.", this);
            }

            return false;
        }

        private bool TryResolveSceneTemplateZombie(out ZombieAI template)
        {
            template = null;

            ZombieAI[] sceneZombies = FindObjectsByType<ZombieAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (sceneZombies == null || sceneZombies.Length == 0)
            {
                return false;
            }

            int bestScore = int.MinValue;

            for (int i = 0; i < sceneZombies.Length; i++)
            {
                ZombieAI candidate = sceneZombies[i];

                if (candidate == null)
                {
                    continue;
                }

                // Ignore pooled/runtime prototypes created under this spawner.
                if (candidate.transform.IsChildOf(transform))
                {
                    continue;
                }

                int score = 0;
                string candidateName = candidate.gameObject.name;

                if (!string.IsNullOrEmpty(candidateName) && candidateName.IndexOf("Zombie Type1", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score += 100;
                }

                if (candidate.GetComponent<ZombieUmaAppearance>() == null)
                {
                    score += 10;
                }

                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                template = candidate;
            }

            return template != null;
        }

        private Vector3 ResolveSpawnPosition(Vector3 requestedPosition)
        {
            Vector3 resolvedPosition = requestedPosition;
            Terrain terrain = ResolveTerrainForPosition(requestedPosition);
            bool hasSurfaceHeight = false;
            float surfaceY = resolvedPosition.y;

            if (alignSpawnToTerrain && terrain != null && terrain.terrainData != null)
            {
                Vector3 terrainOrigin = terrain.GetPosition();
                Vector3 terrainSize = terrain.terrainData.size;

                float minX = terrainOrigin.x;
                float maxX = terrainOrigin.x + terrainSize.x;
                float minZ = terrainOrigin.z;
                float maxZ = terrainOrigin.z + terrainSize.z;

                float clampedX = Mathf.Clamp(resolvedPosition.x, minX, maxX);
                float clampedZ = Mathf.Clamp(resolvedPosition.z, minZ, maxZ);

                resolvedPosition.x = clampedX;
                resolvedPosition.z = clampedZ;

                surfaceY = terrain.SampleHeight(new Vector3(clampedX, terrainOrigin.y, clampedZ)) + terrainOrigin.y;
                resolvedPosition.y = surfaceY;
                hasSurfaceHeight = true;
            }

            if (TrySampleGroundFromPhysics(resolvedPosition, out float physicsGroundY))
            {
                if (!hasSurfaceHeight || physicsGroundY > surfaceY)
                {
                    surfaceY = physicsGroundY;
                    hasSurfaceHeight = true;
                }

                resolvedPosition.y = surfaceY;
            }

            if (snapSpawnToNavMesh)
            {
                Vector3 sampleOrigin = resolvedPosition + Vector3.up * 2f;
                float sampleDistance = Mathf.Max(0.5f, navMeshSampleDistance);
                const int walkableAreaMask = 1 << 0;

                bool hasNavMeshHit =
                    NavMesh.SamplePosition(sampleOrigin, out NavMeshHit navHit, sampleDistance, walkableAreaMask) ||
                    NavMesh.SamplePosition(sampleOrigin, out navHit, sampleDistance, NavMesh.AllAreas);

                if (hasNavMeshHit)
                {
                    // Ignore stale/old navmesh hits that sit below the sampled ground surface.
                    if (!hasSurfaceHeight || navHit.position.y >= surfaceY - 0.25f)
                    {
                        resolvedPosition = navHit.position;
                    }
                }
            }

            if (hasSurfaceHeight && resolvedPosition.y < surfaceY - 0.05f)
            {
                resolvedPosition.y = surfaceY;
            }

            resolvedPosition.y += spawnHeightOffset;
            return resolvedPosition;
        }

        private static bool TrySampleGroundFromPhysics(Vector3 worldPosition, out float groundY)
        {
            Vector3 rayOrigin = worldPosition + Vector3.up * 1200f;

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 2600f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                groundY = hit.point.y;
                return true;
            }

            groundY = worldPosition.y;
            return false;
        }

        private Terrain ResolveTerrainForPosition(Vector3 worldPosition)
        {
            Terrain resolvedTerrain = TerrainResolver.ResolveTerrainForPosition(worldPosition, spawnTerrain);

            if (resolvedTerrain != null)
            {
                spawnTerrain = resolvedTerrain;
            }

            return resolvedTerrain;
        }

        private ZombieAI BuildRuntimeZombiePrototype()
        {
            GameObject root = new GameObject("RuntimeZombiePrototype");
            root.transform.SetParent(transform, false);
            root.SetActive(false);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.up;

            if (body.TryGetComponent(out Collider bodyCollider))
            {
                bodyCollider.enabled = false;
            }

            UnitController controller = root.AddComponent<UnitController>();
            controller.SetRole(UnitRole.Zombie);
            controller.SetMoveSpeed(1.6f);

            UnitHealth health = root.AddComponent<UnitHealth>();
            _ = health;
            UnitCombat combat = root.AddComponent<UnitCombat>();
            _ = combat;
            UnitStats stats = root.AddComponent<UnitStats>();
            _ = stats;

            Unit unit = root.AddComponent<Unit>();
            unit.SetRole(UnitRole.Zombie);

            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 1f, 0f);
            collider.height = 2f;
            collider.radius = 0.3f;

            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.enabled = false;
            agent.radius = 0.3f;
            agent.height = 2f;
            agent.speed = 1.6f;
            agent.acceleration = 20f;
            agent.angularSpeed = 200f;
            agent.stoppingDistance = 0.2f;

            _ = root.AddComponent<ZombieStateMachine>();
            _ = root.AddComponent<ZombieAnimationController>();
            ZombieAI ai = root.AddComponent<ZombieAI>();
            unit.SetOptionalAI(ai);
            return ai;
        }

        private static void ConfigureSpawnedZombie(ZombieAI zombie, ZombieType zombieType)
        {
            if (zombie == null)
            {
                return;
            }

            Unit unit = zombie.GetComponent<Unit>();

            if (unit != null)
            {
                unit.SetRole(UnitRole.Zombie);
                unit.SetOptionalAI(zombie);
            }

            UnitController unitController = zombie.GetComponent<UnitController>();

            if (unitController != null)
            {
                unitController.SetRole(UnitRole.Zombie);
            }

            PlayerInputController playerInputController = zombie.GetComponent<PlayerInputController>();

            if (playerInputController != null)
            {
                playerInputController.enabled = false;
            }

            if (zombieType == null)
            {
                return;
            }

            if (unitController != null)
            {
                unitController.SetMoveSpeed(zombieType.moveSpeed);
            }

            UnitHealth unitHealth = zombie.GetComponent<UnitHealth>();

            if (unitHealth != null)
            {
                unitHealth.SetMaxHealth(zombieType.baseHealth, true);
            }

            UnitCombat unitCombat = zombie.GetComponent<UnitCombat>();

            if (unitCombat != null)
            {
                // 50% slower than the previous 0.5s baseline.
                unitCombat.SetAttackCooldownSeconds(2f);
            }

            UnitStats unitStats = zombie.GetComponent<UnitStats>();

            if (unitStats != null)
            {
                int melee = Mathf.Clamp(Mathf.RoundToInt(zombieType.attackDamage * 3f), 1, 100);
                int strength = Mathf.Clamp(Mathf.RoundToInt(zombieType.attackDamage * 2f), 1, 100);

                unitStats.SetStrengthBaseHealth(zombieType.baseHealth, refillCurrentHealth: true);

                unitStats.SetSkill(UnitSkillType.Melee, melee);
                unitStats.SetSkill(UnitSkillType.Strength, strength);
            }

            zombie.SetAITickInterval(zombieType.defaultAiTickInterval / Mathf.Max(0.1f, zombieType.aggressionMultiplier));

            // Apply archetype perception radius if non-zero.
            if (zombieType.perceptionRadius > 0f)
            {
                zombie.SetDetectionRange(zombieType.perceptionRadius);
            }

            // Propagate archetype profile into ZombieBrain if present.
            ZombieBrain brain = zombie.GetComponent<ZombieBrain>();
            brain?.ApplyArchetypeProfile(zombieType);
        }

        private static void ApplySpawnedZombieVisuals(ZombieAI zombie)
        {
            if (zombie == null)
            {
                return;
            }

            ZombieUmaAppearance umaAppearance = zombie.GetComponent<ZombieUmaAppearance>();
            umaAppearance?.ApplyRandomAppearance();
        }

    }
}