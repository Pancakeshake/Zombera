using System.Collections;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    public interface IWorldSurfacePainter
    {
        void BindTerrain(Terrain terrain, WorldSurfacePalette palette);
        void PaintNaturalSurface(WorldTileInfo tile, LandformField landforms, HydrologyPlan water, BiomeField biomes);
        void PaintInfrastructure(WorldTileInfo tile, WorldBuildArtifacts artifacts);
        IEnumerator SyncDirtyTiles();
    }
}
