namespace Zombera.Characters.Work
{
    public enum WorkJobType
    {
        Looting = 0,
        Mining = 1,
        Building = 2,
        Guarding = 3,
        Cooking = 4,
        Crafting = 5
    }

    public enum WorkPriorityLevel
    {
        Off = 0,
        Priority1 = 1,
        Priority2 = 2,
        Priority3 = 3,
        Priority4 = 4,
        Priority5 = 5,
        Priority6 = 6,
        Priority7 = 7,
        Priority8 = 8,
        Priority9 = 9
    }

    public enum WorkPriorityPreset
    {
        Builder,
        Crafter,
        Scavenger,
        Guard,
        Balanced
    }

    public enum WorkTaskState
    {
        Available = 0,
        Reserved = 1,
        InProgress = 2,
        Completed = 3,
        Failed = 4,
        Blocked = 5
    }
}
