#region

using System.Collections.Generic;
using UnityEngine;

#endregion

// ReSharper disable InvertIf

namespace Zombera.World
{
    /// <summary>
    ///     Resolves terrain tiles for world-space positions across authored and MapMagic-generated terrains.
    /// </summary>
    public static class TerrainResolver
    {
        public static Terrain ResolveTerrainForPosition(Vector3 worldPosition, Terrain preferredTerrain = null)
        {
            var activeTerrain = Terrain.activeTerrain;
            var activeTerrains = Terrain.activeTerrains;
            var allTerrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);

            Terrain highestContainingTerrain = null;
            var highestContainingY = float.NegativeInfinity;
            var containingVisited = new HashSet<Terrain>();

            TryUpdateHighestContaining(
                preferredTerrain,
                worldPosition,
                containingVisited,
                ref highestContainingTerrain,
                ref highestContainingY);

            TryUpdateHighestContaining(
                activeTerrain,
                worldPosition,
                containingVisited,
                ref highestContainingTerrain,
                ref highestContainingY);

            if (activeTerrains != null)
                foreach (var activeTerrainEntry in activeTerrains)
                    TryUpdateHighestContaining(
                        activeTerrainEntry,
                        worldPosition,
                        containingVisited,
                        ref highestContainingTerrain,
                        ref highestContainingY);

            if (allTerrains != null)
                foreach (var terrainEntry in allTerrains)
                    TryUpdateHighestContaining(
                        terrainEntry,
                        worldPosition,
                        containingVisited,
                        ref highestContainingTerrain,
                        ref highestContainingY);

            if (highestContainingTerrain != null) return highestContainingTerrain;

            Terrain nearestTerrain = null;
            var nearestSqrDistance = float.PositiveInfinity;
            var nearestVisited = new HashSet<Terrain>();

            TryUpdateNearest(preferredTerrain, worldPosition, nearestVisited, ref nearestTerrain,
                ref nearestSqrDistance);
            TryUpdateNearest(activeTerrain, worldPosition, nearestVisited, ref nearestTerrain, ref nearestSqrDistance);

            if (activeTerrains != null)
                foreach (var activeTerrainEntry in activeTerrains)
                    TryUpdateNearest(activeTerrainEntry, worldPosition, nearestVisited, ref nearestTerrain,
                        ref nearestSqrDistance);

            if (allTerrains != null)
                foreach (var terrainEntry in allTerrains)
                    TryUpdateNearest(terrainEntry, worldPosition, nearestVisited, ref nearestTerrain,
                        ref nearestSqrDistance);

            return nearestTerrain ?? preferredTerrain;
        }

        public static bool TerrainContainsXZ(Terrain terrain, Vector3 worldPosition)
        {
            if (terrain == null || terrain.terrainData == null) return false;

            var terrainPosition = terrain.GetPosition();
            var terrainSize = terrain.terrainData.size;

            var minX = terrainPosition.x;
            var maxX = terrainPosition.x + terrainSize.x;
            var minZ = terrainPosition.z;
            var maxZ = terrainPosition.z + terrainSize.z;

            return worldPosition.x >= minX && worldPosition.x <= maxX && worldPosition.z >= minZ &&
                   worldPosition.z <= maxZ;
        }

        private static void TryUpdateNearest(
            Terrain terrain,
            Vector3 worldPosition,
            HashSet<Terrain> visited,
            ref Terrain nearestTerrain,
            ref float nearestSqrDistance)
        {
            if (terrain == null || terrain.terrainData == null || !visited.Add(terrain)) return;

            var sqrDistance = GetXZDistanceToTerrainBoundsSqr(terrain, worldPosition);

            if (sqrDistance >= nearestSqrDistance) return;

            nearestSqrDistance = sqrDistance;
            nearestTerrain = terrain;
        }

        private static void TryUpdateHighestContaining(
            Terrain terrain,
            Vector3 worldPosition,
            HashSet<Terrain> visited,
            ref Terrain highestTerrain,
            ref float highestSampleY)
        {
            if (terrain == null || terrain.terrainData == null || !visited.Add(terrain)) return;

            if (!TerrainContainsXZ(terrain, worldPosition)) return;

            var terrainPosition = terrain.GetPosition();
            var sampledY = terrain.SampleHeight(worldPosition) + terrainPosition.y;

            if (sampledY <= highestSampleY) return;

            highestSampleY = sampledY;
            highestTerrain = terrain;
        }

        private static float GetXZDistanceToTerrainBoundsSqr(Terrain terrain, Vector3 worldPosition)
        {
            var terrainPosition = terrain.GetPosition();
            var terrainSize = terrain.terrainData.size;

            var minX = terrainPosition.x;
            var maxX = terrainPosition.x + terrainSize.x;
            var minZ = terrainPosition.z;
            var maxZ = terrainPosition.z + terrainSize.z;

            var dx = 0f;
            if (worldPosition.x < minX)
                dx = minX - worldPosition.x;
            else if (worldPosition.x > maxX) dx = worldPosition.x - maxX;

            var dz = 0f;
            if (worldPosition.z < minZ)
                dz = minZ - worldPosition.z;
            else if (worldPosition.z > maxZ) dz = worldPosition.z - maxZ;

            return dx * dx + dz * dz;
        }
    }
}