using JBooth.MicroSplat;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.City
{
    /// <summary>
    ///     Alphamap → MicroSplat control sync for <see cref="CityLotTerrainPainter"/>.
    ///     Vendor MapMagic control writes go through <see cref="IWorldSurfaceSyncBackend"/>.
    /// </summary>
    public static partial class CityLotTerrainPainter
    {
        private static void EnsureMicroSplatMaterial(Terrain terrain)
        {
            if (terrain == null) return;
            MicroSplatTerrainBinder.BindDefault(terrain);
        }

        private static void SyncAlphamapsToMicroSplat(
            Terrain terrain, float[,,] alphas, int xBase, int zBase)
        {
            var backend = WorldTileInfoUtility.FindSurfaceSyncBackend();
            if (backend != null && backend.OwnsControlSync(terrain))
            {
                backend.SyncAlphamapsToControls(terrain, alphas, xBase, zBase);
                return;
            }

            var mso = terrain.GetComponent<MicroSplatTerrain>();
            if (mso != null)
                mso.Sync();
        }
    }
}
