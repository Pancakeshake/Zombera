using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Allocates scoped terrains and registers them into <see cref="WorldTileCatalog"/>.</summary>
    public sealed class AllocateTerrainGridStage : WorldBuildStageBase
    {
        private readonly List<WorldTileCoord> _tiles = new(64);

        public AllocateTerrainGridStage() : base(WorldBuildStageId.AllocateTerrainGrid)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            if (context?.Profile == null)
                throw new WorldBuildStageException(Descriptor.Id, "Profile is required.");

            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.05f, "Allocating terrain grid");

            var grid = context.Profile.TerrainGrid;
            if (grid == null)
                throw new WorldBuildStageException(Descriptor.Id, "TerrainGridProfile is required.");

            var catalog = context.WorldBuilder != null ? context.WorldBuilder.TileCatalog : null;
            if (catalog == null)
                throw new WorldBuildStageException(Descriptor.Id, "WorldTileCatalog is required.");

            if (catalog.Count == 0)
                catalog.Configure(context.Session);

            WorldBuildScopeUtility.CollectTiles(context.Scope, context.Session, _tiles);
            if (_tiles.Count == 0)
                throw new WorldBuildStageException(Descriptor.Id, "Scope resolved to zero tiles.");

            var seaLevel = context.Profile.Hydrology != null
                ? context.Profile.Hydrology.SeaLevelWorldY
                : 0f;

            var allocator = new WorldTerrainGridAllocator
            {
                Root = ResolveRoot(context.WorldBuilder)
            };

            for (var i = 0; i < _tiles.Count; i++)
            {
                context.Cancellation.ThrowIfRequested();
                var coord = _tiles[i];
                if (!catalog.TryGetTile(coord, out var info))
                    throw new WorldBuildStageException(Descriptor.Id, $"Missing catalog tile {coord.X},{coord.Z}.");

                if (!WorldTileInfoUtility.TryGetLiveTerrain(info, out _))
                {
                    var terrain = allocator.AllocateTile(context.Session, grid, seaLevel, coord);
                    catalog.SetTerrain(coord, terrain);
                }

                if (!catalog.TryGetTile(coord, out info) ||
                    !WorldTileInfoUtility.TryGetLiveTerrain(info, out _))
                    throw new WorldBuildStageException(Descriptor.Id, $"Terrain missing for tile {coord.X},{coord.Z}.");

                if (info.State == WorldTileState.None)
                    catalog.TryTransition(coord, WorldTileState.None, WorldTileState.Allocated);

                if (i % 16 == 0)
                {
                    context.Progress?.Report(
                        Descriptor.Id,
                        0.1f + 0.8f * ((i + 1f) / _tiles.Count),
                        $"Allocated {i + 1}/{_tiles.Count}");
                    yield return null;
                }
            }

            WorldTerrainNeighborUtility.LinkAll(catalog);
            PublishTerrainDescriptors(context, grid, seaLevel);
            context.Progress?.Report(Descriptor.Id, 1f, $"Allocated {_tiles.Count} tiles");
        }

        private void PublishTerrainDescriptors(
            WorldBuildContext context,
            TerrainGridProfile grid,
            float seaLevel)
        {
            if (!WorldStateStageCaptureUtility.TryGetActiveStage(context, Descriptor.Id, out var stage))
                return;

            var records = new List<WorldTileTerrainRecord>(_tiles.Count);
            for (var i = 0; i < _tiles.Count; i++)
            {
                var tile = WorldTileKey.FromCoord(_tiles[i]);
                records.Add(new WorldTileTerrainRecord(
                    tile,
                    CreateTerrainChunk(context, grid, _tiles[i], seaLevel)));
            }

            stage.ReplaceTerrain(records);
        }

        private static TerrainChunkState CreateTerrainChunk(
            WorldBuildContext context,
            TerrainGridProfile grid,
            WorldTileCoord coord,
            float seaLevel)
        {
            const int generatorVersion = 1;
            return new TerrainChunkState
            {
                baseGeneratorId = "WorldBuilder.Terrain",
                baseGeneratorVersion = generatorVersion,
                baseGenerationFingerprint = CreateTerrainFingerprint(
                    context.Session, context.Profile, coord, generatorVersion),
                seaLevelWorldY = seaLevel,
                terrainBaseWorldY = grid.GetTerrainBaseY(seaLevel),
                verticalSizeMeters = grid.TerrainVerticalSize,
                heightmapResolution = grid.HeightmapResolution,
                alphamapResolution = grid.AlphamapResolution,
                baseMapResolution = grid.BaseMapResolution,
                detailResolution = grid.DetailResolution,
                detailSamplesPerPatch = grid.DetailSamplesPerPatch
            };
        }

        private static string CreateTerrainFingerprint(
            WorldMapSession session,
            WorldGenerationProfile profile,
            WorldTileCoord coord,
            int generatorVersion)
        {
            var hasher = new StableHash64(0x5445525241494E01UL);
            hasher.Append(session.Seed);
            hasher.Append(profile != null ? profile.ProfileVersion : session.ProfileVersion);
            hasher.Append(coord.X);
            hasher.Append(coord.Z);
            hasher.Append(generatorVersion);
            return hasher.Finalize().ToString("x16");
        }

        private static Transform ResolveRoot(WorldBuilderService service)
        {
            if (service == null) return null;
            var existing = service.transform.Find("WorldTerrainGrid");
            if (existing != null) return existing;
            var go = new GameObject("WorldTerrainGrid");
            go.transform.SetParent(service.transform, false);
            return go.transform;
        }

    }
}
