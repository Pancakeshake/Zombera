using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Per-tier settlement size-class quotas for world site planning.</summary>
    public readonly struct WorldSettlementHierarchyQuota
    {
        public readonly int Metropolis;
        public readonly int City;
        public readonly int Settlements;

        public WorldSettlementHierarchyQuota(int metropolis, int city, int settlements)
        {
            Metropolis = Mathf.Max(0, metropolis);
            City = Mathf.Max(0, city);
            Settlements = Mathf.Max(0, settlements);
        }

        public int Total => Metropolis + City + Settlements;
    }

    /// <summary>
    ///     Expands hierarchy quotas into an ordered site-type list (largest first)
    ///     so placement reserves the best pads for metropolises and cities.
    /// </summary>
    public static class WorldSettlementHierarchy
    {
        public static List<CitySiteType> ExpandOrderedTypes(
            WorldSettlementHierarchyQuota quota,
            DeterministicRng rng)
        {
            var types = new List<CitySiteType>(quota.Total);
            Append(types, CitySiteType.Metropolis, quota.Metropolis);
            Append(types, CitySiteType.City, quota.City);

            for (var i = 0; i < quota.Settlements; i++)
                types.Add(RollSettlementType(rng));

            return types;
        }

        public static float FootprintRadiusMeters(CitySiteType type)
        {
            var halfW = CitySiteTypePresets.HalfWidth(type);
            var halfD = CitySiteTypePresets.HalfDepth(type);
            return Mathf.Max(halfW, halfD);
        }

        public static string DisplayNamePrefix(CitySiteType type) => type switch
        {
            CitySiteType.Metropolis => "Metropolis",
            CitySiteType.City => "City",
            CitySiteType.Town => "Town",
            CitySiteType.SmallTown => "SmallTown",
            CitySiteType.Village => "Village",
            _ => "Settlement"
        };

        private static void Append(List<CitySiteType> types, CitySiteType type, int count)
        {
            for (var i = 0; i < count; i++)
                types.Add(type);
        }

        /// <summary>Village / SmallTown / Town mix for the "smaller settlements" quota.</summary>
        private static CitySiteType RollSettlementType(DeterministicRng rng)
        {
            if (rng == null)
                return CitySiteType.Town;

            var roll = rng.NextFloat01();
            if (roll < 0.40f)
                return CitySiteType.Village;
            if (roll < 0.75f)
                return CitySiteType.SmallTown;
            return CitySiteType.Town;
        }
    }
}
