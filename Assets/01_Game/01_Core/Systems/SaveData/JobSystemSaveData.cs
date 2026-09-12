using System;
using System.Collections.Generic;
using Zombera.Characters.Work;

namespace Zombera.Core
{
    [Serializable]
    public sealed class JobSystemSaveData
    {
        public bool hasData;
        public bool manualPrioritiesEnabled = true;
        public List<WorkAssignmentProfile> memberProfiles = new();
    }
}
