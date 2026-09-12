using System.Collections;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Reroutes planned roads against WorldCostField-backed TerrainRoadCostField.</summary>
    public sealed class RefineRoadsAgainstTerrainStage : WorldBuildStageBase
    {
        public RefineRoadsAgainstTerrainStage() : base(WorldBuildStageId.RefineRoadsAgainstTerrain)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            if (context?.Artifacts?.Roads == null)
                throw new WorldBuildStageException(Descriptor.Id, "RoadNetworkRuntime is required.");

            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.05f, "Refining roads against terrain");

            var settings = context.Profile?.RoadNetworkSettings;
            if (settings == null)
            {
                context.Progress?.Report(Descriptor.Id, 1f, "No RoadNetworkSettings; skipped");
                yield break;
            }

            context.WorldBuilder?.BindArtifactFields(
                context.Artifacts.Landforms,
                context.Artifacts.Biomes,
                context.Artifacts.Hydrology);

            var query = context.WorldBuilder?.TerrainQuery;
            if (query != null && settings.rerouteRoadsWithTerrainPathfinding)
            {
                var options = new WorldCostFieldOptions
                {
                    CellSizeMeters = Mathf.Max(4f, settings.pathfindingCellSizeMeters),
                    RoadSettings = settings,
                    MaxTraversableWaterDepthMeters = context.Profile?.Hydrology != null
                        ? context.Profile.Hydrology.FordMaxDepthMeters
                        : 0.3f,
                    MaxBridgeableWaterDepthMeters = context.Profile?.Hydrology != null
                        ? context.Profile.Hydrology.BridgeCorridorMaxDepthMeters
                        : 4f,
                    WaterSoftCostPerMeterDepth = context.Profile?.Hydrology != null
                        ? context.Profile.Hydrology.WaterSoftCostPerMeterDepth
                        : 24f,
                    Orogen = context.Artifacts.Orogen,
                    PassAttractHalfWidthMeters = context.Profile?.Landforms != null
                        ? Mathf.Max(40f, context.Profile.Landforms.PassCorridorHalfWidthMeters)
                        : 120f
                };

                var bounds = TerrainRoadCostField.ComputeBounds(
                    context.Artifacts.Roads,
                    settings.pathfindingMarginMeters);
                if (bounds.width <= 0f || bounds.height <= 0f)
                    bounds = context.Session.WorldBoundsXZ;

                if (query.TryBuildCostField(bounds, options, out var worldField) && worldField != null)
                {
                    var costField = TerrainRoadCostField.FromWorldCostField(worldField);
                    WorldMapRoadNetworkGenerator.RefinePlanForTerrain(
                        context.Artifacts.Roads,
                        settings,
                        costField);
                }
                else
                {
                    Debug.LogWarning(
                        "[RefineRoadsAgainstTerrain] TryBuildCostField failed; " +
                        "skipping terrain pathfinding refine (no ProceduralRoadGenerator fallback).");
                }
            }
            else
            {
                ProceduralRoadGenerator.RefinePlanForTerrain(context.Artifacts.Roads, settings);
            }

            yield return null;
            context.Progress?.Report(
                Descriptor.Id,
                1f,
                $"Refined {context.Artifacts.Roads.Roads.Count} roads");
        }
    }
}
