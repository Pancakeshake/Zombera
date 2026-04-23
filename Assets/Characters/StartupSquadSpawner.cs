using System.Collections.Generic;
using System.Collections;
using UMA.CharacterSystem;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Zombera.AI;
using Zombera.Systems;

namespace Zombera.Characters
{
    public delegate bool TrySampleGroundFromPhysicsDelegate(Vector3 worldPosition, out float groundY);

    public readonly struct StartupSquadSpawnConfig
    {
        public readonly bool Enabled;
        public readonly int StartupSquadTotalCount;
        public readonly int MinimumStartupSquadTotalCount;
        public readonly int StartupInitialCharacterCount;
        public readonly float StartupSquadRingRadius;
        public readonly bool RandomizeUmaVisuals;
        public readonly bool ApplySkillTiers;
        public readonly bool LogSpawning;
        public readonly int[] SkillTiers;
        public readonly int[] DefaultSkillTiers;

        public readonly GameObject PlayerPrefabFallback;
        public readonly GameObject SquadMemberPrefab;
        public readonly Transform SquadParent;

        public readonly float TerrainHeightOffset;
        public readonly float NavMeshVerticalExtent;

        public StartupSquadSpawnConfig(
            bool enabled,
            int startupSquadTotalCount,
            int minimumStartupSquadTotalCount,
            int startupInitialCharacterCount,
            float startupSquadRingRadius,
            bool randomizeUmaVisuals,
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
            RandomizeUmaVisuals = randomizeUmaVisuals;
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

    public sealed class StartupSquadSpawner
    {
        private readonly MonoBehaviour _host;
        private readonly System.Func<Unit> _getSpawnedPlayer;
        private readonly System.Func<Scene> _getOwnerScene;
        private readonly System.Action<GameObject> _sanitizeRuntimeUnitHierarchy;
        private readonly TrySampleGroundFromPhysicsDelegate _trySampleGroundFromPhysics;

        private bool _loggedMissingStartupSquadPrefab;

        public StartupSquadSpawner(
            MonoBehaviour host,
            System.Func<Unit> getSpawnedPlayer,
            System.Func<Scene> getOwnerScene,
            System.Action<GameObject> sanitizeRuntimeUnitHierarchy,
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
            if (!config.Enabled || playerUnit == null)
            {
                return null;
            }

            int targetTotalCount = Mathf.Max(1, config.StartupSquadTotalCount, config.MinimumStartupSquadTotalCount, config.StartupInitialCharacterCount);
            int targetNpcCount = Mathf.Max(0, targetTotalCount - 1);

            List<Unit> squadUnits = CollectExistingStartupSquadUnits();
            int requiredNewMembers = Mathf.Max(0, targetNpcCount - squadUnits.Count);
            int spawnedMembers = 0;

            for (int i = 0; i < requiredNewMembers; i++)
            {
                int spawnIndex = squadUnits.Count;
                Unit spawnedUnit = SpawnStartupSquadMember(playerUnit, spawnIndex, Mathf.Max(1, targetNpcCount), config);
                if (spawnedUnit == null)
                {
                    continue;
                }

                squadUnits.Add(spawnedUnit);
                spawnedMembers++;
            }

            ApplyRandomizedUmaVisualsToStartupSquad(squadUnits, config.RandomizeUmaVisuals);

            if (config.ApplySkillTiers)
            {
                ApplyStartupSquadSkillTiers(playerUnit, squadUnits, config.SkillTiers, config.DefaultSkillTiers);
            }

            SquadManager.Instance?.RefreshSquadRoster();

            if (config.LogSpawning)
            {
                Debug.Log(
                    $"[PlayerSpawner] Startup test squad ready. Player='{playerUnit.name}', ExistingMembers={squadUnits.Count - spawnedMembers}, SpawnedMembers={spawnedMembers}, " +
                    $"TargetTotal={targetTotalCount}, TotalRoster={1 + squadUnits.Count}.",
                    playerUnit);
            }

            return squadUnits;
        }

        private IEnumerator EnsureStartupSquadDeferred(Unit playerUnit, StartupSquadSpawnConfig config, int spawnPerFrame)
        {
            if (!config.Enabled || playerUnit == null)
            {
                yield break;
            }

            int targetTotalCount = Mathf.Max(1, config.StartupSquadTotalCount, config.MinimumStartupSquadTotalCount, config.StartupInitialCharacterCount);
            int targetNpcCount = Mathf.Max(0, targetTotalCount - 1);

            List<Unit> squadUnits = CollectExistingStartupSquadUnits();
            int requiredNewMembers = Mathf.Max(0, targetNpcCount - squadUnits.Count);
            int spawnedMembers = 0;

            int safePerFrame = Mathf.Clamp(spawnPerFrame, 1, 10);
            int spawnedThisFrame = 0;

            for (int i = 0; i < requiredNewMembers; i++)
            {
                int spawnIndex = squadUnits.Count;
                Unit spawnedUnit = SpawnStartupSquadMember(playerUnit, spawnIndex, Mathf.Max(1, targetNpcCount), config);
                if (spawnedUnit != null)
                {
                    squadUnits.Add(spawnedUnit);
                    spawnedMembers++;
                    spawnedThisFrame++;
                }

                if (spawnedThisFrame >= safePerFrame)
                {
                    spawnedThisFrame = 0;
                    yield return null; // budget spawn cost across frames
                }
            }

            if (config.RandomizeUmaVisuals && squadUnits != null && squadUnits.Count > 0)
            {
                // Randomizing UMA is heavy; do it over multiple frames too.
                int visualsPerFrame = Mathf.Clamp(safePerFrame, 1, 8);
                int v = 0;
                for (int i = 0; i < squadUnits.Count; i++)
                {
                    ApplyRandomizedUmaVisual(squadUnits[i]);
                    v++;
                    if (v >= visualsPerFrame)
                    {
                        v = 0;
                        yield return null;
                    }
                }
            }

            if (config.ApplySkillTiers)
            {
                ApplyStartupSquadSkillTiers(playerUnit, squadUnits, config.SkillTiers, config.DefaultSkillTiers);
            }

            SquadManager.Instance?.RefreshSquadRoster();

            if (config.LogSpawning)
            {
                Debug.Log(
                    $"[PlayerSpawner] Startup test squad ready (deferred). Player='{playerUnit.name}', ExistingMembers={squadUnits.Count - spawnedMembers}, SpawnedMembers={spawnedMembers}, " +
                    $"TargetTotal={targetTotalCount}, TotalRoster={1 + squadUnits.Count}.",
                    playerUnit);
            }
        }

        private List<Unit> CollectExistingStartupSquadUnits()
        {
            List<Unit> result = new List<Unit>();
            SquadMember[] existingMembers = UnityEngine.Object.FindObjectsByType<SquadMember>(FindObjectsSortMode.None);
            Scene ownerScene = _getOwnerScene != null ? _getOwnerScene() : default;

            for (int i = 0; i < existingMembers.Length; i++)
            {
                SquadMember member = existingMembers[i];
                if (member == null || (ownerScene.IsValid() && member.gameObject.scene != ownerScene))
                {
                    continue;
                }

                Unit memberUnit = member.Unit != null ? member.Unit : member.GetComponent<Unit>();
                Unit spawnedPlayer = _getSpawnedPlayer != null ? _getSpawnedPlayer() : null;
                if (memberUnit == null || memberUnit == spawnedPlayer || result.Contains(memberUnit))
                {
                    continue;
                }

                memberUnit.SetRole(UnitRole.SquadMember);
                result.Add(memberUnit);
            }

            return result;
        }

        private static void ApplyRandomizedUmaVisualsToStartupSquad(List<Unit> squadUnits, bool enabled)
        {
            if (!enabled || squadUnits == null)
            {
                return;
            }

            for (int i = 0; i < squadUnits.Count; i++)
            {
                ApplyRandomizedUmaVisual(squadUnits[i]);
            }
        }

        private static void ApplyRandomizedUmaVisual(Unit unit)
        {
            if (unit == null)
            {
                return;
            }

            if (unit.GetComponent<DynamicCharacterAvatar>() == null)
            {
                return;
            }

            NpcUmaVisualSpawner visualSpawner = unit.GetComponent<NpcUmaVisualSpawner>();
            if (visualSpawner == null)
            {
                visualSpawner = unit.gameObject.AddComponent<NpcUmaVisualSpawner>();
            }

            visualSpawner.ApplyRandomAppearanceNow(force: true);
        }

        private Unit SpawnStartupSquadMember(Unit playerUnit, int spawnIndex, int targetNpcCount, StartupSquadSpawnConfig config)
        {
            if (playerUnit == null)
            {
                return null;
            }

            GameObject spawnPrefab = config.SquadMemberPrefab != null ? config.SquadMemberPrefab : config.PlayerPrefabFallback;
            if (spawnPrefab == null && playerUnit != null)
            {
                spawnPrefab = playerUnit.gameObject;
            }

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

            Vector3 spawnPosition = ResolveStartupSquadSpawnPosition(playerUnit.transform.position, spawnIndex, targetNpcCount, config);
            Transform parent = config.SquadParent != null ? config.SquadParent : null;

            GameObject spawnedObject = parent != null
                ? UnityEngine.Object.Instantiate(spawnPrefab, spawnPosition, playerUnit.transform.rotation, parent)
                : UnityEngine.Object.Instantiate(spawnPrefab, spawnPosition, playerUnit.transform.rotation);

            if (spawnedObject == null)
            {
                return null;
            }

            _sanitizeRuntimeUnitHierarchy?.Invoke(spawnedObject);

            Unit squadUnit = EnsureStartupSquadMemberWiring(spawnedObject);
            if (squadUnit == null)
            {
                UnityEngine.Object.Destroy(spawnedObject);
                return null;
            }

            spawnedObject.name = $"Squadmate_{spawnIndex + 1:00}";
            squadUnit.Health?.ResetHealthToMax();
            return squadUnit;
        }

        private Vector3 ResolveStartupSquadSpawnPosition(Vector3 playerPosition, int spawnIndex, int targetNpcCount, StartupSquadSpawnConfig config)
        {
            int clampedTargetCount = Mathf.Max(1, targetNpcCount);
            float ringRadius = Mathf.Max(0.25f, config.StartupSquadRingRadius);
            float angle = (Mathf.PI * 2f * spawnIndex) / clampedTargetCount;

            Vector3 ringOffset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ringRadius;
            Vector3 spawnPosition = playerPosition + ringOffset;

            // Pre-align Y with physics ground before NavMesh sampling (matches prior PlayerSpawner behavior).
            if (_trySampleGroundFromPhysics != null && _trySampleGroundFromPhysics(spawnPosition, out float sampledGroundY))
            {
                spawnPosition.y = sampledGroundY + Mathf.Max(0f, config.TerrainHeightOffset);
            }

            Vector3 navSampleOrigin = spawnPosition + Vector3.up * 2f;
            float navSampleRadius = Mathf.Max(6f, ringRadius * 1.25f);
            int walkableAreaMask = 1 << 0;
            bool foundNav =
                NavMesh.SamplePosition(navSampleOrigin, out NavMeshHit navHit, navSampleRadius, walkableAreaMask) ||
                NavMesh.SamplePosition(navSampleOrigin, out navHit, navSampleRadius, NavMesh.AllAreas);

            if (!foundNav)
            {
                Vector3 elevatedSampleOrigin = new Vector3(
                    spawnPosition.x,
                    spawnPosition.y + Mathf.Max(config.NavMeshVerticalExtent, 64f),
                    spawnPosition.z);
                float elevatedRadius = Mathf.Max(18f, navSampleRadius * 2.2f);

                foundNav =
                    NavMesh.SamplePosition(elevatedSampleOrigin, out navHit, elevatedRadius, walkableAreaMask) ||
                    NavMesh.SamplePosition(elevatedSampleOrigin, out navHit, elevatedRadius, NavMesh.AllAreas);
            }

            if (foundNav)
            {
                return navHit.position;
            }

            return playerPosition;
        }

        private static Unit EnsureStartupSquadMemberWiring(GameObject squadObject)
        {
            if (squadObject == null)
            {
                return null;
            }

            Unit unit = squadObject.GetComponent<Unit>();
            if (unit == null)
            {
                Debug.LogWarning("[PlayerSpawner] Startup squad member is missing Unit component.", squadObject);
                return null;
            }

            unit.SetRole(UnitRole.SquadMember);

            if (squadObject.GetComponent<SquadMember>() == null)
            {
                squadObject.AddComponent<SquadMember>();
            }

            if (squadObject.GetComponent<FollowController>() == null)
            {
                squadObject.AddComponent<FollowController>();
            }

            UnitController controller = squadObject.GetComponent<UnitController>();
            if (controller != null)
            {
                controller.SetRole(UnitRole.SquadMember);
                controller.ForceEnableAgent();
            }

            return unit;
        }

        private static void ApplyStartupSquadSkillTiers(Unit playerUnit, List<Unit> squadUnits, int[] tiers, int[] defaultTiers)
        {
            if (playerUnit == null)
            {
                return;
            }

            int rosterIndex = 0;
            ApplyUniformSkillTier(playerUnit, ResolveStartupSkillTierForRosterIndex(rosterIndex, tiers, defaultTiers));
            rosterIndex++;

            if (squadUnits == null)
            {
                return;
            }

            for (int i = 0; i < squadUnits.Count; i++)
            {
                Unit unit = squadUnits[i];
                if (unit == null)
                {
                    continue;
                }

                ApplyUniformSkillTier(unit, ResolveStartupSkillTierForRosterIndex(rosterIndex, tiers, defaultTiers));
                rosterIndex++;
            }
        }

        private static int ResolveStartupSkillTierForRosterIndex(int rosterIndex, int[] tiers, int[] defaultTiers)
        {
            int[] resolved = tiers;
            if (resolved == null || resolved.Length == 0)
            {
                resolved = defaultTiers;
            }

            if (resolved == null || resolved.Length == 0)
            {
                return UnitStats.MinSkillLevel;
            }

            int safeIndex = Mathf.Clamp(rosterIndex, 0, resolved.Length - 1);
            int rawTier = resolved[safeIndex];
            return Mathf.Clamp(rawTier, UnitStats.MinSkillLevel, UnitStats.MaxSkillLevel);
        }

        private static void ApplyUniformSkillTier(Unit unit, int tier)
        {
            if (unit == null || unit.Stats == null)
            {
                return;
            }

            int clampedTier = Mathf.Clamp(tier, UnitStats.MinSkillLevel, UnitStats.MaxSkillLevel);
            UnitStats stats = unit.Stats;
            UnitSkillType[] allSkillTypes = (UnitSkillType[])System.Enum.GetValues(typeof(UnitSkillType));
            for (int i = 0; i < allSkillTypes.Length; i++)
            {
                stats.SetSkill(allSkillTypes[i], clampedTier);
            }

            unit.Health?.ResetHealthToMax();
        }
    }
}

