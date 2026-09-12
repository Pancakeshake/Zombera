using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Region bounds and freeze/active masks for sparse thermal erosion (Sonar S107).</summary>
    public static partial class ThermalErosionSolver
    {
        private readonly struct ThermalRegionScope
        {
            public readonly int X0;
            public readonly int X1;
            public readonly int Z0;
            public readonly int Z1;
            public readonly bool[] Freeze;
            public readonly bool[] Active;

            public ThermalRegionScope(int x0, int x1, int z0, int z1, bool[] freeze, bool[] active)
            {
                X0 = x0;
                X1 = x1;
                Z0 = z0;
                Z1 = z1;
                Freeze = freeze;
                Active = active;
            }

            public bool Contains(int x, int z) =>
                x >= X0 && x <= X1 && z >= Z0 && z <= Z1;
        }

        private readonly struct ThermalPassArgs
        {
            public readonly LandformField Field;
            public readonly float[] Heights;
            public readonly float Talus;
            public readonly float Transfer;
            public readonly ThermalRegionScope Region;

            public ThermalPassArgs(
                LandformField field,
                float[] heights,
                float talus,
                float transfer,
                in ThermalRegionScope region)
            {
                Field = field;
                Heights = heights;
                Talus = talus;
                Transfer = transfer;
                Region = region;
            }
        }
    }
}
