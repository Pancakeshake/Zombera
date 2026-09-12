using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Stamps road corridors into Unity terrains via RoadTerrainStamper + mutation commit.</summary>
    public sealed class StampInfrastructureTerrainStage : WorldBuildStageBase
    {
        private readonly List<WorldTileCoord> _tiles = new(64);
        private readonly List<Terrain> _changedTerrains = new(64);

        public StampInfrastructureTerrainStage() : base(WorldBuildStageId.StampInfrastructureTerrain)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            if (context?.Artifacts?.Roads == null)
                throw new WorldBuildStageException(Descriptor.Id, "RoadNetworkRuntime is required.");

            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.05f, "Stamping infrastructure terrain");

            var settings = context.Profile?.RoadNetworkSettings;
            var catalog = context.WorldBuilder?.TileCatalog;
            if (catalog == null || settings == null)
            {
                context.Progress?.Report(Descriptor.Id, 1f, "Stamp skipped (missing catalog/settings)");
                yield break;
            }

            WorldBuildScopeUtility.CollectTiles(context.Scope, context.Session, _tiles);
            InstallWaterProtection(context);

            var stampPass = StampScopedTiles(context, catalog, settings);
            while (stampPass.MoveNext())
                yield return stampPass.Current;

            context.WorldBuilder?.TerrainMutation?.StampRoads(
                context.Artifacts.Roads,
                context.Artifacts.Crossings,
                _changedTerrains);

            if (context.WorldBuilder?.TerrainMutation is WorldTerrainMutationService mutation)
            {
                var commit = mutation.CommitDirtyTiles();
                while (commit.MoveNext())
                    yield return commit.Current;
            }

            var roadSettings = context.Profile?.RoadNetworkSettings;
            if (roadSettings != null && roadSettings.tunnelEnterable)
                TunnelTerrainHoleApplicator.ApplyMouthHoles(context.Artifacts.Tunnels, roadSettings);

            WorldTerrainNeighborUtility.StitchAndLink(catalog);
            AdvanceInfrastructureReady(catalog);

            ClearWaterProtection(context);
            context.Progress?.Report(Descriptor.Id, 1f, "Infrastructure stamped");
        }

        private IEnumerator StampScopedTiles(
            WorldBuildContext context,
            WorldTileCatalog catalog,
            RoadNetworkSettings settings)
        {
            var roads = context.Artifacts.Roads.Roads;
            var crossings = context.Artifacts.Crossings;
            _changedTerrains.Clear();

            for (var t = 0; t < _tiles.Count; t++)
            {
                context.Cancellation.ThrowIfRequested();
                if (!catalog.TryGetTile(_tiles[t], out var info) || info.Terrain == null)
                    continue;

                if (RoadTerrainStamper.StampRoadsIntoTerrain(
                        info.Terrain, roads, settings, info.WorldRectXZ, crossings,
                        alphamapOnlyForHighways: true))
                    _changedTerrains.Add(info.Terrain);

                if (t % 2 != 0)
                    continue;

                context.Progress?.Report(
                    Descriptor.Id,
                    0.1f + 0.7f * ((t + 1f) / _tiles.Count),
                    $"Stamped {t + 1}/{_tiles.Count}");
                yield return null;
            }
        }

        private void AdvanceInfrastructureReady(WorldTileCatalog catalog)
        {
            for (var i = 0; i < _tiles.Count; i++)
            {
                if (!catalog.TryGetTile(_tiles[i], out var info)) continue;
                if (info.State == WorldTileState.TerrainReady)
                    catalog.TryTransition(_tiles[i], WorldTileState.TerrainReady, WorldTileState.InfrastructureReady);
            }
        }

        private static void InstallWaterProtection(WorldBuildContext context)
        {
            var footprint = context.Artifacts?.Hydrology?.FootprintPlan;
            if (footprint == null)
                return;
            var clearance = context.Profile?.Water != null
                ? context.Profile.Water.MinimumBedClearance
                : 0.5f;
            context.CityBuilder?.SetWaterFootprintPlan(footprint, clearance);
        }

        private static void ClearWaterProtection(WorldBuildContext context) =>
            context.CityBuilder?.SetWaterFootprintPlan(null);
    }
}
