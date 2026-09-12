using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    /// <summary>
    /// Owns one active <see cref="WorldStateStageTransaction"/> per pipeline stage.
    /// </summary>
    public sealed class WorldStateGenerationRecorder
    {
        private readonly WorldStateManager _stateManager;

        public WorldStateGenerationRecorder(WorldStateManager stateManager = null)
        {
            _stateManager = stateManager;
        }

        public WorldStateManager StateManager => _stateManager;
        public WorldStateStageTransaction CurrentStage { get; private set; }

        public WorldStateStageTransaction BeginStage(
            WorldBuildStageId stage,
            WorldBuildScope scope,
            WorldStateManager stateManager = null,
            WorldStateHeader header = null,
            int worldSeed = 0)
        {
            if (CurrentStage != null)
                throw new InvalidOperationException("A WorldState generation stage is already active.");

            var manager = stateManager != null ? stateManager : _stateManager;
            CurrentStage = new WorldStateStageTransaction(
                stage,
                scope,
                manager,
                header,
                worldSeed,
                ClearCurrentStage);
            return CurrentStage;
        }

        private void ClearCurrentStage(WorldStateStageTransaction transaction)
        {
            if (ReferenceEquals(CurrentStage, transaction))
                CurrentStage = null;
        }
    }

    public sealed class WorldStateStageTransaction : IDisposable, IGeneratedBuildingStateSink
    {
        private readonly WorldStateManager _stateManager;
        private readonly WorldStateHeader _header;
        private readonly int _worldSeed;
        private readonly Action<WorldStateStageTransaction> _onDispose;
        private readonly WorldGeneratedStateBatch _batch = new();
        private readonly List<UnityEngine.Object> _provisionalViews = new(64);
        private readonly Dictionary<WorldEntityId, string> _collisionRegistry = new();
        private readonly List<BuildingState> _stagedBuildings = new(64);

        private bool _committed;
        private bool _disposed;
        private bool _clearRequested;
        private WorldResetMode _clearMode;
        private bool _replaceWorldPlannedRoads;
        private bool _replaceCityGeneratedRoads;

        internal WorldStateStageTransaction(
            WorldBuildStageId stage,
            WorldBuildScope scope,
            WorldStateManager stateManager,
            WorldStateHeader header,
            int worldSeed,
            Action<WorldStateStageTransaction> onDispose)
        {
            Stage = stage;
            Scope = scope;
            _stateManager = stateManager;
            _header = header;
            _worldSeed = worldSeed;
            _onDispose = onDispose;
            _batch.Reason = stage.ToString();
        }

        public WorldBuildStageId Stage { get; }
        public WorldBuildScope Scope { get; }

        public void ClearGeneratedDomains(WorldResetMode mode)
        {
            ThrowIfInactive();
            _clearRequested = true;
            _clearMode = mode;
        }

        public void ReplaceTerrain(IReadOnlyList<WorldTileTerrainRecord> records)
        {
            ThrowIfInactive();
            _batch.TerrainRecords = CopyTerrainRecords(records);
        }

        public void ReplaceRegions(IReadOnlyList<RegionState> records)
        {
            ThrowIfInactive();
            _batch.Regions = CloneList(records, WorldStateCloner.Clone);
        }

        public void ReplaceSettlements(IReadOnlyList<SettlementState> records)
        {
            ThrowIfInactive();
            _batch.Settlements = CloneList(records, WorldStateCloner.Clone);
        }

        public void ReplaceRoads(RoadSourceKind source, IReadOnlyList<RoadState> records)
        {
            ThrowIfInactive();
            if (source == RoadSourceKind.WorldPlanned)
                _replaceWorldPlannedRoads = true;
            else
                _replaceCityGeneratedRoads = true;

            var cloned = CloneList(records, WorldStateCloner.Clone);
            if (_batch.Roads == null)
                _batch.Roads = new List<RoadState>();

            // Keep only roads of the other source when replacing one layer.
            if (_replaceWorldPlannedRoads && !_replaceCityGeneratedRoads)
                RemoveRoadsOfKind(_batch.Roads, RoadSourceKind.WorldPlanned);
            else if (_replaceCityGeneratedRoads && !_replaceWorldPlannedRoads)
                RemoveRoadsOfKind(_batch.Roads, RoadSourceKind.CityGenerated);
            else
                _batch.Roads.Clear();

            for (var i = 0; i < cloned.Count; i++)
                _batch.Roads.Add(cloned[i]);
        }

        public void ReplaceDistricts(IReadOnlyList<DistrictState> records)
        {
            ThrowIfInactive();
            _batch.Districts = CloneList(records, WorldStateCloner.Clone);
        }

        public void ReplaceLots(IReadOnlyList<LotState> records)
        {
            ThrowIfInactive();
            _batch.Lots = CloneList(records, WorldStateCloner.Clone);
        }

        public void ReplacePois(IReadOnlyList<PoiState> records)
        {
            ThrowIfInactive();
            _batch.Pois = CloneList(records, WorldStateCloner.Clone);
        }

        public bool TryStageBuilding(
            GeneratedBuildingPlacementData placement,
            out WorldEntityId id,
            out string error)
        {
            ThrowIfInactive();
            id = default;
            error = null;

            if (placement == null)
            {
                error = "Placement data is null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(placement.ArchetypeId))
            {
                error = "Building archetypeId is required.";
                return false;
            }

            try
            {
                var parent = placement.LotId.kind == WorldEntityKind.Lot
                    ? placement.LotId
                    : placement.DistrictId;
                var extents = new Vector2(
                    Mathf.Max(0.01f, placement.FootprintXZ.width * 0.5f),
                    Mathf.Max(0.01f, placement.FootprintXZ.height * 0.5f));
                id = WorldStableIdFactory.CreateBuildingId(
                    _worldSeed,
                    parent.kind,
                    parent,
                    placement.SourceId,
                    placement.Ordinal,
                    new Vector2(placement.Position.x, placement.Position.z),
                    extents,
                    placement.Rotation.eulerAngles.y,
                    placement.Scale,
                    _collisionRegistry);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            var building = new BuildingState
            {
                id = id,
                sourceId = placement.SourceId ?? string.Empty,
                settlementId = placement.SettlementId,
                districtId = placement.DistrictId,
                lotId = placement.LotId,
                archetypeId = placement.ArchetypeId,
                typeId = placement.TypeId ?? string.Empty,
                districtType = placement.DistrictType,
                position = placement.Position,
                rotation = placement.Rotation,
                scale = placement.Scale,
                footprintXZ = placement.FootprintXZ,
                streetFace = placement.StreetFace,
                hasDoorAnchor = placement.HasDoorAnchor,
                doorAnchorWorld = placement.DoorAnchorWorld,
                condition01 = 1f,
                abandoned = false,
                abandonedAtHour = -1,
                occupancy = new BuildingOccupancyState(),
                ownership = new BuildingOwnershipState(),
                utilities = new BuildingUtilityState(),
                damage = new BuildingDamageState(),
                fire = new BuildingFireState(),
                loot = new BuildingLootState(),
                modifications = new List<BuildingModificationState>(),
                modules = new List<BuildingModuleState>()
            };

            _stagedBuildings.Add(building);
            if (_batch.Buildings == null)
                _batch.Buildings = new List<BuildingState>();
            _batch.Buildings.Add(WorldStateCloner.Clone(building));
            return true;
        }

        public void RegisterProvisionalView(UnityEngine.Object viewObject)
        {
            ThrowIfInactive();
            if (viewObject != null)
                _provisionalViews.Add(viewObject);
        }

        public bool TryCommit(out WorldStateChangeSet changes, out WorldValidationReport report)
        {
            ThrowIfInactive();
            changes = new WorldStateChangeSet();
            report = new WorldValidationReport { IsValid = true };

            if (_stateManager == null || !_stateManager.HasState)
            {
                _committed = true;
                return true;
            }

            if (_clearRequested)
                ApplyClearToBatch();

            if (!HasBatchContent())
            {
                _committed = true;
                return true;
            }

            if (!_stateManager.TryApplyGeneratedBatch(_batch, out changes, out report))
            {
                DestroyProvisionalViews();
                return false;
            }

            _committed = true;
            _provisionalViews.Clear();
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (!_committed)
                DestroyProvisionalViews();

            _onDispose?.Invoke(this);
        }

        private void ApplyClearToBatch()
        {
            _batch.Regions ??= new List<RegionState>();
            _batch.Settlements ??= new List<SettlementState>();
            _batch.Roads ??= new List<RoadState>();
            _batch.Districts ??= new List<DistrictState>();
            _batch.Lots ??= new List<LotState>();
            _batch.Buildings ??= new List<BuildingState>();
            _batch.Pois ??= new List<PoiState>();
            _batch.TerrainRecords ??= new List<WorldTileTerrainRecord>();
            _batch.Regions.Clear();
            _batch.Settlements.Clear();
            _batch.Roads.Clear();
            _batch.Districts.Clear();
            _batch.Lots.Clear();
            _batch.Buildings.Clear();
            _batch.Pois.Clear();
            if (_clearMode == WorldResetMode.TerrainAndContent)
                _batch.TerrainRecords.Clear();
        }

        private bool HasBatchContent()
        {
            return _clearRequested
                   || HasItems(_batch.TerrainRecords)
                   || HasItems(_batch.Regions)
                   || HasItems(_batch.Settlements)
                   || HasItems(_batch.Roads)
                   || HasItems(_batch.Districts)
                   || HasItems(_batch.Lots)
                   || HasItems(_batch.Buildings)
                   || HasItems(_batch.Pois)
                   || HasItems(_batch.TerrainModifications)
                   || HasItems(_batch.Events);
        }

        private void DestroyProvisionalViews()
        {
            for (var i = 0; i < _provisionalViews.Count; i++)
            {
                var view = _provisionalViews[i];
                if (view == null)
                    continue;

                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(view);
                else
                    UnityEngine.Object.DestroyImmediate(view);
            }

            _provisionalViews.Clear();
        }

        private void ThrowIfInactive()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WorldStateStageTransaction));
            if (_committed)
                throw new InvalidOperationException("Transaction already committed.");
        }

        private static void RemoveRoadsOfKind(List<RoadState> roads, RoadSourceKind kind)
        {
            for (var i = roads.Count - 1; i >= 0; i--)
            {
                if (roads[i] != null && roads[i].sourceKind == kind)
                    roads.RemoveAt(i);
            }
        }

        private static List<T> CloneList<T>(IReadOnlyList<T> source, Func<T, T> clone) where T : class
        {
            var result = new List<T>();
            if (source == null)
                return result;

            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                if (item != null)
                    result.Add(clone(item));
            }

            return result;
        }

        private static List<WorldTileTerrainRecord> CopyTerrainRecords(
            IReadOnlyList<WorldTileTerrainRecord> records)
        {
            var result = new List<WorldTileTerrainRecord>();
            if (records == null)
                return result;

            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                if (record == null)
                    continue;

                result.Add(new WorldTileTerrainRecord(record.Tile, record.Terrain));
            }

            return result;
        }

        private static bool HasItems<T>(IList<T> list) => list != null && list.Count > 0;
    }

    public interface IGeneratedBuildingStateSink
    {
        bool TryStageBuilding(
            GeneratedBuildingPlacementData placement,
            out WorldEntityId id,
            out string error);

        void RegisterProvisionalView(UnityEngine.Object viewObject);
    }

    public sealed class GeneratedBuildingPlacementData
    {
        public string SourceId = string.Empty;
        public string ArchetypeId = string.Empty;
        public string TypeId = string.Empty;
        public WorldEntityId SettlementId;
        public WorldEntityId DistrictId;
        public WorldEntityId LotId;
        public CityDistrictType DistrictType;
        public Vector3 Position;
        public Quaternion Rotation = Quaternion.identity;
        public Vector3 Scale = Vector3.one;
        public Rect FootprintXZ;
        public BlockFace StreetFace;
        public bool HasDoorAnchor;
        public Vector3 DoorAnchorWorld;
        public int Ordinal;
    }
}
