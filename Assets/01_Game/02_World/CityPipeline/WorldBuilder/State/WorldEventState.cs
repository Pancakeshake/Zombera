using System;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    [Serializable]
    public sealed class WorldEventState
    {
        public WorldEntityId id;
        public long sequence;
        public WorldEventType type;
        public WorldEntityId targetId;
        public long scheduledHour;
        public long resolvedHour = -1;
        public float magnitude = 1f;
        public WorldEventStatus status;
        public string resultCode = string.Empty;
    }
}
