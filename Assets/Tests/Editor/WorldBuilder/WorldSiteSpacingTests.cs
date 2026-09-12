#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.Roads;

namespace Zombera.Tests.Editor.WorldBuilder
{
    public sealed class WorldSiteSpacingTests
    {
        [Test]
        public void MinSeparation_MetroAndCity_LeavesApronRoom()
        {
            var metroR = WorldSettlementHierarchy.FootprintRadiusMeters(CitySiteType.Metropolis);
            var cityR = WorldSettlementHierarchy.FootprintRadiusMeters(CitySiteType.City);
            var sep = WorldSiteSpacing.MinCenterSeparationMeters(
                CitySiteType.Metropolis,
                metroR,
                CitySiteType.City,
                cityR,
                roadPadMeters: 100f,
                falloffMinMeters: 120f);

            // Footprints alone + road pad would be ~1820; guards must push well beyond that.
            Assert.Greater(sep, metroR + cityR + 100f + 200f);
        }

        [Test]
        public void ApronGuard_ScalesWithSettlementSize()
        {
            var metro = WorldSiteSpacing.ApronGuardMeters(CitySiteType.Metropolis, 120f);
            var village = WorldSiteSpacing.ApronGuardMeters(CitySiteType.Village, 120f);
            Assert.Greater(metro, village);
            Assert.Greater(metro, 250f);
        }

        [Test]
        public void MinSeparation_Villages_StillRequireGap()
        {
            var r = WorldSettlementHierarchy.FootprintRadiusMeters(CitySiteType.Village);
            var sep = WorldSiteSpacing.MinCenterSeparationMeters(
                CitySiteType.Village,
                r,
                CitySiteType.Village,
                r,
                roadPadMeters: 100f,
                falloffMinMeters: 120f);
            Assert.Greater(sep, r * 2f + 100f);
        }
    }
}
#endif
