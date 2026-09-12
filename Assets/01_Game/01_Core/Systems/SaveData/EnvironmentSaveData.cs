using System;

namespace Zombera.Core
{
    /// <summary>Persisted clock/weather director state for first-party World Builder sessions.</summary>
    [Serializable]
    public sealed class EnvironmentSaveData
    {
        public int formatVersion = 1;
        public float hour;
        public int dayNumber;
        public string weatherId = "Clear";
        public float transitionProgress;
        public ulong weatherRngState;
        public ulong weatherRngStream;
        public float hoursUntilNextChange = 5f;
        public int environmentProfileVersion = 1;
    }
}
