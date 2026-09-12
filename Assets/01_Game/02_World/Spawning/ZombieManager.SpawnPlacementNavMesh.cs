#region

using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.AI;
using Zombera.AI;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Data;
using Zombera.Debugging.DebugLogging;
using Zombera.World;
using Random = UnityEngine.Random;

#endregion

namespace Zombera.Systems
{
    public sealed partial class ZombieManager
    {
        private static readonly string[] BuildingColliderKeywords =
        {
            "building", "house", "wall", "foundation", "roof", "door", "window", "shelter"
        };

        private Vector3 ClampValidationSpawnToPlayerNeighborhood(Vector3 playerPosition, Vector3 candidate)
        {
            var planarOffset = candidate - playerPosition;
            planarOffset.y = 0f;

            var maxAllowedDistance = Mathf.Max(
                validationSpawnDistanceFromPlayer + Mathf.Max(0f, validationSpawnDistanceJitter) + 12f,
                24f);

            if (planarOffset.sqrMagnitude <= maxAllowedDistance * maxAllowedDistance) return candidate;

            var fallbackCandidate = BuildNearbySpawnPoint(playerPosition, validationSpawnDistanceFromPlayer,
                validationSpawnDistanceJitter);

            if (TrySampleGroundFromPhysics(fallbackCandidate, out var groundY)) fallbackCandidate.y = groundY;

            var navSampleOrigin = fallbackCandidate + Vector3.up * 2f;
            var sampleDistance = Mathf.Max(8f, ambientSpawnNavMeshSampleDistance);
            const int walkableAreaMask = 1;

            if (NavMesh.SamplePosition(navSampleOrigin, out var navHit, sampleDistance, walkableAreaMask) ||
                NavMesh.SamplePosition(navSampleOrigin, out navHit, sampleDistance, NavMesh.AllAreas))
                fallbackCandidate = navHit.position;

            if (logValidationSpawn)
                DebugLogger.LogWarning(
                    LogCategory.World,
                    $"[ZombieManager] Validation spawn candidate was too far from player ({planarOffset.magnitude:F1}m). Clamped to {fallbackCandidate}.",
                    this);

            return fallbackCandidate;
        }

        private static Vector3 BuildNearbySpawnPoint(Vector3 playerPosition, float distanceFromPlayer,
            float distanceJitter)
        {
            var direction2D = Random.insideUnitCircle;

            if (direction2D.sqrMagnitude <= 0.0001f) direction2D = Vector2.right;

            direction2D.Normalize();
            var spawnDistance = Mathf.Max(1f, distanceFromPlayer) + Random.Range(0f, Mathf.Max(0f, distanceJitter));
            return playerPosition + new Vector3(direction2D.x, 0f, direction2D.y) * spawnDistance;
        }

        private bool TryGetRandomPointAcrossMap(Vector3 playerPosition, out Vector3 spawnPoint)
        {
            spawnPoint = playerPosition;

            if (!TryResolveAmbientSpawnBounds(playerPosition, out var spawnBounds, out var terrain)) return false;

            var attempts = Mathf.Max(1, ambientSpawnCenterSampleAttempts);
            var minimumDistance = Mathf.Max(0f, minimumAmbientSpawnDistanceFromPlayer);
            var minimumDistanceSqr = minimumDistance * minimumDistance;

            for (var attempt = 0; attempt < attempts; attempt++)
            {
                var candidate = new Vector3(
                    Random.Range(spawnBounds.min.x, spawnBounds.max.x),
                    spawnBounds.center.y,
                    Random.Range(spawnBounds.min.z, spawnBounds.max.z));

                candidate = FinalizeSpawnCenter(candidate, terrain);

                if (minimumDistanceSqr > 0f)
                {
                    var toPlayer = candidate - playerPosition;
                    toPlayer.y = 0f;

                    if (toPlayer.sqrMagnitude < minimumDistanceSqr) continue;
                }

                spawnPoint = candidate;
                return true;
            }

            return false;
        }

        private bool TryResolveAmbientSpawnBounds(Vector3 playerPosition, out Bounds bounds, out Terrain terrain)
        {
            terrain = ResolveAmbientTerrain(playerPosition);

            if (useTerrainBoundsForAmbientSpawnArea && TryGetTerrainData(terrain, out var terrainData))
            {
                var terrainSize = terrainData.size;
                if (terrainSize.x > 0.01f && terrainSize.z > 0.01f)
                {
                    var terrainPosition = terrain.GetPosition();
                    bounds = new Bounds(
                        terrainPosition +
                        new Vector3(terrainSize.x * 0.5f, terrainSize.y * 0.5f, terrainSize.z * 0.5f),
                        terrainSize);

                    return true;
                }
            }

            if (fallbackAmbientSpawnBounds is { size: { x: > 0.01f, z: > 0.01f } })
            {
                bounds = fallbackAmbientSpawnBounds;
                return true;
            }

            bounds = default;
            return false;
        }

        private Vector3 FinalizeSpawnCenter(Vector3 candidate, Terrain terrainHint)
        {
            var terrain = terrainHint ?? ResolveAmbientTerrain(candidate);
            var hasSurfaceHeight = false;
            var surfaceY = candidate.y;

            TerrainData terrainData = null;
            if (terrain != null)
            {
                try
                {
                    terrainData = terrain.terrainData;
                }
                catch (Exception)
                {
                    // Unity can throw for stale serialized terrain refs during scene/bootstrap transitions.
                    terrain = null;
                    terrainData = null;
                }
            }

            if (alignAmbientSpawnCentersToTerrain && terrain != null && terrainData != null)
            {
                var terrainOrigin = terrain.GetPosition();
                var terrainSize = terrainData.size;

                var minX = terrainOrigin.x;
                var maxX = terrainOrigin.x + terrainSize.x;
                var minZ = terrainOrigin.z;
                var maxZ = terrainOrigin.z + terrainSize.z;

                var clampedX = Mathf.Clamp(candidate.x, minX, maxX);
                var clampedZ = Mathf.Clamp(candidate.z, minZ, maxZ);

                candidate.x = clampedX;
                candidate.z = clampedZ;

                surfaceY = terrain.SampleHeight(new Vector3(clampedX, terrainOrigin.y, clampedZ)) + terrainOrigin.y;
                candidate.y = surfaceY;
                hasSurfaceHeight = true;
            }

            if (TrySampleGroundFromPhysics(candidate, out var physicsGroundY))
            {
                if (!hasSurfaceHeight || physicsGroundY > surfaceY)
                {
                    surfaceY = physicsGroundY;
                    hasSurfaceHeight = true;
                }

                candidate.y = surfaceY;
            }

            if (snapAmbientSpawnCentersToNavMesh)
            {
                var sampleOrigin = candidate + Vector3.up * 2f;
                var sampleDistance = Mathf.Max(0.5f, ambientSpawnNavMeshSampleDistance);
                const int walkableAreaMask = 1;


                var hasNavMeshHit =
                    NavMesh.SamplePosition(sampleOrigin, out var navHit, sampleDistance, walkableAreaMask) ||
                    NavMesh.SamplePosition(sampleOrigin, out navHit, sampleDistance, NavMesh.AllAreas);

                if (hasNavMeshHit && (!hasSurfaceHeight || navHit.position.y >= surfaceY - 0.25f))
                    // Ignore stale/old navmesh hits that sit below the sampled ground surface.
                    candidate = navHit.position;
            }

            if (hasSurfaceHeight && candidate.y < surfaceY - 0.05f) candidate.y = surfaceY;

            return candidate;
        }

        private static bool TrySampleGroundFromPhysics(Vector3 worldPosition, out float groundY)
        {
            var rayOrigin = worldPosition + Vector3.up * 1200f;

            if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, 2600f, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                groundY = hit.point.y;
                return true;
            }

            groundY = worldPosition.y;
            return false;
        }

        private bool IsSpawnNavMeshReady(Vector3 referencePosition)
        {
            if (!requireNavMeshForZombieSpawns) return true;

            var now = Time.time;
            var delta = referencePosition - _cachedSpawnNavMeshReference;
            delta.y = 0f;
            if (now < _nextSpawnNavMeshReadyCheckAt && delta.sqrMagnitude <= 25f)
                return _cachedSpawnNavMeshReady;

            _cachedSpawnNavMeshReference = referencePosition;
            _nextSpawnNavMeshReadyCheckAt = now + Mathf.Max(0.05f, spawnNavMeshReadyRecheckSeconds);
            _cachedSpawnNavMeshReady = TryProjectSpawnPositionToNavMesh(referencePosition, out _);

            return _cachedSpawnNavMeshReady;
        }

        private bool TryProjectSpawnPositionToNavMesh(Vector3 requestedPosition, out Vector3 navMeshPosition)
        {
            navMeshPosition = requestedPosition;
            if (!requireNavMeshForZombieSpawns) return true;

            var sampleOrigin = requestedPosition + Vector3.up * 2f;
            var sampleDistance = Mathf.Max(0.5f, requiredZombieSpawnNavMeshSampleDistance);
            const int walkableAreaMask = 1;

            if (NavMesh.SamplePosition(sampleOrigin, out var navHit, sampleDistance, walkableAreaMask) ||
                NavMesh.SamplePosition(sampleOrigin, out navHit, sampleDistance, NavMesh.AllAreas))
            {
                navMeshPosition = navHit.position;
                return true;
            }

            return false;
        }

        private bool TryResolveValidatedSpawnPosition(Vector3 requestedPosition, out Vector3 validatedPosition,
            Vector3? pathTargetHint = null)
        {
            validatedPosition = requestedPosition;

            if (!TryProjectSpawnPositionToNavMesh(requestedPosition, out var navPosition))
            {
                LogSpawnValidationTrace("Rejected spawn: could not project to navmesh", requestedPosition);
                return false;
            }

            if (!validateZombieSpawnPlacement)
            {
                validatedPosition = navPosition;
                return true;
            }

            var attempts = Mathf.Max(1, zombieSpawnPlacementValidationAttempts);
            var jitterRadius = Mathf.Max(0f, zombieSpawnValidationJitterRadius);

            for (var attempt = 0; attempt < attempts; attempt++)
            {
                var candidate = navPosition;

                if (attempt > 0 && jitterRadius > 0f)
                {
                    var spread = jitterRadius * (attempt / (float)Mathf.Max(1, attempts - 1));
                    var jitter = Random.insideUnitCircle * spread;
                    candidate += new Vector3(jitter.x, 0f, jitter.y);

                    if (!TryProjectSpawnPositionToNavMesh(candidate, out candidate))
                        continue;
                }

                if (!IsSpawnPlacementValid(candidate, pathTargetHint)) continue;

                validatedPosition = candidate;
                return true;
            }

            LogSpawnValidationTrace("Rejected spawn: failed placement validation attempts", requestedPosition);
            return false;
        }

        private bool IsSpawnPlacementValid(Vector3 candidate, Vector3? pathTargetHint)
        {
            if (IsSpawnCapsuleBlocked(candidate))
            {
                LogSpawnValidationTrace("Rejected spawn: blocked capsule", candidate);
                return false;
            }

            if (rejectZombieSpawnsInsideBuildingColliders && IsInsideBuildingCollider(candidate))
            {
                LogSpawnValidationTrace("Rejected spawn: inside building collider", candidate);
                return false;
            }

            if (!requireReachablePathToPlayer)
                return true;

            if (!TryResolvePathTarget(pathTargetHint, out var pathTarget))
                return true;

            if (HasReachableNavPath(candidate, pathTarget))
                return true;

            LogSpawnValidationTrace("Rejected spawn: no complete nav path to player", candidate);
            return false;
        }

        private bool IsSpawnCapsuleBlocked(Vector3 candidate)
        {
            var radius = Mathf.Max(0.1f, zombieSpawnClearanceRadius);
            var height = Mathf.Max(radius * 2f + 0.2f, zombieSpawnClearanceHeight);
            var bottom = candidate + Vector3.up * (radius + 0.05f);
            var top = candidate + Vector3.up * (height - radius);

            return Physics.CheckCapsule(
                bottom,
                top,
                radius,
                zombieSpawnBlockingMask,
                QueryTriggerInteraction.Ignore);
        }

        private bool IsInsideBuildingCollider(Vector3 candidate)
        {
            var overlapCount = Physics.OverlapSphereNonAlloc(
                candidate + Vector3.up * 0.2f,
                Mathf.Max(0.1f, zombieSpawnClearanceRadius),
                _spawnValidationOverlapBuffer,
                zombieSpawnBlockingMask,
                QueryTriggerInteraction.Collide);

            if (overlapCount <= 0) return false;

            var checkPoint = candidate + Vector3.up * 0.35f;

            for (var i = 0; i < overlapCount; i++)
            {
                var collider = _spawnValidationOverlapBuffer[i];
                _spawnValidationOverlapBuffer[i] = null;

                if (collider == null || !collider.bounds.Contains(checkPoint)) continue;

                if (collider.GetComponentInParent<RoomVolume>() != null)
                    return true;

                var loweredName = collider.name.ToLowerInvariant();
                for (var keywordIndex = 0; keywordIndex < BuildingColliderKeywords.Length; keywordIndex++)
                {
                    if (!loweredName.Contains(BuildingColliderKeywords[keywordIndex])) continue;
                    return true;
                }
            }

            return false;
        }

        private bool TryResolvePathTarget(Vector3? pathTargetHint, out Vector3 pathTarget)
        {
            pathTarget = default;

            if (pathTargetHint.HasValue)
            {
                pathTarget = pathTargetHint.Value;
                return true;
            }

            return TryResolvePlayerPosition(out pathTarget);
        }

        private bool HasReachableNavPath(Vector3 startPosition, Vector3 targetPosition)
        {
            if (!TryProjectSpawnPositionToNavMesh(startPosition, out var navStart)) return false;
            if (!TryProjectSpawnPositionToNavMesh(targetPosition, out var navTarget)) return false;

            if (!NavMesh.CalculatePath(navStart, navTarget, NavMesh.AllAreas, _spawnValidationPath))
                return false;

            return _spawnValidationPath.status == NavMeshPathStatus.PathComplete;
        }

        private void LogSpawnValidationTrace(string message, Vector3 position)
        {
            if (!logSpawnPlacementDiagnostics) return;

            DebugLogger.LogTrace(
                LogCategory.World,
                "[ZombieManager] " + message + " at " + position + ".",
                this);
        }

        private Terrain ResolveAmbientTerrain(Vector3 referencePosition)
        {
            if (!TryGetTerrainData(ambientSpawnTerrain, out _)) ambientSpawnTerrain = null;

            var resolvedTerrain = TerrainResolver.ResolveTerrainForPosition(referencePosition, ambientSpawnTerrain);

            if (TryGetTerrainData(resolvedTerrain, out _)) ambientSpawnTerrain = resolvedTerrain;

            return resolvedTerrain;
        }

        private static bool TryGetTerrainData(Terrain terrain, out TerrainData terrainData)
        {
            terrainData = null;
            if (terrain == null) return false;

            try
            {
                terrainData = terrain.terrainData;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
            catch (NullReferenceException)
            {
                return false;
            }

            return terrainData != null;
        }
    }
}
