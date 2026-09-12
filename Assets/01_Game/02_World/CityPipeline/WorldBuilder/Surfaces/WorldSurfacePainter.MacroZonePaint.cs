using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    /// Direct macro-zone MicroSplat base painting (classifier bypass).
    /// Elevation overlays always use the full <see cref="WorldSurfacePainter"/> peak ladder.
    /// </summary>
    public sealed partial class WorldSurfacePainter
    {
        private bool TryApplyMacroZoneSurfaces(
            float[,,] map,
            int z,
            int x,
            int layers,
            float worldX,
            float worldZ)
        {
            if (_paintLandformProfile == null || !_paintLandformProfile.UseMacroBiomeRegions || !_paintContextReady)
                return false;
            var bounds = _paintSession.WorldBoundsXZ;
            if (bounds.width <= 1f || bounds.height <= 1f)
                return false;
            return BiomeMacroZoneSurfacePaint.TryAccumulate(
                bounds, worldX, worldZ, this, map, z, x, layers);
        }
    }
}
