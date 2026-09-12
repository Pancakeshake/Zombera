using System.Collections.Generic;
using UnityEngine;
using Zombera.World;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.Roads
{
    public sealed partial class ProceduralRoadSystem
    {
        private void GenerateRoadsForTile(WorldTileInfo tile)
        {
            if (tile.Terrain == null && tile.WorldRectXZ.width <= 0f) return;

            // EasyRoads tile path removed — procedural strip meshes only.
            if (!useProceduralTileMeshes)
            {
                Debug.LogWarning(
                    "[ProceduralRoadSystem] EasyRoads tile generation removed; enabling procedural meshes.",
                    this);
                useProceduralTileMeshes = true;
            }

            GenerateProceduralMeshesForTile(tile);
        }

        private void ClearTileRoads(WorldTileInfo tile)
        {
            var key = (tile.Coord.X, tile.Coord.Z);
            if (!_proceduralTileRoots.TryGetValue(key, out var root) || root == null)
            {
                _proceduralTileRoots.Remove(key);
                return;
            }

            if (Application.isPlaying)
                Destroy(root);
            else
                DestroyImmediate(root);

            _proceduralTileRoots.Remove(key);
        }

        private void ClearAllTileRoads()
        {
            foreach (var kvp in _proceduralTileRoots)
            {
                if (kvp.Value == null) continue;
                if (Application.isPlaying)
                    Destroy(kvp.Value);
                else
                    DestroyImmediate(kvp.Value);
            }

            _proceduralTileRoots.Clear();
        }

        private void GenerateProceduralMeshesForTile(WorldTileInfo tile)
        {
            if (settings == null)
                return;

            var tileRect = tile.WorldRectXZ;
            var sourceRoads = ResolveSourceRoadPolylinesForTile(tile, new RoadGenerationDiagnostics());
            if (sourceRoads == null || sourceRoads.Count == 0)
                return;

            ClearTileRoads(tile);
            var tileRoot = new GameObject($"ProceduralTileRoads_{tile.Coord.X}_{tile.Coord.Z}");
            tileRoot.transform.SetParent(transform, false);
            _proceduralTileRoots[(tile.Coord.X, tile.Coord.Z)] = tileRoot;

            float SampleHeight(Vector2 xz)
            {
                if (tile.Terrain != null)
                    return tile.Terrain.SampleHeight(new Vector3(xz.x, 0f, xz.y))
                           + tile.Terrain.transform.position.y;
                return 0f;
            }

            ProceduralRoadSurfaceBuilder.BuildForTile(
                tileRoot.transform,
                sourceRoads,
                tileRect,
                settings,
                SampleHeight);

            _processedTileCount++;
            RoadsApplied?.Invoke(tile);
        }
    }
}
