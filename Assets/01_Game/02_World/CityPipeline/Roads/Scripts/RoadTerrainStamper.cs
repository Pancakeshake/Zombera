using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Stamps road corridors into Unity terrains (heights + road alphamap).
    ///     Prefer <see cref="StampRoadsIntoTerrain"/> for one Get/Set per tile.
    /// </summary>
    public static partial class RoadTerrainStamper
    {
        private struct StampSampleCtx
        {
            public float TerrainOriginX, TerrainOriginZ;
            public Vector3 TerrainSize;
            public float WidthMeters, SampleStep;
            public Rect TerrainStampRect;
            public int HeightRes, AlphaW, AlphaH;
            public int HeightRadiusX, HeightRadiusZ, AlphaRadiusX, AlphaRadiusZ;
            public float Falloff;
            public float[,] Heights;
            public float[,,] Alphas;
            public int RoadLayer, AlphaLayers;
            public int HeightStartX, HeightStartZ, AlphaStartX, AlphaStartZ;
            public IReadOnlyList<WaterCrossing> SkipBridgeSpans;
            public float SkipRadiusMeters;
            public int RoadId;
            public bool WriteHeights;
        }

        public static void StampRoadIntoTerrain(
            Terrain terrain,
            RoadPolyline road,
            RoadNetworkSettings settings,
            Rect tileQueryRect)
        {
            StampRoadIntoTerrain(terrain, road, settings, tileQueryRect, crossings: null);
        }

        public static void StampRoadIntoTerrain(
            Terrain terrain,
            RoadPolyline road,
            RoadNetworkSettings settings,
            Rect tileQueryRect,
            IReadOnlyList<WaterCrossing> crossings)
        {
            if (road == null) return;
            _singleRoadScratch[0] = road;
            StampRoadsIntoTerrain(terrain, _singleRoadScratch, settings, tileQueryRect, crossings);
        }

        private static readonly RoadPolyline[] _singleRoadScratch = new RoadPolyline[1];

        /// <summary>
        ///     Batches all intersecting roads into one height/alphamap window per terrain.
        ///     Highway corridors that get a later road-bed bake are alphamap-only (heights deferred).
        /// </summary>
        /// <returns>True when the terrain heightmap or alphamap was written.</returns>
        public static bool StampRoadsIntoTerrain(
            Terrain terrain,
            IReadOnlyList<RoadPolyline> roads,
            RoadNetworkSettings settings,
            Rect tileQueryRect,
            IReadOnlyList<WaterCrossing> crossings,
            bool alphamapOnlyForHighways = true)
        {
            if (terrain == null || terrain.terrainData == null || roads == null || settings == null ||
                !settings.applyTerrainDeformationAndPaint)
                return false;

            var td = terrain.terrainData;
            var origin = terrain.transform.position;
            var size = td.size;
            var heightRes = td.heightmapResolution;
            var alphaW = td.alphamapWidth;
            var alphaH = td.alphamapHeight;
            var alphaLayers = td.alphamapLayers;
            if (heightRes <= 1 || alphaW <= 1 || alphaH <= 1 || alphaLayers <= 0)
                return false;

            var roadLayer = ResolveRoadLayerIndex(td);
            if (roadLayer < 0) roadLayer = 0;

            var terrainRect = Rect.MinMaxRect(origin.x, origin.z, origin.x + size.x, origin.z + size.z);
            if (!TryBuildUnionStamp(
                    roads, settings, tileQueryRect, terrainRect, alphamapOnlyForHighways,
                    out var unionRect, out var anyHeights, out var anyAlphas))
                return false;

            if (!TryBuildSampleRegion(unionRect.xMin, unionRect.xMax, origin.x, size.x, heightRes,
                    out var heightStartX, out var heightWidth) ||
                !TryBuildSampleRegion(unionRect.yMin, unionRect.yMax, origin.z, size.z, heightRes,
                    out var heightStartZ, out var heightHeight) ||
                !TryBuildSampleRegion(unionRect.xMin, unionRect.xMax, origin.x, size.x, alphaW,
                    out var alphaStartX, out var alphaWidth) ||
                !TryBuildSampleRegion(unionRect.yMin, unionRect.yMax, origin.z, size.z, alphaH,
                    out var alphaStartZ, out var alphaHeight))
                return false;

            var heights = anyHeights
                ? td.GetHeights(heightStartX, heightStartZ, heightWidth, heightHeight)
                : null;
            var alphas = anyAlphas
                ? td.GetAlphamaps(alphaStartX, alphaStartZ, alphaWidth, alphaHeight)
                : null;

            for (var r = 0; r < roads.Count; r++)
            {
                var road = roads[r];
                if (road?.pointsXZ == null || road.pointsXZ.Count < 2)
                    continue;

                var writeHeights = anyHeights &&
                                   !(alphamapOnlyForHighways && road.roadClass == RoadClass.Highway);
                StampOneRoadIntoBuffers(
                    road, settings, tileQueryRect, terrainRect,
                    origin, size, heightRes, alphaW, alphaH, alphaLayers, roadLayer,
                    heightStartX, heightStartZ, alphaStartX, alphaStartZ,
                    heights, alphas, crossings, writeHeights);
            }

            return CommitStampBuffers(
                terrain, td, heights, alphas,
                heightStartX, heightStartZ, heightWidth, heightHeight,
                alphaStartX, alphaStartZ);
        }

        private static bool CommitStampBuffers(
            Terrain terrain,
            TerrainData td,
            float[,] heights,
            float[,,] alphas,
            int heightStartX,
            int heightStartZ,
            int heightWidth,
            int heightHeight,
            int alphaStartX,
            int alphaStartZ)
        {
            var wrote = false;
            if (heights != null)
            {
                TerrainHeightFlattener.PrepareHeightmapWrite(
                    terrain, heights, heightStartX, heightStartZ, heightWidth, heightHeight);
                td.SetHeightsDelayLOD(heightStartX, heightStartZ, heights);
                wrote = true;
            }

            if (alphas == null)
                return wrote;

            td.SetAlphamaps(alphaStartX, alphaStartZ, alphas);
            return true;
        }
    }
}
