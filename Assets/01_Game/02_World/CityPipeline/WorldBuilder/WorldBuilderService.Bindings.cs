using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Idempotent pipeline reference binding (Sonar S107 / S3776).</summary>
    public sealed partial class WorldBuilderService
    {
        /// <summary>Convenience: bind profile only.</summary>
        public bool BindPipelineReferences(WorldGenerationProfile profile) =>
            BindPipelineReferences(new WorldBuilderPipelineBindArgs { Profile = profile });

        /// <summary>Idempotent wiring for pipeline bootstrap and editor provisioning.</summary>
        public bool BindPipelineReferences(in WorldBuilderPipelineBindArgs args)
        {
            var changed = false;
            changed |= AssignIfChanged(ref _profile, args.Profile);
            changed |= AssignIfChanged(ref _tileCatalog, args.TileCatalog);
            changed |= AssignIfChanged(ref _stateManager, args.StateManager);
            changed |= AssignIfChanged(ref _surfacePainter, args.SurfacePainter);
            changed |= AssignIfChanged(ref _oceanWaterRendererSource, args.OceanWaterRenderer);
            changed |= AssignIfChanged(ref _waterRendererSource, args.WaterRenderer);
            changed |= AssignIfChanged(ref _environmentBackendSource, args.EnvironmentBackend);
            changed |= AssignIfChanged(ref _weatherSource, args.WeatherSource);
            changed |= AssignIfChanged(ref _naturePlacerSource, args.NaturePlacer);
            changed |= AssignIfChanged(ref _poiSinkSource, args.PoiSink);

            if (!changed)
                return false;

            EnsureServices();
            ValidateBindings(log: false);
            return true;
        }

        private static bool AssignIfChanged<T>(ref T field, T value) where T : class
        {
            if (value == null || ReferenceEquals(field, value))
                return false;
            field = value;
            return true;
        }
    }
}
