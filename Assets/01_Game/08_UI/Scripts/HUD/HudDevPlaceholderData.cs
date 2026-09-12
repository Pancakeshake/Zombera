using System.Collections.Generic;
using Zombera.Factions;

namespace Zombera.UI
{
    /// <summary>
    ///     Static preview data for HUD dev scenes (3_Ui, Tester) when live game systems are absent.
    /// </summary>
    public static class HudDevPlaceholderData
    {
        public static List<FactionStandingState> BuildFactionStandings()
        {
            return new List<FactionStandingState>
            {
                new()
                {
                    factionId = FactionIds.ZombieHorde,
                    standingValue = -100,
                    diplomacyState = FactionDiplomacyState.AtWar,
                    discovered = true,
                    lastKnownRegionId = "Highway 9",
                    lastInteractionSummary = "Horde sighted near Route 9 checkpoint."
                },
                new()
                {
                    factionId = FactionIds.BanditRaiders,
                    standingValue = -60,
                    diplomacyState = FactionDiplomacyState.Hostile,
                    discovered = true,
                    lastKnownRegionId = "Scrapyard Outskirts",
                    lastInteractionSummary = "Raiders ambushed a supply run."
                },
                new()
                {
                    factionId = FactionIds.SettlementIronhaven,
                    standingValue = 40,
                    diplomacyState = FactionDiplomacyState.Friendly,
                    discovered = true,
                    lastKnownRegionId = "Ironhaven Gate",
                    lastInteractionSummary = "Traders offered medical supplies at reduced cost."
                },
                new()
                {
                    factionId = FactionIds.SurvivorNeutral,
                    standingValue = 5,
                    diplomacyState = FactionDiplomacyState.Neutral,
                    discovered = true,
                    lastKnownRegionId = "Pine Ridge Camp",
                    lastInteractionSummary = "Independent survivors observed scavenging nearby."
                }
            };
        }

        public static List<FactionListEntry> BuildFactionListEntries()
        {
            return new List<FactionListEntry>
            {
                new()
                {
                    FactionId = FactionIds.ZombieHorde,
                    DisplayName = "Infected Horde",
                    Category = FactionCategory.Infected,
                    StandingValue = -100,
                    DiplomacyState = FactionDiplomacyState.AtWar,
                    LastKnownRegionId = "Highway 9",
                    LastInteractionSummary = "Horde sighted near Route 9 checkpoint.",
                    PrimaryColor = new UnityEngine.Color(0.45f, 0.20f, 0.18f),
                    Description = "Mindless infected drawn to noise and movement."
                },
                new()
                {
                    FactionId = FactionIds.BanditRaiders,
                    DisplayName = "Bandit Raiders",
                    Category = FactionCategory.Raider,
                    StandingValue = -60,
                    DiplomacyState = FactionDiplomacyState.Hostile,
                    LastKnownRegionId = "Scrapyard Outskirts",
                    LastInteractionSummary = "Raiders ambushed a supply run.",
                    PrimaryColor = new UnityEngine.Color(0.55f, 0.28f, 0.16f),
                    Description = "Hostile scavengers who prey on weak settlements."
                },
                new()
                {
                    FactionId = FactionIds.SettlementIronhaven,
                    DisplayName = "Ironhaven Traders",
                    Category = FactionCategory.Trader,
                    StandingValue = 40,
                    DiplomacyState = FactionDiplomacyState.Friendly,
                    LastKnownRegionId = "Ironhaven Gate",
                    LastInteractionSummary = "Traders offered medical supplies at reduced cost.",
                    PrimaryColor = new UnityEngine.Color(0.28f, 0.42f, 0.58f),
                    Description = "A fortified settlement known for cautious trade."
                },
                new()
                {
                    FactionId = FactionIds.SurvivorNeutral,
                    DisplayName = "Neutral Survivors",
                    Category = FactionCategory.Survivor,
                    StandingValue = 5,
                    DiplomacyState = FactionDiplomacyState.Neutral,
                    LastKnownRegionId = "Pine Ridge Camp",
                    LastInteractionSummary = "Independent survivors observed scavenging nearby.",
                    PrimaryColor = new UnityEngine.Color(0.55f, 0.55f, 0.48f),
                    Description = "Independent survivors with no fixed allegiance."
                }
            };
        }
    }
}
