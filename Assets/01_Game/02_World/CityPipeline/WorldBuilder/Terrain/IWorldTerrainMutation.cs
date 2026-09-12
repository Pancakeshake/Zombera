using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    public interface IWorldTerrainMutation
    {
        void ApplyCityPads(IReadOnlyList<CityFlattenPad> pads);
        void CarveHydrology(HydrologyPlan hydrology, WorldBuildScope scope);
        void StampRoads(RoadNetworkRuntime roads, IReadOnlyList<WaterCrossing> crossings);

        void StampRoads(
            RoadNetworkRuntime roads,
            IReadOnlyList<WaterCrossing> crossings,
            IReadOnlyList<Terrain> changedTerrains);
        void QueueSurfacePaint(WorldSurfacePaintCommand command);
        IEnumerator CommitDirtyTiles();
    }
}
