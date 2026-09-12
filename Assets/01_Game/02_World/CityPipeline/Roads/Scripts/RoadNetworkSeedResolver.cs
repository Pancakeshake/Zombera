using UnityEngine;
using Zombera.Core;
using Zombera.World;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Resolves the active world-network seed without MapMagic graph walking.
    ///     Legacy MapMagic graph baseline seeds are optional via scene backends.
    /// </summary>
    public static class RoadNetworkSeedResolver
    {
        public const int DefaultSeed = 12345;

        public static int Resolve(RoadNetworkSettings settings, UnityEngine.Object unusedSeedSource = null)
        {
            _ = unusedSeedSource;

            if (ProceduralWorldSession.IsActive)
                return ProceduralWorldSession.WorldSeed;

            if (settings != null && settings.editorPreviewWorldSeed != 0)
                return settings.editorPreviewWorldSeed;

            if (WorldSeedFallback.RegisteredSeed != 0)
                return WorldSeedFallback.RegisteredSeed;

            return DefaultSeed;
        }
    }
}
