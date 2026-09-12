using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Systems;
using Object = UnityEngine.Object;

namespace Zombera.Characters
{
    public sealed partial class StartupSquadSpawner
    {
        private IEnumerator EnsureStartupSquadDeferred(Unit playerUnit, StartupSquadSpawnConfig config,
            int spawnPerFrame)
        {
            if (!config.Enabled || playerUnit == null) yield break;

            // Defer first spawn batch by one frame so world bootstrap and navmesh wiring can settle.
            yield return null;

            var targetTotalCount = Mathf.Max(1, config.StartupSquadTotalCount, config.MinimumStartupSquadTotalCount,
                config.StartupInitialCharacterCount);
            var targetNpcCount = Mathf.Max(0, targetTotalCount - 1);

            var squadUnits = CollectExistingStartupSquadUnits();
            var requiredNewMembers = Mathf.Max(0, targetNpcCount - squadUnits.Count);
            var spawnedMembers = 0;

            LogStartupSquadDiagnostics(
                "EnsureStartupSquadDeferred start",
                config,
                playerUnit,
                targetTotalCount,
                targetNpcCount,
                squadUnits.Count,
                0);

            var safePerFrame = Mathf.Clamp(spawnPerFrame, 1, 4);
            var spawnedThisFrame = 0;

            var pendingMembers = requiredNewMembers;
            while (pendingMembers-- > 0)
            {
                var spawnIndex = squadUnits.Count;
                var spawnedUnit = SpawnStartupSquadMember(playerUnit, spawnIndex, Mathf.Max(1, targetNpcCount), config);
                if (spawnedUnit != null)
                {
                    squadUnits.Add(spawnedUnit);
                    spawnedMembers++;
                    spawnedThisFrame++;
                }

                if (spawnedThisFrame < safePerFrame) continue;

                spawnedThisFrame = 0;
                yield return null; // budget spawn cost across frames
            }

            if (config.RandomizeVisualVariants && squadUnits.Count > 0)
            {
                // Randomizing visual variants is heavy; do it over multiple frames too.
                var visualsPerFrame = Mathf.Clamp(safePerFrame, 1, 8);
                var v = 0;
                foreach (var squadUnit in squadUnits)
                {
                    ApplyRandomizedVisualVariant(squadUnit);
                    v++;
                    if (v < visualsPerFrame) continue;

                    v = 0;
                    yield return null;
                }
            }

            if (config.ApplySkillTiers)
                ApplyStartupSquadSkillTiers(playerUnit, squadUnits, config.SkillTiers, config.DefaultSkillTiers);

            SquadManager.Instance?.RefreshSquadRoster();

            if (config.LogSpawning)
                Debug.Log(
                    $"[PlayerSpawner] Startup test squad ready (deferred). Player='{playerUnit.name}', ExistingMembers={squadUnits.Count - spawnedMembers}, SpawnedMembers={spawnedMembers}, " +
                    $"TargetTotal={targetTotalCount}, TotalRoster={1 + squadUnits.Count}.",
                    playerUnit);

            LogStartupSquadDiagnostics(
                "EnsureStartupSquadDeferred complete",
                config,
                playerUnit,
                targetTotalCount,
                targetNpcCount,
                squadUnits.Count - spawnedMembers,
                spawnedMembers);
        }

        private List<Unit> CollectExistingStartupSquadUnits()
        {
            var result = new List<Unit>();
            var existingMembers = Object.FindObjectsByType<SquadMember>(FindObjectsSortMode.None);
            var ownerScene = _getOwnerScene?.Invoke() ?? default;
            var spawnedPlayer = _getSpawnedPlayer?.Invoke();

            foreach (var member in existingMembers)
            {
                if (member == null || (ownerScene.IsValid() && member.gameObject.scene != ownerScene)) continue;

                var memberUnit = member.Unit ?? member.GetComponent<Unit>();
                if (memberUnit == null || memberUnit == spawnedPlayer || result.Contains(memberUnit)) continue;

                memberUnit.SetRole(UnitRole.SquadMember);
                result.Add(memberUnit);
            }

            return result;
        }

        private Unit SpawnStartupSquadMember(Unit playerUnit, int spawnIndex, int targetNpcCount,
            StartupSquadSpawnConfig config)
        {
            if (playerUnit == null) return null;

            var spawnPrefab = config.SquadMemberPrefab != null ? config.SquadMemberPrefab : config.PlayerPrefabFallback;
            if (spawnPrefab == null) spawnPrefab = playerUnit.gameObject;

            if (spawnPrefab == null)
            {
                if (!_loggedMissingStartupSquadPrefab)
                {
                    Debug.LogWarning(
                        "[PlayerSpawner] Startup squad spawn skipped: no squad member prefab or fallback player prefab was found.",
                        _host);
                    _loggedMissingStartupSquadPrefab = true;
                }

                return null;
            }

            var spawnPosition =
                ResolveStartupSquadSpawnPosition(playerUnit.transform.position, spawnIndex, targetNpcCount, config);
            var parent = config.SquadParent;
            var shouldLogMemberSpawn = config.LogSpawning
                                       && (spawnIndex == 0
                                           || spawnIndex == targetNpcCount - 1
                                           || (spawnIndex + 1) % 5 == 0);

            if (shouldLogMemberSpawn)
                Debug.Log(
                    "[PlayerSpawner][SquadSpawnDiag] Spawn request: index=" + spawnIndex +
                    ", targetNpcCount=" + targetNpcCount +
                    ", prefab='" + spawnPrefab.name +
                    "', parent='" + (parent != null ? parent.name : "<none>") +
                    "', spawnPos=" + spawnPosition.ToString("F2") + ".",
                    _host);

            var spawnedObject = parent != null
                ? Object.Instantiate(spawnPrefab, spawnPosition, playerUnit.transform.rotation, parent)
                : Object.Instantiate(spawnPrefab, spawnPosition, playerUnit.transform.rotation);

            if (spawnedObject == null) return null;

            _sanitizeRuntimeUnitHierarchy?.Invoke(spawnedObject);

            var squadUnit = EnsureStartupSquadMemberWiring(spawnedObject);
            if (squadUnit == null)
            {
                Object.Destroy(spawnedObject);

                if (config.LogSpawning)
                    Debug.LogWarning(
                        "[PlayerSpawner][SquadSpawnDiag] Spawned squad object failed wiring and was destroyed.",
                        _host);

                return null;
            }

            // Ensure the instance stays active and visible even if the prefab was authored disabled.
            if (!spawnedObject.activeSelf) spawnedObject.SetActive(true);

            // Best-effort visibility: enable renderers now and once more next frame.
            ForceEnableRenderers(spawnedObject);
            if (_host != null) _host.StartCoroutine(ForceEnableRenderersNextFrame(spawnedObject));

            spawnedObject.name = $"Squadmate_{spawnIndex + 1:00}";
            squadUnit.Health?.ResetHealthToMax();

            if (shouldLogMemberSpawn)
                Debug.Log(
                    "[PlayerSpawner][SquadSpawnDiag] Spawned member: name='" + spawnedObject.name +
                    "', role=" + squadUnit.Role +
                    ", pos=" + spawnedObject.transform.position.ToString("F2") + ".",
                    spawnedObject);

            return squadUnit;
        }

        private static IEnumerator ForceEnableRenderersNextFrame(GameObject root)
        {
            yield return null;
            ForceEnableRenderers(root);
        }

        private Vector3 ResolveStartupSquadSpawnPosition(Vector3 playerPosition, int spawnIndex, int targetNpcCount,
            StartupSquadSpawnConfig config)
        {
            var clampedTargetCount = Mathf.Max(1, targetNpcCount);
            var ringRadius = Mathf.Max(0.25f, config.StartupSquadRingRadius);
            var angle = Mathf.PI * 2f * spawnIndex / clampedTargetCount;

            var ringOffset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ringRadius;
            var spawnPosition = playerPosition + ringOffset;

            var referenceY = playerPosition.y;
            if (UnitNavUtils.TryResolveGroundReferenceY(spawnPosition, out var groundedY))
                referenceY = groundedY + Mathf.Max(0f, config.TerrainHeightOffset);

            spawnPosition.y = referenceY;

            var profile = MovementGroundingSettings.Active;
            var sampleTiers = new[] { Mathf.Max(4f, ringRadius * 1.1f), 8f, 15f };
            var sampleOrigin = new Vector3(spawnPosition.x, referenceY + profile.NavSampleUpOffset, spawnPosition.z);
            if (UnitNavUtils.TrySampleTieredNearReferenceY(
                    sampleOrigin,
                    referenceY,
                    out var navPos,
                    profile.MaxNavMeshVerticalDeltaFromGround,
                    sampleTiers))
            {
                if (profile.TryProjectGround(navPos, out var footPoint))
                    return profile.BlendNavMeshWithTerrain(navPos, footPoint);

                return navPos;
            }

            return spawnPosition;
        }

        private Unit EnsureStartupSquadMemberWiring(GameObject squadObject)
        {
            if (squadObject == null) return null;

            var unit = squadObject.GetComponent<Unit>();
            if (unit == null)
            {
                Debug.LogWarning("[PlayerSpawner] Startup squad member is missing Unit component.", squadObject);
                return null;
            }

            unit.SetRole(UnitRole.SquadMember);

            var loadingSaveSession = GameManagerGateway.Instance != null && GameManagerGateway.Instance.IsLoadingSession;
            if (!loadingSaveSession)
                unit.RegenerateUnitId();

            if (squadObject.GetComponent<SquadMember>() == null) squadObject.AddComponent<SquadMember>();

            if (squadObject.GetComponent<FollowController>() == null) squadObject.AddComponent<FollowController>();

            var controller = squadObject.GetComponent<UnitController>();
            if (controller == null) return unit;

            controller.SetRole(UnitRole.SquadMember);
            if (TryHasNearbyNavMesh(controller.transform.position))
                controller.ForceEnableAgent();
            else if (_host != null)
                _host.StartCoroutine(ForceEnableSquadAgentWhenNavMeshReady(controller));

            return unit;
        }

        private static IEnumerator ForceEnableSquadAgentWhenNavMeshReady(UnitController controller)
        {
            if (controller == null) yield break;

            const float timeoutSeconds = 20f;
            var start = Time.unscaledTime;

            while (Time.unscaledTime - start < timeoutSeconds)
            {
                if (controller == null) yield break;

                if (TryHasNearbyNavMesh(controller.transform.position))
                {
                    controller.ForceEnableAgent();
                    yield break;
                }

                yield return StartupNavMeshRetryWait;
            }
        }

        private static bool TryHasNearbyNavMesh(Vector3 origin)
        {
            return UnitNavUtils.TryHasNearbyNavMesh(origin);
        }

        private void LogStartupSquadDiagnostics(
            string context,
            StartupSquadSpawnConfig config,
            Unit playerUnit,
            int targetTotalCount,
            int targetNpcCount,
            int existingMembers,
            int spawnedMembers)
        {
            if (!config.LogSpawning) return;

            var ownerScene = _getOwnerScene != null ? _getOwnerScene.Invoke() : default;
            var navMeshTriangles = GetNavMeshTriangleCount();
            var playerPosition = playerUnit != null ? playerUnit.transform.position : Vector3.zero;
            var navReadyNearPlayer = playerUnit != null && TryHasNearbyNavMesh(playerPosition);

            Debug.Log(
                "[PlayerSpawner][SquadSpawnDiag] " + context +
                ": scene='" + SceneLabel(ownerScene) +
                "', player='" + (playerUnit != null ? playerUnit.name : "<none>") +
                "', playerPos=" + (playerUnit != null ? playerPosition.ToString("F2") : "n/a") +
                ", navReadyNearPlayer=" + navReadyNearPlayer +
                ", navMeshTriangles=" + navMeshTriangles +
                ", targetTotal=" + targetTotalCount +
                ", targetNpc=" + targetNpcCount +
                ", existingNpc=" + existingMembers +
                ", spawnedNpc=" + spawnedMembers +
                ", randomizeVariants=" + config.RandomizeVisualVariants +
                ", applySkillTiers=" + config.ApplySkillTiers + ".",
                _host);
        }

        private static int GetNavMeshTriangleCount()
        {
            var triangulation = NavMesh.CalculateTriangulation();
            return triangulation.indices != null ? triangulation.indices.Length / 3 : 0;
        }

        private static string SceneLabel(Scene scene)
        {
            return scene.IsValid() ? scene.name + "#" + scene.handle : "<invalid>";
        }
    }
}
