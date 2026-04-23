using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World
{
    /// <summary>
    /// Resolves terrain tiles for world-space positions across authored and MapMagic-generated terrains.
    /// </summary>
    public static class TerrainResolver
    {
        public static Terrain ResolveTerrainForPosition(Vector3 worldPosition, Terrain preferredTerrain = null)
        {
            Terrain activeTerrain = Terrain.activeTerrain;
            Terrain[] activeTerrains = Terrain.activeTerrains;
            Terrain[] allTerrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);

            Terrain highestContainingTerrain = null;
            float highestContainingY = float.NegativeInfinity;
            HashSet<Terrain> containingVisited = new HashSet<Terrain>();

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
            {
                for (int i = 0; i < activeTerrains.Length; i++)
                {
                    TryUpdateHighestContaining(
                        activeTerrains[i],
                        worldPosition,
                        containingVisited,
                        ref highestContainingTerrain,
                        ref highestContainingY);
                }
            }

            if (allTerrains != null)
            {
                for (int i = 0; i < allTerrains.Length; i++)
                {
                    TryUpdateHighestContaining(
                        allTerrains[i],
                        worldPosition,
                        containingVisited,
                        ref highestContainingTerrain,
                        ref highestContainingY);
                }
            }

            if (highestContainingTerrain != null)
            {
                return highestContainingTerrain;
            }

            Terrain nearestTerrain = null;
            float nearestSqrDistance = float.PositiveInfinity;
            HashSet<Terrain> nearestVisited = new HashSet<Terrain>();

            TryUpdateNearest(preferredTerrain, worldPosition, nearestVisited, ref nearestTerrain, ref nearestSqrDistance);
            TryUpdateNearest(activeTerrain, worldPosition, nearestVisited, ref nearestTerrain, ref nearestSqrDistance);

            if (activeTerrains != null)
            {
                for (int i = 0; i < activeTerrains.Length; i++)
                {
                    TryUpdateNearest(activeTerrains[i], worldPosition, nearestVisited, ref nearestTerrain, ref nearestSqrDistance);
                }
            }

            if (allTerrains != null)
            {
                for (int i = 0; i < allTerrains.Length; i++)
                {
                    TryUpdateNearest(allTerrains[i], worldPosition, nearestVisited, ref nearestTerrain, ref nearestSqrDistance);
                }
            }

            return nearestTerrain != null ? nearestTerrain : preferredTerrain;
        }

        public static bool TerrainContainsXZ(Terrain terrain, Vector3 worldPosition)
        {
            if (terrain == null || terrain.terrainData == null)
            {
                return false;
            }

            Vector3 terrainPosition = terrain.GetPosition();
            Vector3 terrainSize = terrain.terrainData.size;

            float minX = terrainPosition.x;
            float maxX = terrainPosition.x + terrainSize.x;
            float minZ = terrainPosition.z;
            float maxZ = terrainPosition.z + terrainSize.z;

            return worldPosition.x >= minX && worldPosition.x <= maxX && worldPosition.z >= minZ && worldPosition.z <= maxZ;
        }

        private static void TryUpdateNearest(
            Terrain terrain,
            Vector3 worldPosition,
            HashSet<Terrain> visited,
            ref Terrain nearestTerrain,
            ref float nearestSqrDistance)
        {
            if (terrain == null || terrain.terrainData == null || !visited.Add(terrain))
            {
                return;
            }

            float sqrDistance = GetXZDistanceToTerrainBoundsSqr(terrain, worldPosition);

            if (sqrDistance >= nearestSqrDistance)
            {
                return;
            }

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
            if (terrain == null || terrain.terrainData == null || !visited.Add(terrain))
            {
                return;
            }

            if (!TerrainContainsXZ(terrain, worldPosition))
            {
                return;
            }

            Vector3 terrainPosition = terrain.GetPosition();
            float sampledY = terrain.SampleHeight(worldPosition) + terrainPosition.y;

            if (sampledY <= highestSampleY)
            {
                return;
            }

            highestSampleY = sampledY;
            highestTerrain = terrain;
        }

        private static float GetXZDistanceToTerrainBoundsSqr(Terrain terrain, Vector3 worldPosition)
        {
            Vector3 terrainPosition = terrain.GetPosition();
            Vector3 terrainSize = terrain.terrainData.size;

            float minX = terrainPosition.x;
            float maxX = terrainPosition.x + terrainSize.x;
            float minZ = terrainPosition.z;
            float maxZ = terrainPosition.z + terrainSize.z;

            float dx = 0f;
            if (worldPosition.x < minX)
            {
                dx = minX - worldPosition.x;
            }
            else if (worldPosition.x > maxX)
            {
                dx = worldPosition.x - maxX;
            }

            float dz = 0f;
            if (worldPosition.z < minZ)
            {
                dz = minZ - worldPosition.z;
            }
            else if (worldPosition.z > maxZ)
            {
                dz = worldPosition.z - maxZ;
            }

            return dx * dx + dz * dz;
        }
    }
}