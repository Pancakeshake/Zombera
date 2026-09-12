namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public static class WorldStateSchema
    {
        public const int CurrentVersion = 1;
        public const int CanonicalFormatVersion = 1;
        public const int IdAlgorithmVersion = 1;
    }

    public enum WorldEntityKind
    {
        None = 0,
        Region = 1,
        Settlement = 2,
        Road = 3,
        District = 4,
        Lot = 5,
        Building = 6,
        Poi = 7,
        TerrainModification = 8,
        Event = 9
    }

    public enum TerrainModificationKind
    {
        None = 0,
        BurnArea = 1
    }

    public enum RoadSourceKind
    {
        WorldPlanned = 0,
        CityGenerated = 1
    }

    public enum WorldUtilityStatus
    {
        Unknown = 0,
        Available = 1,
        Failed = 2,
        Disabled = 3
    }

    public enum WorldEventType
    {
        FireBuilding = 1,
        AbandonBuilding = 2
    }

    public enum WorldEventStatus
    {
        Pending = 0,
        Applied = 1,
        Rejected = 2
    }

    public enum WorldStateAvailability
    {
        None = 0,
        Fresh = 1,
        Loaded = 2,
        LegacyNoWorldState = 3
    }

    public enum WorldStateClearReason
    {
        Manual = 0,
        SessionReset = 1,
        LoadReplaced = 2,
        Shutdown = 3
    }
}
