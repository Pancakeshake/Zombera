using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     Ephemeral highway mountain tunnel span (not WorldState). Cleared on reset / SetRoads / SetLandforms.
    ///     v1 is non-enterable: portal gates block agents until NavMesh links exist.
    /// </summary>
    public readonly struct MountainTunnel
    {
        public readonly ulong StableId;
        public readonly int RoadId;
        public readonly Vector2 EntryXZ;
        public readonly Vector2 ExitXZ;
        public readonly float EntryWorldY;
        public readonly float ExitWorldY;
        public readonly float LengthMeters;
        public readonly float PeakCoverMeters;
        public readonly float DaylightMeters;

        public MountainTunnel(
            ulong stableId,
            int roadId,
            Vector2 entryXZ,
            Vector2 exitXZ,
            float entryWorldY,
            float exitWorldY,
            float lengthMeters,
            float peakCoverMeters,
            float daylightMeters)
        {
            StableId = stableId;
            RoadId = roadId;
            EntryXZ = entryXZ;
            ExitXZ = exitXZ;
            EntryWorldY = entryWorldY;
            ExitWorldY = exitWorldY;
            LengthMeters = lengthMeters;
            PeakCoverMeters = peakCoverMeters;
            DaylightMeters = Mathf.Max(0f, daylightMeters);
        }

        public Vector2 MidXZ => (EntryXZ + ExitXZ) * 0.5f;

        public Vector2 ForwardXZ
        {
            get
            {
                var d = ExitXZ - EntryXZ;
                return d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.up;
            }
        }

        /// <summary>Core span that skips corridor carve (excludes portal daylighting bands).</summary>
        public bool TryGetCoreSpan(out Vector2 coreEntry, out Vector2 coreExit)
        {
            coreEntry = EntryXZ;
            coreExit = ExitXZ;
            var len = LengthMeters;
            if (len <= DaylightMeters * 2f + 1f)
                return false;

            var fwd = ForwardXZ;
            coreEntry = EntryXZ + fwd * DaylightMeters;
            coreExit = ExitXZ - fwd * DaylightMeters;
            return Vector2.Distance(coreEntry, coreExit) >= 1f;
        }

        public bool ContainsPointOnSpan(Vector2 worldXZ, float lateralSlopMeters)
        {
            var closest = ClosestPointOnSegment(worldXZ, EntryXZ, ExitXZ);
            return Vector2.Distance(worldXZ, closest) <= Mathf.Max(0f, lateralSlopMeters);
        }

        /// <summary>
        ///     True only for the open bore interior (excludes mouth abutments) so asphalt can
        ///     terminate on <c>Socket_Approach</c> and resume at the far mouth.
        /// </summary>
        public bool ContainsPointOnInteriorSpan(
            Vector2 worldXZ,
            float lateralSlopMeters,
            float mouthKeepMeters = 1f)
        {
            var ab = ExitXZ - EntryXZ;
            var lenSq = ab.sqrMagnitude;
            if (lenSq < 0.0001f)
                return false;

            var len = Mathf.Sqrt(lenSq);
            var t = Vector2.Dot(worldXZ - EntryXZ, ab) / lenSq;
            var closest = EntryXZ + ab * Mathf.Clamp01(t);
            if (Vector2.Distance(worldXZ, closest) > Mathf.Max(0f, lateralSlopMeters))
                return false;

            var along = Mathf.Clamp01(t) * len;
            var keep = Mathf.Max(0.25f, mouthKeepMeters);
            return along > keep && along < len - keep;
        }

        public bool IsInCoreCarveSkip(Vector2 worldXZ, float lateralSlopMeters)
        {
            if (!TryGetCoreSpan(out var a, out var b))
                return false;
            var closest = ClosestPointOnSegment(worldXZ, a, b);
            return Vector2.Distance(worldXZ, closest) <= Mathf.Max(0f, lateralSlopMeters);
        }

        private static Vector2 ClosestPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lenSq = ab.sqrMagnitude;
            if (lenSq < 0.0001f)
                return a;
            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
            return a + ab * t;
        }
    }
}
