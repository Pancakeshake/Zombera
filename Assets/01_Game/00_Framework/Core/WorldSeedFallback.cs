using UnityEngine;

namespace Zombera.Core
{
    /// <summary>
    ///     Optional world-seed fallback registered by World (e.g. ChunkGenerator) so City/Roads
    ///     seed resolvers do not need a hard reference to World types.
    /// </summary>
    public static class WorldSeedFallback
    {
        public static int RegisteredSeed { get; private set; }

        public static void Register(int seed) => RegisteredSeed = seed;

        public static void Clear() => RegisteredSeed = 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForDomainReload() => RegisteredSeed = 0;
    }
}
