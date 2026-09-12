using System;

namespace Zombera.Factions
{
    [Serializable]
    public sealed class FactionStandingState
    {
        public string factionId = string.Empty;
        public int standingValue;
        public FactionDiplomacyState diplomacyState = FactionDiplomacyState.Unknown;
        public bool discovered;
        public string lastKnownRegionId = string.Empty;
        public string lastInteractionSummary = string.Empty;

        public FactionStandingState Clone()
        {
            return new FactionStandingState
            {
                factionId = factionId,
                standingValue = standingValue,
                diplomacyState = diplomacyState,
                discovered = discovered,
                lastKnownRegionId = lastKnownRegionId,
                lastInteractionSummary = lastInteractionSummary
            };
        }
    }
}
