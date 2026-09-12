#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Zombera.Core;
using Zombera.Systems;
using Object = UnityEngine.Object;

#endregion

namespace Zombera.Characters
{
    // ReSharper disable InvertIf
    public delegate bool TrySampleGroundFromPhysicsDelegate(Vector3 worldPosition, out float groundY);

    public readonly struct StartupSquadSpawnConfig
    {
        public readonly bool Enabled;
        public readonly int StartupSquadTotalCount;
        public readonly int MinimumStartupSquadTotalCount;
        public readonly int StartupInitialCharacterCount;
        public readonly float StartupSquadRingRadius;
        public readonly bool RandomizeVisualVariants;
        public readonly bool ApplySkillTiers;
        public readonly bool LogSpawning;
        public readonly int[] SkillTiers;
        public readonly int[] DefaultSkillTiers;

        public readonly GameObject PlayerPrefabFallback;
        public readonly GameObject SquadMemberPrefab;
        public readonly Transform SquadParent;

        public readonly float TerrainHeightOffset;
        public readonly float NavMeshVerticalExtent;

#pragma warning disable S107 // Config struct constructor — all fields are readonly; many params is intentional
        public StartupSquadSpawnConfig(
            bool enabled,
            int startupSquadTotalCount,
            int minimumStartupSquadTotalCount,
            int startupInitialCharacterCount,
            float startupSquadRingRadius,
            bool randomizeVisualVariants,
            bool applySkillTiers,
            bool logSpawning,
            int[] skillTiers,
            int[] defaultSkillTiers,
            GameObject playerPrefabFallback,
            GameObject squadMemberPrefab,
            Transform squadParent,
            float terrainHeightOffset,
            float navMeshVerticalExtent)
        {
            Enabled = enabled;
            StartupSquadTotalCount = startupSquadTotalCount;
            MinimumStartupSquadTotalCount = minimumStartupSquadTotalCount;
            StartupInitialCharacterCount = startupInitialCharacterCount;
            StartupSquadRingRadius = startupSquadRingRadius;
            RandomizeVisualVariants = randomizeVisualVariants;
            ApplySkillTiers = applySkillTiers;
            LogSpawning = logSpawning;
            SkillTiers = skillTiers;
            DefaultSkillTiers = defaultSkillTiers;
            PlayerPrefabFallback = playerPrefabFallback;
            SquadMemberPrefab = squadMemberPrefab;
            SquadParent = squadParent;
            TerrainHeightOffset = terrainHeightOffset;
            NavMeshVerticalExtent = navMeshVerticalExtent;
        }
    }

    public sealed partial class StartupSquadSpawner
    {
        internal static readonly WaitForSeconds StartupNavMeshRetryWait = new(0.5f);

        internal readonly Func<Scene> _getOwnerScene;
        internal readonly Func<Unit> _getSpawnedPlayer;
        private readonly MonoBehaviour _host;
        internal readonly Action<GameObject> _sanitizeRuntimeUnitHierarchy;
        private readonly TrySampleGroundFromPhysicsDelegate _trySampleGroundFromPhysics;

        private bool _loggedMissingStartupSquadPrefab;

        public StartupSquadSpawner(
            MonoBehaviour host,
            Func<Unit> getSpawnedPlayer,
            Func<Scene> getOwnerScene,
            Action<GameObject> sanitizeRuntimeUnitHierarchy,
            TrySampleGroundFromPhysicsDelegate trySampleGroundFromPhysics)
        {
            _host = host;
            _getSpawnedPlayer = getSpawnedPlayer;
            _getOwnerScene = getOwnerScene;
            _sanitizeRuntimeUnitHierarchy = sanitizeRuntimeUnitHierarchy;
            _trySampleGroundFromPhysics = trySampleGroundFromPhysics;
        }

        public void StartEnsureStartupSquadDeferred(Unit playerUnit, StartupSquadSpawnConfig config, int spawnPerFrame)
        {
            if (_host == null)
            {
                // Fall back to immediate.
                _ = EnsureStartupSquad(playerUnit, config);
                return;
            }

            _host.StartCoroutine(EnsureStartupSquadDeferred(playerUnit, config, spawnPerFrame));
        }

        public List<Unit> EnsureStartupSquad(Unit playerUnit, StartupSquadSpawnConfig config)
        {
            if (!config.Enabled || playerUnit == null) return null;

            var targetTotalCount = Mathf.Max(1, config.StartupSquadTotalCount, config.MinimumStartupSquadTotalCount,
                config.StartupInitialCharacterCount);
            var targetNpcCount = Mathf.Max(0, targetTotalCount - 1);

            var squadUnits = CollectExistingStartupSquadUnits();
            var requiredNewMembers = Mathf.Max(0, targetNpcCount - squadUnits.Count);
            var spawnedMembers = 0;

            LogStartupSquadDiagnostics(
                "EnsureStartupSquad start",
                config,
                playerUnit,
                targetTotalCount,
                targetNpcCount,
                squadUnits.Count,
                0);

            var pendingMembers = requiredNewMembers;
            while (pendingMembers-- > 0)
            {
                var spawnIndex = squadUnits.Count;
                var spawnedUnit = SpawnStartupSquadMember(playerUnit, spawnIndex, Mathf.Max(1, targetNpcCount), config);
                if (spawnedUnit == null) continue;

                squadUnits.Add(spawnedUnit);
                spawnedMembers++;
            }

            ApplyRandomizedVisualVariantsToStartupSquad(squadUnits, config.RandomizeVisualVariants);

            if (config.ApplySkillTiers)
                ApplyStartupSquadSkillTiers(playerUnit, squadUnits, config.SkillTiers, config.DefaultSkillTiers);

            SquadManager.Instance?.RefreshSquadRoster();

            if (config.LogSpawning)
                Debug.Log(
                    $"[PlayerSpawner] Startup test squad ready. Player='{playerUnit.name}', ExistingMembers={squadUnits.Count - spawnedMembers}, SpawnedMembers={spawnedMembers}, " +
                    $"TargetTotal={targetTotalCount}, TotalRoster={1 + squadUnits.Count}.",
                    playerUnit);

            LogStartupSquadDiagnostics(
                "EnsureStartupSquad complete",
                config,
                playerUnit,
                targetTotalCount,
                targetNpcCount,
                squadUnits.Count - spawnedMembers,
                spawnedMembers);

            return squadUnits;
        }

    }
}
