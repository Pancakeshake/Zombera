using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Resolved road water crossing between abutments.</summary>
    public readonly struct WaterCrossing
    {
        public readonly ulong StableId;
        public readonly int RoadId;
        public readonly Vector2 EntryXZ;
        public readonly Vector2 ExitXZ;
        public readonly float WidthMeters;
        public readonly float MaximumDepthMeters;
        public readonly float DeckWorldY;
        public readonly RoadClass RoadClass;
        public readonly WaterCrossingPolicy Policy;

        public WaterCrossing(
            ulong stableId,
            int roadId,
            Vector2 entryXZ,
            Vector2 exitXZ,
            float widthMeters,
            float maximumDepthMeters,
            float deckWorldY,
            RoadClass roadClass,
            WaterCrossingPolicy policy)
        {
            StableId = stableId;
            RoadId = roadId;
            EntryXZ = entryXZ;
            ExitXZ = exitXZ;
            WidthMeters = widthMeters;
            MaximumDepthMeters = maximumDepthMeters;
            DeckWorldY = deckWorldY;
            RoadClass = roadClass;
            Policy = policy;
        }

        public Vector2 PositionXZ => (EntryXZ + ExitXZ) * 0.5f;
    }
}
