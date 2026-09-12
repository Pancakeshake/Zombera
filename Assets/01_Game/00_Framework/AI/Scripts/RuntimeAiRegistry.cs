using System.Collections.Generic;
using Zombera.AI;

namespace Zombera.Core
{
    public static class RuntimeAiRegistry
    {
        public static readonly List<ZombieController> Zombies = new();

        public static void RegisterZombie(ZombieController zombie) => Zombies.Add(zombie);
        public static void UnregisterZombie(ZombieController zombie) => Zombies.Remove(zombie);
    }
}
