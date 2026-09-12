namespace Zombera.Characters
{
    /// <summary>
    ///     High-level allegiance bucket used for targeting and encounter rules.
    /// </summary>
    public enum UnitFaction
    {
        Survivor,
        Zombie,
        Bandit
    }

    public static class UnitFactionUtility
    {
        public static UnitFaction FromRole(UnitRole role)
        {
            return role switch
            {
                UnitRole.Zombie or UnitRole.Enemy => UnitFaction.Zombie,
                UnitRole.Bandit => UnitFaction.Bandit,
                _ => UnitFaction.Survivor
            };
        }

        public static string DefaultFactionIdFromRole(UnitRole role)
        {
            return role switch
            {
                UnitRole.Zombie or UnitRole.Enemy => Factions.FactionIds.ZombieHorde,
                UnitRole.Bandit => Factions.FactionIds.BanditRaiders,
                _ => Factions.FactionIds.PlayerColony
            };
        }

        public static UnitFaction CoarseFactionFromFactionId(string factionId)
        {
            if (string.IsNullOrWhiteSpace(factionId)) return UnitFaction.Survivor;
            if (factionId.StartsWith("zombie", System.StringComparison.OrdinalIgnoreCase)) return UnitFaction.Zombie;
            if (factionId.StartsWith("bandit", System.StringComparison.OrdinalIgnoreCase)) return UnitFaction.Bandit;
            return UnitFaction.Survivor;
        }

        public static bool AreHostile(UnitFaction sourceFaction, UnitFaction targetFaction)
        {
            return sourceFaction switch
            {
                UnitFaction.Bandit => targetFaction != UnitFaction.Bandit,
                UnitFaction.Zombie => targetFaction != UnitFaction.Zombie,
                _ => targetFaction is UnitFaction.Zombie or UnitFaction.Bandit
            };
        }
    }
}