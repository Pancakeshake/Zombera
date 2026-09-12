using System;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public enum WorldStateLifecycleReason
    {
        Unknown = 0,
        Loaded = 1,
        Cleared = 2,
        Rebuilt = 3,
        Mutated = 4,
        GeneratedBatchApplied = 5,
        ValidationFailed = 6
    }

    [Serializable]
    public sealed class WorldStateLifecycleChange
    {
        public long Revision;
        public WorldStateLifecycleReason LifecycleReason = WorldStateLifecycleReason.Unknown;
        public string Reason = string.Empty;
        public WorldStateChangeSet ChangeSet = new();
    }
}
