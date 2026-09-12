#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Zombera.AI;
using Zombera.Core;
using Zombera.Data;
using Zombera.Debugging;
using Zombera.Inventory;
using Zombera.Systems;
using Zombera.UI;
using Zombera.World;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Random = UnityEngine.Random;

#endregion

namespace Zombera.Characters
{
    public sealed partial class PlayerSpawner : MonoBehaviour
    {
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

        private void LogFocusedSpawnDiagnostics(string context)
        {
            if (!logFocusedSpawnDiagnostics) return;
            LogFocusedSpawnDiagnosticsInternal(context, false, Vector3.zero);
        }

        private void LogFocusedSpawnDiagnostics(string context, Vector3 focusPosition)
        {
            if (!logFocusedSpawnDiagnostics) return;
            LogFocusedSpawnDiagnosticsInternal(context, true, focusPosition);
        }

        private void LogFocusedSpawnDiagnosticsInternal(string context, bool hasFocusPosition, Vector3 focusPosition)
        {
            if (!logFocusedSpawnDiagnostics) return;

            var scene = gameObject.scene;
            var state = GameManager.Instance != null ? GameManager.Instance.CurrentState.ToString() : "NoGameManager";
            var navMeshTriangles = GetNavMeshTriangleCount();
            var focusNavReady = hasFocusPosition && TryHasNearbyNavMesh(focusPosition);

            var tileStreams = FindObjectsByType<WorldTileStreamSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var tileStreamsInScene = 0;
            foreach (var stream in tileStreams)
            {
                if (stream == null) continue;
                if (!scene.IsValid() || stream.gameObject.scene == scene) tileStreamsInScene++;
            }

            var squadMembers = FindObjectsByType<SquadMember>(FindObjectsSortMode.None);
            var squadMembersInScene = 0;
            foreach (var squadMember in squadMembers)
            {
                if (squadMember == null) continue;
                if (!scene.IsValid() || squadMember.gameObject.scene == scene) squadMembersInScene++;
            }

            var spawnedPlayerName = SpawnedPlayer != null ? SpawnedPlayer.name : "<none>";
            var focusLabel = hasFocusPosition ? focusPosition.ToString("F2") : "n/a";

            Debug.Log(
                "[PlayerSpawner][SpawnDiag] " + context +
                ": state=" + state +
                ", scene='" + SceneLabel(scene) +
                "', spawnedPlayer='" + spawnedPlayerName +
                "', finalized=" + HasFinalizedWorldPlayerSpawn +
                ", expectedRoster=" + GetExpectedStartupSquadTotalRosterCount() +
                ", squadMembersInScene=" + squadMembersInScene +
                ", tileStreamsInScene=" + tileStreamsInScene + "/" + tileStreams.Length +
                ", navMeshTriangles=" + navMeshTriangles +
                ", focus=" + focusLabel +
                ", focusNavReady=" + (hasFocusPosition ? focusNavReady.ToString() : "n/a") + ".",
                this);
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

        private static void LogSpawnTerrainDiagnostics(Vector3 worldPosition)
        {
            var terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            if (terrains == null || terrains.Length == 0)
            {
                Debug.LogWarning(
                    $"[PlayerSpawner] Spawn terrain diagnostics: no Terrain objects found near {worldPosition}.");
                return;
            }

            var overlapCount = 0;
            var highestSampleY = float.NegativeInfinity;
            var highestTerrainName = string.Empty;

            foreach (var terrain in terrains)
            {
                if (!TerrainResolver.TerrainContainsXZ(terrain, worldPosition)) continue;

                var sampleY = terrain.SampleHeight(worldPosition) + terrain.GetPosition().y;
                overlapCount++;

                if (sampleY > highestSampleY)
                {
                    highestSampleY = sampleY;
                    highestTerrainName = terrain.name;
                }

                Debug.Log(
                    $"[PlayerSpawner] Terrain overlap[{overlapCount}] '{terrain.name}': SampleY={sampleY:F2}, BaseY={terrain.GetPosition().y:F2}, " +
                    $"Size={terrain.terrainData.size}, Active={terrain.gameObject.activeInHierarchy}, Enabled={terrain.enabled}");
            }

            if (overlapCount <= 0)
            {
                Debug.LogWarning(
                    $"[PlayerSpawner] Spawn terrain diagnostics: no overlapping terrain contains {worldPosition}. Total terrains in scene: {terrains.Length}.");
                return;
            }

            if (overlapCount > 1)
                Debug.LogWarning(
                    $"[PlayerSpawner] Spawn terrain diagnostics: detected {overlapCount} overlapping terrains at {worldPosition}. Highest sample terrain: '{highestTerrainName}' ({highestSampleY:F2}).");
        }

        private Terrain ResolveSpawnTerrain(Vector3 spawnPosition)
        {
            var resolvedTerrain = TerrainResolver.ResolveTerrainForPosition(spawnPosition, spawnTerrain);

            if (resolvedTerrain != null) spawnTerrain = resolvedTerrain;

            return resolvedTerrain;
        }

        // Camera + input wiring extracted to PlayerSpawnWiringService.

        private void InitializeSquadControlSwap(Unit defaultUnit)
        {
            if (defaultUnit == null) return;

            var units = FindObjectsByType<Unit>(FindObjectsSortMode.None);
            foreach (var unit in units)
            {
                if (!IsControllableSquadUnit(unit)) continue;

                EnsureSquadMemberForControllableUnit(unit);

                var input = EnsurePlayerInputController(unit);
                if (input == null) continue;

                input.enabled = false;
                PlayerSpawnWiringService.WireInputSystems(unit.gameObject);
            }

            EnsureSquadMemberForControllableUnit(defaultUnit);

            var squadManager = SquadManager.Instance != null
                ? SquadManager.Instance
                : FindFirstObjectByType<SquadManager>();

            if (squadManager != null)
            {
                squadManager.RefreshSquadRoster();

                var defaultMember = defaultUnit.GetComponent<SquadMember>();
                if (defaultMember != null)
                    squadManager.SetSelectedMembers(new[] { defaultMember });
            }

            ActivateControlledUnit(defaultUnit, false);
            _squadControlUiCoordinator?.TryBindPortraitStrips();
        }

        private void ActivateControlledUnit(Unit unit, bool syncPortraitSelection = true)
        {
            if (!IsControllableSquadUnit(unit)) return;

            var controlledMember = EnsureSquadMemberForControllableUnit(unit);

            var targetInput = EnsurePlayerInputController(unit);
            if (targetInput == null) return;

            if (_activeInputController != null && _activeInputController != targetInput)
                _activeInputController.enabled = false;

            DisableOtherControllableInputs(targetInput);

            worldCamera = PlayerSpawnWiringService.EnsureWorldCamera(worldCamera, unit.transform.position);
            PlayerSpawnWiringService.BindCameraToUnit(unit.gameObject, worldCamera);
            PlayerSpawnWiringService.WireInputSystems(unit.gameObject);

            targetInput.enabled = true;
            _activeInputController = targetInput;
            _activeControlledUnit = unit;

            if (controlledMember != null)
            {
                var squadManager = SquadManager.Instance != null
                    ? SquadManager.Instance
                    : FindFirstObjectByType<SquadManager>();
                if (squadManager != null)
                    squadManager.SetSelectedMembers(new[] { controlledMember });
            }

            PlayerSpawnWiringService.BindHudToUnit(unit);

            if (syncPortraitSelection) _squadControlUiCoordinator?.SyncPortraitSelection(unit);
        }

        private static SquadMember EnsureSquadMemberForControllableUnit(Unit unit)
        {
            if (unit == null) return null;

            if (unit.Role is not (UnitRole.Player or UnitRole.SquadMember or UnitRole.Survivor))
                return null;

            var squadMember = unit.GetComponent<SquadMember>();
            if (squadMember == null)
                squadMember = unit.gameObject.AddComponent<SquadMember>();

            squadMember.RefreshReferences();
            return squadMember;
        }

        private static PlayerInputController EnsurePlayerInputController(Unit unit)
        {
            if (unit == null) return null;

            var input = unit.GetComponent<PlayerInputController>();
            return input != null ? input : unit.gameObject.AddComponent<PlayerInputController>();
        }

        private void DisableOtherControllableInputs(PlayerInputController activeInput)
        {
            var allInputs = FindObjectsByType<PlayerInputController>(FindObjectsSortMode.None);
            foreach (var input in allInputs)
            {
                if (input == null || input == activeInput) continue;

                var unit = input.GetComponent<Unit>();
                if (!IsControllableSquadUnit(unit)) continue;

                input.enabled = false;
            }
        }

        // Portrait strip + squad control UI extracted to SquadControlUiCoordinator.

        private bool IsControllableSquadUnit(Unit unit)
        {
            if (unit == null) return false;

            // Spawn-time safety: allow the freshly spawned player to become controllable
            // even if IsAlive has not flipped true yet this frame.
            if (unit == SpawnedPlayer) return true;

            if (!unit.IsAlive) return false;

            if (unit.GetComponent<SquadMember>() != null) return true;

                 return unit.Role is UnitRole.Player or UnitRole.SquadMember or UnitRole.Survivor;
        }

        private IEnumerator SpawnValidationZombieNearPlayer(Transform playerTransform)
        {
            if (playerTransform == null) yield break;

            if (validationZombieSpawnDelaySeconds > 0f)
                yield return new WaitForSeconds(validationZombieSpawnDelaySeconds);
            else
                yield return null;

            var zombieManager = FindFirstObjectByType<ZombieManager>();

            if (zombieManager == null)
            {
                var runtimeRoot = GameObject.Find("RuntimeWorldSystems");

                if (runtimeRoot == null) runtimeRoot = new GameObject("RuntimeWorldSystems");

                zombieManager = runtimeRoot.AddComponent<ZombieManager>();
            }

            if (zombieManager == null)
            {
                Debug.LogWarning("[PlayerSpawner] Validation zombie spawn skipped (ZombieManager missing).", this);
                yield break;
            }

            if (!zombieManager.IsInitialized) zombieManager.Initialize();

            var forward = Vector3.ProjectOnPlane(playerTransform.forward, Vector3.up);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                var randomDirection = Random.insideUnitCircle;
                if (randomDirection.sqrMagnitude <= 0.0001f) randomDirection = Vector2.right;

                forward = new Vector3(randomDirection.x, 0f, randomDirection.y);
            }

            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);

            var forwardDistance = Mathf.Max(1f, validationZombieSpawnDistanceFromPlayer);
            var lateralJitter = Random.Range(-Mathf.Max(0f, validationZombieSpawnLateralJitter),
                Mathf.Max(0f, validationZombieSpawnLateralJitter));

            var requestedSpawnPosition = playerTransform.position + forward * forwardDistance + right * lateralJitter;

            const int walkableAreaMask = 1;
            var navSampleOrigin = requestedSpawnPosition + Vector3.up * 2f;
            var hasNavSample =
                NavMesh.SamplePosition(navSampleOrigin, out var navHit, 24f, walkableAreaMask) ||
                NavMesh.SamplePosition(navSampleOrigin, out navHit, 24f, NavMesh.AllAreas);

            if (hasNavSample) requestedSpawnPosition = navHit.position;

            var spawned = zombieManager.SpawnZombie(validationZombieType, requestedSpawnPosition);

            if (spawned == null)
            {
                zombieManager.TickAmbientSpawn(playerTransform.position);
                Debug.LogWarning(
                    $"[PlayerSpawner] Validation zombie spawn failed near {requestedSpawnPosition}; requested ambient fallback tick.",
                    this);
                yield break;
            }

            if (!logValidationZombieSpawn) yield break;

            var offset = spawned.transform.position - playerTransform.position;
            offset.y = 0f;
            Debug.Log(
                $"[PlayerSpawner] Validation zombie spawned at {spawned.transform.position} (planar distance {offset.magnitude:F1}m from player).",
                spawned);
        }

        private void PrepareScenePlayerCandidatesForSpawn()
        {
            var owningUnit = ResolveOwningUnit();
            var units = FindObjectsByType<Unit>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var unit in units)
            {
                if (!CanReusePlayerCandidate(unit, owningUnit)) continue;

                SpawnAppearanceStylingService.PrepareAvatarForSanitizedSpawn(unit.gameObject);
                SanitizeRuntimeUnitHierarchy(unit.gameObject);
            }
        }

        private bool CanReusePlayerCandidate(Unit unit, Unit owningUnit)
        {
            if (!IsPlayerCandidate(unit)) return false;

            if (unit.gameObject.scene != gameObject.scene) return false;

            if (!unit.gameObject.activeInHierarchy) return false;

            if (owningUnit != null && unit == owningUnit) return false;

            var unitTransform = unit.transform;
            if (unitTransform == null) return false;

            if (transform == unitTransform || transform.IsChildOf(unitTransform) || unitTransform.IsChildOf(transform))
                return false;

            return true;
        }

        private static bool IsPlayerCandidate(Unit unit)
        {
            if (unit == null) return false;

            if (unit.Role == UnitRole.Player) return true;

            // Only accept input-driven fallbacks when they look like a designated player root.
            if (unit.GetComponent<PlayerInputController>() == null) return false;

            var name = unit.gameObject.name;
            return !string.IsNullOrWhiteSpace(name)
                   && name.IndexOf("player", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private Unit ResolveOwningUnit()
        {
            var directUnit = GetComponent<Unit>();
            if (directUnit != null) return directUnit;

            var parentUnit = GetComponentInParent<Unit>();
            return parentUnit != null ? parentUnit : GetComponentInChildren<Unit>(true);
        }

        private void SanitizeRuntimeUnitHierarchy(GameObject unitRoot)
        {
            SpawnAppearanceStylingService.SanitizeRuntimeUnitHierarchy(unitRoot, this);
        }

        private bool IsAttachedToUnit()
        {
            if (GetComponent<Unit>() != null) return true;

            if (GetComponentInParent<Unit>() != null) return true;

            return GetComponentInChildren<Unit>(true) != null;
        }

        private void ApplyDevModeSpawnInventory(Unit playerUnit)
        {
            if (playerUnit == null || _devModeInventoryHelper == null) return;

            _devModeInventoryHelper.Apply(
                playerUnit,
                devModeSpawnInventoryItems,
                devModeSpawnQuantityPerItem,
                devModeSpawnAmmoQuantityPerItem,
                devModeSpawnMinimumWeightLimit,
                logDevModeSpawnInventory);
        }

}
}
