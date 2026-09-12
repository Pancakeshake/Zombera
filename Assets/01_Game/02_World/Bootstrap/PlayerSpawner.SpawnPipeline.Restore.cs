using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Zombera.Characters;
using Zombera.Systems;

namespace Zombera.Characters
{
    public sealed partial class PlayerSpawner : MonoBehaviour
    {
        private bool TryResolveLoadBootstrapContext(out Unit playerUnit, out UnitManager unitManager)
        {
            unitManager = FindFirstObjectByType<UnitManager>();
            playerUnit = SpawnedPlayer;

            if (playerUnit == null && unitManager != null)
                playerUnit = unitManager.FindFirstUnitByRole(UnitRole.Player);

            return playerUnit != null && _startupSquadSpawner != null;
        }

        private int BootstrapLoadSquadMembers(Unit playerUnit, UnitManager unitManager, int requiredMembers, int maxAttempts)
        {
            var baseConfig = BuildStartupSquadConfig();
            var ensuredCount = CountActiveSquadCandidates(unitManager);

            for (var attempt = 1; attempt <= maxAttempts && ensuredCount < requiredMembers; attempt++)
            {
                var loadRestoreConfig = BuildLoadRestoreSquadConfig(baseConfig, requiredMembers);
                _ = _startupSquadSpawner.EnsureStartupSquad(playerUnit, loadRestoreConfig);

                unitManager?.RefreshRegistry();
                ensuredCount = CountActiveSquadCandidates(unitManager);

                LogFocusedSpawnDiagnostics(
                    "EnsureLoadedSaveSquadMembers bootstrap attempt=" + attempt + ", required=" + requiredMembers + ", active=" + ensuredCount,
                    playerUnit.transform.position);
            }

            return ensuredCount;
        }

        private static StartupSquadSpawnConfig BuildLoadRestoreSquadConfig(StartupSquadSpawnConfig baseConfig, int requiredMembers)
        {
            return new StartupSquadSpawnConfig(
                true,
                requiredMembers + 1,
                requiredMembers + 1,
                requiredMembers + 1,
                baseConfig.StartupSquadRingRadius,
                false,
                false,
                baseConfig.LogSpawning,
                baseConfig.SkillTiers,
                baseConfig.DefaultSkillTiers,
                baseConfig.PlayerPrefabFallback,
                baseConfig.SquadMemberPrefab,
                baseConfig.SquadParent,
                baseConfig.TerrainHeightOffset,
                baseConfig.NavMeshVerticalExtent);
        }

        private int CountActiveSquadCandidates(UnitManager unitManager)
        {
            if (unitManager == null) return 0;

            var unitBuffer = new List<Unit>(32);
            var activeUnits = unitManager.GetAllActiveUnits(unitBuffer);
            var count = 0;

            for (var i = 0; i < activeUnits.Count; i++)
            {
                var unit = activeUnits[i];
                if (unit == null) continue;
                if (unit == SpawnedPlayer) continue;
                if (unit.Health != null && unit.Health.IsDead) continue;

                if (unit.Role == UnitRole.SquadMember || unit.Role == UnitRole.Survivor)
                    count++;
            }

            return count;
        }

        private IEnumerator EnsureStartupSquadWhenNavMeshReady(Unit playerUnit)
        {
            var timeoutSeconds = Mathf.Max(6f, navMeshRetryAttempts * Mathf.Max(0.1f, navMeshRetryDelaySeconds) + 4f);
            var start = Time.unscaledTime;

            while (Time.unscaledTime - start < timeoutSeconds)
            {
                if (playerUnit == null)
                {
                    _startupSquadSpawnQueuedForNavMesh = false;
                    yield break;
                }

                if (TryHasNearbyNavMesh(playerUnit.transform.position))
                {
                    _startupSquadSpawnQueuedForNavMesh = false;
                    EnsureStartupTestSquad(playerUnit);
                    yield break;
                }

                yield return StartupNavMeshRetryWait;
            }

            _startupSquadSpawnQueuedForNavMesh = false;
            Debug.LogWarning(
                "[PlayerSpawner] NavMesh is still unavailable near the player after waiting. Spawning startup squad without NavMesh fallback.",
                this);

            if (playerUnit != null && _startupSquadSpawner != null)
            {
                var config = BuildStartupSquadConfig();
                if (deferStartupSquadSpawning)
                    _startupSquadSpawner.StartEnsureStartupSquadDeferred(playerUnit, config, startupSquadSpawnPerFrame);
                else
                    _ = _startupSquadSpawner.EnsureStartupSquad(playerUnit, config);
            }

            LogFocusedSpawnDiagnostics("EnsureStartupSquadWhenNavMeshReady timed out", playerUnit != null ? playerUnit.transform.position : transform.position);
        }

        private static bool TryHasNearbyNavMesh(Vector3 origin)
        {
            return UnitNavUtils.TryHasNearbyNavMesh(origin);
        }

        private static IEnumerator ForceEnableAgentWhenNavMeshReady(UnitController controller)
        {
            if (controller == null) yield break;

            // Increased timeout to account for slow terrain streaming on some machines.
            const float timeoutSeconds = 45f;
            var start = Time.unscaledTime;

            while (Time.unscaledTime - start < timeoutSeconds)
            {
                if (TryHasNearbyNavMesh(controller.transform.position))
                {
                    controller.ForceEnableAgent();
                    yield break;
                }

                yield return StartupNavMeshRetryWait;
            }

            Debug.LogWarning($"[PlayerSpawner] Failed to enable NavMeshAgent for {controller.name} after {timeoutSeconds}s near {controller.transform.position}. Spawner retry abandoned.", controller);
        }

        private Vector3 ResolveDefaultSpawnPosition()
        {
            var terrain = ResolveSpawnTerrain(transform.position);
            if (terrain == null || terrain.terrainData == null)
            {
                // No terrain at the spawner's own position (common when MapMagic tiles are at a
                // different world origin). Prefer the center of the currently deployed MapMagic tile rect.
                if (!SpawnPointSelector.TryGetMapMagicDeployedWorldRect(gameObject.scene, out var deployedRect))
                {
                    // If we have no terrain AND no deployed MapMagic rect, we are likely too early.
                    // Return a distinct "Waiting" position (NaN) so callers know to defer.
                    return new Vector3(float.NaN, float.NaN, float.NaN);
                }

                var deployedCenter = new Vector3(
                    deployedRect.x + deployedRect.width * 0.5f,
                    transform.position.y,
                    deployedRect.y + deployedRect.height * 0.5f);

                if (logSpawnTerrainDiagnostics)
                    Debug.Log(
                        $"[PlayerSpawner] ResolveDefaultSpawnPosition: no terrain at spawner origin — using deployed rect center {deployedCenter} (rect={deployedRect}).",
                        this);

                return deployedCenter;
            }

            var terrainOrigin = terrain.transform.position;
            var terrainSize = terrain.terrainData.size;

            return new Vector3(
                terrainOrigin.x + terrainSize.x * 0.5f,
                transform.position.y,
                terrainOrigin.z + terrainSize.z * 0.5f);
        }

        private Vector3 ResolveMainSpawnPosition()
        {
            var fallbackPosition = spawnPoint != null ? spawnPoint.position : ResolveDefaultSpawnPosition();

            if (!useTerrainAsMainSpawn) return fallbackPosition;

            // When MapMagic is the terrain authority, prefer the center of the currently deployed tile rect.
            // This avoids spawning on the edge when only a subset of tiles has generated at bootstrap.
            if (SpawnPointSelector.TryGetMapMagicDeployedWorldRect(gameObject.scene, out var deployedRect))
            {
                var deployedNormalized = new Vector2(
                    Mathf.Clamp01(terrainMainSpawnNormalized.x),
                    Mathf.Clamp01(terrainMainSpawnNormalized.y));

                if (SpawnPointSelector.TrySelectFlatterSpawnInMapMagicRect(
                        deployedRect,
                        gameObject.scene,
                        deployedNormalized,
                        mapMagicSpawnRectEdgeMarginNormalized,
                        mapMagicFlatterSpawnSamples,
                        mapMagicMaxSpawnSlopeDegrees,
                        terrainHeightOffset,
                        ResolveSpawnTerrain,
                        logSpawnTerrainDiagnostics,
                        this,
                        out var flatterSpawn,
                        mapMagicRandomUniformSpawnAttempts))
                    return flatterSpawn;

                var margin = Mathf.Clamp(mapMagicSpawnRectEdgeMarginNormalized, 0f, 0.45f);
                var u = margin >= 0.499f ? deployedNormalized.x : Random.Range(margin, 1f - margin);
                var v = margin >= 0.499f ? deployedNormalized.y : Random.Range(margin, 1f - margin);

                var deployedSpawn = new Vector3(
                    deployedRect.x + deployedRect.width * u,
                    fallbackPosition.y,
                    deployedRect.y + deployedRect.height * v);

                // Use heightmap sampling if we can resolve an actual terrain under this point.
                var deployedTerrain = ResolveSpawnTerrain(deployedSpawn);
                if (deployedTerrain is { terrainData: not null })
                {
                    var origin = deployedTerrain.transform.position;
                    var deployedSampledY = deployedTerrain.SampleHeight(deployedSpawn) + origin.y;
                    deployedSpawn.y = deployedSampledY + Mathf.Max(0f, terrainHeightOffset);
                }

                return deployedSpawn;
            }

            var terrain = ResolveSpawnTerrain(fallbackPosition);
            if (terrain == null || terrain.terrainData == null) return fallbackPosition;

            var terrainOrigin = terrain.transform.position;
            var terrainSize = terrain.terrainData.size;

            var normalized = new Vector2(
                Mathf.Clamp01(terrainMainSpawnNormalized.x),
                Mathf.Clamp01(terrainMainSpawnNormalized.y));

            var terrainSpawnPosition = new Vector3(
                terrainOrigin.x + terrainSize.x * normalized.x,
                terrainOrigin.y,
                terrainOrigin.z + terrainSize.z * normalized.y);

            var sampledY = terrain.SampleHeight(terrainSpawnPosition) + terrainOrigin.y;
            terrainSpawnPosition.y = sampledY + Mathf.Max(0f, terrainHeightOffset);
            return terrainSpawnPosition;
        }

        // MapMagic spawn selection helpers moved to SpawnPointSelector.

    }
}
