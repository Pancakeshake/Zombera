using System.Collections;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Crest / external ocean surface backend (built right after hydrology).</summary>
    public interface IOceanWaterRenderer
    {
        IEnumerator BuildOceanSurfaces(OceanSurfaceBuildRequest request);

        void ClearOcean(WorldBuildScope scope);

        void TearDownOcean();

        void RefreshOceanDepthCache(Rect boundsXZ, float seaLevelWorldY);

        /// <summary>
        /// Resizes full-map Crest WaterBody + depth cache to <paramref name="boundsXZ"/> exactly
        /// (terrain footprint). No-op when bounds are empty.
        /// </summary>
        void ResyncOceanToBounds(Rect boundsXZ, float seaLevelWorldY);

        /// <summary>
        /// Rebinds ocean primary light from <see cref="UnityEngine.RenderSettings.sun"/>
        /// after Hub/Enviro creates the directional light (Crest may build earlier).
        /// </summary>
        void RebindPrimaryLight();
    }
}
