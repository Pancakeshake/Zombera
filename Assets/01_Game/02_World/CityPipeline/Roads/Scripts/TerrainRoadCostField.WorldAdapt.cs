using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Roads
{
    /// <summary>Adapts <see cref="IWorldCostField"/> samples into a terrain A* cost grid.</summary>
    public sealed partial class TerrainRoadCostField
    {
        public static TerrainRoadCostField FromWorldCostField(IWorldCostField worldField)
        {
            if (worldField == null || worldField.Width < 2 || worldField.Height < 2)
                return null;

            var width = worldField.Width;
            var height = worldField.Height;
            var cellSize = Mathf.Max(0.01f, worldField.CellSizeMeters);
            var origin = new Vector2(worldField.BoundsXZ.xMin, worldField.BoundsXZ.yMin);
            var count = width * height;
            var heights = new float[count];
            var localRelief = new float[count];
            var hasSample = new bool[count];
            var softAdditive = new float[count];
            var softMultiplier = new float[count];
            for (var i = 0; i < count; i++)
                softMultiplier[i] = 1f;
            var maxRelief = 0f;

            for (var z = 0; z < height; z++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = z * width + x;
                    if (!worldField.HasSample(x, z)) continue;

                    // Keep cells with samples even when currently non-traversable so endpoints can snap nearby.
                    hasSample[index] = true;
                    heights[index] = worldField.GetHeight(x, z);
                    worldField.GetSoftCostFactors(
                        x, z, RoadClass.Highway, out var softAdd, out var softMul);
                    // Preference only: Ocean×99 / deep water must not break A* admissibility.
                    softMultiplier[index] = Mathf.Clamp(softMul, SoftPreferenceMulMin, SoftPreferenceMulMax);
                    softAdditive[index] = Mathf.Clamp(Mathf.Max(0f, softAdd), 0f, SoftPreferenceAddMaxCells * cellSize);

                    // Soft-block deep water / no-build by clearing traversability for path A*.
                    if (!worldField.IsTraversable(x, z, RoadClass.Highway) &&
                        !worldField.IsTraversable(x, z, RoadClass.Local))
                        hasSample[index] = false;
                }
            }

            for (var z = 0; z < height; z++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = z * width + x;
                    if (!hasSample[index]) continue;

                    var centerHeight = heights[index];
                    var localMin = centerHeight;
                    for (var dz = -1; dz <= 1; dz++)
                    {
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            var nx = x + dx;
                            var nz = z + dz;
                            if (nx < 0 || nz < 0 || nx >= width || nz >= height) continue;
                            var ni = nz * width + nx;
                            if (!hasSample[ni]) continue;
                            localMin = Mathf.Min(localMin, heights[ni]);
                        }
                    }

                    var relief = Mathf.Max(0f, centerHeight - localMin);
                    localRelief[index] = relief;
                    maxRelief = Mathf.Max(maxRelief, relief);
                }
            }

            return new TerrainRoadCostField(
                terrains: null,
                cellSize,
                origin,
                width,
                height,
                heights,
                localRelief,
                hasSample,
                maxRelief,
                softAdditive,
                softMultiplier);
        }
    }
}
