#if UNITY_EDITOR
namespace Zombera.Editor
{
    internal enum WorldDevelopmentHubTab
    {
        Build = 0,
        State = 1,
        Simulation = 2,
        Events = 3,
        Streaming = 4,
        Tests = 5
    }

    internal enum WorldTestStepStatus
    {
        Passed = 0,
        Failed = 1,
        Skipped = 2,
        NotCovered = 3
    }
}
#endif
