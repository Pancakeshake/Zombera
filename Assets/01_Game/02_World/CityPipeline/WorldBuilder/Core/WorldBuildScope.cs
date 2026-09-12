using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Spatial scope for a world-build run.</summary>
    public readonly struct WorldBuildScope
    {
        public WorldBuildScopeKind Kind { get; }
        public Rect BoundsXZ { get; }
        public IReadOnlyList<WorldTileCoord> Tiles { get; }

        public WorldBuildScope(
            WorldBuildScopeKind kind,
            Rect boundsXZ,
            IReadOnlyList<WorldTileCoord> tiles)
        {
            Kind = kind;
            BoundsXZ = boundsXZ;
            Tiles = tiles ?? Array.Empty<WorldTileCoord>();
        }

        public static WorldBuildScope FullMap(Rect boundsXZ) =>
            new(WorldBuildScopeKind.FullMap, boundsXZ, Array.Empty<WorldTileCoord>());

        public static WorldBuildScope Bounds(Rect boundsXZ) =>
            new(WorldBuildScopeKind.Bounds, boundsXZ, Array.Empty<WorldTileCoord>());

        public static WorldBuildScope TileSet(IReadOnlyList<WorldTileCoord> tiles, Rect boundsXZ) =>
            new(WorldBuildScopeKind.TileSet, boundsXZ, tiles ?? Array.Empty<WorldTileCoord>());

        public static WorldBuildScope InitialPlayArea(Rect boundsXZ) =>
            new(WorldBuildScopeKind.InitialPlayArea, boundsXZ, Array.Empty<WorldTileCoord>());
    }
}
