using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    public interface IWorldCostField
    {
        Rect BoundsXZ { get; }
        float CellSizeMeters { get; }
        int Width { get; }
        int Height { get; }
        bool HasSample(int x, int z);
        bool IsTraversable(int x, int z, RoadClass roadClass);
        float GetHeight(int x, int z);
        float GetTraversalCost(int fromX, int fromZ, int toX, int toZ, RoadClass roadClass);
        void GetSoftCostFactors(int x, int z, RoadClass roadClass, out float additive, out float multiplier);
    }
}
