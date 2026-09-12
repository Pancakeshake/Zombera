using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.State;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Optional refs for <see cref="WorldBuilderService.BindPipelineReferences"/> (Sonar S107).</summary>
    public struct WorldBuilderPipelineBindArgs
    {
        public WorldGenerationProfile Profile;
        public WorldTileCatalog TileCatalog;
        public WorldStateManager StateManager;
        public WorldSurfacePainter SurfacePainter;
        public MonoBehaviour OceanWaterRenderer;
        public MonoBehaviour WaterRenderer;
        public MonoBehaviour EnvironmentBackend;
        public MonoBehaviour WeatherSource;
        public MonoBehaviour NaturePlacer;
        public MonoBehaviour PoiSink;
    }
}
