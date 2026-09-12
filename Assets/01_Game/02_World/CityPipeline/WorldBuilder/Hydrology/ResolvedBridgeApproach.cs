using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Physical road-facing sockets resolved after bridge kit placement.</summary>
    public readonly struct ResolvedBridgeApproach
    {
        public readonly ulong StableId;
        public readonly int RoadId;
        public readonly RoadClass RoadClass;
        public readonly Vector3 EntryApproachWorld;
        public readonly Vector3 ExitApproachWorld;

        public ResolvedBridgeApproach(
            ulong stableId,
            int roadId,
            RoadClass roadClass,
            Vector3 entryApproachWorld,
            Vector3 exitApproachWorld)
        {
            StableId = stableId;
            RoadId = roadId;
            RoadClass = roadClass;
            EntryApproachWorld = entryApproachWorld;
            ExitApproachWorld = exitApproachWorld;
        }

        public Vector2 EntryXZ => new(EntryApproachWorld.x, EntryApproachWorld.z);
        public Vector2 ExitXZ => new(ExitApproachWorld.x, ExitApproachWorld.z);
    }
}
