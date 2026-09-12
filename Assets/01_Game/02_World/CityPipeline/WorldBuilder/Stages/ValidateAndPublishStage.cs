using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Validate scoped tiles and publish GameplayReady.</summary>
    public sealed class ValidateAndPublishStage : WorldBuildStageBase
    {
        public ValidateAndPublishStage() : base(WorldBuildStageId.ValidateAndPublish)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            if (context?.WorldBuilder?.TileCatalog == null)
                throw new WorldBuildStageException(Descriptor.Id, "WorldTileCatalog is required.");

            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.1f, "Validating build");

            var catalog = context.WorldBuilder.TileCatalog;
            var buffer = new List<WorldTileInfo>(64);
            catalog.CopyTilesIntersecting(context.Scope.BoundsXZ, buffer);

            var report = new StringBuilder(256);
            var published = 0;
            var warnings = 0;

            for (var i = 0; i < buffer.Count; i++)
            {
                context.Cancellation.ThrowIfRequested();
                var info = buffer[i];

                if (info.Terrain == null)
                {
                    warnings++;
                    context.Progress?.ReportWarning(Descriptor.Id, $"Tile {info.Coord} has no Terrain.");
                    continue;
                }

                if (info.State < WorldTileState.NavigationReady)
                {
                    warnings++;
                    context.Progress?.ReportWarning(
                        Descriptor.Id,
                        $"Tile {info.Coord} state {info.State} is below NavigationReady.");
                    continue;
                }

                if (catalog.TryTransition(info.Coord, WorldTileState.NavigationReady, WorldTileState.GameplayReady))
                    published++;

                if (i % 8 == 0)
                    yield return null;
            }

            report.Append("Published ").Append(published).Append(" GameplayReady tiles");
            if (warnings > 0)
                report.Append(" (").Append(warnings).Append(" warnings)");

            ValidateWorldState(context);
            ValidateLandformGameplayGates(context);
            context.Progress?.Report(Descriptor.Id, 1f, report.ToString());
            yield break;
        }

        private void ValidateLandformGameplayGates(WorldBuildContext context)
        {
            var field = context.Artifacts?.Landforms;
            var profile = context.Profile?.Landforms;
            if (field == null || profile == null)
                return;

            var sea = context.Profile.Hydrology != null ? context.Profile.Hydrology.SeaLevelWorldY : 0f;
            var grid = context.Profile.TerrainGrid;
            var baseY = grid != null ? grid.GetTerrainBaseY(sea) : sea - 200f;
            var maxY = baseY + (grid != null ? grid.TerrainVerticalSize : 1000f);
            var result = LandformValidationUtility.Validate(
                field,
                profile,
                context.Artifacts.Orogen,
                baseY,
                maxY,
                sea);

            if (!result.Passed)
            {
                context.Progress?.ReportWarning(Descriptor.Id, result.Message);
                return;
            }

            context.Progress?.Report(Descriptor.Id, 0.95f, result.Message);
        }

        private void ValidateWorldState(WorldBuildContext context)
        {
            if (context?.StateManager == null || !context.StateManager.HasState)
                return;

            _ = WorldStateStageCaptureUtility.TryGetActiveStage(context, Descriptor.Id, out _);
            var validation = WorldStateValidator.Validate(context.StateManager.CaptureCanonicalCopy());
            for (var i = 0; i < validation.Warnings.Count; i++)
                context.Progress?.ReportWarning(Descriptor.Id, validation.Warnings[i]);

            if (!validation.IsValid)
                throw new WorldBuildStageException(
                    Descriptor.Id,
                    "WorldState validation failed: " + string.Join("; ", validation.Errors));
        }
    }
}
