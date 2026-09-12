using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Shared city footprint flatten for hub (pinned terrains) and runtime (tile-scoped) builds.
    /// </summary>
    public static class CityTerrainFootprintFlattener
    {
        public static bool FlattenOnTerrains(
            IReadOnlyList<Terrain> terrains,
            Rect innerFootprintXZ,
            Rect outerFootprintXZ,
            float targetWorldY,
            float paddingMeters,
            float blendMeters,
            out string summary)
        {
            summary = string.Empty;
            if (terrains == null || terrains.Count == 0)
            {
                summary = "Terrain flatten skipped (no terrains).";
                return false;
            }

            var flattenedCount = 0;
            for (var i = 0; i < terrains.Count; i++)
            {
                var terrain = terrains[i];
                if (terrain == null)
                    continue;

                TerrainHeightFlattener.FlattenNestedRects(
                    terrain,
                    innerFootprintXZ,
                    outerFootprintXZ,
                    targetWorldY,
                    paddingMeters,
                    blendMeters);
                flattenedCount++;
            }

            if (flattenedCount == 0)
            {
                summary = "Terrain flatten skipped (no valid terrains).";
                return false;
            }

            summary = "Flattened city footprint (arterial→outer gradient) at Y=" +
                      targetWorldY.ToString("F1") + "m on " + flattenedCount + " terrain(s).";
            return true;
        }

        public static bool TryResolveTerrainsForTile(WorldTileInfo tile, bool requireMainTerrain, out List<Terrain> terrains)
        {
            terrains = new List<Terrain>(1);
            var terrain = tile.Terrain;
            if (terrain == null)
                return false;

            _ = requireMainTerrain;
            terrains.Add(terrain);
            return true;
        }

        public static bool TrySampleTerrainHeightmap(Vector2 worldXZ, out float worldY)
        {
            worldY = 0f;
            var terrains = Terrain.activeTerrains;
            if (terrains != null)
            {
                for (var i = 0; i < terrains.Length; i++)
                {
                    var terrain = terrains[i];
                    if (terrain?.terrainData == null)
                        continue;

                    var pos = terrain.transform.position;
                    var size = terrain.terrainData.size;
                    if (worldXZ.x < pos.x || worldXZ.x > pos.x + size.x ||
                        worldXZ.y < pos.z || worldXZ.y > pos.z + size.z)
                        continue;

                    worldY = terrain.SampleHeight(new Vector3(worldXZ.x, 0f, worldXZ.y)) + pos.y;
                    return true;
                }
            }

            if (Terrain.activeTerrain == null)
                return false;

            worldY = Terrain.activeTerrain.SampleHeight(new Vector3(worldXZ.x, 0f, worldXZ.y)) +
                     Terrain.activeTerrain.transform.position.y;
            return true;
        }

        public static float SampleTerrainHeightmap(Vector2 worldXZ, float flatFallbackY = 0f) =>
            TrySampleTerrainHeightmap(worldXZ, out var worldY) ? worldY : flatFallbackY;

        public static float SampleGroundHeight(Vector2 worldXZ, Terrain preferredTerrain, float flatFallbackY = 0f)
        {
            var rayOrigin = new Vector3(worldXZ.x, 500f, worldXZ.y);
            if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, 1000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return hit.point.y;

            if (preferredTerrain != null)
                return preferredTerrain.SampleHeight(new Vector3(worldXZ.x, 0f, worldXZ.y)) +
                       preferredTerrain.transform.position.y;

            if (Terrain.activeTerrain != null)
                return Terrain.activeTerrain.SampleHeight(new Vector3(worldXZ.x, 0f, worldXZ.y)) +
                       Terrain.activeTerrain.transform.position.y;

            return flatFallbackY;
        }

        public static string DescribeTerrainLod(WorldTileInfo tile)
        {
            return tile.Terrain != null ? "active" : "none";
        }
    }
}
