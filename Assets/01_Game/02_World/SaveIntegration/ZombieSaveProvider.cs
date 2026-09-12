using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zombera.AI;
using Zombera.Characters;
using Zombera.Core;
using Zombera.UI.Menus.CharacterCreation;

namespace Zombera.Systems
{
    public sealed class ZombieSaveProvider : MonoBehaviour, ISaveProvider
    {
        [SerializeField] private ZombieManager zombieManager;
        [SerializeField] private UnitManager unitManager;
        
        [Header("Snapshot Performance")]
        [SerializeField] [Min(0)] private int maxZombieEntriesPerSnapshot = 128;
        [SerializeField] private bool prioritizeClosestZombiesInSnapshot;
        [SerializeField] private bool includeZombieStateInSnapshot;
        [SerializeField] private bool includeZombieAppearanceProfilesInSnapshot;
        [SerializeField] private bool skipDeadZombiesInSnapshot = true;

        public int Priority => 50; // Load after player/world

        private readonly List<ZombieController> _zombieBuffer = new();
        private readonly List<ZombieSnapshotCandidate> _zombieSnapshotCandidateBuffer = new();

        private struct ZombieSnapshotCandidate
        {
            public ZombieController Zombie;
            public float DistanceSqr;
        }

        public void OnSave(GameSaveData saveData)
        {
            EnsureReferences();
            if (zombieManager == null) return;

            var zombies = zombieManager.GetActiveZombies(_zombieBuffer);
            if (zombies == null || zombies.Count == 0) return;

            var maxEntries = maxZombieEntriesPerSnapshot <= 0
                ? zombies.Count
                : Mathf.Min(maxZombieEntriesPerSnapshot, zombies.Count);

            if (maxEntries <= 0) return;

            if (prioritizeClosestZombiesInSnapshot)
            {
                PopulateZombieSnapshotCandidates(zombies);
                _zombieSnapshotCandidateBuffer.Sort(static (a, b) => a.DistanceSqr.CompareTo(b.DistanceSqr));

                var written = 0;
                for (var i = 0; i < _zombieSnapshotCandidateBuffer.Count && written < maxEntries; i++)
                {
                    if (!TryAppendZombieSaveData(saveData, _zombieSnapshotCandidateBuffer[i].Zombie)) continue;
                    written++;
                }

                _zombieSnapshotCandidateBuffer.Clear();
                return;
            }

            var added = 0;
            for (var i = 0; i < zombies.Count && added < maxEntries; i++)
            {
                if (!TryAppendZombieSaveData(saveData, zombies[i])) continue;
                added++;
            }
        }

        public void OnLoad(GameSaveData saveData)
        {
            EnsureReferences();
            if (zombieManager == null || saveData.zombies == null || saveData.zombies.Count == 0) return;

            var activeZombies = zombieManager.GetActiveZombies(_zombieBuffer);

            foreach (var zombieData in saveData.zombies)
            {
                var zombie = activeZombies.FirstOrDefault(z =>
                {
                    if (z == null) return false;
                    var unit = z.Unit;
                    var id = unit != null ? unit.UnitId : z.GetInstanceID().ToString();
                    return string.Equals(id, zombieData.zombieId, System.StringComparison.Ordinal);
                });

                if (zombie == null)
                {
                    var type = zombieManager.ResolveAmbientZombieTypeForSpawn();
                    zombie = zombieManager.SpawnZombie(type, zombieData.position);
                }

                if (zombie == null) continue;

                ApplyZombieSaveData(zombie, zombieData);
            }
        }

        private void PopulateZombieSnapshotCandidates(List<ZombieController> zombies)
        {
            _zombieSnapshotCandidateBuffer.Clear();
            var referencePosition = ResolveZombieSnapshotReferencePosition();

            for (var i = 0; i < zombies.Count; i++)
            {
                var zombie = zombies[i];
                if (zombie == null) continue;

                var zombieHealth = zombie.Health;
                if (skipDeadZombiesInSnapshot && zombieHealth != null && zombieHealth.IsDead) continue;

                var delta = zombie.transform.position - referencePosition;
                _zombieSnapshotCandidateBuffer.Add(new ZombieSnapshotCandidate
                {
                    Zombie = zombie,
                    DistanceSqr = delta.sqrMagnitude
                });
            }
        }

        private bool TryAppendZombieSaveData(GameSaveData saveData, ZombieController zombie)
        {
            if (zombie == null) return false;

            var zombieUnit = zombie.Unit;
            var zombieHealth = zombie.Health;

            if (skipDeadZombiesInSnapshot && zombieHealth != null && zombieHealth.IsDead)
                return false;

            var appearanceJson = string.Empty;
            if (includeZombieAppearanceProfilesInSnapshot)
            {
                AppearanceProfileService.TryCaptureProfile(zombie.gameObject, out var zombieProfile);
                appearanceJson = CharacterAppearanceProfile.Serialize(zombieProfile);
            }

            var state = string.Empty;
            if (includeZombieStateInSnapshot)
            {
                var stateMachine = zombie.StateMachine;
                state = stateMachine != null ? stateMachine.CurrentState.ToString() : "Unknown";
            }

            saveData.zombies.Add(new ZombieSaveData
            {
                zombieId = zombieUnit != null ? zombieUnit.UnitId : zombie.GetInstanceID().ToString(),
                position = zombie.transform.position,
                health = zombieHealth != null ? zombieHealth.CurrentHealth : 0f,
                state = state,
                appearanceProfileJson = appearanceJson
            });

            return true;
        }

        private Vector3 ResolveZombieSnapshotReferencePosition()
        {
            if (unitManager == null) return Vector3.zero;
            var playerUnit = unitManager.FindFirstUnitByRole(UnitRole.Player);
            return playerUnit != null ? playerUnit.transform.position : Vector3.zero;
        }

        private void EnsureReferences()
        {
            if (zombieManager == null) zombieManager = Object.FindFirstObjectByType<ZombieManager>();
            if (unitManager == null) unitManager = Object.FindFirstObjectByType<UnitManager>();
        }

        private static void ApplyZombieSaveData(ZombieController zombie, ZombieSaveData data)
        {
            zombie.transform.position = data.position;
            var health = zombie.Health;
            if (health != null && data.health > 0f) health.SetHealth(data.health);

            if (!string.IsNullOrWhiteSpace(data.appearanceProfileJson))
            {
                var profile = CharacterAppearanceProfile.Deserialize(data.appearanceProfileJson);
                AppearanceProfileService.TryApplyProfile(zombie.gameObject, profile);
            }
        }
    }
}