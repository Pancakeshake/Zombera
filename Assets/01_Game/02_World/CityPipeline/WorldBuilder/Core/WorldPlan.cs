using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Immutable global world plan produced early in the pipeline.</summary>
    public sealed class WorldPlan
    {
        public WorldMapSession Session { get; }
        public Vector2 PlanningOriginXZ { get; }
        public float CellSizeMeters { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyDictionary<string, int> SubsystemSeeds { get; }
        public ulong Fingerprint { get; }

        public WorldPlan(
            WorldMapSession session,
            Vector2 planningOriginXZ,
            float cellSizeMeters,
            int width,
            int height,
            IReadOnlyDictionary<string, int> subsystemSeeds,
            ulong fingerprint)
        {
            Session = session;
            PlanningOriginXZ = planningOriginXZ;
            CellSizeMeters = cellSizeMeters;
            Width = width;
            Height = height;
            SubsystemSeeds = subsystemSeeds ?? new Dictionary<string, int>();
            Fingerprint = fingerprint;
        }
    }
}
