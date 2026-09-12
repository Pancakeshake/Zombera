using Zombera.Core;

namespace Zombera.Factions
{
    public readonly struct FactionStandingChangedEvent : IGameEvent
    {
        public FactionStandingChangedEvent(string factionId, int previousStanding, int newStanding,
            FactionDiplomacyState previousDiplomacy, FactionDiplomacyState newDiplomacy)
        {
            FactionId = factionId;
            PreviousStanding = previousStanding;
            NewStanding = newStanding;
            PreviousDiplomacy = previousDiplomacy;
            NewDiplomacy = newDiplomacy;
        }

        public string FactionId { get; }
        public int PreviousStanding { get; }
        public int NewStanding { get; }
        public FactionDiplomacyState PreviousDiplomacy { get; }
        public FactionDiplomacyState NewDiplomacy { get; }
    }

    public readonly struct FactionDiscoveredEvent : IGameEvent
    {
        public FactionDiscoveredEvent(string factionId) => FactionId = factionId;
        public string FactionId { get; }
    }
}
