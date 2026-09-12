using System;
using System.Collections.Generic;
using Zombera.Factions;

namespace Zombera.Core
{
    [Serializable]
    public sealed class FactionSystemSaveData
    {
        public bool hasData;
        public List<FactionStandingState> standings = new();
    }
}
