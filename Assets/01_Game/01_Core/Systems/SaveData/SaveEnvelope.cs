using System;
using System.Collections.Generic;

namespace Zombera.Core
{
    [Serializable]
    public sealed class SaveMetadata
    {
        public string slotId;
        public string slotName;
        public string timestamp;
        public float playTimeSeconds;
        public int dayNumber;
        public string locationName;
        public string difficulty;
        public float progressPercent;
        public string gameVersion;
        public string screenshotBase64;
        public List<string> recentActivity = new List<string>();
    }

    [Serializable]
    public sealed class SaveEnvelope
    {
        public int saveVersion;
        public string gameVersion;
        public string timestamp;
        public string payload;
    }

    [Serializable]
    public sealed class SaveMetadataIndex
    {
        public List<string> slotIds = new();
    }
}
