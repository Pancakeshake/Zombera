using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Junction approach data for legacy EasyRoads hub streetscape placement.
    ///     CollectApproaches is a no-op after the EasyRoads purge; procedural hubs use
    ///     <see cref="JunctionApproachUtility"/> instead.
    /// </summary>
    public readonly struct CityJunctionApproach
    {
        public readonly Object Connection;
        public readonly Vector3 JunctionCenter;
        public readonly Vector3 PortPosition;
        public readonly Vector3 ApproachDirection;
        public readonly Vector3 AcrossDirection;
        public readonly float RoadWidthMeters;
        public readonly int PortIndex;
        public readonly bool IsTerminatingBranch;

        public CityJunctionApproach(
            Object connection,
            Vector3 junctionCenter,
            Vector3 portPosition,
            Vector3 approachDirection,
            Vector3 acrossDirection,
            float roadWidthMeters,
            int portIndex,
            bool isTerminatingBranch)
        {
            Connection = connection;
            JunctionCenter = junctionCenter;
            PortPosition = portPosition;
            ApproachDirection = approachDirection;
            AcrossDirection = acrossDirection;
            RoadWidthMeters = roadWidthMeters;
            PortIndex = portIndex;
            IsTerminatingBranch = isTerminatingBranch;
        }
    }

    /// <summary>
    ///     Enumerates junction approaches for hub streetscape placement (signals, signs, exclusions).
    ///     Stubbed empty after EasyRoads purge — use JunctionApproachUtility for procedural hubs.
    /// </summary>
    public static class CityJunctionApproachUtility
    {
        public static void CollectApproaches(
            ProceduralRoadSystem roadSystem,
            List<CityJunctionApproach> buffer,
            bool includeTerminatingBranch)
        {
            _ = roadSystem;
            _ = includeTerminatingBranch;
            buffer?.Clear();
        }
    }
}
