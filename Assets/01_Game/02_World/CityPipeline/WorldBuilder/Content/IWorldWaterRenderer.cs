using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    public interface IWorldWaterRenderer
    {
        IEnumerator BuildSurfaces(HydrologyPlan hydrology, IReadOnlyList<WaterCrossing> crossings, WorldBuildScope scope);

        /// <summary>Clears rebuildable water meshes for <paramref name="scope"/>.</summary>
        void Clear(WorldBuildScope scope);

        /// <summary>
        ///     Full teardown including persistent ocean hosts (hub Reset / session shutdown).
        /// </summary>
        void TearDown();
    }
}
