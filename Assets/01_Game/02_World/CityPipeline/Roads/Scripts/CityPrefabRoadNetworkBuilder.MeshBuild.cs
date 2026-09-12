using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.World.Roads
{
    /// <summary>Hub procedural mesh build + sub-phase timing.</summary>
    public sealed partial class CityPrefabRoadNetworkBuilder
    {
        [System.NonSerialized] private RoadMeshBuildMetrics _roadMeshBuildMetrics;

        private struct RoadMeshBuildMetrics
        {
            public int RoadCount;
            public float BuildComputeMs;
            public int TerrainSamples;
            public int TerrainFallbacks;
            public long TerrainSampleMs;
            public long AsphaltMs;
            public long SidewalkMs;
            public long FootpathMs;
            public long FinalizeMs;
            public int FinalVertexCount;
            public int CombinedObjectCount;
            public bool UsedAccumulator;
        }

        private void BuildRoadMeshes(
            CityRoadNetworkBuildContext buildContext,
            RoadNetworkSettings settings,
            float roadSurfaceLiftMeters,
            System.Diagnostics.Stopwatch phaseSw)
        {
            _roadMeshBuildMetrics = default;

            var roads = buildContext.Network?.Roads;
            if (settings == null)
            {
                Debug.LogError("[CityPrefabRoadNetworkBuilder] Mesh build skipped — RoadNetworkSettings is null.", this);
                return;
            }

            if (roads == null || roads.Count == 0)
            {
                Debug.LogError(
                    "[CityPrefabRoadNetworkBuilder] Mesh build skipped — layout has no road polylines.",
                    this);
                return;
            }

            if (!settings.spawnRoadMeshes)
            {
                Debug.LogWarning(
                    "[CityPrefabRoadNetworkBuilder] spawnRoadMeshes is disabled on RoadNetworkSettings; " +
                    "procedural city meshes will still be built.",
                    this);
            }

            var roadList = new List<RoadPolyline>(roads.Count);
            for (var i = 0; i < roads.Count; i++)
                roadList.Add(roads[i]);

            var useAccumulator = settings.combineProceduralMeshesPerLayer;
            var accumulator = useAccumulator ? new ProceduralLayerMeshAccumulator() : null;
            var terrainSampler = new RoadBuildTerrainSampler();
            var sampleHeight = (System.Func<Vector2, float>)(xz =>
                terrainSampler.Sample(xz, ResolveGroundHeightWithoutHeightmap) + roadSurfaceLiftMeters);

            var sliceSw = System.Diagnostics.Stopwatch.StartNew();
            var proc = ProceduralCityRoadBuilder.Build(
                transform,
                roadList,
                settings,
                sampleHeight,
                new ProceduralCityRoadBuildOptions
                {
                    PlaceAsphalt = true,
                    Accumulator = accumulator
                });
            var asphaltMs = sliceSw.ElapsedMilliseconds;
            var stripCount = proc.StripCount;

            IReadOnlyList<CityNamedArea> blocks = null;
            if (settings.spawnProceduralSidewalkMeshes || settings.spawnFootpathMeshes)
            {
                blocks = RegionModeActive
                    ? BuildNamedAreasForAllSites()
                    : BuildNamedAreasForSingleCity();
            }

            long sidewalkMs = 0;
            if (settings.spawnProceduralSidewalkMeshes && blocks != null && blocks.Count > 0)
            {
                sliceSw.Restart();
                stripCount += ProceduralCityBlockSidewalkBuilder.Build(
                    proc.NetworkRoot,
                    blocks,
                    roadList,
                    settings,
                    sampleHeight,
                    accumulator);
                stripCount += CityPerimeterSidewalkBuilder.Build(
                    proc.NetworkRoot,
                    blocks,
                    roadList,
                    settings,
                    sampleHeight,
                    accumulator);
                sidewalkMs = sliceSw.ElapsedMilliseconds;
            }

            long footpathMs = 0;
            if (settings.spawnFootpathMeshes && blocks != null && blocks.Count > 0 &&
                !settings.spawnProceduralSidewalkMeshes)
            {
                sliceSw.Restart();
                ClearFootpathsInternal(proc.NetworkRoot);
                var footpathsContainer = GetOrCreateFootpathsContainer(proc.NetworkRoot);
                if (footpathsContainer != null)
                {
                    stripCount += CityPerimeterFootpathBuilder.Build(
                        footpathsContainer,
                        blocks,
                        roadList,
                        settings,
                        sampleHeight,
                        accumulator);
                }

                footpathMs = sliceSw.ElapsedMilliseconds;
            }

            sliceSw.Restart();
            var combinedCount = 0;
            if (useAccumulator)
                combinedCount = ProceduralCityRoadBuilder.FinalizeAccumulator(proc.NetworkRoot, settings, accumulator);
            else
                ProceduralCityRoadBuilder.CombineLayerMeshes(proc.NetworkRoot, settings);
            var finalizeMs = sliceSw.ElapsedMilliseconds;

            _roadMeshBuildMetrics = new RoadMeshBuildMetrics
            {
                RoadCount = stripCount,
                BuildComputeMs = phaseSw.ElapsedMilliseconds,
                TerrainSamples = terrainSampler.SampleCount,
                TerrainFallbacks = terrainSampler.FallbackCount,
                TerrainSampleMs = terrainSampler.SampleElapsedMs,
                AsphaltMs = asphaltMs,
                SidewalkMs = sidewalkMs,
                FootpathMs = footpathMs,
                FinalizeMs = finalizeMs,
                FinalVertexCount = accumulator != null ? accumulator.TotalVertexCount : 0,
                CombinedObjectCount = combinedCount,
                UsedAccumulator = useAccumulator
            };

            Debug.Log(
                "[CityPrefabRoadNetworkBuilder] Procedural mesh build: strips=" + stripCount +
                " polylines=" + roadList.Count +
                " asphaltMs=" + asphaltMs +
                " sidewalkMs=" + sidewalkMs +
                " footpathMs=" + footpathMs +
                " finalizeMs=" + finalizeMs +
                " terrainSampleMs=" + terrainSampler.SampleElapsedMs +
                " terrainSamples=" + terrainSampler.SampleCount +
                " terrainFallbacks=" + terrainSampler.FallbackCount +
                " verts=" + _roadMeshBuildMetrics.FinalVertexCount +
                " combined=" + combinedCount +
                " accumulator=" + useAccumulator + ".",
                this);
        }
    }
}
