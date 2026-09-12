using System.Collections.Generic;
using Zombera.Systems;

namespace Zombera.Core
{
    public static class RuntimeGameplayAiRegistry
    {
        public static readonly List<SquadController> Squads = new();
        public static readonly List<SurvivorController> Survivors = new();

        public static void RegisterSquad(SquadController squad) => Squads.Add(squad);
        public static void UnregisterSquad(SquadController squad) => Squads.Remove(squad);

        public static void RegisterSurvivor(SurvivorController survivor) => Survivors.Add(survivor);
        public static void UnregisterSurvivor(SurvivorController survivor) => Survivors.Remove(survivor);
    }
}
