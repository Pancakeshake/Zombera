namespace Zombera.Characters
{
    /// <summary>
    /// High-level allegiance bucket used for targeting and encounter rules.
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
            switch (role)
            {
                case UnitRole.Zombie:
                case UnitRole.Enemy:
                    return UnitFaction.Zombie;
                case UnitRole.Bandit:
                    return UnitFaction.Bandit;
                default:
                    return UnitFaction.Survivor;
            }
        }

        public static bool AreHostile(UnitFaction sourceFaction, UnitFaction targetFaction)
        {
            if (sourceFaction == UnitFaction.Bandit)
            {
                return targetFaction != UnitFaction.Bandit;
            }

            if (sourceFaction == UnitFaction.Zombie)
            {
                return targetFaction != UnitFaction.Zombie;
            }

            return targetFaction == UnitFaction.Zombie || targetFaction == UnitFaction.Bandit;
        }
    }
}