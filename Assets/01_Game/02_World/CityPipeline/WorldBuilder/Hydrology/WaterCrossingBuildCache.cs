using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Session crossing spans that replace procedural asphalt with bridge geometry.</summary>
    public static class WaterCrossingBuildCache
    {
        public static IReadOnlyList<WaterCrossing> Active { get; private set; }
        public static IReadOnlyList<ResolvedBridgeApproach> ResolvedApproaches { get; private set; }

        public static void Set(IReadOnlyList<WaterCrossing> crossings)
        {
            Active = crossings;
            ResolvedApproaches = null;
        }

        public static void SetResolvedApproaches(IReadOnlyList<ResolvedBridgeApproach> approaches) =>
            ResolvedApproaches = approaches;

        public static bool IsBridgeDeckSkip(Vector2 worldXZ, float lateralSlopMeters)
        {
            if (Active == null)
                return false;

            for (var i = 0; i < Active.Count; i++)
            {
                var crossing = Active[i];
                if (crossing.Policy != WaterCrossingPolicy.Bridge &&
                    crossing.Policy != WaterCrossingPolicy.Causeway)
                    continue;

                if (DistanceToSegment(worldXZ, crossing.EntryXZ, crossing.ExitXZ) <= lateralSlopMeters)
                    return true;
            }

            return false;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var delta = b - a;
            var lengthSq = delta.sqrMagnitude;
            if (lengthSq < 0.0001f)
                return Vector2.Distance(point, a);
            var t = Mathf.Clamp01(Vector2.Dot(point - a, delta) / lengthSq);
            return Vector2.Distance(point, a + delta * t);
        }
    }
}
