using System.Collections;
using System.Text;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.CityPipeline.WorldBuilder.Stages
{
    /// <summary>Validates profile refs, resolutions, and surface palette channel reservations.</summary>
    public sealed class ValidateWorldProfileStage : WorldBuildStageBase
    {
        public ValidateWorldProfileStage() : base(WorldBuildStageId.ValidateWorldProfile)
        {
        }

        public override IEnumerator Execute(WorldBuildContext context)
        {
            if (context?.Profile == null)
                throw new WorldBuildStageException(Descriptor.Id, "WorldGenerationProfile is required.");

            context.Cancellation.ThrowIfRequested();
            context.Progress?.Report(Descriptor.Id, 0.1f, "Validating world profile");

            var profile = context.Profile;
            var errors = new StringBuilder();

            Require(profile.MapSizeSettings, nameof(profile.MapSizeSettings), errors);
            Require(profile.TerrainGrid, nameof(profile.TerrainGrid), errors);
            Require(profile.Landforms, nameof(profile.Landforms), errors);
            Require(profile.Hydrology, nameof(profile.Hydrology), errors);
            Require(profile.Water, nameof(profile.Water), errors);
            Require(profile.Biomes, nameof(profile.Biomes), errors);
            Require(profile.Surfaces, nameof(profile.Surfaces), errors);
            Require(profile.Nature, nameof(profile.Nature), errors);
            Require(profile.Pois, nameof(profile.Pois), errors);
            Require(profile.Environment, nameof(profile.Environment), errors);

            ValidateGrid(profile.TerrainGrid, errors);
            ValidateHydrology(profile.Hydrology, errors);
            ValidateSurfaces(profile.Surfaces, errors);
            ValidateSessionBounds(context.Session, errors);

            if (errors.Length > 0)
                throw new WorldBuildStageException(Descriptor.Id, errors.ToString());

            context.Progress?.Report(Descriptor.Id, 1f, "Profile valid");
            yield break;
        }

        private static void Require(Object reference, string name, StringBuilder errors)
        {
            if (reference != null) return;
            errors.AppendLine($"Missing required profile reference: {name}.");
        }

        private static void ValidateGrid(TerrainGridProfile grid, StringBuilder errors)
        {
            if (grid == null) return;
            if (!IsValidHeightmapResolution(grid.HeightmapResolution))
                errors.AppendLine($"TerrainGrid heightmapResolution {grid.HeightmapResolution} must be 2^n+1.");
            if (grid.AlphamapResolution < 16)
                errors.AppendLine("TerrainGrid alphamapResolution must be >= 16.");
            if (grid.TerrainVerticalSize <= 1f)
                errors.AppendLine("TerrainGrid terrainVerticalSize must be > 1.");
        }

        private static void ValidateHydrology(HydrologyProfile hydrology, StringBuilder errors)
        {
            if (hydrology == null) return;
            if (hydrology.CellSizeMeters < 1f)
                errors.AppendLine("Hydrology cellSizeMeters must be >= 1.");
            if (hydrology.RiverFlowThreshold < 1f)
                errors.AppendLine("Hydrology riverFlowThreshold must be >= 1.");
        }

        private static void ValidateSurfaces(WorldSurfacePalette surfaces, StringBuilder errors)
        {
            if (surfaces == null) return;
            if (!surfaces.Validate(out var error))
                errors.AppendLine(error ?? "WorldSurfacePalette failed validation.");

            // Channels 0–16 must be representable; infrastructure 13–16 reserved.
            if (surfaces.Mappings == null) return;
            for (var i = 0; i < surfaces.Mappings.Count; i++)
            {
                var mapping = surfaces.Mappings[i];
                if (mapping == null) continue;
                if (mapping.LayerIndex < 0 || mapping.LayerIndex > WorldSurfacePalette.MaxLayerIndex)
                {
                    errors.AppendLine(
                        $"Surface '{mapping.SemanticName}' layer {mapping.LayerIndex} outside 0–{WorldSurfacePalette.MaxLayerIndex}.");
                }

                var infra = mapping.LayerIndex >= WorldSurfacePalette.InfrastructureMinIndex &&
                            mapping.LayerIndex <= WorldSurfacePalette.InfrastructureMaxIndex;
                if (infra && !mapping.IsInfrastructure)
                {
                    errors.AppendLine(
                        $"Surface '{mapping.SemanticName}' uses reserved infrastructure index {mapping.LayerIndex}.");
                }
            }
        }

        private static void ValidateSessionBounds(WorldMapSession session, StringBuilder errors)
        {
            var bounds = session.WorldBoundsXZ;
            if (float.IsNaN(bounds.x) || float.IsNaN(bounds.y) ||
                float.IsInfinity(bounds.width) || float.IsInfinity(bounds.height) ||
                bounds.width <= 0f || bounds.height <= 0f)
            {
                errors.AppendLine("Session WorldBoundsXZ is not finite/positive.");
            }

            if (session.TilesPerSide <= 0)
                errors.AppendLine("Session TilesPerSide must be > 0.");
        }

        private static bool IsValidHeightmapResolution(int resolution)
        {
            if (resolution < 33) return false;
            var n = resolution - 1;
            return n > 0 && (n & (n - 1)) == 0;
        }
    }
}
