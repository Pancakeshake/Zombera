#region

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Zombera.World;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

#endregion

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
        public delegate bool TrySampleGroundWithYDelegate(Vector3 worldPosition, out float groundY);

        private readonly Object _logContext;
        private readonly Scene _ownerScene;
        private readonly Func<Vector3, Terrain> _resolveSpawnTerrain;
        private readonly TrySampleGroundWithYDelegate _trySampleGroundFromPhysics;

        public PlayerSpawnSnapper(
            Object logContext,
            Scene ownerScene,
            Func<Vector3, Terrain> resolveSpawnTerrain,
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
                Vector3.SqrMagnitude(new Vector3(spawnPosition.x, 0, spawnPosition.z) -
                                     new Vector3(spawnerTransformPosition.x, 0, spawnerTransformPosition.z)) < 1f &&
                Vector3.SqrMagnitude(new Vector3(lastNavMeshCenter.x, 0, lastNavMeshCenter.z) -
                                     new Vector3(spawnerTransformPosition.x, 0, spawnerTransformPosition.z)) > 100f)
            {
                spawnPosition = new Vector3(lastNavMeshCenter.x, spawnPosition.y, lastNavMeshCenter.z);
                if (config.LogSpawnTerrainDiagnostics)
                    Debug.Log(
                        $"[PlayerSpawner] SpawnPlayer: using terrain-fitted NavMesh center {spawnPosition} as spawn origin (no terrain at spawner origin).",
                        _logContext);
            }

            var terrainSnapped =
                TrySnapSpawnToTerrain(ref spawnPosition, config.TerrainHeightOffset, out var terrainSurfaceY) ||
                TrySnapSpawnToPhysicsGround(ref spawnPosition, config.TerrainHeightOffset, out terrainSurfaceY);

            // MapMagic tile heightmap fallback (when TerrainCollider isn't ready).
            if (!terrainSnapped)
                terrainSnapped = TrySnapSpawnToMapMagicTileDirect(ref spawnPosition, config.TerrainHeightOffset,
                    out terrainSurfaceY);

            if (!terrainSnapped &&
                TryEstimateNavMeshHeightAtXZ(spawnPosition, Mathf.Max(64f, config.NavMeshBakeRadius * 0.6f),
                    out var estimatedNavMeshY))
            {
                terrainSnapped = true;
                terrainSurfaceY = estimatedNavMeshY;
                spawnPosition.y = estimatedNavMeshY + Mathf.Max(0f, config.TerrainHeightOffset);

                if (config.LogSpawnTerrainDiagnostics)
                    Debug.Log(
                        $"[PlayerSpawner] Estimated spawn height from NavMesh triangulation at {spawnPosition} (NavMeshY={estimatedNavMeshY:F2}).",
                        _logContext);
            }

            // Snap spawn to nearest WALKABLE NavMesh point at ground level.
            var groundOrigin = spawnPosition + Vector3.up * 2f;
            const int walkableAreaMask = 1;
            var navSampleRadius = Mathf.Clamp(config.NavMeshBakeRadius * 0.12f, 8f, 45f);
            var hasNavMeshHit =
                NavMesh.SamplePosition(groundOrigin, out var hit, navSampleRadius, walkableAreaMask) ||
                NavMesh.SamplePosition(groundOrigin, out hit, navSampleRadius, NavMesh.AllAreas);

            if (!hasNavMeshHit)
            {
                var elevatedSampleOrigin = new Vector3(
                    spawnPosition.x,
                    spawnPosition.y + Mathf.Max(config.NavMeshVerticalExtent, 64f),
                    spawnPosition.z);
                var elevatedSampleRadius = Mathf.Max(
                    navSampleRadius,
                    Mathf.Max(config.NavMeshVerticalExtent * 1.5f, 96f));

                hasNavMeshHit =
                    NavMesh.SamplePosition(elevatedSampleOrigin, out hit, elevatedSampleRadius, walkableAreaMask) ||
                    NavMesh.SamplePosition(elevatedSampleOrigin, out hit, elevatedSampleRadius, NavMesh.AllAreas);

                if (hasNavMeshHit && config.LogSpawnTerrainDiagnostics)
                    Debug.Log(
                        $"[PlayerSpawner] Recovered NavMesh hit from elevated sample origin {elevatedSampleOrigin} within {elevatedSampleRadius:F1}m at {hit.position}.",
                        _logContext);
            }

            if (hasNavMeshHit)
            {
                var belowTolerance = Mathf.Max(0f, config.SpawnNavMeshBelowTerrainToleranceMeters);
                if (!terrainSnapped || hit.position.y >= terrainSurfaceY - Mathf.Max(0.25f, belowTolerance))
                {
                    var navMeshSpawn = hit.position;

                    if (config.PreferLargestConnectedNavMeshRegionForSpawn &&
                        TrySelectBetterConnectedNavMeshSpawn(
                            navMeshSpawn,
                            Mathf.Max(navSampleRadius * 4f, 60f),
                            walkableAreaMask,
                            config.SpawnNavMeshCandidateSamples,
                            config.SpawnNavMeshConnectivityProbes,
                            out var improvedSpawn,
                            out var improvedConnectivityScore))
                    {
                        navMeshSpawn = improvedSpawn;
                        if (config.LogSpawnTerrainDiagnostics)
                            Debug.Log(
                                $"[PlayerSpawner] Selected better-connected NavMesh spawn at {navMeshSpawn} (connectivityScore={improvedConnectivityScore}).",
                                _logContext);
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

        private bool TrySnapSpawnToTerrain(ref Vector3 spawnPosition, float terrainHeightOffset,
            out float terrainSurfaceY)
        {
            var terrain = _resolveSpawnTerrain?.Invoke(spawnPosition);
            if (terrain == null || terrain.terrainData == null)
            {
                terrainSurfaceY = spawnPosition.y;
                return false;
            }

            var terrainOrigin = terrain.transform.position;
            var terrainSize = terrain.terrainData.size;

            var minX = terrainOrigin.x;
            var maxX = terrainOrigin.x + terrainSize.x;
            var minZ = terrainOrigin.z;
            var maxZ = terrainOrigin.z + terrainSize.z;

            var clampedX = Mathf.Clamp(spawnPosition.x, minX, maxX);
            var clampedZ = Mathf.Clamp(spawnPosition.z, minZ, maxZ);
            var sampledY = terrain.SampleHeight(new Vector3(clampedX, terrainOrigin.y, clampedZ)) + terrainOrigin.y;
            terrainSurfaceY = sampledY;

            spawnPosition = new Vector3(clampedX, sampledY + Mathf.Max(0f, terrainHeightOffset), clampedZ);
            Debug.Log($"[PlayerSpawner] Snapped spawn to terrain at {spawnPosition}", _logContext);
            return true;
        }

        private bool TrySnapSpawnToPhysicsGround(ref Vector3 spawnPosition, float terrainHeightOffset,
            out float terrainSurfaceY)
        {
            if (_trySampleGroundFromPhysics == null || !_trySampleGroundFromPhysics(spawnPosition, out var sampledY))
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

        private bool TrySnapSpawnToMapMagicTileDirect(ref Vector3 spawnPosition, float terrainHeightOffset,
            out float terrainSurfaceY)
        {
            var bestTerrain = SpawnPointSelector.FindNearestActiveTerrain(spawnPosition, _ownerScene);
            if (bestTerrain == null || bestTerrain.terrainData == null)
            {
                terrainSurfaceY = spawnPosition.y;
                return false;
            }

            var terrainOrigin = bestTerrain.transform.position;
            var terrainSize = bestTerrain.terrainData.size;
            var clampedX = Mathf.Clamp(spawnPosition.x, terrainOrigin.x, terrainOrigin.x + terrainSize.x);
            var clampedZ = Mathf.Clamp(spawnPosition.z, terrainOrigin.z, terrainOrigin.z + terrainSize.z);
            var sampledY = bestTerrain.SampleHeight(new Vector3(clampedX, terrainOrigin.y, clampedZ)) + terrainOrigin.y;
            terrainSurfaceY = sampledY;
            spawnPosition = new Vector3(clampedX, sampledY + Mathf.Max(0f, terrainHeightOffset), clampedZ);
            Debug.Log(
                $"[PlayerSpawner] Snapped spawn to tile heightmap at {spawnPosition} (tile={bestTerrain.name})",
                _logContext);
            return true;
        }

        private static bool TryEstimateNavMeshHeightAtXZ(Vector3 worldPosition, float maxPlanarDistance,
            out float estimatedY)
        {
            var triangulation = NavMesh.CalculateTriangulation();
            if (triangulation.vertices == null || triangulation.vertices.Length == 0)
            {
                estimatedY = worldPosition.y;
                return false;
            }

            var maxPlanarDistanceSqr = maxPlanarDistance * maxPlanarDistance;
            var bestSqrDistance = float.PositiveInfinity;
            var bestY = worldPosition.y;

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var vertex in triangulation.vertices)
            {
                if (!float.IsFinite(vertex.x) || !float.IsFinite(vertex.y) || !float.IsFinite(vertex.z)) continue;

                var dx = vertex.x - worldPosition.x;
                var dz = vertex.z - worldPosition.z;
                var sqrDistance = dx * dx + dz * dz;
                if (sqrDistance > maxPlanarDistanceSqr || sqrDistance >= bestSqrDistance) continue;

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

            var candidateCount = Mathf.Clamp(candidateSamples, 8, 256);
            var probeCount = Mathf.Clamp(connectivityProbes, 4, 128);

            var probes = new List<Vector3>(probeCount);
            for (var i = 0; i < probeCount; i++)
            {
                var planar = Random.insideUnitCircle * Mathf.Max(8f, searchRadius);
                var desired = new Vector3(initialNavMeshPoint.x + planar.x, initialNavMeshPoint.y + 2f,
                    initialNavMeshPoint.z + planar.y);

                if (NavMesh.SamplePosition(desired, out var hit, Mathf.Max(12f, searchRadius * 0.30f), areaMask) ||
                    NavMesh.SamplePosition(desired, out hit, Mathf.Max(12f, searchRadius * 0.30f), NavMesh.AllAreas))
                    probes.Add(hit.position);
            }

            if (probes.Count == 0) return false;

            var path = new NavMeshPath();
            for (var c = 0; c < candidateCount; c++)
            {
                var candidate = initialNavMeshPoint;
                if (c > 0)
                {
                    var planar = Random.insideUnitCircle * Mathf.Max(6f, searchRadius);
                    var desired = new Vector3(initialNavMeshPoint.x + planar.x, initialNavMeshPoint.y + 2f,
                        initialNavMeshPoint.z + planar.y);

                    if (NavMesh.SamplePosition(desired, out var hit, Mathf.Max(10f, searchRadius * 0.25f), areaMask) ||
                        NavMesh.SamplePosition(desired, out hit, Mathf.Max(10f, searchRadius * 0.25f),
                            NavMesh.AllAreas))
                        candidate = hit.position;
                }

                var score = probes.Count(target =>
                    NavMesh.CalculatePath(candidate, target, areaMask, path) &&
                    path.status == NavMeshPathStatus.PathComplete);

                if (score <= bestConnectivityScore) continue;

                bestConnectivityScore = score;
                bestPoint = candidate;
                if (bestConnectivityScore >= probes.Count) break;
            }

            return bestConnectivityScore > 0;
        }
    }
}