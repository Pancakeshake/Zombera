using System.Collections.Generic;
using MapMagic.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Zombera.World;

namespace Zombera.Characters
{
    public readonly struct PlayerSpawnSnapConfig
    {
        public readonly float TerrainHeightOffset;
        public readonly float NavMeshBakeRadius;
        public readonly float NavMeshVerticalExtent;
        public readonly float SpawnNavMeshBelowTerrainToleranceMeters;
        public readonly bool PreferLargestConnectedNavMeshRegionForSpawn;
        public readonly int SpawnNavMeshCandidateSamples;
        public readonly int SpawnNavMeshConnectivityProbes;
        public readonly bool LogSpawnTerrainDiagnostics;

        public PlayerSpawnSnapConfig(
            float terrainHeightOffset,
            float navMeshBakeRadius,
            float navMeshVerticalExtent,
            float spawnNavMeshBelowTerrainToleranceMeters,
            bool preferLargestConnectedNavMeshRegionForSpawn,
            int spawnNavMeshCandidateSamples,
            int spawnNavMeshConnectivityProbes,
            bool logSpawnTerrainDiagnostics)
        {
            TerrainHeightOffset = terrainHeightOffset;
            NavMeshBakeRadius = navMeshBakeRadius;
            NavMeshVerticalExtent = navMeshVerticalExtent;
            SpawnNavMeshBelowTerrainToleranceMeters = spawnNavMeshBelowTerrainToleranceMeters;
            PreferLargestConnectedNavMeshRegionForSpawn = preferLargestConnectedNavMeshRegionForSpawn;
            SpawnNavMeshCandidateSamples = spawnNavMeshCandidateSamples;
            SpawnNavMeshConnectivityProbes = spawnNavMeshConnectivityProbes;
            LogSpawnTerrainDiagnostics = logSpawnTerrainDiagnostics;
        }
    }

    public sealed class PlayerSpawnSnapper
    {
        private readonly UnityEngine.Object _logContext;
        private readonly Scene _ownerScene;
        private readonly System.Func<Vector3, Terrain> _resolveSpawnTerrain;
        private readonly TrySampleGroundWithYDelegate _trySampleGroundFromPhysics;

        public delegate bool TrySampleGroundWithYDelegate(Vector3 worldPosition, out float groundY);

        public PlayerSpawnSnapper(
            UnityEngine.Object logContext,
            Scene ownerScene,
            System.Func<Vector3, Terrain> resolveSpawnTerrain,
            TrySampleGroundWithYDelegate trySampleGroundFromPhysics)
        {
            _logContext = logContext;
            _ownerScene = ownerScene;
            _resolveSpawnTerrain = resolveSpawnTerrain;
            _trySampleGroundFromPhysics = trySampleGroundFromPhysics;
        }

        public Vector3 SnapSpawnPosition(
            Vector3 spawnPosition,
            bool spawnPointWasNull,
            Vector3 spawnerTransformPosition,
            bool hasLastNavMeshCenter,
            Vector3 lastNavMeshCenter,
            PlayerSpawnSnapConfig config)
        {
            // If spawn resolved to the spawner's own position (no terrain found at origin) but we
            // have a terrain-fitted NavMesh center from BakeNavMesh, use that XZ so that terrain
            // snapping and NavMesh sampling target the actual MapMagic tile region.
            if (hasLastNavMeshCenter &&
                spawnPointWasNull &&
                Vector3.SqrMagnitude(new Vector3(spawnPosition.x, 0, spawnPosition.z) - new Vector3(spawnerTransformPosition.x, 0, spawnerTransformPosition.z)) < 1f &&
                Vector3.SqrMagnitude(new Vector3(lastNavMeshCenter.x, 0, lastNavMeshCenter.z) - new Vector3(spawnerTransformPosition.x, 0, spawnerTransformPosition.z)) > 100f)
            {
                spawnPosition = new Vector3(lastNavMeshCenter.x, spawnPosition.y, lastNavMeshCenter.z);
                if (config.LogSpawnTerrainDiagnostics)
                {
                    Debug.Log($"[PlayerSpawner] SpawnPlayer: using terrain-fitted NavMesh center {spawnPosition} as spawn origin (no terrain at spawner origin).", _logContext);
                }
            }

            bool terrainSnapped = TrySnapSpawnToTerrain(ref spawnPosition, config.TerrainHeightOffset, out float terrainSurfaceY);
            if (!terrainSnapped)
            {
                terrainSnapped = TrySnapSpawnToPhysicsGround(ref spawnPosition, config.TerrainHeightOffset, out terrainSurfaceY);
            }

            // If terrain and collider layers disagree (for example stacked old/new terrain),
            // prefer the highest physical ground so player and zombie spawns stay aligned.
            if (_trySampleGroundFromPhysics != null && _trySampleGroundFromPhysics(spawnPosition, out float physicsGroundY))
            {
                if (!terrainSnapped || physicsGroundY > terrainSurfaceY + 0.05f)
                {
                    terrainSnapped = true;
                    terrainSurfaceY = physicsGroundY;
                    spawnPosition.y = physicsGroundY + Mathf.Max(0f, config.TerrainHeightOffset);

                    Debug.Log($"[PlayerSpawner] Raised spawn to highest physics ground at {spawnPosition} (PhysicsY={physicsGroundY:F2}).", _logContext);
                }
            }

            // MapMagic tile heightmap fallback (when TerrainCollider isn't ready).
            if (!terrainSnapped)
            {
                terrainSnapped = TrySnapSpawnToMapMagicTileDirect(ref spawnPosition, config.TerrainHeightOffset, out terrainSurfaceY);
            }

            if (!terrainSnapped &&
                TryEstimateNavMeshHeightAtXZ(spawnPosition, Mathf.Max(64f, config.NavMeshBakeRadius * 0.6f), out float estimatedNavMeshY))
            {
                terrainSnapped = true;
                terrainSurfaceY = estimatedNavMeshY;
                spawnPosition.y = estimatedNavMeshY + Mathf.Max(0f, config.TerrainHeightOffset);

                if (config.LogSpawnTerrainDiagnostics)
                {
                    Debug.Log($"[PlayerSpawner] Estimated spawn height from NavMesh triangulation at {spawnPosition} (NavMeshY={estimatedNavMeshY:F2}).", _logContext);
                }
            }

            // Snap spawn to nearest WALKABLE NavMesh point at ground level.
            Vector3 groundOrigin = spawnPosition + Vector3.up * 2f;
            int walkableAreaMask = 1 << 0;
            float navSampleRadius = Mathf.Clamp(config.NavMeshBakeRadius * 0.12f, 8f, 45f);
            bool hasNavMeshHit =
                NavMesh.SamplePosition(groundOrigin, out NavMeshHit hit, navSampleRadius, walkableAreaMask) ||
                NavMesh.SamplePosition(groundOrigin, out hit, navSampleRadius, NavMesh.AllAreas);

            if (!hasNavMeshHit)
            {
                Vector3 elevatedSampleOrigin = new Vector3(
                    spawnPosition.x,
                    spawnPosition.y + Mathf.Max(config.NavMeshVerticalExtent, 64f),
                    spawnPosition.z);
                float elevatedSampleRadius = Mathf.Max(
                    navSampleRadius,
                    Mathf.Max(config.NavMeshVerticalExtent * 1.5f, 96f));

                hasNavMeshHit =
                    NavMesh.SamplePosition(elevatedSampleOrigin, out hit, elevatedSampleRadius, walkableAreaMask) ||
                    NavMesh.SamplePosition(elevatedSampleOrigin, out hit, elevatedSampleRadius, NavMesh.AllAreas);

                if (hasNavMeshHit && config.LogSpawnTerrainDiagnostics)
                {
                    Debug.Log(
                        $"[PlayerSpawner] Recovered NavMesh hit from elevated sample origin {elevatedSampleOrigin} within {elevatedSampleRadius:F1}m at {hit.position}.",
                        _logContext);
                }
            }

            if (hasNavMeshHit)
            {
                float belowTolerance = Mathf.Max(0f, config.SpawnNavMeshBelowTerrainToleranceMeters);
                if (!terrainSnapped || hit.position.y >= terrainSurfaceY - Mathf.Max(0.25f, belowTolerance))
                {
                    Vector3 navMeshSpawn = hit.position;

                    if (config.PreferLargestConnectedNavMeshRegionForSpawn &&
                        TrySelectBetterConnectedNavMeshSpawn(
                            navMeshSpawn,
                            Mathf.Max(navSampleRadius * 4f, 60f),
                            walkableAreaMask,
                            config.SpawnNavMeshCandidateSamples,
                            config.SpawnNavMeshConnectivityProbes,
                            out Vector3 improvedSpawn,
                            out int improvedConnectivityScore))
                    {
                        navMeshSpawn = improvedSpawn;
                        if (config.LogSpawnTerrainDiagnostics)
                        {
                            Debug.Log($"[PlayerSpawner] Selected better-connected NavMesh spawn at {navMeshSpawn} (connectivityScore={improvedConnectivityScore}).", _logContext);
                        }
                    }

                    spawnPosition = navMeshSpawn;
                    Debug.Log($"[PlayerSpawner] Snapped spawn to NavMesh at {spawnPosition}", _logContext);
                }
                else
                {
                    Debug.LogWarning(
                        $"[PlayerSpawner] Ignoring NavMesh hit below terrain surface. NavMeshY={hit.position.y:F2}, TerrainY={terrainSurfaceY:F2}",
                        _logContext);
                }
            }
            else
            {
                Debug.LogWarning(
                    $"[PlayerSpawner] Could not find walkable NavMesh near {groundOrigin} within {navSampleRadius:F1}m — spawning at terrain/raw position.",
                    _logContext);
            }

            return spawnPosition;
        }

        private bool TrySnapSpawnToTerrain(ref Vector3 spawnPosition, float terrainHeightOffset, out float terrainSurfaceY)
        {
            Terrain terrain = _resolveSpawnTerrain != null ? _resolveSpawnTerrain(spawnPosition) : null;
            if (terrain == null || terrain.terrainData == null)
            {
                terrainSurfaceY = spawnPosition.y;
                return false;
            }

            Vector3 terrainOrigin = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;

            float minX = terrainOrigin.x;
            float maxX = terrainOrigin.x + terrainSize.x;
            float minZ = terrainOrigin.z;
            float maxZ = terrainOrigin.z + terrainSize.z;

            float clampedX = Mathf.Clamp(spawnPosition.x, minX, maxX);
            float clampedZ = Mathf.Clamp(spawnPosition.z, minZ, maxZ);
            float sampledY = terrain.SampleHeight(new Vector3(clampedX, terrainOrigin.y, clampedZ)) + terrainOrigin.y;
            terrainSurfaceY = sampledY;

            spawnPosition = new Vector3(clampedX, sampledY + Mathf.Max(0f, terrainHeightOffset), clampedZ);
            Debug.Log($"[PlayerSpawner] Snapped spawn to terrain at {spawnPosition}", _logContext);
            return true;
        }

        private bool TrySnapSpawnToPhysicsGround(ref Vector3 spawnPosition, float terrainHeightOffset, out float terrainSurfaceY)
        {
            if (_trySampleGroundFromPhysics == null || !_trySampleGroundFromPhysics(spawnPosition, out float sampledY))
            {
                terrainSurfaceY = spawnPosition.y;
                return false;
            }

            terrainSurfaceY = sampledY;
            spawnPosition = new Vector3(
                spawnPosition.x,
                sampledY + Mathf.Max(0f, terrainHeightOffset),
                spawnPosition.z);

            Debug.Log($"[PlayerSpawner] Snapped spawn to physics ground at {spawnPosition}", _logContext);
            return true;
        }

        private bool TrySnapSpawnToMapMagicTileDirect(ref Vector3 spawnPosition, float terrainHeightOffset, out float terrainSurfaceY)
        {
            MapMagicObject[] mapMagics = UnityEngine.Object.FindObjectsByType<MapMagicObject>(FindObjectsSortMode.None);
            if (mapMagics == null || mapMagics.Length == 0)
            {
                terrainSurfaceY = spawnPosition.y;
                return false;
            }

            Terrain bestTerrain = null;
            float bestDist = float.PositiveInfinity;

            foreach (MapMagicObject mm in mapMagics)
            {
                if (mm == null || (_ownerScene.IsValid() && mm.gameObject.scene != _ownerScene))
                {
                    continue;
                }

                foreach (Terrain t in mm.tiles.AllActiveTerrains())
                {
                    if (t == null || t.terrainData == null)
                    {
                        continue;
                    }

                    if (TerrainResolver.TerrainContainsXZ(t, spawnPosition))
                    {
                        bestTerrain = t;
                        bestDist = 0f;
                        break;
                    }

                    Vector3 tp = t.transform.position;
                    Vector3 ts = t.terrainData.size;
                    float cx = Mathf.Clamp(spawnPosition.x, tp.x, tp.x + ts.x);
                    float cz = Mathf.Clamp(spawnPosition.z, tp.z, tp.z + ts.z);
                    float dx = cx - spawnPosition.x;
                    float dz = cz - spawnPosition.z;
                    float d = dx * dx + dz * dz;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestTerrain = t;
                    }
                }

                if (bestTerrain != null && bestDist == 0f)
                {
                    break;
                }
            }

            if (bestTerrain == null)
            {
                terrainSurfaceY = spawnPosition.y;
                return false;
            }

            Vector3 terrainOrigin = bestTerrain.transform.position;
            Vector3 terrainSize = bestTerrain.terrainData.size;
            float clampedX = Mathf.Clamp(spawnPosition.x, terrainOrigin.x, terrainOrigin.x + terrainSize.x);
            float clampedZ = Mathf.Clamp(spawnPosition.z, terrainOrigin.z, terrainOrigin.z + terrainSize.z);
            float sampledY = bestTerrain.SampleHeight(new Vector3(clampedX, terrainOrigin.y, clampedZ)) + terrainOrigin.y;
            terrainSurfaceY = sampledY;
            spawnPosition = new Vector3(clampedX, sampledY + Mathf.Max(0f, terrainHeightOffset), clampedZ);
            Debug.Log($"[PlayerSpawner] Snapped spawn to MapMagic tile heightmap at {spawnPosition} (tile={bestTerrain.name})", _logContext);
            return true;
        }

        private static bool TryEstimateNavMeshHeightAtXZ(Vector3 worldPosition, float maxPlanarDistance, out float estimatedY)
        {
            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            if (triangulation.vertices == null || triangulation.vertices.Length == 0)
            {
                estimatedY = worldPosition.y;
                return false;
            }

            float maxPlanarDistanceSqr = maxPlanarDistance * maxPlanarDistance;
            float bestSqrDistance = float.PositiveInfinity;
            float bestY = worldPosition.y;

            for (int i = 0; i < triangulation.vertices.Length; i++)
            {
                Vector3 vertex = triangulation.vertices[i];
                if (!float.IsFinite(vertex.x) || !float.IsFinite(vertex.y) || !float.IsFinite(vertex.z))
                {
                    continue;
                }

                float dx = vertex.x - worldPosition.x;
                float dz = vertex.z - worldPosition.z;
                float sqrDistance = dx * dx + dz * dz;
                if (sqrDistance > maxPlanarDistanceSqr || sqrDistance >= bestSqrDistance)
                {
                    continue;
                }

                bestSqrDistance = sqrDistance;
                bestY = vertex.y;
            }

            if (float.IsInfinity(bestSqrDistance))
            {
                estimatedY = worldPosition.y;
                return false;
            }

            estimatedY = bestY;
            return true;
        }

        private static bool TrySelectBetterConnectedNavMeshSpawn(
            Vector3 initialNavMeshPoint,
            float searchRadius,
            int areaMask,
            int candidateSamples,
            int connectivityProbes,
            out Vector3 bestPoint,
            out int bestConnectivityScore)
        {
            bestPoint = initialNavMeshPoint;
            bestConnectivityScore = -1;

            int candidateCount = Mathf.Clamp(candidateSamples, 8, 256);
            int probeCount = Mathf.Clamp(connectivityProbes, 4, 128);

            List<Vector3> probes = new List<Vector3>(probeCount);
            for (int i = 0; i < probeCount; i++)
            {
                Vector2 planar = UnityEngine.Random.insideUnitCircle * Mathf.Max(8f, searchRadius);
                Vector3 desired = new Vector3(initialNavMeshPoint.x + planar.x, initialNavMeshPoint.y + 2f, initialNavMeshPoint.z + planar.y);

                if (NavMesh.SamplePosition(desired, out NavMeshHit hit, Mathf.Max(12f, searchRadius * 0.30f), areaMask) ||
                    NavMesh.SamplePosition(desired, out hit, Mathf.Max(12f, searchRadius * 0.30f), NavMesh.AllAreas))
                {
                    probes.Add(hit.position);
                }
            }

            if (probes.Count == 0)
            {
                return false;
            }

            NavMeshPath path = new NavMeshPath();
            for (int c = 0; c < candidateCount; c++)
            {
                Vector3 candidate = initialNavMeshPoint;
                if (c > 0)
                {
                    Vector2 planar = UnityEngine.Random.insideUnitCircle * Mathf.Max(6f, searchRadius);
                    Vector3 desired = new Vector3(initialNavMeshPoint.x + planar.x, initialNavMeshPoint.y + 2f, initialNavMeshPoint.z + planar.y);

                    if (NavMesh.SamplePosition(desired, out NavMeshHit hit, Mathf.Max(10f, searchRadius * 0.25f), areaMask) ||
                        NavMesh.SamplePosition(desired, out hit, Mathf.Max(10f, searchRadius * 0.25f), NavMesh.AllAreas))
                    {
                        candidate = hit.position;
                    }
                }

                int score = 0;
                for (int p = 0; p < probes.Count; p++)
                {
                    Vector3 target = probes[p];
                    if (NavMesh.CalculatePath(candidate, target, areaMask, path) && path.status == NavMeshPathStatus.PathComplete)
                    {
                        score++;
                    }
                }

                if (score > bestConnectivityScore)
                {
                    bestConnectivityScore = score;
                    bestPoint = candidate;
                    if (bestConnectivityScore >= probes.Count)
                    {
                        break;
                    }
                }
            }

            return bestConnectivityScore > 0;
        }
    }
}

