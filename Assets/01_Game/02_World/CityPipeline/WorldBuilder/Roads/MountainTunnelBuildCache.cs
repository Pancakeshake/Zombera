using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     Session-scoped tunnel list for residual/asphalt/stamp/placer skips.
    ///     Mirrored from <see cref="WorldBuildArtifacts.SetTunnels"/>.
    /// </summary>
    public static class MountainTunnelBuildCache
    {
        public static IReadOnlyList<MountainTunnel> Active { get; private set; }

        public static void Set(IReadOnlyList<MountainTunnel> tunnels) => Active = tunnels;

        public static void Clear() => Active = null;

        public static bool IsCoreSkip(Vector2 worldXZ, int roadId, float lateralSlopMeters)
        {
            var tunnels = Active;
            if (tunnels == null || tunnels.Count == 0)
                return false;

            for (var i = 0; i < tunnels.Count; i++)
            {
                var tunnel = tunnels[i];
                if (tunnel.RoadId != 0 && roadId != 0 && tunnel.RoadId != roadId)
                    continue;
                if (tunnel.IsInCoreCarveSkip(worldXZ, lateralSlopMeters))
                    return true;
            }

            return false;
        }

        /// <summary>True across the complete portal-to-portal span owned by a tunnel mesh.</summary>
        public static bool IsTunnelSpanSkip(Vector2 worldXZ, int roadId, float lateralSlopMeters)
        {
            var tunnels = Active;
            if (tunnels == null || tunnels.Count == 0)
                return false;

            for (var i = 0; i < tunnels.Count; i++)
            {
                var tunnel = tunnels[i];
                if (tunnel.RoadId != 0 && roadId != 0 && tunnel.RoadId != roadId)
                    continue;
                if (tunnel.ContainsPointOnSpan(worldXZ, lateralSlopMeters))
                    return true;
            }

            return false;
        }
    }
}
