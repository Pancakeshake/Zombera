using System;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    [AddComponentMenu("Zombera/World/World State Manager")]
    [DisallowMultipleComponent]
    public sealed partial class WorldStateManager : MonoBehaviour
    {
        private readonly WorldStateIndex _index = new();

        private WorldState _state;
        private WorldStateAvailability _availability = WorldStateAvailability.None;

        public bool HasState => _state != null;
        public long Revision => _state?.revision ?? 0L;
        public WorldStateHeader Header => WorldStateCloner.Clone(_state?.header);
        public WorldStateAvailability Availability => _availability;

        public event Action<WorldStateLifecycleChange> LifecycleChanged;
        public event Action<WorldStateChangeSet> StateChanged;

        public bool TryCreateFresh(WorldStateHeader header, out WorldValidationReport report)
        {
            var candidate = new WorldState
            {
                header = WorldStateCloner.Clone(header) ?? new WorldStateHeader(),
                revision = 0L,
                clock = new WorldSimulationClockState()
            };

            PopulateEmptyTilePartitions(candidate);
            report = WorldStateValidator.Validate(candidate);
            if (!report.IsValid)
            {
                RaiseValidationFailed("Fresh world state validation failed.");
                return false;
            }

            ReplaceState(candidate, WorldStateAvailability.Fresh);
            RaiseLifecycle(WorldStateLifecycleReason.Rebuilt, "Created fresh world state.", null);
            return true;
        }

        public bool TryLoad(WorldState candidate, out WorldValidationReport report)
        {
            var copy = WorldStateCloner.Clone(candidate);
            report = WorldStateValidator.Validate(copy);
            if (!report.IsValid)
            {
                RaiseValidationFailed("World state load validation failed.");
                return false;
            }

            ReplaceState(copy, WorldStateAvailability.Loaded);
            RaiseLifecycle(WorldStateLifecycleReason.Loaded, "Loaded world state.", null);
            return true;
        }

        public WorldState CaptureCanonicalCopy() => WorldStateCloner.Clone(_state);

        public void Clear(WorldStateClearReason reason)
        {
            _state = null;
            _availability = WorldStateAvailability.None;
            _index.Clear();
            RaiseLifecycle(WorldStateLifecycleReason.Cleared, reason.ToString(), null);
        }

        public void MarkLegacyNoWorldState(string reason)
        {
            _state = null;
            _availability = WorldStateAvailability.LegacyNoWorldState;
            _index.Clear();
            RaiseLifecycle(
                WorldStateLifecycleReason.Loaded,
                string.IsNullOrWhiteSpace(reason) ? "Legacy save has no WorldState payload." : reason,
                null);
        }

        private void ReplaceState(WorldState state, WorldStateAvailability availability)
        {
            _state = state;
            _availability = availability;
            RebuildIndexes();
        }

        private void RebuildIndexes()
        {
            _index.Rebuild(_state);
            AddDerivedCoverage();
        }

        private void PopulateEmptyTilePartitions(WorldState state)
        {
            var header = state.header ?? new WorldStateHeader();
            var tilesPerSide = Mathf.Max(0, header.tilesPerSide);
            state.tiles ??= new System.Collections.Generic.List<WorldTilePartitionState>();
            state.tiles.Clear();

            for (var z = 0; z < tilesPerSide; z++)
            {
                for (var x = 0; x < tilesPerSide; x++)
                {
                    state.tiles.Add(new WorldTilePartitionState
                    {
                        key = new WorldTileKey(x, z),
                        terrain = CreateEmptyTerrainDescriptor()
                    });
                }
            }
        }

        private static TerrainChunkState CreateEmptyTerrainDescriptor()
        {
            return new TerrainChunkState
            {
                baseGeneratorId = "WorldBuilder.Terrain",
                baseGeneratorVersion = 1,
                baseGenerationFingerprint = string.Empty,
                seaLevelWorldY = 0f,
                terrainBaseWorldY = 0f,
                verticalSizeMeters = 600f,
                heightmapResolution = 33,
                alphamapResolution = 32,
                baseMapResolution = 16,
                detailResolution = 32,
                detailSamplesPerPatch = 8
            };
        }

        private void RaiseCommitted(
            WorldStateLifecycleReason lifecycleReason,
            WorldStateChangeSet changes)
        {
            StateChanged?.Invoke(changes);
            RaiseLifecycle(lifecycleReason, changes?.Reason ?? string.Empty, changes);
        }

        private void RaiseValidationFailed(string reason)
        {
            RaiseLifecycle(WorldStateLifecycleReason.ValidationFailed, reason, null);
        }

        private void RaiseLifecycle(
            WorldStateLifecycleReason lifecycleReason,
            string reason,
            WorldStateChangeSet changes)
        {
            LifecycleChanged?.Invoke(new WorldStateLifecycleChange
            {
                Revision = Revision,
                LifecycleReason = lifecycleReason,
                Reason = reason ?? string.Empty,
                ChangeSet = changes ?? new WorldStateChangeSet { Revision = Revision }
            });
        }

        private static WorldValidationReport ValidReport() =>
            new() { IsValid = true };

        private static WorldValidationReport ErrorReport(string error)
        {
            var report = new WorldValidationReport { IsValid = false };
            report.Errors.Add(error ?? "WorldState operation failed.");
            return report;
        }
    }
}
